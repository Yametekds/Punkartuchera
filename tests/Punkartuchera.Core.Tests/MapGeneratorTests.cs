using Punkartuchera.Core.Maps;

namespace Punkartuchera.Core.Tests;

public class MapGeneratorTests
{
    private readonly DefaultTerrainCatalog _catalog = new();

    private List<TerrainCount> DefaultCounts(BoardPreset? board = null) =>
        TerrainSelection.ToCounts(TerrainSelection.Defaults(_catalog, board ?? _catalog.DefaultBoard), _catalog);

    private TerrainCount Count(string typeId, int count)
    {
        var type = _catalog.FindType(typeId)!;
        return new TerrainCount(type, count, _catalog.FindMaterial(type.MaterialId)!);
    }

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
    [InlineData("90x90", DeploymentOrientation.Horizontal)]
    [InlineData("120x120", DeploymentOrientation.Horizontal)]
    [InlineData("90x90", DeploymentOrientation.Vertical)]
    [InlineData("120x120", DeploymentOrientation.Vertical)]
    public void Every_piece_has_a_rotated_twin_or_sits_on_the_centre_line(string boardId, DeploymentOrientation orientation)
    {
        var board = _catalog.FindBoard(boardId)!;
        for (var seed = 0; seed < 40; seed++)
        {
            var layout = MapGenerator.Generate(board, DefaultCounts(board), $"SEED{seed}", orientation);

            foreach (var piece in layout.Pieces.Where(p => p.Side != MapSide.Center))
            {
                Assert.Contains(layout.Pieces, twin =>
                    twin.Type == piece.Type
                    && twin.Material == piece.Material
                    && twin.X == board.Width - piece.X
                    && twin.Y == board.Height - piece.Y
                    && twin.Width == piece.Width);
            }

            // La línea central es paralela a las zonas de despliegue.
            Assert.All(layout.Pieces.Where(p => p.Side == MapSide.Center), p =>
                Assert.Equal(orientation == DeploymentOrientation.Vertical ? board.Width / 2.0 : board.Height / 2.0,
                    orientation == DeploymentOrientation.Vertical ? p.X : p.Y));
        }
    }

    [Fact]
    public void Both_sides_get_the_same_cover()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var layout = MapGenerator.Generate(_catalog.DefaultBoard, DefaultCounts(), $"COVER{seed}");

            Assert.Equal(layout.ArmourOn(MapSide.North), layout.ArmourOn(MapSide.South));
            Assert.Equal(layout.PiecesOn(MapSide.North), layout.PiecesOn(MapSide.South));
        }
    }

    [Fact]
    public void Vertical_deployment_puts_the_sides_left_and_right()
    {
        var board = _catalog.DefaultBoard;
        var layout = MapGenerator.Generate(board, DefaultCounts(), "LADOS1", DeploymentOrientation.Vertical);

        Assert.Equal(DeploymentOrientation.Vertical, layout.Orientation);
        Assert.All(layout.Pieces.Where(p => p.Side == MapSide.North), p => Assert.True(p.X < board.Width / 2.0));
        Assert.All(layout.Pieces.Where(p => p.Side == MapSide.South), p => Assert.True(p.X > board.Width / 2.0));
    }

    [Fact]
    public void Orientation_changes_the_map_but_keeps_it_reproducible()
    {
        var board = _catalog.DefaultBoard;
        var horizontal = MapGenerator.Generate(board, DefaultCounts(), "K7Q2XM", DeploymentOrientation.Horizontal);
        var vertical = MapGenerator.Generate(board, DefaultCounts(), "K7Q2XM", DeploymentOrientation.Vertical);
        var again = MapGenerator.Generate(board, DefaultCounts(), "K7Q2XM", DeploymentOrientation.Vertical);

        Assert.Equal(vertical.Pieces, again.Pieces);
        Assert.NotEqual(horizontal.Pieces, vertical.Pieces);
    }

    [Fact]
    public void Pieces_carry_the_chosen_material()
    {
        var ruin = _catalog.FindType("ruina")!;
        var concrete = _catalog.FindMaterial("hormigon")!;
        var layout = MapGenerator.Generate(_catalog.DefaultBoard, [new TerrainCount(ruin, 2, concrete)], "MATER1");

        Assert.All(layout.Pieces, p => Assert.Equal(concrete, p.Material));
        Assert.All(layout.Pieces, p => Assert.Equal("3P", p.Mark));
    }

    [Theory]
    [InlineData("90x90", DeploymentOrientation.Horizontal)]
    [InlineData("120x120", DeploymentOrientation.Horizontal)]
    [InlineData("120x120", DeploymentOrientation.Vertical)]
    public void Pieces_stay_on_the_table_and_apart(string boardId, DeploymentOrientation orientation)
    {
        var board = _catalog.FindBoard(boardId)!;
        for (var seed = 0; seed < 40; seed++)
        {
            var layout = MapGenerator.Generate(board, DefaultCounts(board), $"GAP{seed}", orientation);

            Assert.Empty(layout.Leftovers);
            foreach (var p in layout.Pieces)
            {
                Assert.True(p.Left >= layout.EdgeMargin - 1e-9 && p.Left + p.Width <= board.Width - layout.EdgeMargin + 1e-9);
                Assert.True(p.Top >= layout.EdgeMargin - 1e-9 && p.Top + p.Depth <= board.Height - layout.EdgeMargin + 1e-9);
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

    [Theory]
    [InlineData("90x90")]
    [InlineData("120x120")]
    public void Default_setup_matches_the_rulebook_element_counts(string boardId)
    {
        var board = _catalog.FindBoard(boardId)!;
        var sizes = TerrainSelection.CountBySize(TerrainSelection.Defaults(_catalog, board), _catalog);

        Assert.Equal(board.ElementsBySize, sizes[1..]);
    }

    [Fact]
    public void Boards_use_the_rulebook_deployment_depths()
    {
        Assert.Equal(15, _catalog.FindBoard("90x90")!.DeploymentDepth);
        Assert.Equal(30, _catalog.FindBoard("120x120")!.DeploymentDepth);
    }

    [Fact]
    public void Light_setup_keeps_the_rulebook_gap()
    {
        var layout = MapGenerator.Generate(_catalog.DefaultBoard, [Count("ruina", 2), Count("barricada", 4)], "HOLGAD");

        Assert.Equal(MapGenerator.DefaultMinGap, layout.MinGap);
        Assert.Equal(MapGenerator.DefaultMinGap, layout.EdgeMargin);
    }

    [Fact]
    public void Odd_counts_put_the_spare_piece_on_the_centre_line()
    {
        var layout = MapGenerator.Generate(_catalog.DefaultBoard, [Count("ruina", 3)], "IMPAR1");

        Assert.Equal(3, layout.Pieces.Count);
        var centre = Assert.Single(layout.Pieces, p => p.Side == MapSide.Center);
        Assert.Equal(_catalog.DefaultBoard.Width / 2.0, centre.X);
    }

    [Fact]
    public void Too_much_terrain_reports_leftovers_instead_of_overlapping()
    {
        var board = _catalog.FindBoard("90x90")!;
        var layout = MapGenerator.Generate(board, [Count("ruina", 10), Count("fabrica", 10)], "LLENO1");

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
        var picks = TerrainSelection.Defaults(_catalog, _catalog.DefaultBoard);
        picks["ruina"] = picks["ruina"] with { MaterialId = "hormigon" };
        picks["charca"] = picks["charca"] with { MaterialId = "escombros" };

        var text = TerrainSelection.Format(picks, _catalog);
        var parsed = TerrainSelection.Parse(text, _catalog);

        Assert.Contains("ruina-2-hormigon", text);
        Assert.Contains("charca-0-escombros", text);
        Assert.Equal(picks, parsed);
    }

    [Fact]
    public void Default_materials_stay_out_of_the_link()
    {
        var text = TerrainSelection.Format(TerrainSelection.Defaults(_catalog, _catalog.DefaultBoard), _catalog);

        Assert.DoesNotContain("ladrillo", text);
        Assert.Contains("ruina-2", text);
    }

    [Fact]
    public void Parse_ignores_junk_and_clamps()
    {
        var parsed = TerrainSelection.Parse("ruina-99.ovni-3.coche--1.bidones-x.chabola-2.rocas-1-kriptonita.maleza-2-madera-x", _catalog);

        Assert.Equal(TerrainSelection.MaxPerType, parsed["ruina"].Count);
        Assert.Equal(0, parsed["coche"].Count);
        Assert.Equal(0, parsed["bidones"].Count);
        Assert.Equal(2, parsed["chabola"].Count);
        Assert.Equal(new TerrainPick(1, "piedra"), parsed["rocas"]);
        Assert.Equal(0, parsed["maleza"].Count);
        Assert.False(parsed.ContainsKey("ovni"));
    }

    [Fact]
    public void Old_links_without_materials_still_work()
    {
        var parsed = TerrainSelection.Parse("barricada-4.ruina-2", _catalog);

        Assert.Equal(new TerrainPick(4, "sacos"), parsed["barricada"]);
        Assert.Equal(new TerrainPick(2, "ladrillo"), parsed["ruina"]);
    }

    [Theory]
    [InlineData("v", DeploymentOrientation.Vertical)]
    [InlineData("V", DeploymentOrientation.Vertical)]
    [InlineData("h", DeploymentOrientation.Horizontal)]
    [InlineData(null, DeploymentOrientation.Horizontal)]
    [InlineData("loquesea", DeploymentOrientation.Horizontal)]
    public void Orientation_parses_from_the_link(string? text, DeploymentOrientation expected)
    {
        Assert.Equal(expected, TerrainSelection.ParseOrientation(text));
        Assert.Equal(expected, TerrainSelection.ParseOrientation(TerrainSelection.FormatOrientation(expected)));
    }

    [Fact]
    public void Every_material_has_a_distinct_look_and_a_rulebook_grade()
    {
        var looks = TerrainMaterial.All.Select(m => (m.Grade, m.Pattern)).ToList();

        Assert.Equal(looks.Count, looks.Distinct().Count());
        Assert.All(_catalog.Types, t => Assert.NotNull(_catalog.FindMaterial(t.MaterialId)));
        Assert.All(TerrainMaterial.All, m => Assert.DoesNotContain('-', m.Id));
    }
}
