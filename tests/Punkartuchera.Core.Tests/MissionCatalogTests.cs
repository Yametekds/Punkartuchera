using Punkartuchera.Core.Missions;

namespace Punkartuchera.Core.Tests;

public class MissionCatalogTests
{
    [Fact]
    public void Has_the_eighteen_scenarios_of_the_pdf_in_order_and_then_the_loose_ones()
    {
        var book = MissionCatalog.All.Where(m => m.Pdf is null).ToList();
        Assert.Equal(18, book.Count);
        Assert.Equal(book.OrderBy(m => m.Page).Select(m => m.Id), book.Select(m => m.Id));
        Assert.Equal(book, MissionCatalog.All.Take(book.Count));
        Assert.Equal(MissionCatalog.All.Count, MissionCatalog.All.Select(m => m.Id).Distinct().Count());
        Assert.All(book, m => Assert.InRange(m.Page, 5, 23));
        Assert.All(MissionCatalog.All, m =>
        {
            Assert.InRange(m.Page, 1, 23);
            Assert.InRange(m.ContaminatedZones, 1, 2);
            Assert.False(string.IsNullOrWhiteSpace(m.Summary));
            Assert.False(string.IsNullOrWhiteSpace(m.Scoring));
            Assert.Matches("^[a-z0-9-]+$", m.Id);
        });
    }

    [Fact]
    public void Finds_by_id_and_ignores_unknown_ones()
    {
        Assert.Equal("Agua pura", MissionCatalog.Find("agua-pura")!.Name);
        Assert.Null(MissionCatalog.Find("no-existe"));
        Assert.Null(MissionCatalog.Find(null));
    }

    [Fact]
    public void Random_never_repeats_the_current_one()
    {
        var random = new Random(7);
        for (var i = 0; i < 200; i++)
        {
            Assert.NotEqual("rat-pack", MissionCatalog.Random(random, "rat-pack").Id);
        }

        Assert.Contains(MissionCatalog.Random(random), MissionCatalog.All);
    }

    [Fact]
    public void Pdf_link_opens_the_page_of_the_mission()
    {
        Assert.EndsWith("Escenarios_.pdf#page=17", MissionCatalog.PdfLink(MissionCatalog.Find("sondas-del-espacio-exterior")!));
        Assert.EndsWith("Punkalabazas.pdf#page=1", MissionCatalog.PdfLink(MissionCatalog.Find("punkalabazas")!));
    }

    [Fact]
    public void Every_mission_has_its_photo_and_map_from_the_pdf()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Punkartuchera.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var folder = Path.Combine(dir!.FullName, "src", "Punkartuchera.UI", "wwwroot", "img", "misiones");
        Assert.All(MissionCatalog.All.Where(m => m.HasArt), m =>
        {
            Assert.True(File.Exists(Path.Combine(folder, $"{m.Id}.webp")), m.Id);
            Assert.True(File.Exists(Path.Combine(folder, $"{m.Id}-mapa.webp")), m.Id);
        });
    }
}
