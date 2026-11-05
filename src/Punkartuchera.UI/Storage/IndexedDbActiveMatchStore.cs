using System.Text.Json;
using Punkartuchera.Core.Matches;

namespace Punkartuchera.UI.Storage;

/// <summary>La partida activa, en el almacén <c>matches</c> de IndexedDB.</summary>
public sealed class IndexedDbActiveMatchStore(KeyValueDb db) : IActiveMatchStore
{
    private const string Key = "current";

    public async Task<ActiveMatch?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await db.GetAsync(KeyValueDb.MatchesStore, Key, cancellationToken);
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, MatchJsonContext.Default.ActiveMatch);
        }
        catch (JsonException)
        {
            // Partida guardada con un formato que ya no vale: se descarta.
            return null;
        }
    }

    public Task SaveAsync(ActiveMatch? match, CancellationToken cancellationToken = default) =>
        match is null
            ? db.RemoveAsync(KeyValueDb.MatchesStore, Key, cancellationToken)
            : db.PutAsync(KeyValueDb.MatchesStore, Key, JsonSerializer.Serialize(match, MatchJsonContext.Default.ActiveMatch), cancellationToken);
}
