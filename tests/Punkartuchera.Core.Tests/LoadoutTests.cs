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
}
