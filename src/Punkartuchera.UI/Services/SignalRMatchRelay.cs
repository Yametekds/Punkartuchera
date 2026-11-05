using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Punkartuchera.Core.Matches;
using Punkartuchera.Relay.Contracts;

namespace Punkartuchera.UI.Services;

/// <summary>
/// Cliente del relé: peticiones HTTP a la Function App para crear, entrar, mandar y salir,
/// y una conexión SignalR (WebSocket) directa con Azure SignalR Service para recibir.
/// </summary>
public sealed class SignalRMatchRelay(HttpClient http) : IMatchRelay, IAsyncDisposable
{
    private HubConnection? _connection;

    public bool IsConfigured => http.BaseAddress is not null;

    public async Task<RelayTicket> CreateAsync(CancellationToken cancellationToken = default)
    {
        using var response = await PostAsync(RelayProtocol.CreatePath, null, null, cancellationToken);
        return await ReadAsync(response, RelayJsonContext.Default.RelayTicket, cancellationToken);
    }

    public async Task<RelayTicket?> JoinAsync(string code, CancellationToken cancellationToken = default)
    {
        using var response = await PostAsync(RelayProtocol.JoinPath(code), null, null, cancellationToken);
        return response.StatusCode == HttpStatusCode.NotFound
            ? null
            : await ReadAsync(response, RelayJsonContext.Default.RelayTicket, cancellationToken);
    }

    public async Task ConnectAsync(RelayTicket ticket, Func<string, Task> onMessage, Func<RelayStatus, Task> onStatus, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();

        // La primera negociación da la URL del servicio; el token se pide de nuevo en cada reconexión
        // porque caduca (como mucho a la hora).
        var first = await NegotiateAsync(ticket, cancellationToken);
        RelayConnectionInfo? pending = first;
        var connection = new HubConnectionBuilder()
            .WithUrl(first.Url, options =>
            {
                options.AccessTokenProvider = async () =>
                {
                    var info = pending ?? await NegotiateAsync(ticket, CancellationToken.None);
                    pending = null;
                    return info.AccessToken;
                };
            })
            .WithAutomaticReconnect(new ReconnectPolicy())
            .Build();

        connection.On<string>(RelayProtocol.ClientMethod, onMessage);
        connection.Reconnecting += _ => onStatus(RelayStatus.Reconnecting);
        connection.Reconnected += _ => onStatus(RelayStatus.Online);
        connection.Closed += _ => onStatus(RelayStatus.Offline);
        _connection = connection;

        try
        {
            await connection.StartAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DisconnectAsync();

            // Si la llave ya no vale, el error del proveedor de token puede llegar envuelto.
            for (var inner = ex; inner is not null; inner = inner.InnerException)
            {
                if (inner is RelayRejectedException rejected)
                {
                    throw rejected;
                }
            }

            throw ex as RelayException ?? new RelayException("No se puede conectar con el relé.", ex);
        }

        await onStatus(RelayStatus.Online);
    }

    public async Task<bool> SendAsync(RelayTicket ticket, string message, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(message, System.Text.Encoding.UTF8, "application/json");
        using var response = await PostAsync(RelayProtocol.MessagesPath(ticket.Code), ticket, content, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }

        EnsureSuccess(response);
        return true;
    }

    public async Task LeaveAsync(RelayTicket ticket, CancellationToken cancellationToken = default)
    {
        using var response = await PostAsync(RelayProtocol.LeavePath(ticket.Code), ticket, null, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            EnsureSuccess(response);
        }
    }

    public async Task DisconnectAsync()
    {
        var connection = _connection;
        _connection = null;
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }
    }

    public ValueTask DisposeAsync() => new(DisconnectAsync());

    private async Task<RelayConnectionInfo> NegotiateAsync(RelayTicket ticket, CancellationToken cancellationToken)
    {
        using var response = await PostAsync(RelayProtocol.NegotiatePath(ticket.Code), ticket, null, cancellationToken);
        return await ReadAsync(response, RelayJsonContext.Default.RelayConnectionInfo, cancellationToken);
    }

    private async Task<HttpResponseMessage> PostAsync(string path, RelayTicket? ticket, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        if (ticket is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ticket.Token);
        }

        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new RelayException("No hay conexión con el relé. ¿Tienes internet?", ex);
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync(type, cancellationToken)
            ?? throw new RelayException("El relé ha respondido algo raro.");
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                throw new RelayRejectedException();
            case HttpStatusCode.TooManyRequests:
                throw new RelayException("Demasiadas peticiones seguidas. Espera un minuto.");
            case >= HttpStatusCode.BadRequest:
                throw new RelayException($"El relé ha fallado ({(int)response.StatusCode}). Prueba otra vez en un rato.");
        }
    }

    /// <summary>Reintenta enseguida y luego cada vez más espaciado, hasta rendirse a los diez minutos.</summary>
    private sealed class ReconnectPolicy : IRetryPolicy
    {
        private static readonly TimeSpan[] Delays = [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];

        public TimeSpan? NextRetryDelay(RetryContext context) =>
            context.ElapsedTime > TimeSpan.FromMinutes(10) ? null
            : context.PreviousRetryCount < Delays.Length ? Delays[context.PreviousRetryCount]
            : TimeSpan.FromSeconds(30);
    }
}
