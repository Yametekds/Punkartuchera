using System.Text.Json;
using Punkartuchera.Core.Matches;

namespace Punkartuchera.UI.Storage;

/// <summary>
/// Partidas guardadas en el almacén <c>matches</c> de IndexedDB: una entrada por partida
/// (la clave es su Id) y la entrada <c>current</c> con el Id de la que está en pantalla.
/// </summary>
public sealed class IndexedDbSavedMatchStore(KeyValueDb db) : ISavedMatchStore
{
    private const string CurrentKey = "current";

    private bool _migrated;

    public async Task<IReadOnlyList<ActiveMatch>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await MigrateAsync(cancellationToken);
        var values = await db.GetAllAsync(KeyValueDb.MatchesStore, cancellationToken);
        return values.Select(Read).OfType<ActiveMatch>().ToList();
    }

    public async Task<ActiveMatch?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await MigrateAsync(cancellationToken);
        return Read(await db.GetAsync(KeyValueDb.MatchesStore, Key(id), cancellationToken));
    }

    public Task SaveAsync(ActiveMatch match, CancellationToken cancellationToken = default) =>
        db.PutAsync(KeyValueDb.MatchesStore, Key(match.Id), JsonSerializer.Serialize(match, MatchJsonContext.Default.ActiveMatch), cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.RemoveAsync(KeyValueDb.MatchesStore, Key(id), cancellationToken);

    public async Task<Guid?> GetCurrentIdAsync(CancellationToken cancellationToken = default)
    {
        await MigrateAsync(cancellationToken);
        var value = await db.GetAsync(KeyValueDb.MatchesStore, CurrentKey, cancellationToken);
        return Guid.TryParseExact(value, "N", out var id) ? id : null;
    }

    public Task SetCurrentIdAsync(Guid? id, CancellationToken cancellationToken = default) =>
        id is { } value
            ? db.PutAsync(KeyValueDb.MatchesStore, CurrentKey, Key(value), cancellationToken)
            : db.RemoveAsync(KeyValueDb.MatchesStore, CurrentKey, cancellationToken);

    private static string Key(Guid id) => id.ToString("N");

    /// <summary>Lee una partida. La entrada <c>current</c> y las que tengan un formato que ya no vale se ignoran.</summary>
    private static ActiveMatch? Read(string? json)
    {
        if (json is null || !json.StartsWith('{'))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, MatchJsonContext.Default.ActiveMatch);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Antes solo había una partida y se guardaba entera en <c>current</c>: pasa a su propia entrada.</summary>
    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if (_migrated)
        {
            return;
        }

        _migrated = true;
        var legacy = await db.GetAsync(KeyValueDb.MatchesStore, CurrentKey, cancellationToken);
        if (legacy is null || !legacy.StartsWith('{'))
        {
            return;
        }

        if (Read(legacy) is { } match)
        {
            await SaveAsync(match, cancellationToken);
            await SetCurrentIdAsync(match.Id, cancellationToken);
        }
        else
        {
            await SetCurrentIdAsync(null, cancellationToken);
        }
    }
}
