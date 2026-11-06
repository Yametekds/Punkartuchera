using System.Globalization;

namespace Punkartuchera.Core.Maps;

/// <summary>Cuántas piezas de un tipo se ponen y de qué material son.</summary>
public sealed record TerrainPick(int Count, string MaterialId);

/// <summary>
/// Convierte la escenografía elegida en un texto corto para guardarlo en el enlace del mapa, y al
/// revés. Cada tipo va como "barricada-4"; si el material no es el de siempre, se añade detrás
/// ("ruina-2-hormigon").
/// </summary>
public static class TerrainSelection
{
    public const int MaxPerType = 10;

    /// <summary>Lo que trae el catálogo para esa mesa, con los materiales de siempre.</summary>
    public static Dictionary<string, TerrainPick> Defaults(ITerrainCatalog catalog, BoardPreset board)
    {
        var counts = catalog.DefaultCounts(board);
        return catalog.Types.ToDictionary(t => t.Id, t => new TerrainPick(counts.GetValueOrDefault(t.Id), t.MaterialId));
    }

    /// <summary>Todo a cero, con los materiales de siempre.</summary>
    public static Dictionary<string, TerrainPick> Empty(ITerrainCatalog catalog) =>
        catalog.Types.ToDictionary(t => t.Id, t => new TerrainPick(0, t.MaterialId));

    /// <summary>Solo usa caracteres que no hay que escapar en una URL.</summary>
    public static string Format(IReadOnlyDictionary<string, TerrainPick> picks, ITerrainCatalog catalog)
    {
        var parts = new List<string>();
        foreach (var type in catalog.Types.OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            if (!picks.TryGetValue(type.Id, out var pick))
            {
                continue;
            }

            var customMaterial = pick.MaterialId != type.MaterialId && catalog.FindMaterial(pick.MaterialId) is not null;
            if (pick.Count > 0 || customMaterial)
            {
                parts.Add(customMaterial ? $"{type.Id}-{pick.Count}-{pick.MaterialId}" : $"{type.Id}-{pick.Count}");
            }
        }

        return string.Join('.', parts);
    }

    /// <summary>Ignora tipos y materiales desconocidos y recorta las cantidades al rango permitido.</summary>
    public static Dictionary<string, TerrainPick> Parse(string? text, ITerrainCatalog catalog)
    {
        var picks = Empty(catalog);
        if (string.IsNullOrWhiteSpace(text))
        {
            return picks;
        }

        foreach (var part in text.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split('-');
            if (pieces.Length is < 2 or > 3
                || !picks.TryGetValue(pieces[0], out var current)
                || !int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                continue;
            }

            var material = pieces.Length == 3 && catalog.FindMaterial(pieces[2]) is { } found ? found.Id : current.MaterialId;
            picks[pieces[0]] = new TerrainPick(Math.Clamp(count, 0, MaxPerType), material);
        }

        return picks;
    }

    public static List<TerrainCount> ToCounts(IReadOnlyDictionary<string, TerrainPick> picks, ITerrainCatalog catalog) =>
        catalog.Types
            .Select(t =>
            {
                var pick = picks.GetValueOrDefault(t.Id) ?? new TerrainPick(0, t.MaterialId);
                var material = catalog.FindMaterial(pick.MaterialId) ?? catalog.FindMaterial(t.MaterialId)!;
                return new TerrainCount(t, pick.Count, material);
            })
            .ToList();

    /// <summary>Cuenta cuántas piezas hay de cada tamaño (índices 0 a 3).</summary>
    public static int[] CountBySize(IReadOnlyDictionary<string, TerrainPick> picks, ITerrainCatalog catalog)
    {
        var sizes = new int[4];
        foreach (var type in catalog.Types)
        {
            sizes[Math.Clamp(type.Size, 0, 3)] += picks.GetValueOrDefault(type.Id)?.Count ?? 0;
        }

        return sizes;
    }

    /// <summary>Formato del parámetro de orientación en el enlace: "h" o "v".</summary>
    public static string FormatOrientation(DeploymentOrientation orientation) =>
        orientation == DeploymentOrientation.Vertical ? "v" : "h";

    public static DeploymentOrientation ParseOrientation(string? text) =>
        string.Equals(text?.Trim(), "v", StringComparison.OrdinalIgnoreCase) ? DeploymentOrientation.Vertical : DeploymentOrientation.Horizontal;
}
