using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Rules;

public enum LoadoutSlot
{
    Weapon,
    Armor,
    Gear,

    /// <summary>Mutaciones, poderes y demás listas propias: van a las habilidades.</summary>
    Skill,
}

/// <summary>
/// Algo que el reglamento deja comprar a una miniatura, con su precio, su perfil y,
/// si ahora no se puede añadir, el motivo.
/// </summary>
public sealed record LoadoutOption(string Group, string Name, int Cost, LoadoutSlot Slot)
{
    /// <summary>Perfil o texto de reglas en una línea ("Cuerpo a cuerpo · C +0 · F +3…").</summary>
    public string? Summary { get; init; }

    /// <summary>Se compra por unidades (bombas, granadas, arpones): se puede añadir varias veces.</summary>
    public bool Each { get; init; }

    /// <summary>Cuántas lleva ya la miniatura.</summary>
    public int Owned { get; init; }

    /// <summary>Blindaje que lleva ahora y que esta compra sustituye.</summary>
    public string? Replaces { get; init; }

    /// <summary>Por qué no se puede añadir ahora; nulo si se puede.</summary>
    public string? Blocked { get; init; }

    public bool CanAdd => Blocked is null;
}

/// <summary>Cuántas armas puede llevar una tropa ("Al menos una y como máximo dos…").</summary>
public sealed record WeaponLimit(int Min, int Max, string Text);

/// <summary>Lo que puede comprar una miniatura y cómo va de armas.</summary>
public sealed record Loadout(
    IReadOnlyList<LoadoutOption> Gear,
    IReadOnlyList<LoadoutOption> Skills,
    WeaponLimit? Limit,
    int Weapons,
    bool Closed,
    bool FromTroopList,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Dice qué armas, blindajes, equipo y mejoras puede comprar una miniatura según el reglamento:
/// las opciones de su tropa (o todo lo de su facción si es una ficha hecha a mano), con las
/// restricciones de armas (máximo, A dos manos, Mamotreto, escudo), un solo blindaje y los
/// límites que marca el reglamento: hasta 3 bombas o arpones, Escasa, lo que es único por banda,
/// quién puede comprar de cada lista propia (Reliquias, Mutaciones, Artículos de coña…) y cuántas,
/// y el equipo vetado en cada subfacción.
/// </summary>
public sealed class LoadoutAdvisor(Rulebook book, GearLookup lookup, UnitCostCalculator costs)
{
    private const int ScarceLimit = 3;

    /// <summary>Lo que se compra por unidades (bombas, arpones…) llega hasta 3 y cuenta como una sola arma.</summary>
    public const int MaxEach = 3;

    /// <param name="others">El resto de miniaturas de la banda, para los límites por banda.</param>
    public Loadout Advise(Miniature member, string? factionId, string? subfactionId = null, IEnumerable<Miniature>? others = null)
    {
        var troop = costs.FindTroop(member.Role, factionId, subfactionId);
        var closed = troop is not null && (troop.Personality || book.Factions.Any(f => f.IsMercenaries && f.Troops.Contains(troop)));
        var limit = troop is null ? null : ParseLimit(troop.WeaponRule);
        var band = (others ?? []).Where(m => m.Id != member.Id).Append(member).ToList();

        var worn = member.Gear.Select(g => Describe(g.Name, factionId)).ToList();
        var weapons = worn.Count(w => w.Slot == LoadoutSlot.Weapon);
        var state = new Worn(
            Weapons: weapons,
            TwoHanded: worn.Any(w => w.TwoHanded),
            Alone: worn.FirstOrDefault(w => w.Alone)?.Name,
            Armor: worn.FirstOrDefault(w => w.Slot == LoadoutSlot.Armor)?.Name,
            Shield: worn.Any(w => IsShield(w.Name)));

        var gear = new List<LoadoutOption>();
        var skills = new List<LoadoutOption>();
        if (!closed)
        {
            var seen = new List<string>();
            foreach (var candidate in Candidates(troop, factionId, subfactionId))
            {
                if (seen.Any(name => UnitCostCalculator.SameItem(name, candidate.Name)))
                {
                    continue;
                }

                seen.Add(candidate.Name);
                var option = Evaluate(candidate, member, band, limit, state, factionId);
                (option.Slot == LoadoutSlot.Skill ? skills : gear).Add(option);
            }
        }

        var warnings = Warnings(limit, worn);
        if (!closed)
        {
            warnings.AddRange(ExtraWarnings(member, troop, factionId, subfactionId));
        }

        return new Loadout(gear, skills, limit, weapons, closed, troop is not null, warnings);
    }

    /// <summary>
    /// "Una" → solo una; "Puede llevar una" → ninguna o una; "Al menos una y como máximo dos" → una o dos.
    /// Nulo si la tropa no elige armas.
    /// </summary>
    public static WeaponLimit? ParseLimit(string? rule)
    {
        var text = RulesText.Fold(rule);
        if (text.Length == 0)
        {
            return null;
        }

        var max = text.Contains("maximo dos", StringComparison.Ordinal) ? 2 : 1;
        var min = text.StartsWith("puede", StringComparison.Ordinal) ? 0 : 1;
        return new WeaponLimit(min, max, rule!.Trim());
    }

    /// <summary>
    /// Añade una compra a una lista de nombres: lo que va por unidades suma cantidad ("Bomba de humo x2")
    /// y un blindaje nuevo sustituye al que llevaba.
    /// </summary>
    public static void Add(List<string> names, LoadoutOption option)
    {
        if (option.Replaces is not null)
        {
            names.Remove(option.Replaces);
        }

        var index = option.Each ? names.FindIndex(n => UnitCostCalculator.SameItem(UnitCostCalculator.Parse(n).Name, option.Name)) : -1;
        if (index < 0)
        {
            names.Add(option.Name);
            return;
        }

        var quantity = UnitCostCalculator.Parse(names[index]).Quantity + 1;
        names[index] = $"{option.Name} x{quantity}";
    }

    /// <summary>Las listas propias que puede comprar esta tropa, con su límite si lo hay.</summary>
    private IEnumerable<(ExtraGroup Group, ExtraBuyer? Buyer)> ExtrasFor(Troop? troop, FactionRules? faction, FactionRules? subfaction)
    {
        foreach (var owner in new[] { faction, subfaction }.OfType<FactionRules>())
        {
            foreach (var group in owner.Extras)
            {
                if (owner == faction && subfaction is not null && subfaction.BannedExtras.Contains(group.Title))
                {
                    continue;
                }

                if (group.Buyers.Count == 0)
                {
                    yield return (group, null);
                    continue;
                }

                // Ficha hecha a mano: no se sabe qué tropa es, así que no se limita.
                var buyer = troop is null
                    ? null
                    : group.Buyers.FirstOrDefault(b => RulesText.Fold(b.Troop) == RulesText.Fold(troop.Name))
                        ?? group.Buyers.FirstOrDefault(b => b.Troop == "*");
                if (troop is null || buyer is not null)
                {
                    yield return (group, buyer);
                }
            }
        }
    }

    private IEnumerable<Candidate> Candidates(Troop? troop, string? factionId, string? subfactionId)
    {
        var faction = book.FindFaction(factionId);
        var subfaction = faction?.Subfactions.FirstOrDefault(s => s.Id == subfactionId);
        var banned = subfaction?.BannedGear
            .Where(b => b.Troops.Count == 0 || (troop is not null && b.Troops.Any(t => SameTroop(t, troop.Name))))
            .SelectMany(b => b.Names)
            .ToList() ?? [];

        foreach (var candidate in Gear().Where(c => !banned.Any(name => UnitCostCalculator.SameItem(name, c.Name))))
        {
            yield return candidate;
        }

        foreach (var (group, buyer) in ExtrasFor(troop, faction, subfaction))
        {
            foreach (var item in group.Items.Where(i => i.Cost is not null))
            {
                if (buyer is not null && buyer.Only.Count > 0 && !buyer.Only.Any(o => UnitCostCalculator.SameItem(o, item.Name)))
                {
                    continue;
                }

                var exempt = group.PerBandExcept.Any(e => UnitCostCalculator.SameItem(e, item.Name));
                yield return new Candidate(item.Name, item.Cost!.Value, false, group, buyer, exempt ? null : group.PerBand);
            }
        }

        IEnumerable<Candidate> Gear()
        {
            if (troop is not null)
            {
                foreach (var o in troop.WeaponOptions.Concat(troop.ArmorOptions).Concat(troop.EquipmentOptions).Where(o => o.Cost is not null))
                {
                    yield return new Candidate(o.Name, o.Cost!.Value, o.Each, null, null, PerBandOf(o.Name));
                }
            }
            else
            {
                // Ficha hecha a mano: todo lo que tiene precio en su facción.
                var pool = faction is null ? book.WeaponsFor(factionId) : book.WeaponsFor(subfaction ?? faction);
                foreach (var w in pool.Where(w => w.Cost is not null).OrderBy(w => w.Kind).ThenBy(w => w.Cost))
                {
                    yield return new Candidate(w.Name, w.Cost!.Value, false, null, null, null);
                }

                foreach (var a in book.Armors.Where(a => a.Cost is not null && Fits(a.FactionId, a.SubfactionId)).OrderBy(a => a.Cost))
                {
                    yield return new Candidate(a.Name, a.Cost!.Value, false, null, null, null);
                }

                foreach (var g in book.Gear.Where(g => g.Cost is not null && g.FactionId is not null && Fits(g.FactionId, g.SubfactionId)))
                {
                    yield return new Candidate(g.Name, g.Cost!.Value, false, null, null, g.PerBand);
                }
            }

            var noGeneric = troop is not null && new[] { faction, subfaction }.OfType<FactionRules>()
                .Any(f => f.NoGenericGear.Any(t => SameTroop(t, troop.Name)));
            foreach (var owner in new[] { faction, subfaction }.OfType<FactionRules>())
            {
                foreach (var o in owner.GenericGear.Where(o => o.Cost is not null && !noGeneric))
                {
                    if (troop is null || !o.ExcludeTroops.Any(t => SameTroop(t, troop.Name)))
                    {
                        yield return new Candidate(o.Name, o.Cost!.Value, o.Each, null, null, PerBandOf(o.Name));
                    }
                }

                foreach (var g in owner.Gear.Where(g => g.Cost is not null))
                {
                    yield return new Candidate(g.Name, g.Cost!.Value, false, null, null, g.PerBand);
                }
            }
        }

        int? PerBandOf(string name) =>
            book.Gear.Concat(faction?.Gear ?? []).Concat(subfaction?.Gear ?? [])
                .FirstOrDefault(g => UnitCostCalculator.SameItem(g.Name, name))?.PerBand;

        bool Fits(string? itemFaction, string? itemSubfaction) =>
            itemFaction is null || (itemFaction == factionId && (itemSubfaction is null || itemSubfaction == subfactionId));
    }

    private LoadoutOption Evaluate(Candidate candidate, Miniature member, List<Miniature> band, WeaponLimit? limit, Worn worn, string? factionId)
    {
        var item = Describe(candidate.Name, factionId);
        var slot = candidate.Group is not null ? LoadoutSlot.Skill : item.Slot;
        var owned = slot == LoadoutSlot.Skill
            ? member.Skills.Count(s => UnitCostCalculator.SameItem(s, candidate.Name))
            : member.Gear.Where(g => UnitCostCalculator.SameItem(UnitCostCalculator.Parse(g.Name).Name, candidate.Name))
                .Sum(g => UnitCostCalculator.Parse(g.Name).Quantity);

        return new LoadoutOption(candidate.Group?.Title ?? GroupOf(slot, item.Kind), candidate.Name, candidate.Cost, slot)
        {
            Summary = item.Info?.Summary,
            Each = candidate.Each,
            Owned = owned,
            Replaces = slot == LoadoutSlot.Armor && owned == 0 ? worn.Armor : null,
            Blocked = Block(),
        };

        string? Block()
        {
            if (owned > 0 && !candidate.Each)
            {
                return slot == LoadoutSlot.Skill ? "Ya la tiene" : "Ya lo lleva";
            }

            if (candidate.Each && owned >= MaxEach)
            {
                return $"Como máximo {MaxEach}";
            }

            // Miniaturas de la banda (sin contar esta) que ya lo llevan.
            var carriers = band.Count(m => m.Id != member.Id && (slot == LoadoutSlot.Skill
                ? m.Skills.Any(s => UnitCostCalculator.SameItem(s, candidate.Name))
                : m.Gear.Any(g => UnitCostCalculator.SameItem(UnitCostCalculator.Parse(g.Name).Name, candidate.Name))));
            if (owned == 0 && candidate.PerBand is { } perBand && carriers >= perBand)
            {
                return perBand == 1 ? "Única en la banda: ya está en otra miniatura" : $"Como máximo {perBand} en la banda";
            }

            if (owned == 0 && item.Scarce && carriers >= ScarceLimit)
            {
                return $"Escasa: ya hay {ScarceLimit} en la banda";
            }

            if (candidate.Group is { } group && candidate.Buyer is { } buyer)
            {
                var chosen = member.Skills
                    .Select(s => group.Items.FirstOrDefault(i => UnitCostCalculator.SameItem(i.Name, s)))
                    .OfType<NamedText>()
                    .ToList();
                if (buyer.Max is { } max && chosen.Count >= max)
                {
                    return max == 1 ? $"Solo puede llevar una de {group.Title}" : $"Ya lleva {max} de {group.Title}";
                }

                if (buyer.MaxPoints is { } maxPoints && chosen.Sum(i => i.Cost ?? 0) + candidate.Cost > maxPoints)
                {
                    return $"Pasaría de {maxPoints} ptos en {group.Title}";
                }
            }

            if (slot == LoadoutSlot.Weapon && owned == 0)
            {
                if (worn.Alone is not null)
                {
                    return $"Con {worn.Alone} no lleva otra arma";
                }

                if (item.Alone && worn.Weapons > 0)
                {
                    return "No se combina con otras armas";
                }

                if (limit is not null && worn.Weapons >= limit.Max)
                {
                    return limit.Max == 1 ? "Ya lleva su arma" : $"Ya lleva {limit.Max} armas";
                }

                if (item.TwoHanded && worn.TwoHanded)
                {
                    return "Ya lleva algo A dos manos";
                }

                if (item.TwoHanded && worn.Shield)
                {
                    return "No se combina con el escudo";
                }
            }

            if (IsShield(candidate.Name) && worn.TwoHanded)
            {
                return "No se combina con armas A dos manos";
            }

            return null;
        }
    }

    /// <summary>Lo que falta para cumplir los mínimos de las listas propias (una especialidad, 10 ptos de Rarezas…).</summary>
    private IEnumerable<string> ExtraWarnings(Miniature member, Troop? troop, string? factionId, string? subfactionId)
    {
        if (troop is null)
        {
            yield break;
        }

        var faction = book.FindFaction(factionId);
        var subfaction = faction?.Subfactions.FirstOrDefault(s => s.Id == subfactionId);
        foreach (var (group, buyer) in ExtrasFor(troop, faction, subfaction))
        {
            if (buyer is null)
            {
                continue;
            }

            var chosen = member.Skills
                .Select(s => group.Items.FirstOrDefault(i => UnitCostCalculator.SameItem(i.Name, s)))
                .OfType<NamedText>()
                .ToList();
            if (chosen.Count < buyer.Min)
            {
                yield return buyer.Min == 1
                    ? $"Tiene que llevar al menos una de {group.Title}."
                    : $"Tiene que llevar al menos {buyer.Min} de {group.Title}.";
            }

            if (buyer.Max is { } max && chosen.Count > max)
            {
                yield return $"Lleva {chosen.Count} de {group.Title} y su tropa solo permite {max}.";
            }

            var points = chosen.Sum(i => i.Cost ?? 0);
            if (points < buyer.MinPoints)
            {
                yield return $"Tiene que gastar al menos {buyer.MinPoints} ptos en {group.Title} (lleva {points}).";
            }

            if (buyer.MaxPoints is { } maxPoints && points > maxPoints)
            {
                yield return $"Gasta {points} ptos en {group.Title} y el máximo es {maxPoints}.";
            }
        }
    }

    private static bool SameTroop(string a, string b) => RulesText.Fold(a) == RulesText.Fold(b);

    private static List<string> Warnings(WeaponLimit? limit, List<Item> worn)
    {
        var warnings = new List<string>();
        var weapons = worn.Count(w => w.Slot == LoadoutSlot.Weapon);
        if (limit is not null && weapons < limit.Min)
        {
            warnings.Add("Le falta un arma: el reglamento pide al menos una.");
        }

        if (limit is not null && weapons > limit.Max)
        {
            warnings.Add($"Lleva {weapons} armas y su tropa solo permite {limit.Max}.");
        }

        if (worn.Count(w => w.TwoHanded) > 1)
        {
            warnings.Add("Lleva más de un objeto A dos manos.");
        }

        if (worn.FirstOrDefault(w => w.Alone)?.Name is { } alone && weapons > 1)
        {
            warnings.Add($"Con {alone} no puede llevar otras armas.");
        }

        if (worn.Count(w => w.Slot == LoadoutSlot.Armor) > 1)
        {
            warnings.Add("Lleva más de un blindaje.");
        }

        return warnings;
    }

    private Item Describe(string name, string? factionId)
    {
        var info = lookup.Find(UnitCostCalculator.Parse(name).Name, factionId);
        var slot = info?.Kind switch
        {
            GearInfoKind.MeleeWeapon or GearInfoKind.RangedWeapon => LoadoutSlot.Weapon,
            GearInfoKind.Armor => LoadoutSlot.Armor,
            _ => LoadoutSlot.Gear,
        };
        var rules = info?.SpecialRules.Select(r => RulesText.Fold(RulesText.StripParameter(r))).ToList() ?? [];
        var notes = RulesText.Fold(info?.Summary);
        var alone = rules.Contains("mamotreto") || notes.Contains("no se combina con otras armas", StringComparison.Ordinal);
        return new Item(name, info, slot, info?.Kind,
            TwoHanded: alone && rules.Contains("mamotreto") || rules.Contains("a dos manos"),
            Alone: alone,
            Scarce: rules.Contains("escasa"));
    }

    private static bool IsShield(string name) => RulesText.Fold(UnitCostCalculator.Parse(name).Name) == "escudo";

    private static string GroupOf(LoadoutSlot slot, GearInfoKind? kind) => slot switch
    {
        LoadoutSlot.Weapon => kind == GearInfoKind.RangedWeapon ? "Armas a distancia" : "Armas cuerpo a cuerpo",
        LoadoutSlot.Armor => "Blindaje",
        _ => "Equipo",
    };

    private sealed record Candidate(string Name, int Cost, bool Each, ExtraGroup? Group, ExtraBuyer? Buyer, int? PerBand);

    private sealed record Item(string Name, GearInfo? Info, LoadoutSlot Slot, GearInfoKind? Kind, bool TwoHanded, bool Alone, bool Scarce);

    private sealed record Worn(int Weapons, bool TwoHanded, string? Alone, string? Armor, bool Shield);
}
