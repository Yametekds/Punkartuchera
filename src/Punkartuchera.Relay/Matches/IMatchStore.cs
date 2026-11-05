namespace Punkartuchera.Relay.Matches;

public interface IMatchStore
{
    /// <summary>Guarda una partida nueva. Devuelve false si ya existe una con ese código.</summary>
    Task<bool> TryAddAsync(MatchRecord match, CancellationToken cancellationToken = default);

    Task<MatchRecord?> GetAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sustituye la partida solo si nadie la ha cambiado desde que se leyó (<see cref="MatchRecord.Version"/>).
    /// </summary>
    Task<bool> TryReplaceAsync(MatchRecord match, CancellationToken cancellationToken = default);

    Task DeleteAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Borra las partidas caducadas y devuelve cuántas.</summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
