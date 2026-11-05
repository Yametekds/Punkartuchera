using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Punkartuchera.Relay.Contracts;
using Punkartuchera.Relay.Matches;
using Punkartuchera.Relay.Security;

namespace Punkartuchera.Relay.Functions;

/// <summary>
/// Puntos de entrada HTTP del relé. Son anónimos para Azure (no hay clave de función que esconder
/// en una app pública): la autorización es la llave de jugador que va en la cabecera
/// <c>Authorization: Bearer</c>, comprobada en cada petición.
/// </summary>
public sealed class MatchFunctions(MatchRelay relay, RequestThrottle throttle)
{
    [Function("CrearPartida")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "partidas")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!throttle.TryAcquire(RequestThrottle.Bucket.Create, ClientAddress(request)))
        {
            return TooManyRequests();
        }

        var ticket = await relay.CreateAsync(cancellationToken);
        return Private(request, new JsonResult(ticket, RelayJsonContext.Default.Options) { StatusCode = StatusCodes.Status201Created });
    }

    [Function("EntrarEnPartida")]
    public async Task<IActionResult> Join(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "partidas/{code}/entrar")] HttpRequest request,
        string code,
        CancellationToken cancellationToken)
    {
        if (!throttle.TryAcquire(RequestThrottle.Bucket.Join, ClientAddress(request)))
        {
            return TooManyRequests();
        }

        if (!MatchCode.TryNormalize(code, out var normalized)
            || await relay.JoinAsync(normalized, cancellationToken) is not { } ticket)
        {
            return NotFound();
        }

        return Private(request, new JsonResult(ticket, RelayJsonContext.Default.Options));
    }

    [Function("ConectarPartida")]
    public async Task<IActionResult> Negotiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "partidas/{code}/conexion")] HttpRequest request,
        string code,
        CancellationToken cancellationToken)
    {
        if (await AuthenticateAsync(request, code, cancellationToken) is not { } player)
        {
            return new UnauthorizedResult();
        }

        var info = await relay.ConnectAsync(player, cancellationToken);
        return Private(request, new JsonResult(info, RelayJsonContext.Default.Options));
    }

    [Function("MandarMensaje")]
    public async Task<IActionResult> Send(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "partidas/{code}/mensajes")] HttpRequest request,
        string code,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > RelayProtocol.MaxMessageBytes)
        {
            return new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);
        }

        if (await AuthenticateAsync(request, code, cancellationToken) is not { } player)
        {
            return new UnauthorizedResult();
        }

        if (!throttle.TryAcquire(RequestThrottle.Bucket.Message, player.PlayerId))
        {
            return TooManyRequests();
        }

        if (await ReadLimitedAsync(request.Body, RelayProtocol.MaxMessageBytes, cancellationToken) is not { } body)
        {
            return new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);
        }

        return await relay.ForwardAsync(player, body, cancellationToken) switch
        {
            ForwardResult.Sent => new AcceptedResult(),
            ForwardResult.TooLarge => new StatusCodeResult(StatusCodes.Status413PayloadTooLarge),
            ForwardResult.NoOpponent => new ConflictResult(),
            _ => new BadRequestResult(),
        };
    }

    [Function("SalirDePartida")]
    public async Task<IActionResult> Leave(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "partidas/{code}/salir")] HttpRequest request,
        string code,
        CancellationToken cancellationToken)
    {
        if (await AuthenticateAsync(request, code, cancellationToken) is not { } player)
        {
            return new UnauthorizedResult();
        }

        await relay.LeaveAsync(player, cancellationToken);
        return new NoContentResult();
    }

    private Task<PlayerSession?> AuthenticateAsync(HttpRequest request, string code, CancellationToken cancellationToken)
    {
        const string scheme = "Bearer ";
        var header = request.Headers.Authorization.ToString();
        if (!MatchCode.TryNormalize(code, out var normalized) || !header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<PlayerSession?>(null);
        }

        return relay.AuthenticateAsync(normalized, header[scheme.Length..].Trim(), cancellationToken);
    }

    /// <summary>
    /// Lee el cuerpo sin pasar de <paramref name="limit"/> bytes aunque la cabecera Content-Length
    /// falte o mienta. Devuelve null si se pasa.
    /// </summary>
    internal static async Task<ReadOnlyMemory<byte>?> ReadLimitedAsync(Stream body, int limit, CancellationToken cancellationToken)
    {
        var buffer = new byte[limit + 1];
        var total = 0;
        int read;
        while ((read = await body.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
        {
            total += read;
            if (total > limit)
            {
                return null;
            }
        }

        return buffer.AsMemory(0, total);
    }

    /// <summary>
    /// IP del cliente. En Azure, el frontal añade la IP real al final de X-Forwarded-For;
    /// se coge la última para que el cliente no pueda inventarse otra poniéndola delante.
    /// </summary>
    internal static string ClientAddress(HttpRequest request)
    {
        var forwarded = request.Headers["X-Forwarded-For"].ToString();
        var last = forwarded.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (last is not null)
        {
            // Puede venir con puerto ("1.2.3.4:5678"); IPEndPoint entiende también IPv6 entre corchetes.
            return IPEndPoint.TryParse(last, out var endpoint) ? endpoint.Address.ToString() : last;
        }

        return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
    }

    /// <summary>Las respuestas con llaves o tokens no se guardan en ninguna caché.</summary>
    private static IActionResult Private(HttpRequest request, IActionResult result)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        return result;
    }

    private static NotFoundResult NotFound() => new();

    private static StatusCodeResult TooManyRequests() => new(StatusCodes.Status429TooManyRequests);
}
