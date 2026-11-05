using System.Text;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Services;

namespace Punkartuchera.Core.Sharing;

/// <summary>
/// Limpia una banda que llega de fuera (archivo o QR) antes de guardarla.
/// Todo lo que trae el rival se trata como no fiable: puede venir roto, inflado o manipulado a mano.
/// </summary>
public static class RivalBandSanitizer
{
    public const int MaxMembers = 50;

    /// <summary>Largo máximo de nombres de miembro, tipos, habilidades y piezas de equipo.</summary>
    public const int MaxTextLength = 200;

    public const int MaxNotesLength = 1000;

    /// <summary>Máximo de habilidades y de piezas de equipo por miembro.</summary>
    public const int MaxListItems = 30;

    public const int MaxCost = 100_000;

    /// <summary>Una foto encuadrada pesa unos 100 KB; esto deja margen para fotos antiguas sin recortar.</summary>
    public const int MaxPhotoBytes = 5 * 1024 * 1024;

    /// <summary>Deja la banda lista para guardarla como rival: sin nulos, dentro de los límites y sin referencias externas.</summary>
    public static void Sanitize(Band band, IRulesCatalog rules)
    {
        band.Name = Clean(band.Name, BandService.MaxNameLength);
        band.FactionId = Clean(band.FactionId, MaxTextLength);
        band.SubfactionId = NullIfEmpty(Clean(band.SubfactionId, MaxTextLength));
        band.Notes = null;
        band.Ownership = BandOwnership.Rival;

        band.Members = (band.Members ?? [])
            .OfType<Miniature>()
            .Take(MaxMembers)
            .ToList();

        foreach (var member in band.Members)
        {
            // Identificadores nuevos: el archivo podría repetirlos o reutilizar los de otra banda.
            member.Id = Guid.NewGuid();
            member.Name = Clean(member.Name, BandService.MaxNameLength);
            member.Role = Clean(member.Role, MaxTextLength);
            member.Notes = NullIfEmpty(Clean(member.Notes, MaxNotesLength));
            member.Ammo = 0;
            member.Cost = member.Cost is { } cost ? Math.Clamp(cost, 0, MaxCost) : null;

            member.Skills = (member.Skills ?? [])
                .Select(s => Clean(s, MaxTextLength))
                .Where(s => s.Length > 0)
                .Take(MaxListItems)
                .ToList();

            member.Gear = (member.Gear ?? [])
                .OfType<Equipment>()
                .Select(g => new Equipment { Name = Clean(g.Name, MaxTextLength), Notes = NullIfEmpty(Clean(g.Notes, MaxNotesLength)) })
                .Where(g => g.Name.Length > 0)
                .Take(MaxListItems)
                .ToList();

            // Solo los atributos del reglamento, y dentro de su rango.
            var stats = member.Stats ?? [];
            member.Stats = rules.Stats
                .Where(s => stats.ContainsKey(s.Key))
                .ToDictionary(s => s.Key, s => Math.Clamp(stats[s.Key], s.Min, s.Max));

            member.Photo = member.Photo switch
            {
                { Source: PhotoSource.Stored } stored when !string.IsNullOrEmpty(stored.Key) => stored,
                { Source: PhotoSource.Stock } stock when StockPortraits.Exists(stock.Key) => stock,
                { Source: PhotoSource.Official } official when rules.IsOfficialPhoto(official.Key) => official,
                _ => null,
            };
        }

        band.Name = band.Name.Length == 0 ? "Rival sin nombre" : band.Name;
    }

    /// <summary>
    /// Comprueba que los bytes son de verdad la imagen que dicen ser, mirando su firma.
    /// Así no se guarda un SVG, un HTML o cualquier otra cosa disfrazada de foto.
    /// </summary>
    public static bool IsImage(byte[]? data, string? contentType)
    {
        if (data is null || data.Length == 0 || data.Length > MaxPhotoBytes)
        {
            return false;
        }

        ReadOnlySpan<byte> bytes = data;
        return contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]),
            "image/png" => bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            "image/webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8),
            _ => false,
        };
    }

    /// <summary>
    /// Recorta, quita espacios sobrantes y elimina caracteres de control y de cambio de dirección
    /// del texto (estos últimos sirven para que un nombre se vea distinto de lo que es).
    /// </summary>
    private static string Clean(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(text.Length, maxLength));
        foreach (var c in text)
        {
            if (builder.Length >= maxLength)
            {
                break;
            }

            if ((char.IsControl(c) && c != '\n') || IsBidiControl(c))
            {
                continue;
            }

            builder.Append(c);
        }

        // Un recorte a mitad de un emoji dejaría medio carácter suelto.
        if (builder.Length > 0 && char.IsHighSurrogate(builder[^1]))
        {
            builder.Length--;
        }

        return builder.ToString().Trim();
    }

    private static bool IsBidiControl(char c) =>
        c is >= '‪' and <= '‮' or >= '⁦' and <= '⁩' or '‎' or '‏' or '؜';

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
