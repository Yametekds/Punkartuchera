using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Rules;

/// <summary>
/// Lo que se compra para la banda entera y no para una miniatura (el Gato parameño): qué hay
/// para cada facción y cómo se añade o se quita de una banda.
/// </summary>
public sealed class BandExtras(Rulebook book)
{
    public const string Cat = "Gato parameño";

    /// <summary>Las compras de banda que admite una facción o subfacción.</summary>
    public IReadOnlyList<GearItem> For(string? factionId, string? subfactionId = null)
    {
        var faction = book.FindFaction(factionId);
        if (faction is null || faction.IsMercenaries)
        {
            return [];
        }

        var subfaction = faction.Subfactions.FirstOrDefault(s => s.Id == subfactionId);
        var banned = subfaction?.BannedGear.Where(b => b.Troops.Count == 0).SelectMany(b => b.Names).ToList() ?? [];
        return book.Gear
            .Where(g => g.Band && g.Cost is not null)
            .Where(g => g.FactionId is null || (g.FactionId == faction.Id && (g.SubfactionId is null || g.SubfactionId == subfactionId)))
            .Where(g => !banned.Any(b => UnitCostCalculator.SameItem(b, g.Name)))
            .ToList();
    }

    public GearItem? Find(string? name, string? factionId, string? subfactionId = null) =>
        For(factionId, subfactionId).FirstOrDefault(g => UnitCostCalculator.SameItem(g.Name, name));

    /// <summary>Cuántos lleva la banda de esa compra.</summary>
    public static int Count(Band band, string name) =>
        band.Extras.Count(e => UnitCostCalculator.SameItem(e.Name, name));

    /// <summary>Por qué no se puede añadir ahora; nulo si se puede.</summary>
    public static string? Blocked(Band band, GearItem item) =>
        item.PerBand is { } max && Count(band, item.Name) >= max
            ? max == 1 ? "Solo uno por banda" : $"Como máximo {max} por banda"
            : null;

    /// <summary>Añade la compra con su coste. Devuelve falso si ya se ha llegado al máximo.</summary>
    public static bool Add(Band band, GearItem item)
    {
        if (Blocked(band, item) is not null)
        {
            return false;
        }

        band.Extras.Add(new BandExtra { Name = item.Name, Cost = item.Cost });
        return true;
    }

    /// <summary>Quita una unidad de esa compra.</summary>
    public static bool Remove(Band band, string name)
    {
        var index = band.Extras.FindIndex(e => UnitCostCalculator.SameItem(e.Name, name));
        if (index < 0)
        {
            return false;
        }

        band.Extras.RemoveAt(index);
        return true;
    }
}
