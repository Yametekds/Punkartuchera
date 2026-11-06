namespace Punkartuchera.Core.Maps;

/// <summary>
/// Escenografía típica de un páramo punk, con los tamaños de mesa, zonas de despliegue y cantidades
/// de la colocación reglada del PDF de Escenarios (p. 1 y 2). El tamaño de cada pieza va por su
/// altura y el grado de Cobertura por su material, como en el reglamento (p. 17).
/// </summary>
public sealed class DefaultTerrainCatalog : ITerrainCatalog
{
    private static readonly TerrainType[] AllTypes =
    [
        new("ruina", "Casa derruida", "CD", 20, 15, TerrainShape.Rectangle, 3, "ladrillo", "Cuatro paredes medio en pie donde atrincherarse."),
        new("fabrica", "Nave oxidada", "NA", 25, 15, TerrainShape.Rectangle, 3, "chapagruesa", "Restos de fábrica del Mundo de Antaño."),
        new("chabola", "Chabola", "CH", 12, 12, TerrainShape.Rectangle, 3, "chapafina", "Chapas y palés clavados a lo bruto."),
        new("rocas", "Formación rocosa", "FR", 12, 10, TerrainShape.Circle, 2, "piedra", "Peñascos para parapetarse."),
        new("chatarra", "Montón de chatarra", "MC", 12, 8, TerrainShape.Rectangle, 2, "escombros", "Hierros retorcidos y cascotes."),
        new("maleza", "Maleza mutante", "MM", 10, 8, TerrainShape.Circle, 2, "vegetacion", "Matojos que ni los mutardos se comen."),
        new("coche", "Coche calcinado", "CC", 10, 5, TerrainShape.Rectangle, 1, "vehiculo", "Carrocería quemada, buena para agacharse."),
        new("barricada", "Barricada", "BA", 12, 3, TerrainShape.Rectangle, 1, "sacos", "Muro bajo de sacos."),
        new("bidones", "Bidones", "BI", 5, 5, TerrainShape.Circle, 1, "chapafina", "Un par de bidones. Mejor no dispararles."),
        new("charca", "Charca tóxica", "CT", 20, 20, TerrainShape.Circle, 0, "toxico", "Zona de Terreno contaminado como las de los escenarios."),
    ];

    private static readonly BoardPreset[] AllBoards =
    [
        new("90x90", "90×90", 90, 90, 15, "Hasta 400 puntos", [8, 6, 4]),
        new("120x120", "120×120", 120, 120, 30, "Más de 400 puntos", [10, 8, 6]),
    ];

    private static readonly Dictionary<string, Dictionary<string, int>> Defaults = new()
    {
        ["90x90"] = new()
        {
            ["ruina"] = 2, ["chabola"] = 2,
            ["rocas"] = 2, ["chatarra"] = 2, ["maleza"] = 2,
            ["coche"] = 2, ["barricada"] = 4, ["bidones"] = 2,
        },
        ["120x120"] = new()
        {
            ["ruina"] = 2, ["fabrica"] = 2, ["chabola"] = 2,
            ["rocas"] = 2, ["chatarra"] = 4, ["maleza"] = 2,
            ["coche"] = 2, ["barricada"] = 4, ["bidones"] = 4,
        },
    };

    public IReadOnlyList<TerrainType> Types => AllTypes;

    public IReadOnlyList<TerrainMaterial> Materials => TerrainMaterial.All;

    public IReadOnlyList<BoardPreset> Boards => AllBoards;

    public BoardPreset DefaultBoard => AllBoards[1];

    public IReadOnlyDictionary<string, int> DefaultCounts(BoardPreset board) =>
        Defaults.TryGetValue(board.Id, out var counts) ? counts : Defaults[DefaultBoard.Id];

    public TerrainType? FindType(string? id) => AllTypes.FirstOrDefault(t => t.Id == id);

    public TerrainMaterial? FindMaterial(string? id) => TerrainMaterial.Find(id);

    public BoardPreset? FindBoard(string? id) => AllBoards.FirstOrDefault(b => b.Id == id);
}
