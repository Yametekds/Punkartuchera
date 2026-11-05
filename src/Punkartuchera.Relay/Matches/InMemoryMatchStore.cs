using System.Collections.Concurrent;

namespace Punkartuchera.Relay.Matches;

/// <summary>Almacén en memoria para los tests. Imita la concurrencia optimista de Table Storage.</summary>
public sealed class InMemoryMatchStore : IMatchStore
{
    private readonly ConcurrentDictionary<string, MatchRecord> _matches = new(StringComparer.Ordinal);

    public int Count => _matches.Count;

    public Task<bool> TryAddAsync(MatchRecord match, CancellationToken cancellationToken = default)
    {
        var copy = Copy(match);
        copy.Version = Guid.NewGuid().ToString("N");
        return Task.FromResult(_matches.TryAdd(match.Code, copy));
    }

    public Task<MatchRecord?> GetAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(_matches.TryGetValue(code, out var match) ? Copy(match) : null);

    public Task<bool> TryReplaceAsync(MatchRecord match, CancellationToken cancellationToken = default)
    {
        if (!_matches.TryGetValue(match.Code, out var current) || current.Version != match.Version)
        {
            return Task.FromResult(false);
        }

        var copy = Copy(match);
        copy.Version = Guid.NewGuid().ToString("N");
        return Task.FromResult(_matches.TryUpdate(match.Code, copy, current));
    }

    public Task DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        _matches.TryRemove(code, out _);
        return Task.CompletedTask;
    }

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var expired = _matches.Values.Where(m => m.ExpiresAt <= now).Select(m => m.Code).ToList();
        foreach (var code in expired)
        {
            _matches.TryRemove(code, out _);
        }

        return Task.FromResult(expired.Count);
    }

    private static MatchRecord Copy(MatchRecord m) => new()
    {
        Code = m.Code,
        HostId = m.HostId,
        HostTokenHash = m.HostTokenHash,
        GuestId = m.GuestId,
        GuestTokenHash = m.GuestTokenHash,
        CreatedAt = m.CreatedAt,
        ExpiresAt = m.ExpiresAt,
        Version = m.Version,
    };
}
