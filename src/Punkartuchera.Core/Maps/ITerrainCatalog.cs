namespace Punkartuchera.Core.Maps;

/// <summary>
/// Tipos de escenografía y tamaños de mesa disponibles en el generador de mapas.
/// </summary>
public interface ITerrainCatalog
{
    /// <summary>Indica que las medidas no proceden del reglamento oficial.</summary>
    bool IsPlaceholder { get; }

    IReadOnlyList<TerrainType> Types { get; }

    IReadOnlyList<BoardPreset> Boards { get; }

    BoardPreset DefaultBoard { get; }

    /// <summary>Cantidad de cada tipo con la que arranca el selector.</summary>
    IReadOnlyDictionary<string, int> DefaultCounts { get; }

    TerrainType? FindType(string? id);

    BoardPreset? FindBoard(string? id);
}
