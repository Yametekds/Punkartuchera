using System.Text.RegularExpressions;

namespace Punkartuchera.Core.Rules;

/// <summary>Lo que dice el reglamento de un arma, blindaje o pieza de equipo que lleva un miembro.</summary>
public sealed record GearInfo(string Name, GearInfoKind Kind, string Summary, string? Href)
{
    /// <summary>Reglas especiales que lleva (armas y blindajes): "A dos manos", "Escasa"…</summary>
    public IReadOnlyList<string> SpecialRules { get; init; } = [];
}

/// <summary>Una regla especial con su texto, para explicarla sin salir de la ficha.</summary>
public sealed record RuleDetail(string Name, string Text, string Href);

public enum GearInfoKind
{
    MeleeWeapon,
    RangedWeapon,
    Armor,
    Gear,
    Extra,
    Rule,
}

/// <summary>
/// Busca en el reglamento el equipo que se escribe a mano en las fichas. Acepta nombres
/// aproximados: "Filo medio incrustado" encuentra "Filo medio" y "Chasqueador" encuentra
/// "Chasqueador y traje voltaico". Lo que va entre paréntesis no cuenta.
/// </summary>
public sealed partial class GearLookup
{
    private readonly List<(string Key, string? FactionId, GearInfo Info)> _entries = [];
    private readonly Rulebook _book;

    public GearLookup(Rulebook book)
    {
        _book = book;
        foreach (var weapon in book.Weapons)
        {
            Add(weapon.Name, weapon.FactionId, Describe(weapon), $"reglas/equipo#{weapon.Slug}",
                weapon.Kind == WeaponKind.Melee ? GearInfoKind.MeleeWeapon : GearInfoKind.RangedWeapon, weapon.Rules);
        }

        foreach (var armor in book.Armors)
        {
            var text = Join($"Blindaje {armor.Armor}", string.Join(", ", armor.Rules), armor.Notes);
            Add(armor.Name, armor.FactionId, text, $"reglas/equipo#{armor.Slug}", GearInfoKind.Armor, armor.Rules);
        }

        foreach (var gear in book.Gear)
        {
            Add(gear.Name, gear.FactionId, gear.Text, $"reglas/equipo#{gear.Slug}", GearInfoKind.Gear);
        }

        foreach (var faction in book.Factions)
        {
            AddFaction(faction, faction.Id, $"reglas/faccion/{faction.Id}");
            foreach (var subfaction in faction.Subfactions)
            {
                AddFaction(subfaction, faction.Id, $"reglas/faccion/{faction.Id}/subfaccion/{subfaction.Id}");
            }
        }

        foreach (var rule in book.SpecialRules)
        {
            Add(rule.Name, null, rule.Text, $"reglas/especiales#{rule.Slug}", GearInfoKind.Rule);
        }
    }

    /// <summary>Busca un nombre; con facción, prefiere lo genérico y lo de esa facción.</summary>
    public GearInfo? Find(string? name, string? factionId = null)
    {
        var key = Key(name);
        if (key.Length == 0)
        {
            return null;
        }

        var candidates = _entries
            .OrderBy(e => e.FactionId is null || e.FactionId == factionId ? 0 : 1)
            .ToList();

        return candidates.FirstOrDefault(e => e.Key == key).Info
            ?? candidates.Where(e => key.StartsWith(e.Key + " ", StringComparison.Ordinal))
                .OrderByDescending(e => e.Key.Length).Select(e => e.Info).FirstOrDefault()
            ?? candidates.FirstOrDefault(e => e.Key.StartsWith(key + " ", StringComparison.Ordinal)).Info
            ?? candidates.FirstOrDefault(e => key.Length >= 5 && e.Key.Contains(key, StringComparison.Ordinal)).Info;
    }

    /// <summary>"Cuerpo a cuerpo · C +1 · F +3 · Pen 6 · A dos manos".</summary>
    public static string Describe(Weapon weapon)
    {
        var profile = weapon.Kind == WeaponKind.Melee
            ? $"Cuerpo a cuerpo · C {weapon.Combat ?? "+0"} · F {weapon.Strength} · Pen {weapon.Penetration}"
            : $"{weapon.Range} cm · F {weapon.Strength} · Pen {weapon.Penetration}";
        return Join(profile, string.Join(", ", weapon.Rules), weapon.Notes);
    }

    private void AddFaction(FactionRules faction, string factionId, string root)
    {
        foreach (var group in faction.Extras)
        {
            foreach (var item in group.Items)
            {
                var href = faction.IsSubfaction ? $"{root}#{item.Slug}" : $"{root}/reglas#{item.Slug}";
                Add(item.Name, factionId, item.Text, href, GearInfoKind.Extra);
            }
        }

        foreach (var rule in faction.Troops.SelectMany(t => t.Rules))
        {
            Add(rule.Name, factionId, rule.Text, null, GearInfoKind.Rule);
        }
    }

    /// <summary>El texto de cada regla especial que lleva una pieza de equipo.</summary>
    public IReadOnlyList<RuleDetail> RulesOf(GearInfo info) =>
        info.SpecialRules
            .Select(name => (Name: name, Rule: _book.FindSpecialRule(name)))
            .Where(r => r.Rule is not null)
            .Select(r => new RuleDetail(r.Name, r.Rule!.Text, $"reglas/especiales#{r.Rule.Slug}"))
            .ToList();

    private void Add(string name, string? factionId, string summary, string? href, GearInfoKind kind, IReadOnlyList<string>? rules = null) =>
        _entries.Add((Key(name), factionId, new GearInfo(name, kind, summary, href) { SpecialRules = rules ?? [] }));

    private static string Key(string? name) =>
        Spaces().Replace(Parenthesis().Replace(RulesText.Fold(name), " "), " ").Trim();

    private static string Join(params string?[] parts) =>
        string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex Parenthesis();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
