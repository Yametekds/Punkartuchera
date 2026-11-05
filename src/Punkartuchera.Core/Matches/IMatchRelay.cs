using Punkartuchera.Relay.Contracts;

namespace Punkartuchera.Core.Matches;

/// <summary>
/// Cliente del relé en línea. La app lo implementa con HTTP y SignalR; los tests, en memoria.
/// </summary>
public interface IMatchRelay
{
    /// <summary>False si la app no tiene dirección de relé configurada.</summary>
    bool IsConfigured { get; }

    /// <exception cref="RelayException">Si el relé no responde o rechaza la petición.</exception>
    Task<RelayTicket> CreateAsync(CancellationToken cancellationToken = default);

    /// <summary>Devuelve null si no hay partida con ese código o ya tiene rival.</summary>
    Task<RelayTicket?> JoinAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre la conexión en tiempo real. Los mensajes del rival llegan a <paramref name="onMessage"/>
    /// y los cambios de conexión a <paramref name="onStatus"/>.
    /// </summary>
    /// <exception cref="RelayRejectedException">Si el relé ya no reconoce la llave (partida cerrada o caducada).</exception>
    Task ConnectAsync(RelayTicket ticket, Func<string, Task> onMessage, Func<RelayStatus, Task> onStatus, CancellationToken cancellationToken = default);

    /// <summary>Manda un mensaje al rival. Devuelve false si todavía no hay rival.</summary>
    Task<bool> SendAsync(RelayTicket ticket, string message, CancellationToken cancellationToken = default);

    Task LeaveAsync(RelayTicket ticket, CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}

public class RelayException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>El relé no reconoce la llave: la partida se cerró o caducó.</summary>
public sealed class RelayRejectedException() : RelayException("La partida ya no existe en el relé.");

public interface IActiveMatchStore
{
    Task<ActiveMatch?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Guarda la partida; con null la borra.</summary>
    Task SaveAsync(ActiveMatch? match, CancellationToken cancellationToken = default);
}

public sealed class InMemoryActiveMatchStore : IActiveMatchStore
{
    private string? _json;

    public Task<ActiveMatch?> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_json is null ? null : System.Text.Json.JsonSerializer.Deserialize(_json, MatchJsonContext.Default.ActiveMatch));

    public Task SaveAsync(ActiveMatch? match, CancellationToken cancellationToken = default)
    {
        _json = match is null ? null : System.Text.Json.JsonSerializer.Serialize(match, MatchJsonContext.Default.ActiveMatch);
        return Task.CompletedTask;
    }
}

/// <summary>Relé sin configurar: la app solo ofrece partidas en un móvil.</summary>
public sealed class DisabledMatchRelay : IMatchRelay
{
    public bool IsConfigured => false;

    public Task<RelayTicket> CreateAsync(CancellationToken cancellationToken = default) => throw Unavailable();

    public Task<RelayTicket?> JoinAsync(string code, CancellationToken cancellationToken = default) => throw Unavailable();

    public Task ConnectAsync(RelayTicket ticket, Func<string, Task> onMessage, Func<RelayStatus, Task> onStatus, CancellationToken cancellationToken = default) =>
        throw Unavailable();

    public Task<bool> SendAsync(RelayTicket ticket, string message, CancellationToken cancellationToken = default) => throw Unavailable();

    public Task LeaveAsync(RelayTicket ticket, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DisconnectAsync() => Task.CompletedTask;

    private static RelayException Unavailable() => new("Esta versión de la app no tiene partidas en línea.");
}
