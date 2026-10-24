using System.Collections.Concurrent;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Serialization;

namespace Punkartuchera.Core.Storage;

/// <summary>
/// Repositorio en memoria. Guarda copias serializadas para comportarse igual
/// que un almacenamiento real (modificar el objeto devuelto no altera lo guardado).
/// </summary>
public sealed class InMemoryBandRepository : IBandRepository
{
    private readonly ConcurrentDictionary<Guid, string> _bands = new();

    public Task<IReadOnlyList<Band>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Band> bands = _bands.Values.Select(BandSerializer.Deserialize).ToList();
        return Task.FromResult(bands);
    }

    public Task<Band?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_bands.TryGetValue(id, out var json) ? BandSerializer.Deserialize(json) : null);

    public Task SaveAsync(Band band, CancellationToken cancellationToken = default)
    {
        _bands[band.Id] = BandSerializer.Serialize(band);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _bands.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryPhotoStore : IPhotoStore
{
    private readonly ConcurrentDictionary<string, StoredPhoto> _photos = new();

    public Task<string> SaveAsync(byte[] data, string contentType, CancellationToken cancellationToken = default)
    {
        var key = Guid.NewGuid().ToString("N");
        _photos[key] = new StoredPhoto(key, contentType, data.ToArray());
        return Task.FromResult(key);
    }

    public Task<StoredPhoto?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_photos.TryGetValue(key, out var photo) ? photo : null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _photos.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
