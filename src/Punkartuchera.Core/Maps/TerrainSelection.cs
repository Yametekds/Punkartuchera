using System.Globalization;

namespace Punkartuchera.Core.Maps;

/// <summary>
/// Convierte la cantidad de piezas de cada tipo en un texto corto ("barricada-4.ruina-2") para
/// guardarlo en el enlace del mapa, y al revés.
/// </summary>
public static class TerrainSelection
{
    public const int MaxPerType = 10;

    /// <summary>Solo usa caracteres que no hay que escapar en una URL.</summary>
    public static string Format(IReadOnlyDictionary<string, int> counts) =>
        string.Join('.', counts.Where(c => c.Value > 0).OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key}-{c.Value}"));

    /// <summary>Ignora tipos desconocidos y recorta las cantidades al rango permitido.</summary>
    public static Dictionary<string, int> Parse(string? text, ITerrainCatalog catalog)
    {
        var counts = catalog.Types.ToDictionary(t => t.Id, _ => 0);
        if (string.IsNullOrWhiteSpace(text))
        {
            return counts;
        }

        foreach (var part in text.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split('-');
            if (pieces.Length == 2
                && counts.ContainsKey(pieces[0])
                && int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                counts[pieces[0]] = Math.Clamp(count, 0, MaxPerType);
            }
        }

        return counts;
    }

    public static List<TerrainCount> ToCounts(IReadOnlyDictionary<string, int> counts, ITerrainCatalog catalog) =>
        catalog.Types.Select(t => new TerrainCount(t, counts.GetValueOrDefault(t.Id))).ToList();
}
