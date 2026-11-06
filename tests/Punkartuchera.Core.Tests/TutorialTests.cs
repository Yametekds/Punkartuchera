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
}
