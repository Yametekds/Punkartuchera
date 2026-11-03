using System.Text.Json;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Serialization;
using Punkartuchera.Core.Storage;

namespace Punkartuchera.Core.Sharing;

/// <summary>
/// Exporta una banda propia para pasársela al rival, en un archivo <c>.punkband</c> (con fotos)
/// o en el texto de un QR (sin fotos), e importa las que llegan como bandas rivales de solo lectura.
/// Al exportar se quitan las balas y las notas de la banda: eso no lo ve el rival.
/// </summary>
public sealed class BandExchangeService(IBandRepository bands, IPhotoStore photos, IRulesCatalog rules)
{
    public const string FileExtension = ".punkband";
    public const string ContentType = "application/json";

    /// <summary>Tamaño máximo aceptado al importar.</summary>
    public const long MaxFileBytes = 20 * 1024 * 1024;

    private static readonly HashSet<string> AllowedPhotoTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    public async Task<ExportedBand> ExportAsync(Guid bandId, CancellationToken cancellationToken = default)
    {
        var source = await GetExportableAsync(bandId, cancellationToken);
        var band = BandSerializer.Clone(source);
        band.Notes = null;
        band.Ownership = BandOwnership.Rival;
        band.SourceId = null;

        var package = new BandPackage { Band = band };
        foreach (var member in band.Members)
        {
            member.Ammo = 0;
            if (member.Photo is not { Source: PhotoSource.Stored } photo)
            {
                continue;
            }

            if (package.Photos.Any(p => p.Key == photo.Key))
            {
                continue;
            }

            var stored = await photos.GetAsync(photo.Key, cancellationToken);
            if (stored is null)
            {
                member.Photo = null;
                continue;
            }

            package.Photos.Add(new PackagedPhoto { Key = photo.Key, ContentType = stored.ContentType, Data = stored.Data });
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(package, PunkJsonContext.Default.BandPackage);
        return new ExportedBand(FileNameFor(source.Name), json);
    }

    /// <summary>Texto del QR con la banda sin fotos ni balas.</summary>
    /// <exception cref="BandExchangeException">Si la banda no cabe en un QR.</exception>
    public async Task<BandQrPayload> ExportQrAsync(Guid bandId, CancellationToken cancellationToken = default)
    {
        var source = await GetExportableAsync(bandId, cancellationToken);
        return BandQrCodec.Encode(source, rules.Stats.Select(s => s.Key).ToList());
    }

    /// <summary>
    /// Guarda la banda del archivo como rival. Si ya había una copia de esa misma banda, la sustituye.
    /// </summary>
    public async Task<Band> ImportAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var package = await ReadAsync(content, cancellationToken);
        var packaged = package.Photos
            .Where(p => AllowedPhotoTypes.Contains(p.ContentType) && p.Data.Length > 0)
            .GroupBy(p => p.Key)
            .ToDictionary(g => g.Key, g => g.First());
        return await SaveRivalAsync(package.Band, packaged, cancellationToken);
    }

    /// <summary>
    /// Guarda como rival la banda leída de un QR. Si ya se tenía esa banda (por ejemplo, del archivo),
    /// la sustituye y conserva las fotos de los miembros que siguen llamándose igual.
    /// </summary>
    public Task<Band> ImportQrAsync(string qrText, CancellationToken cancellationToken = default) =>
        SaveRivalAsync(BandQrCodec.Decode(qrText), new Dictionary<string, PackagedPhoto>(), cancellationToken);

    private async Task<Band> GetExportableAsync(Guid bandId, CancellationToken cancellationToken)
    {
        var source = await bands.GetAsync(bandId, cancellationToken)
            ?? throw new BandExchangeException("La banda no existe.");
        return source.IsReadOnly
            ? throw new BandExchangeException("Las bandas rivales no se pueden volver a pasar.")
            : source;
    }

    private async Task<Band> SaveRivalAsync(Band band, Dictionary<string, PackagedPhoto> packaged, CancellationToken cancellationToken)
    {
        var sourceId = band.Id;

        var previous = (await bands.GetAllAsync(cancellationToken))
            .FirstOrDefault(b => b.IsReadOnly && b.SourceId == sourceId);

        band.Id = previous?.Id ?? Guid.NewGuid();
        band.SourceId = sourceId;
        band.Ownership = BandOwnership.Rival;
        band.Notes = null;
        band.Name = string.IsNullOrWhiteSpace(band.Name) ? "Rival sin nombre" : band.Name.Trim();
        band.CreatedAt = previous?.CreatedAt ?? DateTimeOffset.UtcNow;
        band.UpdatedAt = DateTimeOffset.UtcNow;
        band.SchemaVersion = Band.CurrentSchemaVersion;

        var saved = new Dictionary<string, string>();
        var kept = new HashSet<string>();

        foreach (var member in band.Members)
        {
            member.Ammo = 0;
            member.Stats ??= [];
            member.Skills ??= [];
            member.Gear ??= [];
            if (member.Photo is null
                && previous?.Members.FirstOrDefault(m => m.Name == member.Name)?.Photo is { Source: PhotoSource.Stored } earlier)
            {
                member.Photo = earlier;
                kept.Add(earlier.Key);
                continue;
            }

            if (member.Photo is not { Source: PhotoSource.Stored } photo)
            {
                continue;
            }

            if (!saved.TryGetValue(photo.Key, out var key))
            {
                if (!packaged.TryGetValue(photo.Key, out var data))
                {
                    member.Photo = null;
                    continue;
                }

                key = await photos.SaveAsync(data.Data, data.ContentType, cancellationToken);
                saved[photo.Key] = key;
            }

            member.Photo = PhotoRef.Stored(key);
        }

        await bands.SaveAsync(band, cancellationToken);

        if (previous is not null)
        {
            foreach (var member in previous.Members)
            {
                if (member.Photo is { Source: PhotoSource.Stored } old && !kept.Contains(old.Key))
                {
                    await photos.DeleteAsync(old.Key, cancellationToken);
                }
            }
        }

        return band;
    }

    private static async Task<BandPackage> ReadAsync(Stream content, CancellationToken cancellationToken)
    {
        BandPackage? package;
        try
        {
            package = await JsonSerializer.DeserializeAsync(content, PunkJsonContext.Default.BandPackage, cancellationToken);
        }
        catch (JsonException)
        {
            throw new BandExchangeException("Eso no es una banda de Punkartuchera. Pide al rival que te la pase otra vez.");
        }

        if (package is null || package.Format != BandPackage.FormatName || package.Band is null)
        {
            throw new BandExchangeException("Eso no es una banda de Punkartuchera. Pide al rival que te la pase otra vez.");
        }

        if (package.Version > BandPackage.CurrentVersion)
        {
            throw new BandExchangeException("La banda viene de una versión más nueva de la app. Actualiza y vuelve a probar.");
        }

        package.Band.Members ??= [];
        package.Photos ??= [];
        return package;
    }

    private static string FileNameFor(string bandName)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var safe = new string(bandName.Select(c => invalid.Contains(c) || char.IsControl(c) ? '-' : c).ToArray()).Trim(' ', '.', '-');
        return $"{(safe.Length == 0 ? "banda" : safe)}{FileExtension}";
    }
}

public sealed record ExportedBand(string FileName, byte[] Content);

public sealed class BandExchangeException(string message) : Exception(message);
