using Punkartuchera.Core.Models;
using Punkartuchera.Core.Storage;

namespace Punkartuchera.UI.Services;

/// <summary>Resuelve la URL de la imagen de una miniatura y cachea las fotos ya leídas.</summary>
public sealed class PhotoUrlProvider(IPhotoStore photos)
{
    private readonly Dictionary<string, string?> _cache = [];

    public async Task<string?> GetUrlAsync(PhotoRef? photo)
    {
        switch (photo)
        {
            case null:
                return null;
            case { Source: PhotoSource.Stock }:
                return StockImages.Url(photo.Key);
        }

        if (_cache.TryGetValue(photo.Key, out var cached))
        {
            return cached;
        }

        var stored = await photos.GetAsync(photo.Key);
        var url = stored?.ToDataUrl();
        _cache[photo.Key] = url;
        return url;
    }

    public void Remember(string key, string dataUrl) => _cache[key] = dataUrl;

    public void Forget(string key) => _cache.Remove(key);
}
