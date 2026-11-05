using System.Text;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Sharing;
using Punkartuchera.Core.Storage;

namespace Punkartuchera.Core.Tests;

public class UnitCostTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private readonly RosterBuilder _builder = new(Book, new RulebookCatalog(Book));
    private readonly UnitCostCalculator _costs = new(Book);

    private Troop Troop(string factionId, string name) =>
        Book.FindFaction(factionId)!.Troops.First(t => t.Name == name);

    [Fact]
    public void Bullets_cost_what_the_rulebook_says()
    {
        Assert.Equal(15, _costs.BulletPrice);
    }

    [Fact]
    public void A_recruit_costs_its_base_plus_purchases_and_bullets()
    {
        var bruto = _builder.FromTroop(Troop("pandilleros", "Bruto"), "pandilleros");
        Assert.Equal(40, _costs.Calculate(bruto, "pandilleros").Total);

        bruto.Gear.Add(new Equipment { Name = "Escopeta" });
        bruto.Gear.Add(new Equipment { Name = "Escudo" });
        bruto.Gear.Add(new Equipment { Name = "Protección contra la contaminación" });
        bruto.Ammo = 3;

        var cost = _costs.Calculate(bruto, "pandilleros");
        Assert.Equal(40 + 6 + 6 + 5 + 3 * 15, cost.Total);
        Assert.Contains(cost.Lines, l => l.Kind == CostLineKind.Ammo && l.Points == 45 && l.Quantity == 3);
    }

    [Fact]
    public void Adding_bullets_raises_the_saved_cost()
    {
        var bruto = _builder.FromTroop(Troop("pandilleros", "Bruto"), "pandilleros");
        bruto.Ammo = 2;

        _costs.Apply(bruto, "pandilleros");

        Assert.Equal(70, bruto.Cost);
        Assert.Equal(40, bruto.BaseCost);
    }

    [Fact]
    public void Fixed_gear_and_troop_rules_do_not_count_twice()
    {
        var mel = _builder.FromTroop(Book.FindFaction("mercenarios")!.Troops.First(t => t.Name == "Crazy Mel"), "pandilleros");
        var cost = _costs.Calculate(mel, "pandilleros");

        Assert.Equal(mel.BaseCost, cost.Total);
        Assert.All(cost.Lines.Skip(1), l => Assert.Equal(CostLineKind.Included, l.Kind));
    }

    [Fact]
    public void Preset_lists_keep_the_cost_of_their_control_sheet()
    {
        foreach (var list in Book.Lists)
        {
            var total = _builder.FromList(list).Sum(m => _costs.Calculate(m, list.FactionId).Total);
            Assert.Equal(list.Cost, total);
        }
    }

    [Fact]
    public void Older_members_keep_their_cost_and_add_bullets()
    {
        var old = new Miniature { Name = "Morro", Cost = 50, Ammo = 1, Gear = [new Equipment { Name = "Escopeta" }], Skills = ["Duro"] };

        _costs.Apply(old, "pandilleros");

        Assert.Equal(65, old.Cost);
        Assert.Equal(50, old.BaseCost);
        Assert.Equal(["Escopeta", "Duro"], old.Included);
    }

    [Fact]
    public void Gear_without_a_price_is_flagged_and_counts_nothing()
    {
        var member = new Miniature { Name = "X", BaseCost = 20, Gear = [new Equipment { Name = "Cacharro inventado" }] };
        var cost = _costs.Calculate(member, "pandilleros");

        Assert.Equal(20, cost.Total);
        Assert.Single(cost.Unpriced);
    }

    [Fact]
    public void Quantities_multiply_the_price()
    {
        var member = new Miniature { Name = "X", Role = "Bruto", BaseCost = 40, Gear = [new Equipment { Name = "2 Escopetas" }] };
        Assert.Equal(52, _costs.Calculate(member, "pandilleros").Total);
    }

    [Fact]
    public void Options_list_what_the_troop_can_buy()
    {
        var options = _costs.OptionsFor("Bruto", "pandilleros");
        Assert.Contains(options, o => o.Name == "Escopeta" && o.Cost == 6 && !o.IsSkill);
        Assert.Contains(options, o => o.Name == "Protección contra la contaminación" && o.Cost == 5);
        Assert.Empty(_costs.OptionsFor("Crazy Mel", "pandilleros"));
    }

    [Fact]
    public void Faction_ammo_can_be_bought_per_bullet()
    {
        Assert.Contains(_costs.OptionsFor("Científico", "irradiados", "rebelion-isotopica"), o => o.Name == "Munición atómica" && o.Cost == 15);
        var member = new Miniature { Name = "X", Role = "Científico", BaseCost = 15, Gear = [new Equipment { Name = "3 Munición atómica" }] };
        Assert.Equal(60, _costs.Calculate(member, "irradiados", "rebelion-isotopica").Total);
    }

    [Fact]
    public async Task Export_sends_only_the_band_total()
    {
        var bands = new InMemoryBandRepository();
        var photos = new InMemoryPhotoStore();
        var exchange = new BandExchangeService(bands, photos, new RulebookCatalog(Book));
        var bruto = _builder.FromTroop(Troop("pandilleros", "Bruto"), "pandilleros");
        bruto.Ammo = 4;
        _costs.Apply(bruto, "pandilleros");
        var band = new Band { Name = "Los Oxidados", FactionId = "pandilleros", Members = [bruto] };
        await bands.SaveAsync(band);

        var json = Encoding.UTF8.GetString((await exchange.ExportAsync(band.Id)).Content);
        var rival = await new BandExchangeService(new InMemoryBandRepository(), new InMemoryPhotoStore(), new RulebookCatalog(Book))
            .ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        // Solo viaja el total de la banda: con el coste de cada miniatura se deducirían sus balas.
        Assert.Null(rival.Members[0].Cost);
        Assert.Null(rival.Members[0].BaseCost);
        Assert.Empty(rival.Members[0].Included);
        Assert.Equal(100, rival.TotalCost);
        Assert.Equal(100, rival.Cost);
        Assert.Equal(0, rival.Members[0].Ammo);
    }
}
