using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Sharing;

/// <summary>
/// Contenido de un archivo <c>.punkband</c>: la banda tal como la verá el rival
/// y las fotos de sus miniaturas, para que todo quepa en un solo archivo.
/// </summary>
public sealed class BandPackage
{
    public const string FormatName = "punkartuchera-band";
    public const int CurrentVersion = 1;

    public string Format { get; set; } = FormatName;

    public int Version { get; set; } = CurrentVersion;

    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;

    public Band Band { get; set; } = new();

    /// <summary>Fotos propias indexadas por la clave que usa <see cref="PhotoRef.Key"/> dentro de la banda.</summary>
    public List<PackagedPhoto> Photos { get; set; } = [];
}

public sealed class PackagedPhoto
{
    public string Key { get; set; } = string.Empty;

    public string ContentType { get; set; } = "image/jpeg";

    public byte[] Data { get; set; } = [];
}
