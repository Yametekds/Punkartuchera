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
/// límites por banda (Escasa, "solo uno por banda").
/// </summary>
public sealed class LoadoutAdvisor(Rulebook book, GearLookup lookup, UnitCostCalculator costs)
{
    private const int ScarceLimit = 3;

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

        return new Loadout(gear, skills, limit, weapons, closed, troop is not null, Warnings(limit, worn));
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

    private IEnumerable<Candidate> Candidates(Troop? troop, string? factionId, string? subfactionId)
    {
        var faction = book.FindFaction(factionId);
        var subfaction = faction?.Subfactions.FirstOrDefault(s => s.Id == subfactionId);

        if (troop is not null)
        {
            foreach (var o in troop.WeaponOptions.Concat(troop.ArmorOptions).Concat(troop.EquipmentOptions).Where(o => o.Cost is not null))
            {
                yield return new Candidate(o.Name, o.Cost!.Value, o.Each, null, null);
            }
        }
        else
        {
            // Ficha hecha a mano: todo lo que tiene precio en su facción.
            var pool = faction is null ? book.WeaponsFor(factionId) : book.WeaponsFor(subfaction ?? faction);
            foreach (var w in pool.Where(w => w.Cost is not null).OrderBy(w => w.Kind).ThenBy(w => w.Cost))
            {
                yield return new Candidate(w.Name, w.Cost!.Value, false, null, null);
            }

            foreach (var a in book.Armors.Where(a => a.Cost is not null && Fits(a.FactionId, a.SubfactionId)).OrderBy(a => a.Cost))
            {
                yield return new Candidate(a.Name, a.Cost!.Value, false, null, null);
            }

            foreach (var g in book.Gear.Where(g => g.Cost is not null && g.FactionId is not null && Fits(g.FactionId, g.SubfactionId)))
            {
                yield return new Candidate(g.Name, g.Cost!.Value, false, null, null);
            }
        }

        foreach (var owner in new[] { faction, subfaction }.OfType<FactionRules>())
        {
            foreach (var o in owner.GenericGear.Where(o => o.Cost is not null))
            {
                yield return new Candidate(o.Name, o.Cost!.Value, o.Each, null, null);
            }

            foreach (var g in owner.Gear.Where(g => g.Cost is not null))
            {
                yield return new Candidate(g.Name, g.Cost!.Value, false, null, null);
            }

            foreach (var group in owner.Extras)
            {
                var perBand = RulesText.Fold(group.Intro).Contains("mas de tres", StringComparison.Ordinal) ? ScarceLimit : (int?)null;
                foreach (var item in group.Items.Where(i => i.Cost is not null))
                {
                    yield return new Candidate(item.Name, item.Cost!.Value, false, group.Title, perBand);
                }
            }
        }

        bool Fits(string? itemFaction, string? itemSubfaction) =>
            itemFaction is null || (itemFaction == factionId && (itemSubfaction is null || itemSubfaction == subfactionId));
    }

    private LoadoutOption Evaluate(Candidate candidate, Miniature member, List<Miniature> band, WeaponLimit? limit, Worn worn, string? factionId)
    {
        var item = Describe(candidate.Name, factionId);
        var slot = candidate.SkillGroup is not null ? LoadoutSlot.Skill : item.Slot;
        var owned = slot == LoadoutSlot.Skill
            ? member.Skills.Count(s => UnitCostCalculator.SameItem(s, candidate.Name))
            : member.Gear.Where(g => UnitCostCalculator.SameItem(UnitCostCalculator.Parse(g.Name).Name, candidate.Name))
                .Sum(g => UnitCostCalculator.Parse(g.Name).Quantity);

        return new LoadoutOption(candidate.SkillGroup ?? GroupOf(slot, item.Kind), candidate.Name, candidate.Cost, slot)
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

            var inBand = band.Sum(m => slot == LoadoutSlot.Skill
                ? m.Skills.Count(s => UnitCostCalculator.SameItem(s, candidate.Name))
                : m.Gear.Count(g => UnitCostCalculator.SameItem(UnitCostCalculator.Parse(g.Name).Name, candidate.Name)));
            if (candidate.PerBand is { } perBand && inBand >= perBand)
            {
                return $"Ya hay {perBand} en la banda con esta";
            }

            if (item.Scarce && inBand >= ScarceLimit && owned == 0)
            {
                return $"Escasa: ya hay {ScarceLimit} en la banda";
            }

            if (item.OnePerBand && inBand > 0)
            {
                return "Solo uno por banda";
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
            Scarce: rules.Contains("escasa"),
            OnePerBand: info?.Kind == GearInfoKind.Gear && notes.Contains("solo uno por banda", StringComparison.Ordinal));
    }

    private static bool IsShield(string name) => RulesText.Fold(UnitCostCalculator.Parse(name).Name) == "escudo";

    private static string GroupOf(LoadoutSlot slot, GearInfoKind? kind) => slot switch
    {
        LoadoutSlot.Weapon => kind == GearInfoKind.RangedWeapon ? "Armas a distancia" : "Armas cuerpo a cuerpo",
        LoadoutSlot.Armor => "Blindaje",
        _ => "Equipo",
    };

    private sealed record Candidate(string Name, int Cost, bool Each, string? SkillGroup, int? PerBand);

    private sealed record Item(string Name, GearInfo? Info, LoadoutSlot Slot, GearInfoKind? Kind, bool TwoHanded, bool Alone, bool Scarce, bool OnePerBand);

    private sealed record Worn(int Weapons, bool TwoHanded, string? Alone, string? Armor, bool Shield);
}
