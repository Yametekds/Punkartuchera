namespace Punkartuchera.Core.Maps;

/// <summary>
/// Tipos de escenografía, materiales y tamaños de mesa disponibles en el generador de mapas.
/// </summary>
public interface ITerrainCatalog
{
    IReadOnlyList<TerrainType> Types { get; }

    IReadOnlyList<TerrainMaterial> Materials { get; }

    IReadOnlyList<BoardPreset> Boards { get; }

    BoardPreset DefaultBoard { get; }

    /// <summary>Cantidad de cada tipo con la que arranca el selector para esa mesa.</summary>
    IReadOnlyDictionary<string, int> DefaultCounts(BoardPreset board);

    TerrainType? FindType(string? id);

    TerrainMaterial? FindMaterial(string? id);

    BoardPreset? FindBoard(string? id);
}
