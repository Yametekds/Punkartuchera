using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

public class RulebookTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private static readonly string[] AttributeKeys = ["a", "c", "p", "ag", "f", "d", "t"];

    [Fact]
    public void Loads_core_and_every_faction()
    {
        Assert.Equal(AttributeKeys, Book.Attributes.Select(a => a.Key));
        Assert.NotEmpty(Book.Sections);
        Assert.NotEmpty(Book.SpecialRules);
        Assert.Equal(
            ["pandilleros", "mutardos", "chatarreros", "sangre-negra", "v-reich", "idos", "irradiados", "troupe", "unami", "banda-del-comic", "mercenarios"],
            Book.Factions.Select(f => f.Id));
    }

    [Fact]
    public void Every_troop_has_a_full_profile_and_cost()
    {
        foreach (var troop in Book.Factions.SelectMany(f => f.Troops))
        {
            Assert.True(troop.Cost > 0, troop.Name);
            Assert.Equal(AttributeKeys, troop.Stats.Keys);
            Assert.All(troop.Profiles, p => Assert.Equal(AttributeKeys, p.Stats.Keys));
        }
    }

    [Fact]
    public void Ids_and_anchors_are_unique()
    {
        Assert.Equal(Book.Sections.Count, Book.Sections.Select(s => s.Id).Distinct().Count());
        Assert.Equal(Book.SpecialRules.Count, Book.SpecialRules.Select(r => r.Slug).Distinct().Count());
        Assert.Equal(Book.Weapons.Count, Book.Weapons.Select(w => w.Slug).Distinct().Count());
        foreach (var faction in Book.Factions)
        {
            Assert.Equal(faction.Troops.Count, faction.Troops.Select(t => t.Slug).Distinct().Count());
        }
    }

    [Fact]
    public void Sources_and_special_rules_referenced_by_data_exist()
    {
        Assert.All(Book.Sections, s => Assert.NotNull(Book.FindSource(s.Source)));
        Assert.All(Book.Factions, f => Assert.NotNull(Book.FindSource(f.Source)));

        var referenced = Book.Weapons.SelectMany(w => w.Rules)
            .Concat(Book.Armors.SelectMany(a => a.Rules))
            .Concat(Book.Factions.SelectMany(f => f.Troops).SelectMany(t => t.SpecialRules))
            .Distinct();
        var known = Book.Factions.SelectMany(f => f.Rules).Select(r => r.Name).Append("Personalidad").Append("Mercenario").ToHashSet();
        foreach (var name in referenced.Where(n => !known.Contains(n)))
        {
            Assert.True(Book.FindSpecialRule(name) is not null, $"Regla especial sin definir: {name}");
        }
    }

    [Fact]
    public void Troop_weapon_options_exist_in_the_armoury()
    {
        foreach (var faction in Book.Factions)
        {
            var names = Book.WeaponsFor(faction.Id).Select(w => w.Name).ToHashSet();
            foreach (var option in faction.Troops.SelectMany(t => t.WeaponOptions))
            {
                Assert.True(names.Contains(option.Name), $"{faction.Name}: arma sin perfil {option.Name}");
            }
        }
    }

    [Fact]
    public void Faction_specific_gear_is_tagged()
    {
        var motosierra = Assert.Single(Book.Weapons, w => w.Name == "Motosierra");
        Assert.Equal("sangre-negra", motosierra.FactionId);
        Assert.Null(Assert.Single(Book.Weapons, w => w.Name == "Pistola").FactionId);
    }

    [Theory]
    [InlineData("Retraso (2)", "Retraso (X)")]
    [InlineData("catapum", null)]
    [InlineData("¡¡¡CATAPUM!!!", "¡¡¡CATAPUM!!!")]
    [InlineData("mongüer", "Mónguer")]
    [InlineData("Mónguer", "Mónguer")]
    public void FindSpecialRule_ignores_parameters_and_accents(string query, string? expected)
    {
        Assert.Equal(expected, Book.FindSpecialRule(query)?.Name);
    }

    [Fact]
    public void Catalog_exposes_real_attributes_factions_and_troops()
    {
        var catalog = new RulebookCatalog(Book);

        Assert.False(catalog.IsPlaceholder);
        Assert.Equal(AttributeKeys, catalog.Stats.Select(s => s.Key));
        Assert.DoesNotContain(catalog.Factions, f => f.Id == "mercenarios");
        Assert.NotNull(catalog.FindFaction("sangre-negra"));
        Assert.Contains("Jefe", catalog.Roles);
        Assert.Contains("Pocho", catalog.Roles);
    }
}

public class RulesSearchTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private readonly RulesSearch _search = new(Book);

    [Fact]
    public void Finds_without_accents_or_case()
    {
        var hit = _search.Search("PENETRACION").First();

        Assert.Equal("Blindaje y Penetración", hit.Title);
        Assert.Equal("reglas/basicas#blindaje", hit.Href);
    }

    [Fact]
    public void Title_matches_rank_first()
    {
        var hit = _search.Search("fuego defensivo").First();

        Assert.Equal(RuleHitKind.Section, hit.Kind);
        Assert.Equal("Fuego defensivo", hit.Title);
    }

    [Fact]
    public void Requires_every_word()
    {
        Assert.Empty(_search.Search("motosierra zzzz"));
    }

    [Fact]
    public void Finds_troops_with_link_to_their_card()
    {
        var hit = Assert.Single(_search.Search("bestia del pozo"), h => h.Kind == RuleHitKind.Troop);

        Assert.Equal("reglas/faccion/mutardos/miembros#bestia-del-pozo", hit.Href);
    }

    [Fact]
    public void Faction_filter_keeps_generic_rules_and_hides_other_factions()
    {
        var hits = _search.Search("lanzallamas", factionId: "pandilleros");

        Assert.DoesNotContain(hits, h => h.FactionId == "sangre-negra");
        Assert.NotEmpty(_search.Search("lanzallamas", factionId: "sangre-negra"));
    }

    [Fact]
    public void Ignores_stop_words()
    {
        Assert.NotEmpty(_search.Search("la escopeta"));
        Assert.Equal(_search.Search("escopeta").Count, _search.Search("la escopeta").Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_query_returns_nothing(string? query)
    {
        Assert.Empty(_search.Search(query));
    }
}
