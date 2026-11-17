using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

/// <summary>Todo lo que el reglamento deja reclutar o comprar se puede elegir en algún sitio de la app.</summary>
public class RecruitCoverageTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private static readonly RosterBuilder Builder = new(Book, new RulebookCatalog(Book));
    private static readonly UnitCostCalculator Costs = new(Book);
    private static readonly LoadoutAdvisor Advisor = new(Book, new GearLookup(Book), Costs);

    private static IEnumerable<(FactionRules Faction, FactionRules? Subfaction)> Bands() =>
        Book.Factions.Where(f => !f.IsMercenaries)
            .SelectMany(f => f.Subfactions.Select(s => (f, (FactionRules?)s)).Prepend((f, null)));

    [Fact]
    public void Every_troop_can_be_recruited_somewhere()
    {
        var reachable = Bands()
            .SelectMany(b => Builder.ChoicesFor(b.Faction.Id, b.Subfaction?.Id))
            .Select(c => $"{c.Owner.Id}/{c.Troop.Name}")
            .ToHashSet();

        var missing = Book.Factions
            .SelectMany(f => f.Subfactions.Prepend(f))
            .SelectMany(owner => owner.Troops.Select(t => $"{owner.Id}/{t.Name}"))
            .Where(key => !reachable.Contains(key))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_priced_weapon_armor_gear_and_extra_can_be_bought_by_some_troop()
    {
        var offered = new HashSet<string>();
        foreach (var (faction, subfaction) in Bands())
        {
            foreach (var choice in Builder.ChoicesFor(faction.Id, subfaction?.Id))
            {
                var member = Builder.FromTroop(choice.Troop, faction.Id);
                var loadout = Advisor.Advise(member, faction.Id, subfaction?.Id, []);
                foreach (var option in loadout.Gear.Concat(loadout.Skills))
                {
                    offered.Add($"{faction.Id}|{RulesText.Fold(option.Name)}");
                }
            }
        }

        var missing = new List<string>();
        foreach (var faction in Book.Factions.Where(f => !f.IsMercenaries))
        {
            foreach (var owner in faction.Subfactions.Prepend(faction))
            {
                var names = owner.Weapons.Where(w => w.Cost is not null).Select(w => w.Name)
                    .Concat(owner.Armors.Where(a => a.Cost is not null).Select(a => a.Name))
                    .Concat(owner.Gear.Where(g => g.Cost is not null && !g.Band).Select(g => g.Name))
                    .Concat(owner.Extras.SelectMany(g => g.Items).Where(i => i.Cost is not null).Select(i => i.Name));
                missing.AddRange(names.Where(n => !offered.Contains($"{faction.Id}|{RulesText.Fold(n)}")).Select(n => $"{owner.Id}: {n}"));
            }
        }

        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("chatarreros", "innovadores", "Pistón", "Guante de energía")]
    [InlineData("chatarreros", "innovadores", "Dinamo", "Cañón sónico")]
    [InlineData("sangre-negra", "nuevos-hijos", "Ascua", "Desbrozadora")]
    [InlineData("sangre-negra", "nuevos-hijos", "Ascua", "Sierra de mano")]
    [InlineData("pandilleros", "fraus", "Piltrafa", "Honda")]
    [InlineData("pandilleros", "fraus", "Machaca", "Honda")]
    public void Base_troops_can_buy_the_subfaction_weapons_meant_for_them(string factionId, string subfactionId, string troop, string weapon)
    {
        var choice = Builder.ChoicesFor(factionId, subfactionId).Single(c => c.Troop.Name == troop);
        var member = Builder.FromTroop(choice.Troop, factionId);

        var option = Assert.Single(Advisor.Advise(member, factionId, subfactionId).Gear, o => o.Name == weapon);
        member.Gear.Add(new Equipment { Name = weapon });
        Assert.Equal(choice.Troop.Cost + option.Cost, Costs.Calculate(member, factionId, subfactionId).Total);
    }

    [Fact]
    public void The_sling_replaces_the_throwing_knives_only_in_the_fraus()
    {
        var fraus = Builder.ChoicesFor("pandilleros", "fraus").Single(c => c.Troop.Name == "Piltrafa").Troop;
        var plain = Book.FindFaction("pandilleros")!.Troops.Single(t => t.Name == "Piltrafa");

        Assert.DoesNotContain(fraus.WeaponOptions, o => o.Name == "Cuchillos arrojadizos");
        Assert.Contains(plain.WeaponOptions, o => o.Name == "Cuchillos arrojadizos");
        Assert.DoesNotContain(plain.WeaponOptions, o => o.Name == "Honda");
        Assert.DoesNotContain(Advisor.Advise(Builder.FromTroop(plain, "pandilleros"), "pandilleros").Gear, o => o.Name == "Honda");
    }

    [Fact]
    public void Subfaction_troop_options_point_at_real_troops_and_weapons()
    {
        foreach (var subfaction in Book.Factions.SelectMany(f => f.Subfactions))
        {
            foreach (var (troop, options) in subfaction.TroopOptions)
            {
                Assert.Contains(troop, subfaction.BaseTroops);
                Assert.All(options, o => Assert.Contains(subfaction.Weapons, w => w.Name == o.Name));
            }
        }
    }
}
