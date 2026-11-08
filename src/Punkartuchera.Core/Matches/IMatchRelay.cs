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

/// <summary>
/// Partidas guardadas en el dispositivo. Puede haber varias a medias; una de ellas
/// es la que está en pantalla (la actual) y se retoma al abrir la app.
/// </summary>
public interface ISavedMatchStore
{
    Task<IReadOnlyList<ActiveMatch>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ActiveMatch?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(ActiveMatch match, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>La partida que estaba en pantalla, si había alguna.</summary>
    Task<Guid?> GetCurrentIdAsync(CancellationToken cancellationToken = default);

    Task SetCurrentIdAsync(Guid? id, CancellationToken cancellationToken = default);
}

public sealed class InMemorySavedMatchStore : ISavedMatchStore
{
    private readonly Dictionary<Guid, string> _matches = [];
    private Guid? _current;

    public Task<IReadOnlyList<ActiveMatch>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ActiveMatch> all = _matches.Values.Select(Read).ToList();
        return Task.FromResult(all);
    }

    public Task<ActiveMatch?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_matches.TryGetValue(id, out var json) ? Read(json) : null);

    public Task SaveAsync(ActiveMatch match, CancellationToken cancellationToken = default)
    {
        _matches[match.Id] = System.Text.Json.JsonSerializer.Serialize(match, MatchJsonContext.Default.ActiveMatch);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _matches.Remove(id);
        return Task.CompletedTask;
    }

    public Task<Guid?> GetCurrentIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(_current);

    public Task SetCurrentIdAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        _current = id;
        return Task.CompletedTask;
    }

    private static ActiveMatch Read(string json) =>
        System.Text.Json.JsonSerializer.Deserialize(json, MatchJsonContext.Default.ActiveMatch)!;
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
