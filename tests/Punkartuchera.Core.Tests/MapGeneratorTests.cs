using Punkartuchera.Core.Maps;

namespace Punkartuchera.Core.Tests;

public class MapGeneratorTests
{
    private readonly DefaultTerrainCatalog _catalog = new();

    private List<TerrainCount> DefaultCounts() =>
        TerrainSelection.ToCounts(_catalog.DefaultCounts, _catalog);

    [Fact]
    public void Same_code_and_settings_give_the_same_map()
    {
        var first = MapGenerator.Generate(_catalog.DefaultBoard, DefaultCounts(), "K7Q2XM");
        var second = MapGenerator.Generate(_catalog.DefaultBoard, DefaultCounts(), "K7Q2XM");

        Assert.Equal(first.Pieces, second.Pieces);
    }

    [Fact]
    public void Different_codes_give_different_maps()
    {
        var first = MapGenerator.Generate(_catalog.DefaultBoard, DefaultCounts(), "AAAAAA");
        var second = MapGenerator.Generate(_catalog.DefaultBoard, DefaultCounts(), "BBBBBB");

        Assert.NotEqual(first.Pieces, second.Pieces);
    }

    [Theory]
    [InlineData("90x90")]
    [InlineData("120x120")]
    [InlineData("180x120")]
    public void Every_piece_has_a_rotated_twin_or_sits_on_the_centre_line(string boardId)
    {
        var board = _catalog.FindBoard(boardId)!;
        for (var seed = 0; seed < 40; seed++)
        {
            var layout = MapGenerator.Generate(board, DefaultCounts(), $"SEED{seed}");

            foreach (var piece in layout.Pieces.Where(p => p.Side != MapSide.Center))
            {
                Assert.Contains(layout.Pieces, twin =>
                    twin.Type == piece.Type
                    && twin.X == board.Width - piece.X
                    && twin.Y == board.Height - piece.Y
                    && twin.Width == piece.Width);
            }

            Assert.All(layout.Pieces.Where(p => p.Side == MapSide.Center), p => Assert.Equal(board.Height / 2.0, p.Y));
        }
    }

    [Fact]
    public void Both_sides_get_the_same_cover()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var layout = MapGenerator.Generate(_catalog.DefaultBoard, DefaultCounts(), $"COVER{seed}");

            Assert.Equal(layout.CoverOn(MapSide.North), layout.CoverOn(MapSide.South));
            Assert.Equal(layout.BlockersOn(MapSide.North), layout.BlockersOn(MapSide.South));
        }
    }

    [Theory]
    [InlineData("90x90")]
    [InlineData("120x120")]
    [InlineData("180x120")]
    public void Pieces_stay_on_the_table_out_of_deployment_and_apart(string boardId)
    {
        var board = _catalog.FindBoard(boardId)!;
        for (var seed = 0; seed < 40; seed++)
        {
            var layout = MapGenerator.Generate(board, DefaultCounts(), $"GAP{seed}");

            Assert.Empty(layout.Leftovers);
            foreach (var p in layout.Pieces)
            {
                Assert.True(p.Left >= 0 && p.Left + p.Width <= board.Width);
                Assert.True(p.Top >= board.DeploymentDepth, $"{p.Type.Id} invade el despliegue norte");
                Assert.True(p.Top + p.Depth <= board.Height - board.DeploymentDepth, $"{p.Type.Id} invade el despliegue sur");
            }

            for (var i = 0; i < layout.Pieces.Count; i++)
            {
                for (var j = i + 1; j < layout.Pieces.Count; j++)
                {
                    Assert.True(Gap(layout.Pieces[i], layout.Pieces[j]) >= layout.MinGap - 1e-9);
                }
            }
        }
    }

    [Fact]
    public void Default_setup_keeps_the_full_gap()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var layout = MapGenerator.Generate(_catalog.DefaultBoard, DefaultCounts(), $"FULL{seed}");
            Assert.Equal(MapGenerator.DefaultMinGap, layout.MinGap);
        }
    }

    [Fact]
    public void Odd_counts_put_the_spare_piece_on_the_centre_line()
    {
        var ruin = _catalog.FindType("ruina")!;
        var layout = MapGenerator.Generate(_catalog.DefaultBoard, [new TerrainCount(ruin, 3)], "IMPAR1");

        Assert.Equal(3, layout.Pieces.Count);
        var centre = Assert.Single(layout.Pieces, p => p.Side == MapSide.Center);
        Assert.Equal(_catalog.DefaultBoard.Width / 2.0, centre.X);
    }

    [Fact]
    public void Too_much_terrain_reports_leftovers_instead_of_overlapping()
    {
        var ruin = _catalog.FindType("ruina")!;
        var board = _catalog.FindBoard("90x90")!;
        var layout = MapGenerator.Generate(board, [new TerrainCount(ruin, 20)], "LLENO1");

        Assert.NotEmpty(layout.Leftovers);
        Assert.Equal(20, layout.Pieces.Count + layout.Leftovers.Count);
    }

    [Fact]
    public void Empty_selection_gives_an_empty_map()
    {
        var layout = MapGenerator.Generate(_catalog.DefaultBoard, [], "VACIO1");

        Assert.Empty(layout.Pieces);
        Assert.Empty(layout.Leftovers);
    }

    [Theory]
    [InlineData(" k7q-2xm ", "K7Q2XM")]
    [InlineData("o0i1", null)]
    [InlineData("", null)]
    public void Codes_are_normalized(string input, string? expected) => Assert.Equal(expected, MapSeed.Normalize(input));

    [Fact]
    public void New_codes_use_the_safe_alphabet()
    {
        var code = MapSeed.NewCode();

        Assert.Equal(MapSeed.Length, code.Length);
        Assert.All(code, c => Assert.Contains(c, MapSeed.Alphabet));
    }

    private static double Gap(PlacedTerrain a, PlacedTerrain b)
    {
        var dx = Math.Max(0, Math.Abs(a.X - b.X) - (a.Width + b.Width) / 2.0);
        var dy = Math.Max(0, Math.Abs(a.Y - b.Y) - (a.Depth + b.Depth) / 2.0);
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public class TerrainSelectionTests
{
    private readonly DefaultTerrainCatalog _catalog = new();

    [Fact]
    public void Format_and_parse_round_trip()
    {
        var text = TerrainSelection.Format(_catalog.DefaultCounts);
        var parsed = TerrainSelection.Parse(text, _catalog);

        Assert.All(_catalog.Types, t => Assert.Equal(_catalog.DefaultCounts.GetValueOrDefault(t.Id), parsed[t.Id]));
    }

    [Fact]
    public void Parse_ignores_junk_and_clamps()
    {
        var parsed = TerrainSelection.Parse("ruina-99.ovni-3.coche--1.bidones-x.chabola-2", _catalog);

        Assert.Equal(TerrainSelection.MaxPerType, parsed["ruina"]);
        Assert.Equal(0, parsed["coche"]);
        Assert.Equal(0, parsed["bidones"]);
        Assert.Equal(2, parsed["chabola"]);
        Assert.False(parsed.ContainsKey("ovni"));
    }
}
