using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Rules;

/// <summary>De dónde sale una regla que se muestra en la ficha.</summary>
public enum UnitRuleOrigin
{
    /// <summary>Palabra clave o regla propia de su tropa.</summary>
    Troop,

    /// <summary>Habilidad, mutación o mejora que se le ha comprado.</summary>
    Skill,

    /// <summary>Regla especial de un arma, blindaje o pieza de equipo que lleva.</summary>
    Gear,
}

/// <summary>Una regla con su texto completo, para leerla en la ficha sin ir al reglamento.</summary>
public sealed record UnitRule(string Name, string Text, UnitRuleOrigin Origin)
{
    public RuleTable? Table { get; init; }

    /// <summary>Dónde está en las reglas de la app, si tiene sitio propio.</summary>
    public string? Href { get; init; }

    /// <summary>Objetos de su equipo que la tienen ("Escopeta, Rifle").</summary>
    public string? From { get; init; }
}

/// <summary>
/// Reúne el texto completo de todas las reglas de una miniatura: las palabras clave de su tropa
/// (Raudo, Mónguer, Personalidad…), sus reglas propias (Escoria, Cosecha…), las habilidades que
/// lleva y las reglas especiales de su equipo.
/// </summary>
public sealed class UnitRules(Rulebook book, UnitCostCalculator costs, GearLookup lookup)
{
    public IReadOnlyList<UnitRule> For(Miniature member, string? factionId, string? subfactionId = null)
    {
        var faction = book.FindFaction(factionId);
        var subfaction = faction?.Subfactions.FirstOrDefault(s => s.Id == subfactionId);
        var troop = costs.FindTroop(member.Role, factionId, subfactionId);
        var owners = new[] { subfaction, faction }.OfType<FactionRules>().ToList();
        var rules = new List<UnitRule>();

        foreach (var name in troop?.SpecialRules ?? [])
        {
            Add(Resolve(name, UnitRuleOrigin.Troop));
        }

        foreach (var rule in troop?.Rules ?? [])
        {
            Add(new UnitRule(rule.Name, rule.Text, UnitRuleOrigin.Troop) { Table = rule.Table });
        }

        foreach (var skill in member.Skills)
        {
            Add(Resolve(skill, UnitRuleOrigin.Skill));
        }

        var gearRules = new List<(string Rule, string Item)>();
        foreach (var gear in member.Gear)
        {
            var info = lookup.Find(UnitCostCalculator.Parse(gear.Name).Name, factionId);
            gearRules.AddRange((info?.SpecialRules ?? []).Select(r => (r, gear.Name)));
        }

        foreach (var group in gearRules.GroupBy(g => RulesText.Fold(RulesText.StripParameter(g.Rule))))
        {
            if (Resolve(group.First().Rule, UnitRuleOrigin.Gear) is { } rule)
            {
                Add(rule with { From = string.Join(", ", group.Select(g => g.Item).Distinct()) });
            }
        }

        return rules;

        void Add(UnitRule? rule)
        {
            if (rule is not null && !rules.Any(r => SameRule(r.Name, rule.Name)))
            {
                rules.Add(rule);
            }
        }

        UnitRule? Resolve(string name, UnitRuleOrigin origin)
        {
            if (book.FindSpecialRule(name) is { } special)
            {
                // "Retraso (2)" se queda con su número; el texto es el de "Retraso (X)".
                return new UnitRule(name, special.Text, origin) { Href = $"reglas/especiales#{special.Slug}" };
            }

            foreach (var owner in owners)
            {
                var root = owner.IsSubfaction ? $"reglas/faccion/{owner.ParentId}/subfaccion/{owner.Id}" : $"reglas/faccion/{owner.Id}/reglas";
                if (owner.Rules.FirstOrDefault(r => SameRule(r.Name, name)) is { } rule)
                {
                    return new UnitRule(name, rule.Text, origin) { Table = rule.Table, Href = $"{root}#{rule.Slug}" };
                }

                if (owner.Extras.SelectMany(g => g.Items).FirstOrDefault(i => SameRule(i.Name, name)) is { } item)
                {
                    return new UnitRule(name, item.Text, origin) { Table = item.Table, Href = $"{root}#{item.Slug}" };
                }

                if (owner.Troops.SelectMany(t => t.Rules).FirstOrDefault(r => SameRule(r.Name, name)) is { } troopRule)
                {
                    return new UnitRule(name, troopRule.Text, origin) { Table = troopRule.Table };
                }
            }

            return null;
        }
    }

    private static bool SameRule(string a, string b) =>
        RulesText.Fold(RulesText.StripParameter(a)) == RulesText.Fold(RulesText.StripParameter(b));
}
