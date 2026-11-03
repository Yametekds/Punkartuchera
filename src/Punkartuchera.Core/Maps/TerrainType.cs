namespace Punkartuchera.Core.Maps;

public enum TerrainShape
{
    Rectangle,
    Circle,
}

/// <summary>
/// Tipo de elemento de escenografía. Las medidas son la huella sobre la mesa en centímetros.
/// </summary>
/// <param name="Cover">Peso de cobertura que aporta; sirve para comparar los dos bandos.</param>
/// <param name="BlocksSight">Indica si tapa la línea de visión por completo.</param>
public sealed record TerrainType(
    string Id,
    string Name,
    string Abbreviation,
    int Width,
    int Depth,
    TerrainShape Shape,
    int Cover,
    bool BlocksSight,
    string? Description = null)
{
    public int Area => Width * Depth;
}

/// <summary>Tamaño de mesa con su zona de despliegue.</summary>
public sealed record BoardPreset(string Id, string Name, int Width, int Height, int DeploymentDepth);
