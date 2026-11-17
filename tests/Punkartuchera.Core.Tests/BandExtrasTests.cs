using Punkartuchera.Core.Matches;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

public class BandExtrasTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private readonly BandExtras _extras = new(Book);

    public static TheoryData<string, string?> AllBands()
    {
        var data = new TheoryData<string, string?>();
        foreach (var faction in Book.Factions.Where(f => !f.IsMercenaries))
        {
            data.Add(faction.Id, null);
            foreach (var subfaction in faction.Subfactions)
            {
                data.Add(faction.Id, subfaction.Id);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllBands))]
    public void Every_band_can_take_the_cat(string factionId, string? subfactionId)
    {
        var cat = Assert.Single(_extras.For(factionId, subfactionId), g => g.Name == BandExtras.Cat);
        Assert.Equal(10, cat.Cost);
    }

    [Fact]
    public void Mercenaries_are_not_a_band()
    {
        Assert.Empty(_extras.For("mercenarios"));
    }

    [Fact]
    public void Only_one_cat_and_it_counts_in_the_band_cost()
    {
        var band = new Band { FactionId = "pandilleros", Members = [new Miniature { Name = "Morro", Cost = 30 }] };
        var cat = _extras.Find(BandExtras.Cat, band.FactionId)!;

        Assert.True(BandExtras.Add(band, cat));
        Assert.False(BandExtras.Add(band, cat));
        Assert.Equal("Solo uno por banda", BandExtras.Blocked(band, cat));
        Assert.Equal(40, band.Cost);

        Assert.True(BandExtras.Remove(band, BandExtras.Cat));
        Assert.Equal(30, band.Cost);
        Assert.Null(BandExtras.Blocked(band, cat));
    }

    [Fact]
    public void A_band_with_only_the_cat_still_has_a_cost()
    {
        var band = new Band { FactionId = "pandilleros" };
        BandExtras.Add(band, _extras.Find(BandExtras.Cat, band.FactionId)!);

        Assert.Equal(10, band.Cost);
    }

    [Fact]
    public void The_cat_is_not_offered_to_a_member()
    {
        var advisor = new LoadoutAdvisor(Book, new GearLookup(Book), new UnitCostCalculator(Book));
        var builder = new RosterBuilder(Book, new RulebookCatalog(Book));
        var piltrafa = builder.FromTroop(Book.FindFaction("pandilleros")!.Troops.First(t => t.Name == "Piltrafa"), "pandilleros");

        Assert.DoesNotContain(advisor.Advise(piltrafa, "pandilleros").Gear, o => o.Name == BandExtras.Cat);
        Assert.DoesNotContain(advisor.Advise(new Miniature { Name = "x", Role = "A mano" }, "pandilleros").Gear, o => o.Name == BandExtras.Cat);
    }

    [Fact]
    public void The_match_has_the_cat_reroll_once()
    {
        var costs = new UnitCostCalculator(Book);
        var catalog = new MatchAidCatalog(Book, costs, new GearLookup(Book));
        var band = new Band { FactionId = "pandilleros", Members = [new Miniature { Name = "Morro", Role = "Piltrafa" }] };

        Assert.Null(catalog.For(band, ["Piltrafa"]).FindBand("gato"));

        band.Extras.Add(new BandExtra { Name = BandExtras.Cat, Cost = 10 });
        var aid = catalog.For(band, ["Piltrafa"]).FindBand("gato");

        Assert.NotNull(aid);
        Assert.Equal(AidKind.Once, aid.Kind);
    }
}
