using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

public class CloseCombatTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private readonly CloseCombat _combat = new(Book, new GearLookup(Book));

    private static Miniature With(params string[] gear) =>
        new() { Name = "Prueba", Gear = gear.Select(g => new Equipment { Name = g }).ToList() };

    [Theory]
    [InlineData("Arco")]
    [InlineData("Escopeta")]
    [InlineData("Rifle")]
    [InlineData("Ballesta")]
    public void Two_handed_ranged_weapons_fight_as_a_free_light_mace(string weapon)
    {
        var fallback = Assert.Single(_combat.For(With(weapon), "pandilleros"));

        Assert.Equal(MeleeStandIn.TwoHanded, fallback.Reason);
        Assert.Equal(weapon, fallback.From);
        Assert.Equal("Maza ligera", fallback.Profile.Name);
        Assert.Equal("+1", fallback.Profile.Strength);
    }

    [Fact]
    public void The_light_mace_is_shown_even_with_a_melee_weapon()
    {
        var fallback = Assert.Single(_combat.For(With("Escopeta", "Filo pequeño"), "pandilleros"));

        Assert.Equal(MeleeStandIn.TwoHanded, fallback.Reason);
    }

    [Fact]
    public void One_handed_ranged_weapon_without_melee_weapon_is_improvised()
    {
        var fallback = Assert.Single(_combat.For(With("Pistola", "Cuero endurecido"), "pandilleros"));

        Assert.Equal(MeleeStandIn.Improvised, fallback.Reason);
        Assert.Equal("Pistola", fallback.From);
        Assert.Equal("-1", fallback.Profile.Combat);
    }

    [Fact]
    public void One_handed_ranged_weapon_with_melee_weapon_needs_nothing()
    {
        Assert.Empty(_combat.For(With("Pistola", "Maza"), "pandilleros"));
    }

    [Fact]
    public void Two_handed_weapon_beats_the_improvised_one()
    {
        var fallback = Assert.Single(_combat.For(With("Pistola", "Rifle"), "pandilleros"));

        Assert.Equal(MeleeStandIn.TwoHanded, fallback.Reason);
        Assert.Equal("Rifle", fallback.From);
    }

    [Fact]
    public void Mamotreto_fights_as_a_heavy_mace()
    {
        var fallback = Assert.Single(_combat.For(With("Lanzaarpones"), "chatarreros"));

        Assert.Equal(MeleeStandIn.Mamotreto, fallback.Reason);
        Assert.Equal("Maza pesada", fallback.Profile.Name);
    }

    [Fact]
    public void Bombs_alone_leave_it_unarmed()
    {
        var fallback = Assert.Single(_combat.For(With("Cóctel molotov"), "sangre-negra"));

        Assert.Equal(MeleeStandIn.Unarmed, fallback.Reason);
        Assert.Equal("-2", fallback.Profile.Combat);
    }

    [Fact]
    public void Melee_only_members_have_nothing_extra()
    {
        Assert.Empty(_combat.For(With("Filo medio"), "pandilleros"));
    }
}
