namespace Punkartuchera.Core.Maps;

/// <summary>Dónde van las zonas de despliegue: arriba y abajo, o a izquierda y derecha.</summary>
public enum DeploymentOrientation
{
    Horizontal,
    Vertical,
}

/// <summary>
/// Mitad de la mesa. <see cref="North"/> es el lado de la banda B (arriba, o a la izquierda si el
/// despliegue es vertical), <see cref="South"/> el de la banda A y el centro es tierra de nadie.
/// </summary>
public enum MapSide
{
    North,
    South,
    Center,
}

public sealed record TerrainCount(TerrainType Type, int Count, TerrainMaterial Material);

/// <summary>
/// Pieza colocada. <see cref="X"/> e <see cref="Y"/> son el centro en centímetros desde la esquina
/// superior izquierda; <see cref="Width"/> y <see cref="Depth"/> ya tienen en cuenta el giro.
/// </summary>
public sealed record PlacedTerrain(TerrainType Type, TerrainMaterial Material, double X, double Y, int Width, int Depth, MapSide Side)
{
    public double Left => X - Width / 2.0;

    public double Top => Y - Depth / 2.0;

    /// <summary>Marca del reglamento para la pieza: tamaño y grado, como "2M".</summary>
    public string Mark => Type.Size == 0 ? string.Empty : $"{Type.Size}{Material.GradeLetter}";
}

/// <param name="MinGap">Hueco mínimo que se ha respetado entre piezas, en centímetros.</param>
/// <param name="EdgeMargin">Hueco mínimo que se ha respetado con los bordes de la mesa.</param>
public sealed record MapLayout(
    string Code,
    BoardPreset Board,
    DeploymentOrientation Orientation,
    IReadOnlyList<PlacedTerrain> Pieces,
    IReadOnlyList<TerrainType> Leftovers,
    int MinGap,
    int EdgeMargin)
{
    public int PiecesOn(MapSide side) => Pieces.Count(p => p.Side == side);

    /// <summary>Suma de lo que aportan al Blindaje las Coberturas de ese lado; sirve para comparar.</summary>
    public int ArmourOn(MapSide side) => Pieces.Where(p => p.Side == side).Sum(p => (int)p.Material.Grade);

    /// <summary>Piezas de cada tamaño (índices 0 a 3), para compararlas con lo que pide el reglamento.</summary>
    public int CountOfSize(int size) => Pieces.Count(p => p.Type.Size == size);
}
