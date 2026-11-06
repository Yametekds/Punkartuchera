using Punkartuchera.Core.Matches;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

public class MatchAidTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private static readonly UnitCostCalculator Costs = new(Book);
    private readonly MatchAidCatalog _catalog = new(Book, Costs, new GearLookup(Book));

    [Fact]
    public void Every_troop_the_aids_mention_is_in_the_rulebook()
    {
        foreach (var (factionId, subfactionId, troop) in MatchAidCatalog.ReferencedTroops)
        {
            Assert.True(Costs.FindTroop(troop, factionId, subfactionId) is not null, $"{troop} ({subfactionId ?? factionId})");
        }
    }

    [Fact]
    public void Idos_count_ambrosia_and_despojos_cannot_take_it()
    {
        var band = Band("idos", null, ("Despojo", 15), ("Cosechador", 40), ("Aberración", 35), ("Glotón", 45), ("Desgarrador", 40), ("Despojo", 15), ("Desgarrador", 40), ("Madre", 90), ("Despojo", 15));
        var aids = For(band);

        var ambrosia = Assert.Single(aids.Band, a => a.Key == "ambrosia");
        Assert.Equal(3, ambrosia.Start); // 335 puntos: tres fracciones completas de 100.
        Assert.DoesNotContain(aids.Of(0), a => a.Key == "dosis");
        Assert.DoesNotContain(aids.Of(7), a => a.Key == "dosis"); // Madre nunca la toma.

        var dose = Assert.Single(aids.Of(2), a => a.Key == "dosis");
        Assert.Equal("ambrosia", dose.Pool);
        Assert.Contains("−3", dose.Note!(new AidState(1, 1, 0, 9))); // Aberración: Sobredosis −2 y una ya tomada.
        Assert.Contains("Madre", dose.Note!(new AidState(0, 1, 0, 9)));
        Assert.Contains(aids.Of(3), a => a.Key == "devorados");
    }

    [Fact]
    public void Devoradores_have_no_ambrosia()
    {
        var aids = For(Band("idos", "devoradores", ("Glotón", 45), ("Despojo", 15)));

        Assert.DoesNotContain(aids.Band, a => a.Key == "ambrosia");
        Assert.Contains(aids.Of(0), a => a.Key == "devorados");
    }

    [Fact]
    public void Unami_refill_mojo_every_round_and_descastados_do_not()
    {
        var mojo = Assert.Single(For(Band("unami", null, ("Chamán", 0))).Band, a => a.Key == "mojo");
        Assert.Equal(2, mojo.PerStandingEachRound);

        var outcasts = Assert.Single(For(Band("unami", "descastados", ("Descastado", 0), ("Renegado", 0))).Band, a => a.Key == "mojo");
        Assert.Equal(0, outcasts.PerStandingEachRound);
        Assert.Equal(1, outcasts.Start);
    }

    [Fact]
    public void Fallen_fraus_feed_their_markers()
    {
        var aids = For(Band("pandilleros", "fraus", ("Callisto", 0), ("Amacizona", 0), ("Frau", 0)));

        Assert.Equal(new DownFeed("hasta-el-cono", 3), aids.DownFeedOf(0));
        Assert.Equal(new DownFeed("hasta-el-cono", 2), aids.DownFeedOf(1));
        Assert.Equal(new DownFeed("hasta-el-cono", 1), aids.DownFeedOf(2));
    }

    [Fact]
    public void Fuel_weapons_get_their_charges()
    {
        var band = Band("sangre-negra", null, ("Quemado", 0));
        band.Members[0].Gear.Add(new Equipment { Name = "Lanzallamas" });

        var fuel = Assert.Single(For(band).Of(0), a => a.Key.StartsWith("cargas-"));
        Assert.Equal(10, fuel.Start);
    }

    [Fact]
    public void Jefe_de_pista_can_tick_each_show_effect_once()
    {
        var aids = For(Band("troupe", null, ("Jefe de pista", 0))).Of(0);

        Assert.True(aids.Count(a => a.Kind == AidKind.Once) >= 3);
        Assert.Contains(aids, a => a.Name == "Reclamar la atención");
    }

    [Fact]
    public void The_sea_always_claims_asks_which_attribute_and_takes_one_point()
    {
        var aids = For(Band("craabianos", null, ("Marinero", 30), ("Grumete", 18), ("Custodio del Abismo", 60), ("Ochodedos", 0)));

        var sea = Assert.Single(aids.Of(0), a => a.Key == "mar");
        Assert.Equal(AidKind.Pick, sea.Kind);
        Assert.DoesNotContain(sea.Choices, c => c.StatKey == "a"); // Acciones no se tira.
        Assert.DoesNotContain(aids.Of(1), a => a.Key == "mar"); // Grumetes no.
        Assert.DoesNotContain(aids.Of(3), a => a.Key == "mar"); // Los Mercenarios no son craabianos.
        Assert.Contains(aids.Band, a => a.Key == "ofrendas");

        var combat = sea.Choices.ToList().FindIndex(c => c.StatKey == "c") + 1;
        var counters = new Dictionary<string, int> { [MatchAid.MemberKey("mar", 0)] = combat };
        Assert.Equal(-1, aids.StatChanges(counters, 0)["c"]);
        Assert.Empty(aids.StatChanges(counters, 2));
        Assert.Contains("Combate", sea.Note!(new AidState(combat, 1, 0, 4)));

        var toughness = sea.Choices.ToList().FindIndex(c => c.StatKey == "d") + 1;
        Assert.Contains("Abatida", sea.Note!(new AidState(toughness, 1, 0, 4)));

        // Deshacer: vuelve a 0 y la ficha queda como estaba.
        counters[MatchAid.MemberKey("mar", 0)] = 0;
        Assert.Empty(aids.StatChanges(counters, 0));
    }

    [Fact]
    public void Resacados_do_not_have_the_sea_always_claims()
    {
        var aids = For(Band("craabianos", "resacados", ("Marinero", 30)));

        Assert.DoesNotContain(aids.Of(0), a => a.Key == "mar");
        Assert.Contains(aids.Of(0), a => a.Key == "bebida");
    }

    private BandAids For(Band band) => _catalog.For(band, band.Members.Select(m => m.Role).ToList());

    private static Band Band(string factionId, string? subfactionId, params (string Role, int Cost)[] members) => new()
    {
        Name = "Prueba",
        FactionId = factionId,
        SubfactionId = subfactionId,
        Members = members.Select(m => new Miniature { Name = m.Role, Role = m.Role, Cost = m.Cost }).ToList(),
    };
}
