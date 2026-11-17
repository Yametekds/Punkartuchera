using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

public class MemberArmorTests
{
    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private static readonly GearLookup Lookup = new(Book);
    private static readonly UnitCostCalculator Costs = new(Book);
    private static readonly RosterBuilder Roster = new(Book, new RulebookCatalog(Book).Stats);
    private readonly MemberArmor _armor = new(Book, Lookup, Costs);

    [Theory]
    [InlineData("pandilleros", null, "Piltrafa", 1)]
    [InlineData("pandilleros", null, "Bruto", 3)]
    [InlineData("v-reich", null, "Subhumano", 5)]
    [InlineData("mutardos", null, "Magmador", 5)]
    [InlineData("mutardos", "macumba", "Caimanomorfo", 3)]
    [InlineData("chatarreros", null, "Caronte", 3)]
    [InlineData("irradiados", "herederos-mecanicos", "Robotrón", 3)]
    [InlineData("irradiados", "inquisicion-atomica", "La Gran Inquisidora", 2)]
    [InlineData("mercenarios", null, "El Viejo Sajarratas", 3)]
    [InlineData("craabianos", null, "Bloody Mary", 5)]
    [InlineData("idos", "hermandad-de-la-bestia", "Desgarrador", 0)]
    public void Troops_show_the_armor_they_wear(string factionId, string? subfactionId, string troopName, int expected)
    {
        var troop = Costs.FindTroop(troopName, factionId, subfactionId)!;
        var member = Roster.FromTroop(troop, factionId);

        Assert.Equal(expected, _armor.For(member, factionId, subfactionId).Value);
    }

    [Fact]
    public void Changing_the_armor_changes_the_value()
    {
        var member = Member("pandilleros", "Bruto", "Filo pesado", "Pura chapa");
        var rating = _armor.For(member, "pandilleros");

        Assert.Equal(5, rating.Value);
        Assert.Equal(["Pura chapa"], rating.Sources);

        member.Gear.RemoveAll(g => g.Name == "Pura chapa");
        Assert.Equal(0, _armor.For(member, "pandilleros").Value);
    }

    [Fact]
    public void Only_the_best_own_armor_counts()
    {
        var member = Member("pandilleros", "Bruto", "Ropas resistentes", "Armadura metálica");

        Assert.Equal(4, _armor.For(member, "pandilleros").Value);
    }

    [Fact]
    public void Situational_armor_is_listed_apart()
    {
        var shield = _armor.For(Member("pandilleros", "Jefe", "Cuero endurecido", "Escudo"), "pandilleros");
        Assert.Equal(3, shield.Value);
        Assert.Contains(shield.Situational, s => s.StartsWith("Escudo:", StringComparison.Ordinal));

        var vest = _armor.For(Member("banda-del-comic", "Dan", "Chaleco antibalas"), "banda-del-comic");
        Assert.Equal(3, vest.Value);
        Assert.Contains(vest.Situational, s => s.Contains("5/6/7 contra Arma de fuego", StringComparison.Ordinal));
    }

    [Fact]
    public void Flat_and_any_armor_bonuses_add_up()
    {
        var troupe = Book.FindFaction("troupe")!.Subfactions.First(s => s.Extras.SelectMany(g => g.Items).Any(i => i.Name == "Piel escamosa"));
        var troop = troupe.Troops.FirstOrDefault() ?? Book.FindFaction("troupe")!.Troops[0];
        var member = Member("troupe", troop.Name, "Ropas resistentes", "Piel escamosa");
        Assert.Equal(3, _armor.For(member, "troupe", troupe.Id).Value);

        var abisal = Book.FindFaction("craabianos")!.Subfactions.First(s => s.Id == "abisales");
        var scaly = abisal.Troops.First(t => t.Rules.Any(r => r.Name == "Piel escamosa"));
        Assert.Equal(2, _armor.For(Member("craabianos", scaly.Name, "Ropas resistentes"), "craabianos", "abisales").Value);
        Assert.Equal(0, _armor.For(Member("craabianos", scaly.Name), "craabianos", "abisales").Value);
    }

    private static Miniature Member(string factionId, string role, params string[] gear) => new()
    {
        Role = role,
        Gear = gear.Select(g => new Equipment { Name = g }).ToList(),
    };
}
