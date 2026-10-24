using Punkartuchera.Core.Models;
using Punkartuchera.Core.Serialization;
using Punkartuchera.Core.Storage;

namespace Punkartuchera.UI.Storage;

public sealed class IndexedDbBandRepository(KeyValueDb db) : IBandRepository
{
    public async Task<IReadOnlyList<Band>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var values = await db.GetAllAsync(KeyValueDb.BandsStore, cancellationToken);
        return values.Select(BandSerializer.Deserialize).ToList();
    }

    public async Task<Band?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var json = await db.GetAsync(KeyValueDb.BandsStore, Key(id), cancellationToken);
        return json is null ? null : BandSerializer.Deserialize(json);
    }

    public Task SaveAsync(Band band, CancellationToken cancellationToken = default) =>
        db.PutAsync(KeyValueDb.BandsStore, Key(band.Id), BandSerializer.Serialize(band), cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.RemoveAsync(KeyValueDb.BandsStore, Key(id), cancellationToken);

    private static string Key(Guid id) => id.ToString("N");
}
