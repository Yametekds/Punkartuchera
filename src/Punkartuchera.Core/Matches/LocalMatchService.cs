namespace Punkartuchera.Core.Matches;

/// <summary>
/// Partida en un solo dispositivo. Las operaciones en línea lanzan
/// <see cref="OnlineMatchesUnavailableException"/> hasta que exista un servicio de emparejamiento.
/// </summary>
public sealed class LocalMatchService : IMatchService
{
    public bool SupportsOnlineMatches => false;

    public ActiveMatch? Current { get; private set; }

    public Task<ActiveMatch> StartLocalAsync(Guid ownBandId, Guid? rivalBandId, CancellationToken cancellationToken = default)
    {
        Current = new ActiveMatch { OwnBandId = ownBandId, RivalBandId = rivalBandId };
        return Task.FromResult(Current);
    }

    public Task EndAsync(CancellationToken cancellationToken = default)
    {
        Current = null;
        return Task.CompletedTask;
    }

    public Task<string> CreateOnlineAsync(Guid ownBandId, CancellationToken cancellationToken = default) =>
        throw new OnlineMatchesUnavailableException();

    public Task<ActiveMatch> JoinOnlineAsync(string code, Guid ownBandId, CancellationToken cancellationToken = default) =>
        throw new OnlineMatchesUnavailableException();
}
