namespace Punkartuchera.Core.Maps;

/// <summary>
/// Escenografía típica de un páramo punk. Las huellas y los pesos de cobertura son orientativos
/// y se ajustarán con el reglamento.
/// </summary>
public sealed class DefaultTerrainCatalog : ITerrainCatalog
{
    private static readonly TerrainType[] AllTypes =
    [
        new("ruina", "Ruina gorda", "RU", 25, 20, TerrainShape.Rectangle, 3, true, "Edificio medio derruido donde atrincherarse."),
        new("chabola", "Chabola", "CH", 15, 15, TerrainShape.Rectangle, 2, true, "Chapas y palés clavados a lo bruto."),
        new("chatarra", "Montón de chatarra", "MC", 15, 10, TerrainShape.Rectangle, 2, false, "Hierros retorcidos para parapetarse."),
        new("coche", "Coche calcinado", "CC", 12, 6, TerrainShape.Rectangle, 2, false, "Carrocería quemada, buena para agacharse."),
        new("barricada", "Barricada", "BA", 15, 4, TerrainShape.Rectangle, 1, false, "Muro bajo de sacos y neumáticos."),
        new("bidones", "Bidones", "BI", 6, 6, TerrainShape.Circle, 1, false, "Un par de bidones. Mejor no dispararles."),
        new("charco", "Charco tóxico", "CT", 12, 12, TerrainShape.Circle, 0, false, "No cubre, pero nadie quiere cruzarlo."),
    ];

    private static readonly BoardPreset[] AllBoards =
    [
        new("90x90", "Escaramuza 90×90", 90, 90, 15),
        new("120x120", "Estándar 120×120", 120, 120, 20),
        new("180x120", "Bestial 180×120", 180, 120, 20),
    ];

    public bool IsPlaceholder => true;

    public IReadOnlyList<TerrainType> Types => AllTypes;

    public IReadOnlyList<BoardPreset> Boards => AllBoards;

    public BoardPreset DefaultBoard => AllBoards[1];

    public IReadOnlyDictionary<string, int> DefaultCounts { get; } = new Dictionary<string, int>
    {
        ["ruina"] = 2,
        ["chabola"] = 2,
        ["chatarra"] = 2,
        ["coche"] = 2,
        ["barricada"] = 4,
        ["bidones"] = 4,
        ["charco"] = 1,
    };

    public TerrainType? FindType(string? id) => AllTypes.FirstOrDefault(t => t.Id == id);

    public BoardPreset? FindBoard(string? id) => AllBoards.FirstOrDefault(b => b.Id == id);
}
