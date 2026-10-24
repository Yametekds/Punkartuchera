namespace Punkartuchera.Core.Matches;

/// <summary>
/// Gestión de partidas activas. La partida local corre en un único dispositivo;
/// las partidas en línea por código necesitan un servidor y aún no están disponibles.
/// </summary>
public interface IMatchService
{
    /// <summary>Indica si se pueden crear o unirse a partidas por código.</summary>
    bool SupportsOnlineMatches { get; }

    ActiveMatch? Current { get; }

    Task<ActiveMatch> StartLocalAsync(Guid ownBandId, Guid? rivalBandId, CancellationToken cancellationToken = default);

    Task EndAsync(CancellationToken cancellationToken = default);

    /// <summary>Crea una partida en línea y devuelve el código para compartir.</summary>
    Task<string> CreateOnlineAsync(Guid ownBandId, CancellationToken cancellationToken = default);

    Task<ActiveMatch> JoinOnlineAsync(string code, Guid ownBandId, CancellationToken cancellationToken = default);
}

public sealed class ActiveMatch
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid OwnBandId { get; init; }

    public Guid? RivalBandId { get; set; }

    /// <summary>Código de unión; nulo en partidas locales.</summary>
    public string? Code { get; init; }

    public int Round { get; set; } = 1;

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class OnlineMatchesUnavailableException()
    : NotSupportedException("Las partidas en línea por código todavía no están disponibles.");
