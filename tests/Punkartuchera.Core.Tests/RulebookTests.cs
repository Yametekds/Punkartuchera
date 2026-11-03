using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

public class RulebookTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private static readonly string[] AttributeKeys = ["a", "c", "p", "ag", "f", "d", "t"];
    private static readonly List<FactionRules> AllFactions = [.. Book.Factions, .. Book.Factions.SelectMany(f => f.Subfactions)];

    [Fact]
    public void Loads_core_and_every_faction()
    {
        Assert.Equal(AttributeKeys, Book.Attributes.Select(a => a.Key));
        Assert.NotEmpty(Book.Sections);
        Assert.NotEmpty(Book.SpecialRules);
        Assert.Equal(
            ["pandilleros", "mutardos", "chatarreros", "sangre-negra", "v-reich", "idos", "irradiados", "troupe", "unami", "craabianos", "banda-del-comic", "mercenarios"],
            Book.Factions.Select(f => f.Id));
    }

    [Fact]
    public void Every_troop_has_a_full_profile_and_cost()
    {
        foreach (var troop in AllFactions.SelectMany(f => f.Troops))
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
        foreach (var faction in AllFactions)
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
            .Concat(AllFactions.SelectMany(f => f.Troops).SelectMany(t => t.SpecialRules))
            .Distinct();
        var known = AllFactions.SelectMany(f => f.Rules).Select(r => r.Name).Append("Personalidad").Append("Mercenario").ToHashSet();
        foreach (var name in referenced.Where(n => !known.Contains(n)))
        {
            Assert.True(Book.FindSpecialRule(name) is not null, $"Regla especial sin definir: {name}");
        }
    }

    [Fact]
    public void Troop_weapon_options_exist_in_the_armoury()
    {
        foreach (var faction in AllFactions)
        {
            var names = Book.WeaponsFor(faction).Select(w => w.Name).ToHashSet();
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

    [Fact]
    public void Every_playable_faction_but_the_comic_band_has_three_subfactions()
    {
        foreach (var faction in Book.Factions.Where(f => !f.IsMercenaries && f.Id != "banda-del-comic"))
        {
            Assert.Equal(3, faction.Subfactions.Count);
            Assert.All(faction.Subfactions, s =>
            {
                Assert.Equal(faction.Id, s.ParentId);
                Assert.NotNull(Book.FindSource(s.Source));
                Assert.NotEmpty(s.Restrictions);
            });
        }

        var slashers = Book.FindSubfaction("slashers");
        Assert.NotNull(slashers);
        Assert.Contains(slashers.Troops, t => t.Name == "Psycho-killer");
        var plasma = Assert.Single(Book.Weapons, w => w.Name == "Cortadora de plasma");
        Assert.Equal(("irradiados", "rebelion-isotopica"), (plasma.FactionId, plasma.SubfactionId));
    }

    [Fact]
    public void Shared_weapons_reach_other_factions()
    {
        Assert.Contains(Book.WeaponsFor("craabianos"), w => w.Name == "Trabuco");
        Assert.DoesNotContain(Book.WeaponsFor("pandilleros"), w => w.Name == "Trabuco");
    }

    [Fact]
    public void Preset_lists_use_real_troops_and_about_350_points()
    {
        Assert.NotEmpty(Book.Lists);
        Assert.Equal(Book.Lists.Count, Book.Lists.Select(l => l.Id).Distinct().Count());
        foreach (var list in Book.Lists)
        {
            var faction = Book.FindFaction(list.FactionId);
            Assert.NotNull(faction);
            Assert.InRange(list.Cost, 330, 360);
            Assert.All(list.Members, m => Assert.Contains(faction.Troops, t => t.Name == m.Troop));
        }
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
    public void Finds_subfaction_troops()
    {
        var hit = Assert.Single(_search.Search("psycho killer"), h => h.Title == "Psycho-killer");

        Assert.Equal("reglas/faccion/idos/subfaccion/slashers#psycho-killer", hit.Href);
        Assert.Equal("idos", hit.FactionId);
    }

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

public class RosterBuilderTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private readonly RosterBuilder _builder = new(Book, new RulebookCatalog(Book));

    [Fact]
    public void Troop_becomes_a_member_with_profile_cost_and_fixed_gear()
    {
        var worms = Assert.Single(Book.FindSubfaction("slashers")!.Troops, t => t.Name == "El Hombre de los Gusanos");

        var member = _builder.FromTroop(worms);

        Assert.Equal(("El Hombre de los Gusanos", 90), (member.Name, member.Cost));
        Assert.Equal(5, member.Stats["c"]);
        Assert.Equal(3, member.Stats["p"]);
        Assert.Contains(member.Gear, g => g.Name == "Filo medio");
        Assert.DoesNotContain("Personalidad", member.Skills);
        Assert.Contains("Todoterreno", member.Skills);
    }

    [Fact]
    public void Dashes_use_the_minimum_or_the_default()
    {
        var stats = _builder.ParseStats(new Dictionary<string, string> { ["a"] = "-", ["c"] = "X", ["p"] = "-", ["f"] = "4 (7)/(5)" });

        Assert.Equal(2, stats["a"]);
        Assert.Equal(0, stats["c"]);
        Assert.Equal(0, stats["p"]);
        Assert.Equal(7, stats["f"]);
    }

    [Fact]
    public void Choices_include_subfaction_troops_and_mercenaries()
    {
        var choices = _builder.ChoicesFor("idos", "slashers");

        Assert.Contains(choices, c => c.Troop.Name == "Psycho-killer");
        Assert.Contains(choices, c => c.Group == "Mercenarios");
        Assert.DoesNotContain(choices, c => c.Troop.Name == "Abominación");
        var again = _builder.FindChoice(choices.First(c => c.Troop.Name == "Psycho-killer").Key);
        Assert.Equal("slashers", again?.Owner.Id);
    }

    [Fact]
    public void Preset_list_keeps_names_costs_and_printed_stats()
    {
        var list = Book.FindList("pandilleros-1")!;

        var members = _builder.FromList(list);

        Assert.Equal(list.Cost, members.Sum(m => m.Cost));
        var boss = members[0];
        Assert.Equal(("Jefa", "Jefe"), (boss.Name, boss.Role));
        Assert.Equal(7, boss.Stats["c"]);
        Assert.Contains(boss.Gear, g => g.Name == "Rifle");
        Assert.Contains("Retraso (3)", boss.Skills);
    }
}

public class GearLookupTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private readonly GearLookup _lookup = new(Book);

    [Theory]
    [InlineData("Pistola", "Pistola", GearInfoKind.RangedWeapon)]
    [InlineData("filo MEDIO incrustado", "Filo medio", GearInfoKind.MeleeWeapon)]
    [InlineData("Chasqueador", "Chasqueador y traje voltaico", GearInfoKind.MeleeWeapon)]
    [InlineData("Cuero endurecido", "Cuero endurecido", GearInfoKind.Armor)]
    [InlineData("Pez globo", "Pez globo", GearInfoKind.Extra)]
    public void Finds_gear_by_approximate_name(string query, string expected, GearInfoKind kind)
    {
        var info = _lookup.Find(query, "chatarreros") ?? _lookup.Find(query);

        Assert.NotNull(info);
        Assert.Equal((expected, kind), (info.Name, info.Kind));
        Assert.False(string.IsNullOrWhiteSpace(info.Summary));
    }

    [Fact]
    public void Weapon_summary_has_its_profile()
    {
        var info = _lookup.Find("Pistola");

        Assert.Contains("Pen", info!.Summary);
        Assert.Contains("cm", info.Summary);
    }

    [Fact]
    public void Every_item_in_the_preset_lists_has_rules_data()
    {
        foreach (var list in Book.Lists)
        {
            foreach (var name in list.Members.SelectMany(m => m.Weapons.Concat(m.Gear).Append(m.Armor ?? "Ropas resistentes")))
            {
                Assert.True(_lookup.Find(name, list.FactionId) is not null, $"{list.Name}: sin datos para {name}");
            }
        }
    }
}
