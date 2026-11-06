using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

public class UnitRulesTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private readonly RosterBuilder _builder = new(Book, new RulebookCatalog(Book));
    private readonly UnitRules _rules = new(Book, new UnitCostCalculator(Book), new GearLookup(Book));

    private Miniature Recruit(string factionId, string troop) =>
        _builder.FromTroop(Book.FindFaction(factionId)!.Troops.First(t => t.Name == troop), factionId);

    [Fact]
    public void Scum_shows_its_full_rule()
    {
        var rules = _rules.For(Recruit("idos", "Despojo"), "idos");

        var escoria = Assert.Single(rules, r => r.Name == "Escoria");
        Assert.Equal("No puede tomar Ambrosía ni llevar armas incrustadas.", escoria.Text);
    }

    [Fact]
    public void Keywords_come_with_their_text_and_number()
    {
        var rules = _rules.For(Recruit("mercenarios", "Imperatrix Rabiosa"), "pandilleros");

        Assert.Contains(rules, r => r.Name == "Mercenario" && r.Text.Contains("Como máximo uno por banda", StringComparison.Ordinal));
        Assert.Contains(rules, r => r.Name == "Retraso (2)" && r.Href == $"reglas/especiales#{Book.FindSpecialRule("Retraso (X)")!.Slug}");
    }

    [Fact]
    public void Faction_keywords_are_found_in_the_faction_rules()
    {
        var rules = _rules.For(Recruit("irradiados", "Discípulo"), "irradiados");

        Assert.Contains(rules, r => r.Name == "Radioinmune" && r.Text.Length > 0);
        Assert.Contains(rules, r => r.Name == "Contenedor radiactivo" && r.Text.Length > 0);
    }

    [Fact]
    public void Gear_keywords_say_which_item_has_them()
    {
        var bruto = Recruit("pandilleros", "Bruto");
        bruto.Gear.Add(new Equipment { Name = "Escopeta" });

        var rules = _rules.For(bruto, "pandilleros");

        Assert.Contains(rules, r => r.Origin == UnitRuleOrigin.Gear && r.Name == "A dos manos" && r.From == "Escopeta");
    }

    [Fact]
    public void Tables_travel_with_the_rule()
    {
        var rules = _rules.For(Recruit("idos", "Cosechador"), "idos");

        Assert.NotNull(Assert.Single(rules, r => r.Name == "Cosecha").Table);
    }

    [Fact]
    public void Every_keyword_of_every_troop_has_its_text()
    {
        foreach (var faction in Book.Factions)
        {
            foreach (var (owner, troop) in faction.Subfactions.Prepend(faction).SelectMany(o => o.Troops.Select(t => (o, t))))
            {
                var member = _builder.FromTroop(troop, faction.Id);
                var subfactionId = owner.IsSubfaction ? owner.Id : null;
                var rules = _rules.For(member, faction.Id, subfactionId);
                foreach (var keyword in troop.SpecialRules)
                {
                    Assert.True(rules.Any(r => r.Name == keyword && r.Text.Length > 0), $"{faction.Id}/{troop.Name}: {keyword}");
                }
            }
        }
    }
}
