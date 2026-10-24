using Punkartuchera.Core.Storage;

namespace Punkartuchera.UI.Storage;

/// <summary>Guarda cada foto como data URL, que es lo que necesita la etiqueta img.</summary>
public sealed class IndexedDbPhotoStore(KeyValueDb db) : IPhotoStore
{
    public async Task<string> SaveAsync(byte[] data, string contentType, CancellationToken cancellationToken = default)
    {
        var key = Guid.NewGuid().ToString("N");
        var photo = new StoredPhoto(key, contentType, data);
        await db.PutAsync(KeyValueDb.PhotosStore, key, photo.ToDataUrl(), cancellationToken);
        return key;
    }

    public async Task<StoredPhoto?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var dataUrl = await db.GetAsync(KeyValueDb.PhotosStore, key, cancellationToken);
        return dataUrl is null ? null : Parse(key, dataUrl);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        db.RemoveAsync(KeyValueDb.PhotosStore, key, cancellationToken);

    private static StoredPhoto? Parse(string key, string dataUrl)
    {
        // Formato: data:<tipo>;base64,<datos>
        const string prefix = "data:";
        const string marker = ";base64,";
        var markerIndex = dataUrl.IndexOf(marker, StringComparison.Ordinal);
        if (!dataUrl.StartsWith(prefix, StringComparison.Ordinal) || markerIndex < 0)
        {
            return null;
        }

        var contentType = dataUrl[prefix.Length..markerIndex];
        var data = Convert.FromBase64String(dataUrl[(markerIndex + marker.Length)..]);
        return new StoredPhoto(key, contentType, data);
    }
}
