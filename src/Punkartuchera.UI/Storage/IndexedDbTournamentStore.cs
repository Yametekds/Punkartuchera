using System.Text.Json;
using Punkartuchera.Core.Tournaments;

namespace Punkartuchera.UI.Storage;

/// <summary>Torneos en el almacén <c>tournaments</c> de IndexedDB, uno por entrada (la clave es su Id).</summary>
public sealed class IndexedDbTournamentStore(KeyValueDb db) : ITournamentStore
{
    public async Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var values = await db.GetAllAsync(KeyValueDb.TournamentsStore, cancellationToken);
        return values.Select(Read).OfType<Tournament>().ToList();
    }

    public async Task<Tournament?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Read(await db.GetAsync(KeyValueDb.TournamentsStore, Key(id), cancellationToken));

    public Task SaveAsync(Tournament tournament, CancellationToken cancellationToken = default) =>
        db.PutAsync(KeyValueDb.TournamentsStore, Key(tournament.Id), JsonSerializer.Serialize(tournament, TournamentJsonContext.Default.Tournament), cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.RemoveAsync(KeyValueDb.TournamentsStore, Key(id), cancellationToken);

    private static string Key(Guid id) => id.ToString("N");

    private static Tournament? Read(string? json)
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, TournamentJsonContext.Default.Tournament);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
