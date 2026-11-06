namespace Punkartuchera.Core.Maps;

/// <summary>Grado de la Cobertura (reglas, p. 17). El valor es lo que suma al Blindaje.</summary>
public enum CoverGrade
{
    None = 0,
    Light = 3,
    Medium = 5,
    Heavy = 7,
}

/// <summary>Trama con la que se dibuja un material en el plano.</summary>
public enum MaterialPattern
{
    Dots,
    Planks,
    Rings,
    Bricks,
    Diagonal,
    Crosshatch,
    Waves,
}

/// <summary>
/// Material de un elemento de escenografía. El grado de la Cobertura sale de la lista del
/// reglamento; el color va con el grado y la trama distingue el material dentro de cada grado.
/// </summary>
public sealed record TerrainMaterial(string Id, string Name, CoverGrade Grade, MaterialPattern Pattern)
{
    public static readonly IReadOnlyList<TerrainMaterial> All =
    [
        new("vegetacion", "Vegetación densa", CoverGrade.Light, MaterialPattern.Dots),
        new("madera", "Madera", CoverGrade.Light, MaterialPattern.Planks),
        new("neumaticos", "Neumáticos", CoverGrade.Light, MaterialPattern.Rings),
        new("sacos", "Sacos", CoverGrade.Light, MaterialPattern.Bricks),
        new("adobe", "Adobe", CoverGrade.Medium, MaterialPattern.Dots),
        new("ladrillo", "Ladrillo", CoverGrade.Medium, MaterialPattern.Bricks),
        new("chapafina", "Chapa fina", CoverGrade.Medium, MaterialPattern.Diagonal),
        new("escombros", "Escombros", CoverGrade.Medium, MaterialPattern.Crosshatch),
        new("vehiculo", "Vehículo", CoverGrade.Heavy, MaterialPattern.Diagonal),
        new("chapagruesa", "Chapa gruesa", CoverGrade.Heavy, MaterialPattern.Crosshatch),
        new("hormigon", "Hormigón", CoverGrade.Heavy, MaterialPattern.Dots),
        new("piedra", "Piedra", CoverGrade.Heavy, MaterialPattern.Planks),
        new("toxico", "Contaminado", CoverGrade.None, MaterialPattern.Waves),
    ];

    public static TerrainMaterial? Find(string? id) => All.FirstOrDefault(m => m.Id == id);

    /// <summary>Letra del grado, como en el truco del reglamento de marcar las piezas (2M, 1P, 3L…).</summary>
    public string GradeLetter => Grade switch
    {
        CoverGrade.Light => "L",
        CoverGrade.Medium => "M",
        CoverGrade.Heavy => "P",
        _ => "",
    };

    public string GradeName => Grade switch
    {
        CoverGrade.Light => "Ligera",
        CoverGrade.Medium => "Media",
        CoverGrade.Heavy => "Pesada",
        _ => "Sin Cobertura",
    };

    /// <summary>Lo que suma al Blindaje, como texto ("+5"), o vacío si no cubre.</summary>
    public string ArmourText => Grade == CoverGrade.None ? string.Empty : $"+{(int)Grade}";
}
