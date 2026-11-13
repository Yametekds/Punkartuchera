using Punkartuchera.Core.Matches;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

public class WeaponUseTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private static readonly GearLookup Lookup = new(Book);
    private static readonly UnitCostCalculator Costs = new(Book);
    private static readonly RosterBuilder Roster = new(Book, new RulebookCatalog(Book).Stats);
    private readonly MemberWeapons _weapons = new(Lookup);
    private readonly LoadoutModifiers _modifiers = new(Book, Lookup, Costs);

    [Fact]
    public void Craabianos_weapons_do_not_take_bullets()
    {
        foreach (var weapon in new[] { "Pistola de mecha", "Trabuco", "Arpón", "Red", "Cañón de mano", "Tridente" })
        {
            var member = Member("craabianos", "Marinero", weapon);
            Assert.False(_weapons.CanBuyAmmo(member, "craabianos"), weapon);
        }

        Assert.True(_weapons.CanBuyAmmo(Member("pandilleros", "Jefe", "Pistola"), "pandilleros"));
    }

    [Fact]
    public void Lentorra_weapons_reload_and_one_shot_weapons_are_spent()
    {
        var weapons = _weapons.Of(["Pistola de mecha", "Trabuco", "Red", "Cañón de mano", "Arpón x2", "Tridente"], "craabianos");

        Assert.Contains(new RangedWeapon("Pistola de mecha", ShotKind.Reload, 2), weapons);
        Assert.Contains(new RangedWeapon("Trabuco", ShotKind.Reload, 1), weapons);
        Assert.Contains(new RangedWeapon("Red", ShotKind.SingleUse), weapons);
        Assert.Contains(new RangedWeapon("Cañón de mano", ShotKind.SingleUse), weapons);
        Assert.Contains(new RangedWeapon("Arpón", ShotKind.SingleUse, 0, 2), weapons);
        Assert.DoesNotContain(weapons, w => w.Name == "Tridente");
    }

    [Theory]
    [InlineData("craabianos", null, "Davinnie Jones", ShotKind.Reload)]
    [InlineData("pandilleros", null, "Clint Tirofijo", ShotKind.Bullets)]
    [InlineData("mercenarios", null, "Crazy Mel", ShotKind.Bullets)]
    [InlineData("chatarreros", null, "Samantha O'Sullivan", ShotKind.Bullets)]
    public void Fixed_weapons_of_personalities_are_read(string factionId, string? subfactionId, string troopName, ShotKind kind)
    {
        var troop = Costs.FindTroop(troopName, factionId, subfactionId)!;
        var member = Roster.FromTroop(troop, factionId);

        Assert.Contains(_weapons.Of(member, factionId), w => w.Kind == kind);
    }

    [Theory]
    [InlineData("irradiados", null, "El Guardián de las Llaves")]
    [InlineData("chatarreros", "innovadores", "El Profesor")]
    public void Guns_without_bullets_do_not_take_ammo(string factionId, string? subfactionId, string troopName)
    {
        var troop = Costs.FindTroop(troopName, factionId, subfactionId)!;
        var member = Roster.FromTroop(troop, factionId);

        Assert.False(_weapons.UsesBullets(member, factionId));
    }

    [Fact]
    public void Preset_lists_keep_their_cost_with_the_bullets_of_the_official_sheet()
    {
        var withAmmo = 0;
        foreach (var list in Book.Lists)
        {
            foreach (var preset in list.Members)
            {
                var member = Roster.FromPreset(list, preset);
                Assert.Equal(preset.Ammo, member.Ammo);
                Assert.Equal(preset.Cost, Costs.Calculate(member, list.FactionId).Total);
                if (member.Ammo > 0)
                {
                    withAmmo++;
                    Assert.True(_weapons.UsesBullets(member, list.FactionId), $"{list.Id}: {preset.Troop}");
                }
            }
        }

        Assert.True(withAmmo > 10);
        var jefa = Book.Lists.Single(l => l.Id == "pandilleros-1").Members[0];
        Assert.Equal(2, jefa.Ammo);
    }

    [Fact]
    public void Armatoste_bought_later_lowers_agility_on_the_match_sheet()
    {
        var arponero = Roster.FromTroop(Costs.FindTroop("Arponero", "craabianos")!, "craabianos");
        arponero.Gear.Add(new Equipment { Name = "Cañón de mano" });

        var agility = _modifiers.For(arponero, "craabianos")["ag"];
        Assert.Equal(-1, agility.Delta);
        Assert.Equal(-1, agility.Pending);
        Assert.Contains("Armatoste", agility.Sources[0]);
    }

    [Fact]
    public void Armatoste_already_in_the_official_profile_is_marked_but_not_applied_twice()
    {
        var list = Book.Lists.Single(l => l.Id == "pandilleros-1");
        var bruto = Roster.FromPreset(list, list.Members.Single(m => m.Troop == "Bruto"));

        var agility = _modifiers.For(bruto, "pandilleros")["ag"];
        Assert.Equal(3, bruto.GetStat("ag"));
        Assert.Equal(-1, agility.Delta);
        Assert.Equal(0, agility.Pending);
    }

    [Fact]
    public void Only_one_armatoste_counts_and_bestia_parda_ignores_it()
    {
        var bruto = Member("pandilleros", "Bruto", "Filo pesado", "Pura chapa");
        Assert.Equal(-1, _modifiers.For(bruto, "pandilleros")["ag"].Delta);

        var forzudo = Roster.FromTroop(Costs.FindTroop("Forzudo", "troupe")!, "troupe");
        forzudo.Gear.Add(new Equipment { Name = "Filo pesado" });
        Assert.False(_modifiers.For(forzudo, "troupe").ContainsKey("ag"));
    }

    [Fact]
    public void Flat_mutations_change_the_sheet_and_weapon_strength_does_not()
    {
        var mutante = Roster.FromTroop(Costs.FindTroop("Mutante", "mutardos")!, "mutardos");
        mutante.Skills.Add("Cachas");
        mutante.Gear.Add(new Equipment { Name = "Filo medio" });

        var modifiers = _modifiers.For(mutante, "mutardos");
        Assert.Equal(2, modifiers["f"].Pending);
        Assert.Single(modifiers);
    }

    [Fact]
    public void Match_aids_count_shots_and_reloads()
    {
        var catalog = new MatchAidCatalog(Book, Costs, Lookup);
        var band = new Band
        {
            Name = "Prueba",
            FactionId = "craabianos",
            Members = [Member("craabianos", "Marinero", "Pistola de mecha", "Filo medio")],
        };
        var aid = Assert.Single(catalog.For(band, ["Marinero"]).Of(0), a => a.Kind == AidKind.Toggle);
        Assert.Equal("Cargada", aid.OffLabel);
        Assert.Contains("2 Acciones", aid.Note!(new AidState(1, 1, 0, 1)));
        Assert.DoesNotContain(catalog.For(band, ["Marinero"]).Of(0), a => a.Key == "disparos");

        var gang = new Band
        {
            Name = "Prueba",
            FactionId = "pandilleros",
            Members = [Member("pandilleros", "Jefe", "Pistola")],
        };
        gang.Members[0].Ammo = 2;
        var shots = Assert.Single(catalog.For(gang, ["Jefe"]).Of(0), a => a.Key == "disparos");
        Assert.Equal("Le quedan 1 de 2.", shots.Note!(new AidState(1, 1, 0, 1)));

        gang.Ownership = BandOwnership.Rival;
        var secret = Assert.Single(catalog.For(gang, ["Jefe"]).Of(0), a => a.Key == "disparos");
        Assert.Null(secret.Note!(new AidState(1, 1, 0, 1)));
    }

    private static Miniature Member(string factionId, string role, params string[] gear) => new()
    {
        Name = role,
        Role = role,
        BaseCost = 0,
        Stats = Roster.ParseStats(Costs.FindTroop(role, factionId)?.Stats ?? []),
        Gear = gear.Select(g => new Equipment { Name = g }).ToList(),
    };
}
