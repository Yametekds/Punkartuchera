using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

public class BandCompositionTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private readonly RosterBuilder _builder = new(Book, new RulebookCatalog(Book));
    private readonly BandComposition _composition = new(Book);

    private static Band BandOf(string factionId, string? subfactionId, params (string Role, int Cost)[] members) => new()
    {
        Name = "Prueba",
        FactionId = factionId,
        SubfactionId = subfactionId,
        Members = [.. members.Select(m => new Miniature { Name = m.Role, Role = m.Role, Cost = m.Cost })],
    };

    [Fact]
    public void Every_playable_faction_and_subfaction_has_limits_with_real_troops()
    {
        var names = Book.Factions.SelectMany(f => f.Troops.Concat(f.Subfactions.SelectMany(s => s.Troops))).Select(t => t.Name).ToHashSet();
        foreach (var faction in Book.Factions.Where(f => !f.IsMercenaries))
        {
            Assert.NotEmpty(faction.Limits);
            Assert.All(faction.Subfactions, s => Assert.NotEmpty(s.Limits));
            foreach (var limit in faction.Limits.Concat(faction.Subfactions.SelectMany(s => s.Limits)))
            {
                Assert.False(string.IsNullOrWhiteSpace(limit.Text));
                Assert.All(limit.Troops.Concat(limit.Than), t => Assert.Contains(t, names));
            }
        }
    }

    [Fact]
    public void Official_starter_lists_follow_their_restrictions()
    {
        foreach (var list in Book.Lists)
        {
            var band = new Band { Name = list.Name, FactionId = list.FactionId, Members = [.. _builder.FromList(list)] };
            Assert.True(_composition.Check(band).Count == 0, $"{list.Name}: {string.Join(" / ", _composition.Check(band).Select(i => i.Rule + " " + i.Detail))}");
        }
    }

    [Fact]
    public void Two_bosses_or_a_boss_and_lord_homoeroticus_are_too_many()
    {
        Assert.Contains(_composition.Check(BandOf("pandilleros", null, ("Jefe", 60), ("Jefe", 60), ("Curtido", 40))), i => i.Rule.Contains("Jefe"));
        Assert.Contains(_composition.Check(BandOf("pandilleros", null, ("Jefe", 60), ("Lord Homoeroticus", 100), ("Curtido", 40))), i => i.Rule.Contains("Jefe"));
    }

    [Fact]
    public void Piltrafas_cannot_outnumber_the_rest()
    {
        Assert.Empty(_composition.Check(BandOf("pandilleros", null, ("Piltrafa", 20), ("Curtido", 40))));
        var issue = Assert.Single(_composition.Check(BandOf("pandilleros", null, ("Piltrafa", 20), ("Piltrafa", 20), ("Curtido", 40))));
        Assert.Contains("Llevas 2", issue.Detail);
    }

    [Fact]
    public void Harvesters_count_whole_fractions_of_200_points_and_madre_counts_as_one()
    {
        Assert.Empty(_composition.Check(BandOf("idos", null, ("Cosechador", 40), ("Glotón", 160))));
        Assert.NotEmpty(_composition.Check(BandOf("idos", null, ("Cosechador", 40), ("Madre", 90), ("Glotón", 100))));
        Assert.Empty(_composition.Check(BandOf("idos", null, ("Cosechador", 40), ("Madre", 90), ("Glotón", 300))));
    }

    [Fact]
    public void One_personality_or_mercenary_per_band()
    {
        var issues = _composition.Check(BandOf("pandilleros", null, ("Lord Homoeroticus", 100), ("Crazy Mel", 80), ("Curtido", 40)));
        Assert.Contains(issues, i => i.Rule.Contains("Personalidad"));
    }

    [Fact]
    public void Troupe_needs_at_least_as_many_pachachos_as_of_each_other_troop()
    {
        Assert.Empty(_composition.Check(BandOf("troupe", null, ("Pachacho", 20), ("Forzudo", 40), ("Acróbata", 40))));
        Assert.NotEmpty(_composition.Check(BandOf("troupe", null, ("Pachacho", 20), ("Forzudo", 40), ("Forzudo", 40))));
    }

    [Fact]
    public void Ninja_colours_go_from_more_black_to_fewer_white()
    {
        Assert.Empty(_composition.Check(BandOf("pandilleros", "flipaos-de-los-ninjas", ("Piltrafa", 20), ("Piltrafa", 20), ("Machaca", 30))));
        var issue = Assert.Single(_composition.Check(BandOf("pandilleros", "flipaos-de-los-ninjas", ("Piltrafa", 20), ("Machaca", 30))));
        Assert.Contains("Ninja negro", issue.Detail);
    }

    [Fact]
    public void Comic_band_takes_no_mercenaries_and_no_repeats_but_caronte_can_join()
    {
        var choices = _builder.ChoicesFor("banda-del-comic");
        Assert.DoesNotContain(choices, c => c.Owner.IsMercenaries);
        Assert.Contains(choices, c => c.Troop.Name == "Caronte");

        var issues = _composition.Check(BandOf("banda-del-comic", null, ("Dan", 50), ("Dan novato", 30), ("Rita", 50), ("Rita", 50), ("Crazy Mel", 80)));
        Assert.Equal(3, issues.Count);
    }
}
