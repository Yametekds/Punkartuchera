namespace Punkapocalyptic.Core.Storage;

/// <summary>
/// Almacén de fotos de miniaturas, separado de las bandas para no cargar
/// todas las imágenes al listar.
/// </summary>
public interface IPhotoStore
{
    /// <summary>Guarda la imagen y devuelve su clave.</summary>
    Task<string> SaveAsync(byte[] data, string contentType, CancellationToken cancellationToken = default);

    Task<StoredPhoto?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public sealed record StoredPhoto(string Key, string ContentType, byte[] Data)
{
    public string ToDataUrl() => $"data:{ContentType};base64,{Convert.ToBase64String(Data)}";
}
