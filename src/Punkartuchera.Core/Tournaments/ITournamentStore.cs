using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Punkartuchera.Core.Tournaments;

/// <summary>Dónde guarda el organizador sus torneos. Hoy, en el propio navegador.</summary>
public interface ITournamentStore
{
    Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Tournament?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(Tournament tournament, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class InMemoryTournamentStore : ITournamentStore
{
    private readonly ConcurrentDictionary<Guid, string> _items = new();

    public Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Tournament> all = _items.Values.Select(Read).ToList();
        return Task.FromResult(all);
    }

    public Task<Tournament?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.TryGetValue(id, out var json) ? Read(json) : null);

    public Task SaveAsync(Tournament tournament, CancellationToken cancellationToken = default)
    {
        _items[tournament.Id] = JsonSerializer.Serialize(tournament, TournamentJsonContext.Default.Tournament);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _items.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    private static Tournament Read(string json) =>
        JsonSerializer.Deserialize(json, TournamentJsonContext.Default.Tournament)!;
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Tournament))]
public sealed partial class TournamentJsonContext : JsonSerializerContext;
