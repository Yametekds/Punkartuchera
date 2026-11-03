using System.Text.RegularExpressions;
using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Rules;

public enum CostLineKind
{
    /// <summary>Coste base de la tropa o el que se puso a mano.</summary>
    Base,

    /// <summary>Ya va incluido en el coste base (equipo fijo, reglas de la tropa).</summary>
    Included,

    /// <summary>Comprado aparte con su precio del reglamento.</summary>
    Purchase,

    /// <summary>No tiene precio en el reglamento: no suma.</summary>
    Unpriced,

    Ammo,
}

/// <summary>Una compra posible: armas, blindaje y equipo van al equipo; mutaciones, poderes y demás, a las habilidades.</summary>
public sealed record PurchaseOption(string Group, string Name, int Cost, bool IsSkill);

public sealed record CostLine(string Label, CostLineKind Kind, int Points = 0, int Quantity = 1);

/// <summary>Desglose del coste de una miniatura.</summary>
public sealed record UnitCost(IReadOnlyList<CostLine> Lines, int BulletPrice)
{
    public int Total => Lines.Sum(l => l.Points);

    public IEnumerable<CostLine> Unpriced => Lines.Where(l => l.Kind == CostLineKind.Unpriced);
}

/// <summary>
/// Calcula lo que cuesta una miniatura según el reglamento: coste base de la tropa,
/// más las armas, blindajes, equipo y extras comprados aparte, más las balas.
/// Lo que la tropa ya trae (equipo fijo, sus reglas especiales) no suma otra vez.
/// </summary>
public sealed partial class UnitCostCalculator(Rulebook book)
{
    /// <summary>Precio de una bala si el reglamento no lo diera (Equipo, pág. 3).</summary>
    public const int DefaultBulletPrice = 15;

    public int BulletPrice =>
        book.Gear.FirstOrDefault(g => g.FactionId is null && RulesText.Fold(g.Name) == "balas")?.Cost ?? DefaultBulletPrice;

    public UnitCost Calculate(Miniature member, string? factionId, string? subfactionId = null)
    {
        var troop = FindTroop(member.Role, factionId, subfactionId);
        var lines = new List<CostLine>
        {
            new(BaseLabel(member, troop), CostLineKind.Base, member.BaseCost ?? member.Cost ?? 0),
        };

        // Si no hay coste base guardado (miembros de antes), lo que ya llevaba se da por pagado.
        var included = member.BaseCost is null
            ? member.Gear.Select(g => g.Name).Concat(member.Skills).Select(Key).ToList()
            : member.Included.Select(Key).ToList();

        foreach (var name in member.Gear.Select(g => g.Name).Concat(member.Skills))
        {
            lines.Add(Line(name, included, troop, factionId, subfactionId));
        }

        var bullet = BulletPrice;
        if (member.Ammo > 0)
        {
            lines.Add(new CostLine("Balas", CostLineKind.Ammo, member.Ammo * bullet, member.Ammo));
        }

        return new UnitCost(lines, bullet);
    }

    /// <summary>Guarda en la miniatura el total y el precio de bala con el que se ha calculado.</summary>
    public UnitCost Apply(Miniature member, string? factionId, string? subfactionId = null)
    {
        var cost = Calculate(member, factionId, subfactionId);
        if (member.BaseCost is null)
        {
            member.BaseCost = member.Cost ?? 0;
            member.Included = [.. member.Gear.Select(g => g.Name), .. member.Skills];
        }

        member.Cost = cost.Total;
        member.BulletPrice = cost.BulletPrice;
        return cost;
    }

    /// <summary>Precio de una compra por su nombre, o nulo si el reglamento no le pone precio.</summary>
    public int? PriceOf(string name, string? factionId, string? subfactionId = null, string? troopName = null) =>
        Price(Parse(name).Name, FindTroop(troopName, factionId, subfactionId), factionId, subfactionId);

    /// <summary>
    /// Lo que puede comprar esta tropa según el reglamento: sus opciones de armas, blindaje y equipo,
    /// el equipo genérico de la facción y las listas propias con precio (mutaciones, poderes…).
    /// </summary>
    public IReadOnlyList<PurchaseOption> OptionsFor(string? troopName, string? factionId, string? subfactionId = null)
    {
        var options = new List<PurchaseOption>();
        var troop = FindTroop(troopName, factionId, subfactionId);
        if (troop is not null)
        {
            options.AddRange(Priced(troop.WeaponOptions, "Armas", false));
            options.AddRange(Priced(troop.ArmorOptions, "Blindaje", false));
            options.AddRange(Priced(troop.EquipmentOptions, "Equipo", false));
        }

        // Personalidades y mercenarios llevan el equipo cerrado.
        if (troop is not null && (troop.Personality || book.Factions.Any(f => f.IsMercenaries && f.Troops.Contains(troop))))
        {
            return options;
        }

        var faction = book.FindFaction(factionId);
        var subfaction = faction?.Subfactions.FirstOrDefault(s => s.Id == subfactionId);
        foreach (var owner in new[] { faction, subfaction }.OfType<FactionRules>())
        {
            options.AddRange(Priced(owner.GenericGear, "Equipo", false));
            options.AddRange(owner.Gear.Where(g => g.Cost is not null)
                .Select(g => new PurchaseOption("Equipo", g.Name, g.Cost!.Value, false)));
            foreach (var group in owner.Extras)
            {
                options.AddRange(group.Items.Where(i => i.Cost is not null)
                    .Select(i => new PurchaseOption(group.Title, i.Name, i.Cost!.Value, true)));
            }
        }

        return options.DistinctBy(o => (o.Group, Key(o.Name))).ToList();

        static IEnumerable<PurchaseOption> Priced(IEnumerable<PriceOption> source, string group, bool isSkill) =>
            source.Where(o => o.Cost is not null).Select(o => new PurchaseOption(group, o.Name, o.Cost!.Value, isSkill));
    }

    private CostLine Line(string name, List<string> included, Troop? troop, string? factionId, string? subfactionId)
    {
        var key = Key(name);
        var index = included.IndexOf(key);
        if (index >= 0)
        {
            included.RemoveAt(index);
            return new CostLine(name, CostLineKind.Included);
        }

        var (quantity, item) = Parse(name);
        var price = Price(item, troop, factionId, subfactionId);
        return price is { } each
            ? new CostLine(name, CostLineKind.Purchase, each * quantity, quantity)
            : new CostLine(name, CostLineKind.Unpriced);
    }

    private int? Price(string name, Troop? troop, string? factionId, string? subfactionId)
    {
        var key = Key(name);
        if (key.Length == 0)
        {
            return null;
        }

        foreach (var candidates in Catalogs(troop, factionId, subfactionId))
        {
            var list = candidates.Where(c => c.Cost is not null).ToList();
            var match = list.FirstOrDefault(c => c.Key == key)
                ?? list.FirstOrDefault(c => c.Key == Singular(key))
                ?? list.Where(c => key.StartsWith(c.Key + " ", StringComparison.Ordinal)).OrderByDescending(c => c.Key.Length).FirstOrDefault();
            if (match is not null)
            {
                return match.Cost;
            }
        }

        return null;
    }

    /// <summary>Dónde buscar precios, de lo más concreto a lo más genérico.</summary>
    private IEnumerable<IEnumerable<Priced>> Catalogs(Troop? troop, string? factionId, string? subfactionId)
    {
        if (troop is not null)
        {
            yield return troop.WeaponOptions.Concat(troop.ArmorOptions).Concat(troop.EquipmentOptions)
                .Select(o => new Priced(Key(o.Name), o.Cost));
        }

        var faction = book.FindFaction(factionId);
        var subfaction = faction?.Subfactions.FirstOrDefault(s => s.Id == subfactionId);
        foreach (var owner in new[] { subfaction, faction }.OfType<FactionRules>())
        {
            yield return owner.GenericGear.Select(o => new Priced(Key(o.Name), o.Cost))
                .Concat(owner.Extras.SelectMany(g => g.Items).Select(i => new Priced(Key(i.Name), i.Cost)))
                .Concat(owner.Rules.Select(r => new Priced(Key(r.Name), r.Cost)));
        }

        var weapons = faction is null ? book.WeaponsFor(factionId) : book.WeaponsFor(subfaction ?? faction);
        yield return weapons.OrderBy(w => w.FactionId is null ? 1 : 0).Select(w => new Priced(Key(w.Name), w.Cost))
            .Concat(book.Armors.Where(a => Fits(a.FactionId, a.SubfactionId)).OrderBy(a => a.FactionId is null ? 1 : 0).Select(a => new Priced(Key(a.Name), a.Cost)))
            .Concat(book.Gear.Where(g => Fits(g.FactionId, g.SubfactionId)).OrderBy(g => g.FactionId is null ? 1 : 0).Select(g => new Priced(Key(g.Name), g.Cost)));

        bool Fits(string? itemFaction, string? itemSubfaction) =>
            itemFaction is null || (itemFaction == factionId && (itemSubfaction is null || itemSubfaction == subfactionId));
    }

    private Troop? FindTroop(string? name, string? factionId, string? subfactionId)
    {
        var key = RulesText.Fold(name?.Trim());
        if (key.Length == 0)
        {
            return null;
        }

        var faction = book.FindFaction(factionId);
        var owners = new[] { faction?.Subfactions.FirstOrDefault(s => s.Id == subfactionId), faction }
            .Concat(book.Factions.Where(f => f.IsMercenaries))
            .OfType<FactionRules>();
        return owners.SelectMany(o => o.Troops).FirstOrDefault(t => RulesText.Fold(t.Name) == key);
    }

    private static string BaseLabel(Miniature member, Troop? troop) =>
        troop is not null ? $"Coste base ({troop.Name})" : "Coste base";

    /// <summary>"2 pistolas" → (2, "pistolas"); "Bomba x3" → (3, "Bomba").</summary>
    private static (int Quantity, string Name) Parse(string name)
    {
        var text = name.Trim();
        if (LeadingCount().Match(text) is { Success: true } lead)
        {
            return (int.Parse(lead.Groups[1].Value), lead.Groups[2].Value);
        }

        if (TrailingCount().Match(text) is { Success: true } trail)
        {
            return (int.Parse(trail.Groups[2].Value), trail.Groups[1].Value);
        }

        return (1, text);
    }

    /// <summary>"pistolas" → "pistola", "cuchillos arrojadizos" → "cuchillo arrojadizo".</summary>
    private static string Singular(string key) =>
        string.Join(' ', key.Split(' ').Select(w => w.Length > 3 && w.EndsWith('s') ? w[..^1] : w));

    private static string Key(string? name) =>
        Spaces().Replace(Parenthesis().Replace(RulesText.Fold(name), " "), " ").Trim().TrimEnd('.');

    private sealed record Priced(string Key, int? Cost);

    [GeneratedRegex(@"^(\d+)\s*[x×]?\s+(.+)$")]
    private static partial Regex LeadingCount();

    [GeneratedRegex(@"^(.+?)\s*[x×]\s*(\d+)$")]
    private static partial Regex TrailingCount();

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex Parenthesis();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
