using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

public class SubfactionRecruitTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private readonly RosterBuilder _builder = new(Book, new RulebookCatalog(Book));

    public static TheoryData<string, string> AllSubfactions()
    {
        var data = new TheoryData<string, string>();
        foreach (var faction in Book.Factions)
        {
            foreach (var subfaction in faction.Subfactions)
            {
                data.Add(faction.Id, subfaction.Id);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllSubfactions))]
    public void Subfaction_data_points_at_real_troops(string factionId, string subfactionId)
    {
        var faction = Book.FindFaction(factionId)!;
        var subfaction = Book.FindSubfaction(subfactionId)!;
        var baseNames = faction.Troops.Where(t => !t.Personality).Select(t => t.Name).ToList();

        Assert.NotEmpty(subfaction.BaseTroops);
        Assert.All(subfaction.BaseTroops, name => Assert.Contains(name, baseNames));
        Assert.All(subfaction.TroopNames.Keys, name => Assert.Contains(name, subfaction.BaseTroops));
        Assert.All(subfaction.Troops.Where(t => t.Replaces is not null), t => Assert.Contains(t.Replaces, baseNames));
    }

    [Theory]
    [MemberData(nameof(AllSubfactions))]
    public void Subfaction_offers_its_own_members_first_and_only_the_allowed_base_ones(string factionId, string subfactionId)
    {
        var faction = Book.FindFaction(factionId)!;
        var subfaction = Book.FindSubfaction(subfactionId)!;
        var choices = _builder.ChoicesFor(factionId, subfactionId);
        var fromFaction = choices.Where(c => c.Owner == faction).ToList();

        Assert.All(subfaction.Troops, t => Assert.Contains(choices, c => c.Owner == subfaction && c.Troop == t));
        Assert.All(fromFaction, c => Assert.False(c.Troop.Personality, c.Troop.Name));
        Assert.All(fromFaction, c => Assert.Contains(c.Troop.Name, subfaction.BaseTroops));
        Assert.All(subfaction.Troops.Where(t => t.Replaces is not null), t => Assert.DoesNotContain(fromFaction, c => c.Troop.Name == t.Replaces));
        if (subfaction.Troops.Any(t => !t.Personality))
        {
            Assert.Equal(subfaction, choices[0].Owner);
        }
    }

    [Fact]
    public void Slashers_do_not_get_the_excluded_idos_or_their_personalities()
    {
        var names = _builder.ChoicesFor("idos", "slashers").Select(c => c.Troop.Name).ToList();

        Assert.Contains("Psycho-killer", names);
        Assert.Contains("Cosechador", names);
        Assert.DoesNotContain("Desgarrador", names);
        Assert.DoesNotContain("Aberración", names);
        Assert.DoesNotContain("Tío", names);
        Assert.Contains("Crazy Mel", names);
    }

    [Fact]
    public void Renamed_base_troops_keep_their_rulebook_role()
    {
        var ninja = _builder.ChoicesFor("pandilleros", "flipaos-de-los-ninjas").Single(c => c.Troop.Name == "Piltrafa");

        Assert.Equal("Ninja negro", ninja.Name);
        Assert.Equal("De Pandilleros", ninja.Group);
        Assert.Equal("Piltrafa", _builder.FromTroop(ninja.Troop, "pandilleros").Role);
    }

    [Fact]
    public void Subfaction_versions_replace_the_base_troop_and_its_cost()
    {
        var choices = _builder.ChoicesFor("chatarreros", "carroneros");
        var sonda = Assert.Single(choices, c => c.Troop.Name == "Sonda");

        Assert.Equal("carroneros", sonda.Owner.Id);
        Assert.Equal(25, sonda.Troop.Cost);
        Assert.DoesNotContain("Infiltración", sonda.Troop.SpecialRules);
        Assert.Single(_builder.ChoicesFor("troupe", "cirque-du-no-se"), c => c.Troop.Name == "Acróbata" && c.Troop.Cost == 35);
        Assert.DoesNotContain(_builder.ChoicesFor("sangre-negra", "rugientes"), c => c.Troop.Name == "Chispa");
    }

    [Fact]
    public void Rugientes_take_no_mercenaries()
    {
        Assert.DoesNotContain(_builder.ChoicesFor("sangre-negra", "rugientes"), c => c.Owner.IsMercenaries);
        Assert.Contains(_builder.ChoicesFor("sangre-negra", "nuevos-hijos"), c => c.Owner.IsMercenaries);
    }
}
