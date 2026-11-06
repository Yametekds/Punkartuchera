namespace Punkartuchera.Core.Maps;

public enum TerrainShape
{
    Rectangle,
    Circle,
}

/// <summary>
/// Tipo de elemento de escenografía. Las medidas son la huella sobre la mesa en centímetros; el
/// reglamento no fija huellas, así que son las de una pieza típica de ese tipo.
/// </summary>
/// <param name="Size">
/// Tamaño de la Cobertura según la altura (reglas, p. 17): 1 hasta unos 3 cm, 2 entre 3 y 6 cm,
/// 3 por encima. 0 es una zona a ras de suelo que no cubre (una charca, por ejemplo).
/// </param>
/// <param name="MaterialId">Material con el que arranca; cada jugador puede cambiarlo.</param>
public sealed record TerrainType(
    string Id,
    string Name,
    string Abbreviation,
    int Width,
    int Depth,
    TerrainShape Shape,
    int Size,
    string MaterialId,
    string? Description = null)
{
    public int Area => Width * Depth;
}

/// <summary>
/// Tamaño de mesa del reglamento (escenarios, p. 1 y 2) con su zona de despliegue y la escenografía
/// que pide la colocación reglada.
/// </summary>
/// <param name="ElementsBySize">Elementos recomendados de tamaño 1, 2 y 3, en ese orden.</param>
public sealed record BoardPreset(
    string Id,
    string Name,
    int Width,
    int Height,
    int DeploymentDepth,
    string Note,
    IReadOnlyList<int> ElementsBySize);
