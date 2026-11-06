using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

public class LoadoutTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private readonly RosterBuilder _builder = new(Book, new RulebookCatalog(Book));
    private readonly LoadoutAdvisor _advisor = new(Book, new GearLookup(Book), new UnitCostCalculator(Book));

    private Miniature Recruit(string factionId, string troop) =>
        _builder.FromTroop(Book.FindFaction(factionId)!.Troops.First(t => t.Name == troop), factionId);

    private Miniature RecruitSub(string factionId, string subfactionId, string troop) =>
        _builder.FromTroop(Book.FindSubfaction(subfactionId)!.Troops.First(t => t.Name == troop), factionId);

    private Miniature Buy(Miniature member, string factionId, string name, string? subfactionId = null, IEnumerable<Miniature>? band = null)
    {
        var loadout = _advisor.Advise(member, factionId, subfactionId, band);
        var option = Option(loadout, name);
        Assert.True(option.CanAdd, $"{name}: {option.Blocked}");
        var names = option.Slot == LoadoutSlot.Skill ? member.Skills : member.Gear.Select(g => g.Name).ToList();
        LoadoutAdvisor.Add(names, option);
        if (option.Slot != LoadoutSlot.Skill)
        {
            member.Gear = names.Select(n => new Equipment { Name = n }).ToList();
        }

        return member;
    }

    private static LoadoutOption Option(Loadout loadout, string name) =>
        loadout.Gear.Concat(loadout.Skills).First(o => o.Name == name);

    [Fact]
    public void Offers_the_troop_list_with_prices_and_profiles()
    {
        var loadout = _advisor.Advise(Recruit("pandilleros", "Bruto"), "pandilleros");

        Assert.True(loadout.FromTroopList);
        Assert.Equal(new WeaponLimit(1, 2, "Al menos una y como máximo dos (nunca dos A dos manos)"), loadout.Limit);
        var shotgun = Option(loadout, "Escopeta");
        Assert.Equal(LoadoutSlot.Weapon, shotgun.Slot);
        Assert.Equal("Armas a distancia", shotgun.Group);
        Assert.Equal(6, shotgun.Cost);
        Assert.Contains("Pen 3", shotgun.Summary);
        Assert.Equal(LoadoutSlot.Armor, Option(loadout, "Armadura metálica").Slot);
        Assert.Contains(loadout.Gear, o => o.Name == "Escudo");
        Assert.Contains(loadout.Gear, o => o.Name == "Protección contra la contaminación");
        // Armas que no están en la lista del Bruto no se ofrecen.
        Assert.DoesNotContain(loadout.Gear, o => o.Name == "Rifle");
        Assert.Contains("Le falta un arma: el reglamento pide al menos una.", loadout.Warnings);
    }

    [Fact]
    public void Two_handed_weapons_shield_and_armor_block_each_other()
    {
        var bruto = Recruit("pandilleros", "Bruto");
        bruto.Gear.Add(new Equipment { Name = "Escopeta" });
        var loadout = _advisor.Advise(bruto, "pandilleros");

        Assert.Equal("Ya lo lleva", Option(loadout, "Escopeta").Blocked);
        Assert.Equal("Ya lleva algo A dos manos", Option(loadout, "Maza pesada").Blocked);
        Assert.Null(Option(loadout, "Maza").Blocked);
        Assert.Equal("No se combina con armas A dos manos", Option(loadout, "Escudo").Blocked);

        bruto.Gear.Add(new Equipment { Name = "Maza" });
        bruto.Gear.RemoveAll(g => g.Name == "Cuero endurecido");
        bruto.Gear.Add(new Equipment { Name = "Pura chapa" });
        loadout = _advisor.Advise(bruto, "pandilleros");
        Assert.Equal("Ya lleva 2 armas", Option(loadout, "Maza pesada").Blocked);
        Assert.Equal("Pura chapa", Option(loadout, "Armadura metálica").Replaces);
        Assert.Empty(loadout.Warnings);
    }

    [Fact]
    public void A_better_armor_replaces_the_one_the_troop_wears()
    {
        var bruto = Recruit("pandilleros", "Bruto");
        var names = bruto.Gear.Select(g => g.Name).ToList();
        Assert.Contains("Cuero endurecido", names);

        var plate = Option(_advisor.Advise(bruto, "pandilleros"), "Armadura metálica");
        Assert.Null(plate.Blocked);
        Assert.Equal("Cuero endurecido", plate.Replaces);

        LoadoutAdvisor.Add(names, plate);
        bruto.Gear = names.Select(n => new Equipment { Name = n }).ToList();

        Assert.DoesNotContain("Cuero endurecido", names);
        Assert.Equal(40 + 10, new UnitCostCalculator(Book).Calculate(bruto, "pandilleros").Total);
    }

    [Fact]
    public void Single_weapon_troops_stop_at_one()
    {
        var guardian = Recruit("sangre-negra", "Guardián de la Llama");
        guardian.Gear.Add(new Equipment { Name = "Maza" });
        var loadout = _advisor.Advise(guardian, "sangre-negra");

        Assert.Equal("Ya lleva su arma", Option(loadout, "Pistola").Blocked);
    }

    [Fact]
    public void Units_bought_by_the_piece_stack_up()
    {
        var dinamo = Recruit("chatarreros", "Dinamo");
        var names = dinamo.Gear.Select(g => g.Name).ToList();
        var bomb = Option(_advisor.Advise(dinamo, "chatarreros"), "Bomba de humo");
        Assert.True(bomb.Each);

        LoadoutAdvisor.Add(names, bomb);
        LoadoutAdvisor.Add(names, bomb);

        Assert.Contains("Bomba de humo x2", names);
        dinamo.Gear = names.Select(n => new Equipment { Name = n }).ToList();
        Assert.Equal(2, Option(_advisor.Advise(dinamo, "chatarreros"), "Bomba de humo").Owned);
        Assert.Equal(5 * 2, new UnitCostCalculator(Book).Calculate(dinamo, "chatarreros").Total - dinamo.BaseCost);
    }

    [Fact]
    public void Scarce_weapons_are_limited_across_the_band()
    {
        var band = Enumerable.Range(0, 3).Select(_ =>
        {
            var m = Recruit("pandilleros", "Curtido");
            m.Gear.Add(new Equipment { Name = "Rifle" });
            return m;
        }).ToList();
        var fourth = Recruit("pandilleros", "Curtido");

        var loadout = _advisor.Advise(fourth, "pandilleros", null, band);

        Assert.Equal("Escasa: ya hay 3 en la banda", Option(loadout, "Rifle").Blocked);
    }

    [Fact]
    public void Faction_upgrades_go_to_skills()
    {
        var loadout = _advisor.Advise(Recruit("mutardos", "Mutante"), "mutardos");

        Assert.NotEmpty(loadout.Skills);
        Assert.All(loadout.Skills, o => Assert.Equal(LoadoutSlot.Skill, o.Slot));
        Assert.Contains(loadout.Skills, o => o.Group == "Mutaciones");
    }

    [Fact]
    public void Personalities_and_mercenaries_have_closed_gear()
    {
        var mel = _builder.FromTroop(Book.FindFaction("mercenarios")!.Troops.First(t => t.Name == "Crazy Mel"), "pandilleros");
        var loadout = _advisor.Advise(mel, "pandilleros");

        Assert.True(loadout.Closed);
        Assert.Empty(loadout.Gear);
        Assert.Empty(loadout.Skills);
    }

    [Fact]
    public void Hand_made_members_see_everything_priced_for_their_faction()
    {
        var loadout = _advisor.Advise(new Miniature { Name = "Fulano", Role = "Matón" }, "pandilleros");

        Assert.False(loadout.FromTroopList);
        Assert.Null(loadout.Limit);
        Assert.Contains(loadout.Gear, o => o.Name == "Rifle");
        Assert.DoesNotContain(loadout.Gear, o => o.Name == "Balas");
    }

    [Theory]
    [InlineData("Una", 1, 1)]
    [InlineData("Puede llevar una", 0, 1)]
    [InlineData("Al menos una y como máximo dos (nunca dos A dos manos)", 1, 2)]
    public void Weapon_rules_are_read_from_the_troop(string rule, int min, int max)
    {
        var limit = LoadoutAdvisor.ParseLimit(rule)!;
        Assert.Equal((min, max), (limit.Min, limit.Max));
    }

    [Fact]
    public void Harpoons_and_other_unit_purchases_stop_at_three()
    {
        var arponero = Recruit("craabianos", "Arponero");
        for (var i = 0; i < 3; i++)
        {
            Buy(arponero, "craabianos", "Arpón");
        }

        Assert.Contains(arponero.Gear, g => g.Name == "Arpón x3");
        Assert.Equal("Como máximo 3", Option(_advisor.Advise(arponero, "craabianos"), "Arpón").Blocked);
        // Los tres arpones cuentan como una sola arma.
        Assert.Equal(1, _advisor.Advise(arponero, "craabianos").Weapons);
    }

    [Fact]
    public void Every_unit_purchase_in_every_faction_stops_at_three()
    {
        foreach (var faction in Book.Factions.Where(f => !f.IsMercenaries))
        {
            foreach (var owner in faction.Subfactions.Prepend(faction))
            {
                foreach (var troop in owner.Troops.Where(t => !t.Personality))
                {
                    foreach (var option in troop.WeaponOptions.Where(o => o.Each && o.Cost is not null))
                    {
                        var member = _builder.FromTroop(troop, faction.Id);
                        member.Gear.RemoveAll(_ => true);
                        var sub = owner == faction ? null : owner.Id;
                        for (var i = 0; i < 3; i++)
                        {
                            Buy(member, faction.Id, option.Name, sub);
                        }

                        Assert.Equal("Como máximo 3", Option(_advisor.Advise(member, faction.Id, sub), option.Name).Blocked);
                    }
                }
            }
        }
    }

    [Fact]
    public void Abyssal_relics_only_for_captains_and_custodians_and_once_per_band()
    {
        Assert.DoesNotContain(_advisor.Advise(Recruit("craabianos", "Grumete"), "craabianos").Skills, o => o.Group == "Reliquias abisales");

        var captain = Buy(Recruit("craabianos", "Capitán"), "craabianos", "Pez globo");
        Assert.Equal("Solo puede llevar una de Reliquias abisales", Option(_advisor.Advise(captain, "craabianos"), "Máscara tentacular").Blocked);

        var custodian = Recruit("craabianos", "Custodio del Abismo");
        var loadout = _advisor.Advise(custodian, "craabianos", null, [captain]);
        Assert.Equal("Única en la banda: ya está en otra miniatura", Option(loadout, "Pez globo").Blocked);
        Buy(custodian, "craabianos", "Máscara tentacular", null, [captain]);
        Buy(custodian, "craabianos", "Diente de leviatán", null, [captain]);
        Assert.Equal("Ya lleva 2 de Reliquias abisales", Option(_advisor.Advise(custodian, "craabianos", null, [captain]), "Cáliz de salmuera").Blocked);
    }

    [Fact]
    public void Joke_items_follow_each_troop_and_are_banned_in_subfactions()
    {
        var pachacho = Recruit("troupe", "Pachacho");
        var first = _advisor.Advise(pachacho, "troupe").Skills.First(o => o.Group == "Artículos de coña");
        Buy(pachacho, "troupe", first.Name);
        var second = _advisor.Advise(pachacho, "troupe").Skills.First(o => o.Group == "Artículos de coña" && o.CanAdd);
        Buy(pachacho, "troupe", second.Name);
        Assert.All(_advisor.Advise(pachacho, "troupe").Skills.Where(o => o.Group == "Artículos de coña"), o => Assert.False(o.CanAdd));

        Assert.DoesNotContain(_advisor.Advise(Recruit("troupe", "Domador"), "troupe").Skills, o => o.Group == "Artículos de coña");
        Assert.DoesNotContain(_advisor.Advise(Recruit("troupe", "Acróbata"), "troupe", "cirque-du-no-se").Skills, o => o.Group == "Artículos de coña");
    }

    [Fact]
    public void Mutations_follow_each_troop()
    {
        var beast = Recruit("mutardos", "Bestia del pozo");
        var options = _advisor.Advise(beast, "mutardos").Skills.Where(o => o.Group == "Mutaciones").ToList();
        Assert.Equal(8, options.Count);
        Assert.DoesNotContain(options, o => o.Name == "Rana");

        var mutant = Recruit("mutardos", "Mutante");
        Assert.Contains("Tiene que llevar al menos una de Mutaciones.", _advisor.Advise(mutant, "mutardos").Warnings);
        Assert.Empty(_advisor.Advise(Recruit("mutardos", "Perromorfo"), "mutardos").Skills.Where(o => o.Group == "Mutaciones"));

        var band = Enumerable.Range(0, 3).Select(_ => Buy(Recruit("mutardos", "Mutante"), "mutardos", "Rana")).ToList();
        Assert.Equal("Como máximo 3 en la banda", Option(_advisor.Advise(mutant, "mutardos", null, band), "Rana").Blocked);
    }

    [Fact]
    public void Oddities_have_a_points_budget()
    {
        var acrobat = Recruit("troupe", "Acróbata");
        var loadout = _advisor.Advise(acrobat, "troupe", "freak-show");
        Assert.Equal("Pasaría de 15 ptos en Rarezas", Option(loadout, "Siamés").Blocked);

        var freak = RecruitSub("troupe", "freak-show", "Bicho raro");
        Assert.Null(Option(_advisor.Advise(freak, "troupe", "freak-show"), "Siamés").Blocked);
        Assert.Contains(_advisor.Advise(freak, "troupe", "freak-show").Warnings, w => w.StartsWith("Tiene que gastar al menos 10 ptos en Rarezas"));
    }

    [Fact]
    public void Subfactions_ban_some_gear()
    {
        Assert.DoesNotContain(_advisor.Advise(Recruit("chatarreros", "Pistón"), "chatarreros", "carroneros").Gear, o => o.Name == "Puño balístico");
        Assert.DoesNotContain(_advisor.Advise(Recruit("chatarreros", "Engranaje"), "chatarreros", "carroneros").Gear, o => o.Name == "Bombeadora");
        Assert.DoesNotContain(_advisor.Advise(Recruit("chatarreros", "Pistón"), "chatarreros", "innovadores").Gear, o => o.Name == "Puño balístico");
        Assert.Contains(_advisor.Advise(Recruit("chatarreros", "Pistón"), "chatarreros").Gear, o => o.Name == "Puño balístico");
        Assert.DoesNotContain(_advisor.Advise(Recruit("sangre-negra", "Ascua"), "sangre-negra", "nuevos-hijos").Gear, o => o.Name == "Cóctel molotov");
    }

    [Fact]
    public void Some_troops_cannot_buy_generic_gear()
    {
        Assert.DoesNotContain(_advisor.Advise(Recruit("v-reich", "Subhumano"), "v-reich").Gear, o => o.Name == "Protección contra la contaminación");
        Assert.DoesNotContain(_advisor.Advise(Recruit("idos", "Aberración"), "idos").Gear, o => o.Name == "Protección contra la contaminación");
        Assert.Contains(_advisor.Advise(Recruit("v-reich", "Soldat"), "v-reich").Gear, o => o.Name == "Protección contra la contaminación");
    }

    [Fact]
    public void Scout_skills_are_unique_in_the_band_except_two()
    {
        var sonda = Recruit("chatarreros", "Sonda");
        Buy(sonda, "chatarreros", "Felino", "carroneros");
        Buy(sonda, "chatarreros", "Velocista", "carroneros");
        var other = Recruit("chatarreros", "Sonda");
        var loadout = _advisor.Advise(other, "chatarreros", "carroneros", [sonda]);

        Assert.NotNull(Option(loadout, "Felino").Blocked);
        Assert.Null(Option(loadout, "Velocista").Blocked);
        Assert.Equal("Ya lleva 2 de Habilidades de Sonda", Option(_advisor.Advise(sonda, "chatarreros", "carroneros"), "Acusica").Blocked);
    }
}
