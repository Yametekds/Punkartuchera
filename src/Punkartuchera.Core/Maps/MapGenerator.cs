namespace Punkartuchera.Core.Maps;

/// <summary>
/// Reparte la escenografía de forma justa:
/// <list type="bullet">
/// <item>Simetría central: cada pieza de una mitad tiene su gemela girada 180° en la otra, así los
/// dos bandos ven exactamente el mismo terreno desde su zona de despliegue.</item>
/// <item>Si un tipo tiene un número impar de piezas, la que sobra va a la línea central (lo más
/// cerca posible del centro), a la misma distancia de los dos bandos.</item>
/// <item>Como pide el reglamento (escenarios, p. 1), cada pieza queda a 10 cm de las demás y de los
/// bordes siempre que se pueda. Si no cabe todo, el hueco se va reduciendo poco a poco.</item>
/// <item>Como la simetría es central, sirve igual con el despliegue arriba y abajo que a los lados: el
/// despliegue vertical es el mismo reparto puesto de lado.</item>
/// <item>Cada pieza se coloca en el mejor de varios sitios al azar, el que más se aleja de las ya
/// puestas, para que el terreno quede repartido y no amontonado.</item>
/// </list>
/// El resultado depende solo del código y de la configuración, nunca del dispositivo.
/// </summary>
public static class MapGenerator
{
    /// <summary>Hueco mínimo entre piezas y con los bordes que pide el reglamento, en centímetros.</summary>
    public const int DefaultMinGap = 10;

    /// <summary>Huecos (entre piezas, con los bordes) que se prueban, del reglamentario al más apretado.</summary>
    private static readonly (int Gap, int Edge)[] Spacings = [(10, 10), (10, 5), (7, 5), (5, 3), (3, 2), (2, 1)];

    private const int AttemptsPerGap = 12;
    private const int TriesPerPiece = 300;
    private const int CandidatesPerPiece = 24;

    public static MapLayout Generate(
        BoardPreset board,
        IEnumerable<TerrainCount> counts,
        string code,
        DeploymentOrientation orientation = DeploymentOrientation.Horizontal)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        // Se reparte siempre con el despliegue arriba y abajo; para el vertical se gira después.
        var vertical = orientation == DeploymentOrientation.Vertical;
        var frame = vertical ? board with { Width = board.Height, Height = board.Width } : board;

        var items = BuildItems(counts);
        var random = new SeededRandom(MapSeed.ToNumber(code));

        Attempt? best = null;
        foreach (var (gap, edge) in Spacings)
        {
            for (var i = 0; i < AttemptsPerGap; i++)
            {
                var attempt = Place(frame, items, gap, edge, random);
                if (best is null || attempt.Leftovers.Count < best.Leftovers.Count)
                {
                    best = attempt;
                }

                if (best.Leftovers.Count == 0)
                {
                    return best.ToLayout(code, board, frame, orientation);
                }
            }
        }

        return best!.ToLayout(code, board, frame, orientation);
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

        foreach (var (type, count, material) in ordered)
        {
            if (count % 2 == 1)
            {
                items.Add(new Item(type, material, Paired: false));
            }

            for (var i = 0; i < count / 2; i++)
            {
                items.Add(new Item(type, material, Paired: true));
            }
        }

        return items;
    }

    private static Attempt Place(BoardPreset board, List<Item> items, int gap, int edge, SeededRandom random)
    {
        var placed = new List<Box>();
        var leftovers = new List<TerrainType>();

        foreach (var item in items)
        {
            var spot = FindSpot(board, item, placed, gap, edge, random);
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

        return new Attempt(placed, leftovers, gap, edge);
    }

    private static Box? FindSpot(BoardPreset board, Item item, List<Box> placed, int gap, int edgeMargin, SeededRandom random)
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

            var minX = (int)Math.Ceiling(width / 2.0) + edgeMargin;
            var maxX = board.Width - minX;
            if (maxX < minX)
            {
                continue;
            }

            double x, y;
            if (item.Paired)
            {
                // Mitad norte; la gemela cae en la sur. El reglamento deja poner escenografía también
                // en las zonas de despliegue.
                var minY = edgeMargin + (int)Math.Ceiling(depth / 2.0);
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
                if (middle - depth / 2.0 < edgeMargin)
                {
                    continue;
                }

                // La primera prueba es el centro exacto, que deja la pieza suelta perfectamente simétrica.
                x = i == 0 ? board.Width / 2.0 : random.Next(minX, maxX);
                y = middle;
            }

            var box = new Box(item.Type, item.Material, x, y, width, depth);
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

    private sealed record Item(TerrainType Type, TerrainMaterial Material, bool Paired);

    private sealed record Attempt(List<Box> Placed, List<TerrainType> Leftovers, int Gap, int Edge)
    {
        public MapLayout ToLayout(string code, BoardPreset board, BoardPreset frame, DeploymentOrientation orientation)
        {
            var middle = frame.Height / 2.0;
            var vertical = orientation == DeploymentOrientation.Vertical;
            var pieces = Placed
                .OrderBy(b => b.Y)
                .ThenBy(b => b.X)
                .Select(b =>
                {
                    var side = b.Y < middle ? MapSide.North : b.Y > middle ? MapSide.South : MapSide.Center;

                    // Poner el reparto de lado: lo que estaba arriba pasa a la izquierda.
                    return vertical
                        ? new PlacedTerrain(b.Type, b.Material, b.Y, b.X, b.Depth, b.Width, side)
                        : new PlacedTerrain(b.Type, b.Material, b.X, b.Y, b.Width, b.Depth, side);
                })
                .ToList();

            return new MapLayout(code, board, orientation, pieces, Leftovers, Gap, Edge);
        }
    }

    private sealed record Box(TerrainType Type, TerrainMaterial Material, double X, double Y, int Width, int Depth)
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
