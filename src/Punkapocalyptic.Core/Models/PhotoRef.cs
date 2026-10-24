namespace Punkapocalyptic.Core.Models;

/// <summary>
/// Referencia a la imagen de una miniatura. Las fotos propias viven en <see cref="Storage.IPhotoStore"/>
/// y la banda solo guarda su clave, para que el listado de bandas sea ligero.
/// </summary>
public sealed record PhotoRef(PhotoSource Source, string Key)
{
    public static PhotoRef Stored(string key) => new(PhotoSource.Stored, key);

    public static PhotoRef Stock(string key) => new(PhotoSource.Stock, key);
}

public enum PhotoSource
{
    /// <summary>Foto hecha con la cámara o elegida de la galería.</summary>
    Stored,

    /// <summary>Imagen genérica incluida en la aplicación.</summary>
    Stock
}
