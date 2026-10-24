namespace Punkartuchera.Core.Maps;

/// <summary>
/// Reparte la escenografía de forma justa:
/// <list type="bullet">
/// <item>Simetría central: cada pieza de una mitad tiene su gemela girada 180° en la otra, así los
/// dos bandos ven exactamente el mismo terreno desde su zona de despliegue.</item>
/// <item>Si un tipo tiene un número impar de piezas, la que sobra va a la línea central (lo más
/// cerca posible del centro), a la misma distancia de los dos bandos.</item>
/// <item>Las zonas de despliegue quedan libres y entre piezas siempre hay un hueco mínimo para pasar.</item>
/// <item>Cada pieza se coloca en el mejor de varios sitios al azar, el que más se aleja de las ya
/// puestas, para que el terreno quede repartido y no amontonado.</item>
/// </list>
/// El resultado depende solo del código y de la configuración, nunca del dispositivo.
/// </summary>
public static class MapGenerator
{
    /// <summary>Hueco mínimo entre piezas, en centímetros.</summary>
    public const int DefaultMinGap = 7;

    /// <summary>Separación mínima con los bordes laterales.</summary>
    private const int EdgeMargin = 2;

    private const int AttemptsPerGap = 12;
    private const int TriesPerPiece = 300;
    private const int CandidatesPerPiece = 24;

    public static MapLayout Generate(BoardPreset board, IEnumerable<TerrainCount> counts, string code, int minGap = DefaultMinGap)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var items = BuildItems(counts);
        var random = new SeededRandom(MapSeed.ToNumber(code));

        Attempt? best = null;
        foreach (var gap in new[] { minGap, Math.Max(3, minGap * 2 / 3), 2 }.Distinct())
        {
            for (var i = 0; i < AttemptsPerGap; i++)
            {
                var attempt = Place(board, items, gap, random);
                if (best is null || attempt.Leftovers.Count < best.Leftovers.Count)
                {
                    best = attempt;
                }

                if (best.Leftovers.Count == 0)
                {
                    return best.ToLayout(code, board);
                }
            }
        }

        return best!.ToLayout(code, board);
    }

    /// <summary>
    /// Las piezas grandes primero, que son las que más cuesta encajar. Cada tipo se reparte en
    /// parejas simétricas y, si es impar, una pieza central.
    /// </summary>
    private static List<Item> BuildItems(IEnumerable<TerrainCount> counts)
    {
        var items = new List<Item>();
        var ordered = counts
            .Where(c => c.Count > 0)
            .OrderByDescending(c => c.Type.Area)
            .ThenBy(c => c.Type.Id, StringComparer.Ordinal);

        foreach (var (type, count) in ordered)
        {
            if (count % 2 == 1)
            {
                items.Add(new Item(type, Paired: false));
            }

            for (var i = 0; i < count / 2; i++)
            {
                items.Add(new Item(type, Paired: true));
            }
        }

        return items;
    }

    private static Attempt Place(BoardPreset board, List<Item> items, int gap, SeededRandom random)
    {
        var placed = new List<Box>();
        var leftovers = new List<TerrainType>();

        foreach (var item in items)
        {
            var spot = FindSpot(board, item, placed, gap, random);
            if (spot is null)
            {
                leftovers.Add(item.Type);
                if (item.Paired)
                {
                    leftovers.Add(item.Type);
                }

                continue;
            }

            placed.Add(spot);
            if (item.Paired)
            {
                placed.Add(spot.Mirror(board));
            }
        }

        return new Attempt(placed, leftovers, gap);
    }

    private static Box? FindSpot(BoardPreset board, Item item, List<Box> placed, int gap, SeededRandom random)
    {
        Box? best = null;
        var bestScore = double.MinValue;
        var candidates = 0;
        var middle = board.Height / 2.0;

        for (var i = 0; i < TriesPerPiece && candidates < CandidatesPerPiece; i++)
        {
            var type = item.Type;
            var rotated = type.Shape == TerrainShape.Rectangle && type.Width != type.Depth && random.NextBool();
            var width = rotated ? type.Depth : type.Width;
            var depth = rotated ? type.Width : type.Depth;

            var minX = (int)Math.Ceiling(width / 2.0) + EdgeMargin;
            var maxX = board.Width - minX;
            if (maxX < minX)
            {
                continue;
            }

            double x, y;
            if (item.Paired)
            {
                // Mitad norte, sin pisar la zona de despliegue; la gemela cae en la sur.
                var minY = board.DeploymentDepth + (int)Math.Ceiling(depth / 2.0);
                var maxY = (int)Math.Floor(middle);
                if (maxY < minY)
                {
                    continue;
                }

                x = random.Next(minX, maxX);
                y = random.Next(minY, maxY);
            }
            else
            {
                if (middle - depth / 2.0 < board.DeploymentDepth)
                {
                    continue;
                }

                // La primera prueba es el centro exacto, que deja la pieza suelta perfectamente simétrica.
                x = i == 0 ? board.Width / 2.0 : random.Next(minX, maxX);
                y = middle;
            }

            var box = new Box(item.Type, x, y, width, depth);
            var nearest = NearestGap(box, placed);
            if (item.Paired)
            {
                // Las piezas sueltas de la línea central no son simétricas: la gemela también se comprueba.
                var mirror = box.Mirror(board);
                nearest = Math.Min(nearest, Math.Min(box.GapTo(mirror), NearestGap(mirror, placed)));
            }

            if (nearest < gap)
            {
                continue;
            }

            if (!item.Paired && i == 0)
            {
                return box;
            }

            candidates++;
            var edge = Math.Min(box.Left, board.Width - box.Right);
            var centerBias = item.Paired ? 0 : -Math.Abs(x - board.Width / 2.0);
            var score = Math.Min(nearest, 30) + 0.5 * Math.Min(edge, 15) + centerBias + random.NextDouble() * 6;
            if (score > bestScore)
            {
                bestScore = score;
                best = box;
            }
        }

        return best;
    }

    private static double NearestGap(Box box, List<Box> placed) =>
        placed.Count == 0 ? double.MaxValue : placed.Min(box.GapTo);

    private sealed record Item(TerrainType Type, bool Paired);

    private sealed record Attempt(List<Box> Placed, List<TerrainType> Leftovers, int Gap)
    {
        public MapLayout ToLayout(string code, BoardPreset board)
        {
            var middle = board.Height / 2.0;
            var pieces = Placed
                .OrderBy(b => b.Y)
                .ThenBy(b => b.X)
                .Select(b => new PlacedTerrain(
                    b.Type, b.X, b.Y, b.Width, b.Depth,
                    b.Y < middle ? MapSide.North : b.Y > middle ? MapSide.South : MapSide.Center))
                .ToList();

            return new MapLayout(code, board, pieces, Leftovers, Gap);
        }
    }

    private sealed record Box(TerrainType Type, double X, double Y, int Width, int Depth)
    {
        public double Left => X - Width / 2.0;

        public double Right => X + Width / 2.0;

        /// <summary>La gemela girada 180° alrededor del centro de la mesa.</summary>
        public Box Mirror(BoardPreset board) => this with { X = board.Width - X, Y = board.Height - Y };

        /// <summary>Distancia más corta entre los bordes de las dos piezas (0 si se tocan o se pisan).</summary>
        public double GapTo(Box other)
        {
            var dx = Math.Max(0, Math.Abs(X - other.X) - (Width + other.Width) / 2.0);
            var dy = Math.Max(0, Math.Abs(Y - other.Y) - (Depth + other.Depth) / 2.0);
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
