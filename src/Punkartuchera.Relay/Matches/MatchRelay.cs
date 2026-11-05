using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Punkartuchera.Relay.Contracts;
using Punkartuchera.Relay.Realtime;
using Punkartuchera.Relay.Security;

namespace Punkartuchera.Relay.Matches;

/// <summary>
/// Lógica del relé, sin nada de HTTP: crear partida, entrar con código, comprobar la llave
/// de cada jugador y pasar sus mensajes al rival. El relé no entiende el juego; solo comprueba
/// que el mensaje es JSON, de un tipo permitido y de un tamaño razonable.
/// </summary>
public sealed class MatchRelay(IMatchStore store, IRealtimeChannel channel, TimeProvider time, ILogger<MatchRelay> logger)
{
    /// <summary>Tiempo que espera una partida a que entre el rival.</summary>
    public static readonly TimeSpan OpenFor = TimeSpan.FromMinutes(30);

    /// <summary>Duración máxima de una partida desde que entra el rival.</summary>
    public static readonly TimeSpan PlayFor = TimeSpan.FromHours(12);

    /// <summary>Duración del token de SignalR. La app pide otro al reconectar.</summary>
    public static readonly TimeSpan ConnectionTokenLifetime = TimeSpan.FromHours(1);

    private const int CreateAttempts = 5;

    public async Task<RelayTicket> CreateAsync(CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        for (var attempt = 0; attempt < CreateAttempts; attempt++)
        {
            var token = PlayerTokens.NewToken();
            var match = new MatchRecord
            {
                Code = MatchCode.Generate(),
                HostId = PlayerTokens.NewPlayerId(),
                HostTokenHash = PlayerTokens.Hash(token),
                CreatedAt = now,
                ExpiresAt = now + OpenFor,
            };

            // Si el código ya está en uso (muy raro), se prueba otro.
            if (await store.TryAddAsync(match, cancellationToken))
            {
                logger.LogInformation("Partida {Code} creada", match.Code);
                return new RelayTicket(match.Code, match.HostId, token, IsHost: true, match.ExpiresAt);
            }
        }

        throw new InvalidOperationException("No se ha encontrado un código libre.");
    }

    /// <summary>
    /// Entra como rival. Devuelve null si el código no existe, ha caducado o ya tiene rival:
    /// los tres casos responden igual para no dar pistas a quien pruebe códigos.
    /// </summary>
    public async Task<RelayTicket?> JoinAsync(string code, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var match = await store.GetAsync(code, cancellationToken);
        if (match is null || match.ExpiresAt <= now || match.IsFull)
        {
            return null;
        }

        var token = PlayerTokens.NewToken();
        match.GuestId = PlayerTokens.NewPlayerId();
        match.GuestTokenHash = PlayerTokens.Hash(token);
        match.ExpiresAt = now + PlayFor;

        // Concurrencia optimista: si otro ha entrado justo antes, la versión ya no coincide y se rechaza.
        if (!await store.TryReplaceAsync(match, cancellationToken))
        {
            return null;
        }

        logger.LogInformation("Rival dentro de la partida {Code}", code);
        return new RelayTicket(code, match.GuestId, token, IsHost: false, match.ExpiresAt);
    }

    /// <summary>Comprueba la llave del jugador. Devuelve null si no vale para esa partida.</summary>
    public async Task<PlayerSession?> AuthenticateAsync(string code, string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 100)
        {
            return null;
        }

        var match = await store.GetAsync(code, cancellationToken);
        if (match is null || match.ExpiresAt <= time.GetUtcNow())
        {
            return null;
        }

        // Se comparan las dos huellas siempre, para tardar lo mismo sea quien sea.
        var isHost = PlayerTokens.Matches(token, match.HostTokenHash);
        var isGuest = PlayerTokens.Matches(token, match.GuestTokenHash);
        return isHost ? new PlayerSession(match, match.HostId)
            : isGuest ? new PlayerSession(match, match.GuestId!)
            : null;
    }

    public Task<RelayConnectionInfo> ConnectAsync(PlayerSession player, CancellationToken cancellationToken = default)
    {
        var remaining = player.Match.ExpiresAt - time.GetUtcNow();
        var lifetime = remaining < ConnectionTokenLifetime ? remaining : ConnectionTokenLifetime;
        return channel.NegotiateAsync(player.PlayerId, lifetime, cancellationToken);
    }

    public async Task<ForwardResult> ForwardAsync(PlayerSession player, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
    {
        if (body.Length is 0 or > RelayProtocol.MaxMessageBytes)
        {
            return ForwardResult.TooLarge;
        }

        if (!IsAllowedMessage(body))
        {
            return ForwardResult.Invalid;
        }

        var opponent = player.Match.OpponentOf(player.PlayerId);
        if (opponent is null)
        {
            return ForwardResult.NoOpponent;
        }

        await channel.SendAsync(opponent, Encoding.UTF8.GetString(body.Span), cancellationToken);
        return ForwardResult.Sent;
    }

    /// <summary>Cierra la partida para los dos y avisa al rival.</summary>
    public async Task LeaveAsync(PlayerSession player, CancellationToken cancellationToken = default)
    {
        await store.DeleteAsync(player.Match.Code, cancellationToken);
        logger.LogInformation("Partida {Code} cerrada", player.Match.Code);

        if (player.Match.OpponentOf(player.PlayerId) is { } opponent)
        {
            await channel.SendAsync(opponent, $$"""{"type":"{{MessageTypes.Bye}}"}""", cancellationToken);
        }
    }

    public Task<int> CleanupAsync(CancellationToken cancellationToken = default) =>
        store.DeleteExpiredAsync(time.GetUtcNow(), cancellationToken);

    /// <summary>Un objeto JSON con <c>type</c> de los que puede mandar un jugador. El resto del contenido es cosa de la app.</summary>
    internal static bool IsAllowedMessage(ReadOnlyMemory<byte> body)
    {
        try
        {
            // Parse rechaza también lo que venga detrás del objeto.
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 8 });
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && RelayProtocol.ClientMessageTypes.Contains(type.GetString()!);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public sealed record PlayerSession(MatchRecord Match, string PlayerId);

public enum ForwardResult
{
    Sent,
    TooLarge,
    Invalid,
    NoOpponent,
}
