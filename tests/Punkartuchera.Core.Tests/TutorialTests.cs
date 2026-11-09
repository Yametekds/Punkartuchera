using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Tutorials;

namespace Punkartuchera.Core.Tests;

public class TutorialTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();

    // Rutas de la app a las que puede llevar un tutorial.
    private static readonly string[] Routes = ["bandas/nueva", "reglas", "reglas/equipo", "reglas/especiales", "mapa", "partida"];

    [Fact]
    public void Has_the_four_guides_with_unique_ids()
    {
        Assert.Equal(["app", "jugar", "banda", "terreno"], TutorialCatalog.All.Select(t => t.Id));
        Assert.All(TutorialCatalog.All, t => Assert.NotEmpty(t.Steps));
    }

    [Fact]
    public void Every_rule_reference_exists_in_the_rulebook()
    {
        var sections = Book.Sections.ToDictionary(s => s.Id);
        foreach (var step in TutorialCatalog.All.SelectMany(t => t.Steps))
        {
            Assert.All(step.RuleIds, id => Assert.True(sections.ContainsKey(id), $"{step.Title}: {id}"));
            if (step.TableFrom is { } table)
            {
                Assert.True(sections.TryGetValue(table, out var section) && section.Table is not null, $"{step.Title}: {table}");
            }
        }
    }

    [Fact]
    public void Links_point_to_existing_screens()
    {
        foreach (var link in TutorialCatalog.All.SelectMany(t => t.Steps).Select(s => s.Link).OfType<TutorialLink>())
        {
            Assert.Contains(link.Href, Routes);
        }
    }

    [Fact]
    public void Every_photo_exists_and_has_a_caption()
    {
        var folder = ImageFolder();
        foreach (var image in TutorialCatalog.All.SelectMany(t => t.Steps).SelectMany(s => s.Images))
        {
            Assert.True(File.Exists(Path.Combine(folder, image.File)), image.File);
            Assert.False(string.IsNullOrWhiteSpace(image.Caption), image.File);
            Assert.False(string.IsNullOrWhiteSpace(image.Alt), image.File);
            Assert.InRange(image.Page, 1, 26);
        }
    }

    [Fact]
    public void Every_app_screenshot_exists_and_has_a_caption()
    {
        var folder = Path.Combine(ImageFolder(), "app");
        var shots = TutorialCatalog.All.SelectMany(t => t.Steps).SelectMany(s => s.Shots).ToList();
        Assert.NotEmpty(shots);
        foreach (var shot in shots)
        {
            Assert.True(File.Exists(Path.Combine(folder, shot.File)), shot.File);
            Assert.False(string.IsNullOrWhiteSpace(shot.Caption), shot.File);
            Assert.False(string.IsNullOrWhiteSpace(shot.Alt), shot.File);
        }
    }

    [Fact]
    public void Highlight_markers_come_in_pairs()
    {
        foreach (var step in TutorialCatalog.All.SelectMany(t => t.Steps))
        {
            Assert.All(step.Text, text => Assert.True(TutorialText.IsBalanced(text), $"{step.Title}: {text}"));
            Assert.True(TutorialText.IsBalanced(step.InApp), $"{step.Title}: {step.InApp}");
        }
    }

    [Theory]
    [InlineData("sin nada", "sin nada|")]
    [InlineData("dale a **Reclutar** y listo", "dale a |**Reclutar**| y listo|")]
    [InlineData("**Balas**: 15 puntos", "**Balas**|: 15 puntos|")]
    public void Split_separates_highlighted_words(string text, string expected)
    {
        var joined = string.Concat(TutorialText.Split(text).Select(r => (r.Key ? $"**{r.Text}**" : r.Text) + "|"));
        Assert.Equal(expected, joined);
    }

    [Theory]
    [InlineData("a **b** c", true)]
    [InlineData("a **b c", false)]
    [InlineData("a **** c", false)]
    [InlineData("a ** b** c", false)]
    public void IsBalanced_detects_broken_markers(string text, bool expected) =>
        Assert.Equal(expected, TutorialText.IsBalanced(text));

    private static string ImageFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Punkartuchera.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "Punkartuchera.UI", "wwwroot", "img", "tutoriales");
    }
}
