namespace Punkartuchera.Core.Maps;

/// <summary>Mitad de la mesa. El norte despliega arriba, el sur abajo y el centro es tierra de nadie.</summary>
public enum MapSide
{
    North,
    South,
    Center,
}

public sealed record TerrainCount(TerrainType Type, int Count);

/// <summary>
/// Pieza colocada. <see cref="X"/> e <see cref="Y"/> son el centro en centímetros desde la esquina
/// superior izquierda; <see cref="Width"/> y <see cref="Depth"/> ya tienen en cuenta el giro.
/// </summary>
public sealed record PlacedTerrain(TerrainType Type, double X, double Y, int Width, int Depth, MapSide Side)
{
    public double Left => X - Width / 2.0;

    public double Top => Y - Depth / 2.0;

    public bool Rotated => Width != Type.Width;
}

public sealed record MapLayout(
    string Code,
    BoardPreset Board,
    IReadOnlyList<PlacedTerrain> Pieces,
    IReadOnlyList<TerrainType> Leftovers,
    int MinGap)
{
    public int CoverOn(MapSide side) => Pieces.Where(p => p.Side == side).Sum(p => p.Type.Cover);

    public int PiecesOn(MapSide side) => Pieces.Count(p => p.Side == side);

    public int BlockersOn(MapSide side) => Pieces.Count(p => p.Side == side && p.Type.BlocksSight);
}
