using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Sharing;

/// <summary>
/// Mete una banda entera, sin fotos ni balas, en el texto de un QR y la recupera.
/// Formato: <c>PKB1:</c> + Base45( CRC-32 (4 bytes) + JSON compacto comprimido con Deflate ).
/// La suma de comprobación hace que una lectura dañada se rechace en vez de cargar datos rotos.
/// </summary>
public static class BandQrCodec
{
    public const string Prefix = "PKB1:";

    /// <summary>
    /// Máximo de caracteres del QR. Por encima el código sale tan denso que cuesta leerlo con el móvil.
    /// </summary>
    public const int MaxLength = 2400;

    /// <summary>Lo más que cabe en un QR (versión 40, modo alfanumérico). Un texto más largo no viene de un QR.</summary>
    public const int MaxReadLength = 4296;

    /// <summary>
    /// Tope del JSON descomprimido. Una banda normal ocupa unos pocos KB; sin tope, un QR pequeño
    /// podría descomprimirse en megas (bomba de descompresión).
    /// </summary>
    public const int MaxJsonBytes = 64 * 1024;

    /// <exception cref="BandExchangeException">Si la banda no cabe ni quitando las notas.</exception>
    public static BandQrPayload Encode(Band band, IReadOnlyList<string> statOrder, string? playerName = null)
    {
        var compact = ToCompact(band, statOrder);
        compact.Player = NullIfEmpty(playerName);
        var text = Pack(compact);
        if (text.Length <= MaxLength)
        {
            return new BandQrPayload(text, NotesDropped: false);
        }

        foreach (var member in compact.Members)
        {
            member.Notes = null;
            foreach (var gear in member.Gear)
            {
                gear.RemoveRange(1, gear.Count - 1);
            }
        }

        text = Pack(compact);
        return text.Length <= MaxLength
            ? new BandQrPayload(text, NotesDropped: true)
            : throw new BandExchangeException("Esta banda es demasiado gorda para un QR. Mándale el archivo.");
    }

    public static bool LooksLikeBand(string? text) =>
        text is not null && text.Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Devuelve la banda con el identificador que tenía en el dispositivo del rival.</summary>
    /// <exception cref="BandExchangeException">Si el texto no es una banda o llegó dañado.</exception>
    public static Band Decode(string text)
    {
        text = text.Trim();
        if (!LooksLikeBand(text) || text.Length > MaxReadLength)
        {
            throw new BandExchangeException("Ese QR no es una banda de Punkartuchera.");
        }

        CompactBand? compact;
        try
        {
            var bytes = Base45.Decode(text[Prefix.Length..].ToUpperInvariant());
            if (bytes.Length < 5 || BinaryPrimitives.ReadUInt32BigEndian(bytes) != Crc32.Compute(bytes.AsSpan(4)))
            {
                throw new InvalidDataException();
            }

            var json = Inflate(bytes.AsSpan(4).ToArray(), MaxJsonBytes);
            compact = JsonSerializer.Deserialize(json, QrJsonContext.Default.CompactBand);
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or JsonException or IOException)
        {
            throw new BandExchangeException("El QR se ha leído mal. Acércate un poco y vuelve a escanearlo.");
        }

        return compact is null
            ? throw new BandExchangeException("El QR se ha leído mal. Acércate un poco y vuelve a escanearlo.")
            : FromCompact(compact);
    }

    /// <summary>Descomprime sin pasar de <paramref name="maxBytes"/>.</summary>
    /// <exception cref="InvalidDataException">Si el resultado sería más grande.</exception>
    private static byte[] Inflate(byte[] compressed, int maxBytes)
    {
        using var input = new DeflateStream(new MemoryStream(compressed), CompressionMode.Decompress);
        var output = new byte[maxBytes + 1];
        var total = 0;
        int read;
        while ((read = input.Read(output, total, output.Length - total)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new InvalidDataException("El contenido del QR es demasiado grande.");
            }
        }

        return output[..total];
    }

    private static string Pack(CompactBand compact)
    {
        using var buffer = new MemoryStream();
        buffer.Write(new byte[4]);
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            JsonSerializer.Serialize(deflate, compact, QrJsonContext.Default.CompactBand);
        }

        var bytes = buffer.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(bytes, Crc32.Compute(bytes.AsSpan(4)));
        return Prefix + Base45.Encode(bytes);
    }

    private static CompactBand ToCompact(Band band, IReadOnlyList<string> statOrder)
    {
        // Atributos como lista en un orden fijo: las claves solo se escriben una vez.
        var keys = statOrder
            .Concat(band.Members.SelectMany(m => m.Stats.Keys))
            .Distinct()
            .ToList();

        return new CompactBand
        {
            Id = band.Id.ToString("N"),
            Name = band.Name,
            Faction = band.FactionId,
            TotalCost = band.Cost,
            StatKeys = keys,
            Members = band.Members.Select(m => new CompactMember
            {
                Name = m.Name,
                Role = NullIfEmpty(m.Role),
                Stats = keys.Select(k => m.Stats.TryGetValue(k, out var v) ? v : (int?)null).ToList(),
                Skills = m.Skills.Count == 0 ? null : m.Skills,
                Gear = m.Gear.Select(g => string.IsNullOrWhiteSpace(g.Notes) ? new List<string> { g.Name } : [g.Name, g.Notes]).ToList(),
                Notes = NullIfEmpty(m.Notes),
                Portrait = m.Photo is { Source: PhotoSource.Stock } photo ? photo.Key : null,
            }).ToList(),
        };
    }

    private static Band FromCompact(CompactBand compact)
    {
        var keys = compact.StatKeys ?? [];
        var band = new Band
        {
            Id = Guid.TryParseExact(compact.Id, "N", out var id) ? id : Guid.NewGuid(),
            Name = compact.Name ?? string.Empty,
            FactionId = compact.Faction ?? string.Empty,
            Ownership = BandOwnership.Rival,
            TotalCost = compact.TotalCost,
            PlayerName = compact.Player,
            // El JSON puede traer nulos donde no debería: se saltan en vez de romper.
            Members = (compact.Members ?? []).OfType<CompactMember>().Select(m =>
            {
                var member = new Miniature
                {
                    Name = m.Name ?? string.Empty,
                    Role = m.Role ?? string.Empty,
                    Skills = (m.Skills ?? []).OfType<string>().ToList(),
                    Gear = (m.Gear ?? [])
                        .Where(g => g is { Count: > 0 } && g[0] is not null)
                        .Select(g => new Equipment { Name = g[0], Notes = g.Count > 1 ? g[1] : null })
                        .ToList(),
                    Cost = m.Cost,
                    Notes = m.Notes,
                    Photo = string.IsNullOrEmpty(m.Portrait) ? null : PhotoRef.Stock(m.Portrait),
                };
                var stats = m.Stats ?? [];
                for (var i = 0; i < Math.Min(keys.Count, stats.Count); i++)
                {
                    if (stats[i] is { } value && keys[i] is { } key)
                    {
                        member.Stats[key] = value;
                    }
                }

                return member;
            }).ToList(),
        };

        // Códigos antiguos traían el coste por miniatura: se queda solo el total.
        band.KeepOnlyTotalCost();
        return band;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed record BandQrPayload(string Text, bool NotesDropped);

// Nombres de una letra: cada byte cuenta en un QR.
internal sealed class CompactBand
{
    [JsonPropertyName("i")] public string? Id { get; set; }

    [JsonPropertyName("n")] public string? Name { get; set; }

    [JsonPropertyName("f")] public string? Faction { get; set; }

    [JsonPropertyName("t")] public int? TotalCost { get; set; }

    [JsonPropertyName("k")] public List<string>? StatKeys { get; set; }

    [JsonPropertyName("m")] public List<CompactMember> Members { get; set; } = [];

    /// <summary>Nickname de quien pasa la banda.</summary>
    [JsonPropertyName("u")] public string? Player { get; set; }
}

internal sealed class CompactMember
{
    [JsonPropertyName("n")] public string? Name { get; set; }

    [JsonPropertyName("r")] public string? Role { get; set; }

    [JsonPropertyName("s")] public List<int?>? Stats { get; set; }

    [JsonPropertyName("h")] public List<string>? Skills { get; set; }

    /// <summary>Cada pieza de equipo es [nombre] o [nombre, notas].</summary>
    [JsonPropertyName("g")] public List<List<string>> Gear { get; set; } = [];

    [JsonPropertyName("c")] public int? Cost { get; set; }

    [JsonPropertyName("o")] public string? Notes { get; set; }

    /// <summary>Clave del retrato genérico, si lo tiene.</summary>
    [JsonPropertyName("p")] public string? Portrait { get; set; }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CompactBand))]
internal sealed partial class QrJsonContext : JsonSerializerContext;

internal static class Crc32
{
    private static readonly uint[] Table = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        }

        return c;
    }).ToArray();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
