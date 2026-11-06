using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

/// <summary>Las reglas especiales que vetan equipo se cumplen al elegirlo.</summary>
public class SpecialRuleRestrictionTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private readonly RosterBuilder _builder = new(Book, new RulebookCatalog(Book));
    private readonly LoadoutAdvisor _advisor = new(Book, new GearLookup(Book), new UnitCostCalculator(Book));

    private Miniature Recruit(string factionId, string troop) =>
        _builder.FromTroop(Book.FindFaction(factionId)!.Troops.First(t => t.Name == troop), factionId);

    private Miniature RecruitSub(string subfactionId, string troop, string factionId) =>
        _builder.FromTroop(Book.FindSubfaction(subfactionId)!.Troops.First(t => t.Name == troop), factionId);

    private static LoadoutOption Option(Loadout loadout, string name) =>
        loadout.Gear.Concat(loadout.Skills).First(o => o.Name == name);

    [Fact]
    public void Scum_cannot_embed_weapons()
    {
        var despojo = Recruit("idos", "Despojo");
        despojo.Gear.Add(new Equipment { Name = "Hoz" });

        var loadout = _advisor.Advise(despojo, "idos");

        Assert.Equal("Escoria: no puede llevarlo", Option(loadout, "Arma incrustada").Blocked);
    }

    [Fact]
    public void Scum_with_an_embedded_weapon_already_saved_gets_a_warning()
    {
        var despojo = Recruit("idos", "Despojo");
        despojo.Gear.Add(new Equipment { Name = "Hoz incrustada" });

        var loadout = _advisor.Advise(despojo, "idos");

        Assert.Contains("Escoria: no puede llevar Hoz incrustada.", loadout.Warnings);
    }

    [Fact]
    public void Embedded_weapon_needs_a_sickle_or_a_blade()
    {
        var desgarrador = Recruit("idos", "Desgarrador");
        Assert.Equal("Necesita llevar hoz, filo pequeño o filo medio", Option(_advisor.Advise(desgarrador, "idos"), "Arma incrustada").Blocked);

        desgarrador.Gear.Add(new Equipment { Name = "Filo medio" });
        Assert.Null(Option(_advisor.Advise(desgarrador, "idos"), "Arma incrustada").Blocked);
    }

    [Fact]
    public void Ambrosia_bans_are_data_for_the_match_screen()
    {
        var idos = Book.FindFaction("idos")!;
        Assert.Contains("Ambrosía", idos.Troops.First(t => t.Name == "Despojo").Rules.Single(r => r.Name == "Escoria").Bans);
        Assert.Contains("Ambrosía", idos.Troops.First(t => t.Name == "Madre").Rules.Single(r => r.Name == "Lucidez").Bans);
    }

    [Fact]
    public void Only_tenaceros_wear_chitin_armor_in_mariscadores()
    {
        var marinero = Recruit("craabianos", "Marinero");

        Assert.Equal("Movimientos libres: no puede llevarlo", Option(_advisor.Advise(marinero, "craabianos", "mariscadores"), "Armadura quitinosa").Blocked);
        Assert.Null(Option(_advisor.Advise(marinero, "craabianos"), "Armadura quitinosa").Blocked);
    }

    [Fact]
    public void Ninjas_buy_no_armor_no_firearms_and_no_crossbows_and_may_go_unarmed()
    {
        var jefe = Recruit("pandilleros", "Jefe");
        var loadout = _advisor.Advise(jefe, "pandilleros", "flipaos-de-los-ninjas");

        Assert.Equal("Colorinchis: no puede llevarlo", Option(loadout, "Pistola").Blocked);
        Assert.Equal("Colorinchis: no puede llevarlo", Option(loadout, "Ballesta").Blocked);
        Assert.Null(Option(loadout, "Maza").Blocked);
        Assert.DoesNotContain(loadout.Warnings, w => w.StartsWith("Le falta un arma", StringComparison.Ordinal));
    }

    [Fact]
    public void Fraus_cannot_buy_better_armor()
    {
        var bruto = Recruit("pandilleros", "Bruto");

        Assert.Equal("Ligeras de ropa: no puede llevarlo", Option(_advisor.Advise(bruto, "pandilleros", "fraus"), "Armadura metálica").Blocked);
    }

    [Fact]
    public void Clint_bans_firearms_for_everyone_else()
    {
        var clint = Recruit("pandilleros", "Clint Tirofijo");
        var bruto = Recruit("pandilleros", "Bruto");

        var loadout = _advisor.Advise(bruto, "pandilleros", null, [clint]);

        Assert.Equal("Imperdonable (Clint Tirofijo): nadie más de la banda puede llevarlo", Option(loadout, "Escopeta").Blocked);
        Assert.Null(Option(loadout, "Maza").Blocked);
        Assert.Null(Option(_advisor.Advise(bruto, "pandilleros"), "Escopeta").Blocked);
    }

    [Fact]
    public void Claws_do_not_mix_with_weapons()
    {
        var mutante = Recruit("mutardos", "Mutante");
        mutante.Skills.Add("Garras");
        var loadout = _advisor.Advise(mutante, "mutardos");
        Assert.Equal("No se combina con Garras", Option(loadout, "Maza").Blocked);
        Assert.DoesNotContain(loadout.Warnings, w => w.StartsWith("Le falta un arma", StringComparison.Ordinal));

        var armed = Recruit("mutardos", "Mutante");
        armed.Gear.Add(new Equipment { Name = "Maza" });
        Assert.Equal("No se combina con Maza", Option(_advisor.Advise(armed, "mutardos"), "Garras").Blocked);
    }

    [Fact]
    public void Only_one_kind_of_special_ammo()
    {
        var discipulo = Recruit("irradiados", "Discípulo");
        discipulo.Gear.Add(new Equipment { Name = "Munición irradiada" });

        var loadout = _advisor.Advise(discipulo, "irradiados");

        Assert.Equal("No se combina con Munición irradiada", Option(loadout, "Munición contaminada").Blocked);
    }

    [Fact]
    public void Dwarfism_and_gigantism_exclude_each_other()
    {
        var bicho = RecruitSub("freak-show", "Bicho raro", "troupe");
        bicho.Skills.Add("Enanismo");

        Assert.Equal("No se combina con Enanismo", Option(_advisor.Advise(bicho, "troupe", "freak-show"), "Gigantismo").Blocked);
    }

    [Fact]
    public void Only_one_bulky_item()
    {
        // Ficha hecha a mano: se le ofrece todo lo de su facción.
        var member = new Miniature { Name = "Chatarrero", Role = "A mano" };
        member.Gear.Add(new Equipment { Name = "Traje voltaico" });

        var loadout = _advisor.Advise(member, "chatarreros");

        Assert.Equal("Ya lleva algo con Armatoste (Traje voltaico)", Option(loadout, "Lanzaarpones").Blocked);
    }

    [Fact]
    public void Every_ban_names_something_that_exists()
    {
        var names = Book.Weapons.Select(w => w.Name)
            .Concat(Book.Armors.Select(a => a.Name))
            .Concat(Book.Gear.Select(g => g.Name))
            .Concat(Book.SpecialRules.Select(r => RulesText.StripParameter(r.Name)))
            .Concat(Book.Factions.SelectMany(f => f.Subfactions.Prepend(f)).SelectMany(f => f.Extras).SelectMany(g => g.Items).Select(i => i.Name))
            .Concat(["Armas", "Blindaje", "Ambrosía"])
            .Select(RulesText.Fold)
            .ToHashSet();
        var owners = Book.Factions.SelectMany(f => f.Subfactions.Prepend(f)).ToList();
        var bans = owners.SelectMany(f => f.Rules.Concat(f.Troops.SelectMany(t => t.Rules)).Concat(f.Extras.SelectMany(g => g.Items)))
            .SelectMany(r => r.Bans.Select(b => (r.Name, Ban: b)))
            .Concat(owners.SelectMany(f => f.Gear).Concat(Book.Gear).SelectMany(g => g.Bans.Concat(g.Requires).Select(b => (g.Name, Ban: b))))
            .ToList();

        Assert.NotEmpty(bans);
        Assert.All(bans, b => Assert.True(names.Contains(RulesText.Fold(b.Ban)), $"{b.Name}: {b.Ban}"));
    }
}
