using System.Text.RegularExpressions;
using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Rules;

/// <summary>Un cambio fijo a un Atributo por algo que lleva el miembro ("Pura chapa", Armatoste: −1 a Agilidad).</summary>
public sealed record StatModifier(string StatKey, int Delta, string Source, bool Included);

/// <summary>
/// Cómo queda un Atributo en la hoja de partida por lo que lleva el miembro.
/// <see cref="Delta"/> es lo que se marca encima del número; <see cref="Pending"/> es lo que aún hay
/// que sumar a la ficha guardada (lo que ya venía restado en el perfil oficial no se resta dos veces).
/// </summary>
public sealed record StatAdjustment(int Delta, int Pending, IReadOnlyList<string> Sources);

/// <summary>
/// Saca del equipo, las mutaciones y las mejoras de un miembro los cambios de Atributos que duran
/// toda la partida: Armatoste (−1 a Agilidad, Reglas pág. 23) y las compras cuyo texto es solo un
/// bonificador ("Cachas: +2 a Fuerza"). Lo que depende de la situación (la Fuerza de un arma al
/// disparar, "hasta el final del Turno", "contra…") no cambia la hoja.
/// </summary>
public sealed partial class LoadoutModifiers(Rulebook book, GearLookup lookup, UnitCostCalculator costs)
{
    private const string Armatoste = "Armatoste";

    /// <summary>Cada cambio fijo con la pieza que lo da.</summary>
    public IReadOnlyList<StatModifier> Of(Miniature member, string? factionId, string? subfactionId = null)
    {
        var troop = costs.FindTroop(member.Role, factionId, subfactionId);
        var included = member.BaseCost is null
            ? member.Gear.Select(g => g.Name).Concat(member.Skills).Select(RulesText.Fold).ToList()
            : member.Included.Select(RulesText.Fold).ToList();
        var ignoresArmatoste = troop?.Rules.Any(r => IgnoresArmatoste(r.Text)) == true;
        var result = new List<StatModifier>();

        foreach (var name in member.Gear.Select(g => g.Name).Concat(member.Skills))
        {
            var key = RulesText.Fold(name);
            var isIncluded = included.Remove(key);
            var info = lookup.Find(name, factionId);
            if (info is null)
            {
                continue;
            }

            // Armatoste: un solo objeto con esta regla, así que nunca resta más de 1.
            if (info.SpecialRules.Any(r => RulesText.Fold(r) == RulesText.Fold(Armatoste)))
            {
                if (!ignoresArmatoste && !result.Any(m => m.Source.EndsWith($"({Armatoste})", StringComparison.Ordinal)))
                {
                    result.Add(new StatModifier("ag", -1, $"{name.Trim()} ({Armatoste})", isIncluded));
                }

                continue;
            }

            if (info.Kind is GearInfoKind.Extra or GearInfoKind.Gear)
            {
                foreach (var (stat, delta) in FlatBonuses(info.Summary))
                {
                    result.Add(new StatModifier(stat, delta, name.Trim(), isIncluded));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Los cambios por Atributo. Lo que trae de serie (o venía en la ficha de control) ya suele estar
    /// en el perfil oficial entre paréntesis: si el valor guardado ya no es el de base, se da por aplicado.
    /// </summary>
    public IReadOnlyDictionary<string, StatAdjustment> For(Miniature member, string? factionId, string? subfactionId = null)
    {
        var modifiers = Of(member, factionId, subfactionId);
        var result = new Dictionary<string, StatAdjustment>(StringComparer.OrdinalIgnoreCase);
        if (modifiers.Count == 0)
        {
            return result;
        }

        var troop = costs.FindTroop(member.Role, factionId, subfactionId);
        foreach (var group in modifiers.GroupBy(m => m.StatKey))
        {
            var delta = group.Sum(m => m.Delta);
            var included = group.Where(m => m.Included).Sum(m => m.Delta);
            var bought = delta - included;
            var baseValue = troop is not null && troop.Stats.TryGetValue(group.Key, out var text) && FirstNumber().Match(text) is { Success: true } number
                ? int.Parse(number.Groups[1].Value)
                : (int?)null;
            var alreadyApplied = included != 0 && baseValue is { } value && member.GetStat(group.Key, value) != value;
            var pending = bought + (alreadyApplied ? 0 : included);
            if (delta != 0)
            {
                result[group.Key] = new StatAdjustment(delta, pending, group.Select(m => m.Source).ToList());
            }
        }

        return result;
    }

    private static bool IgnoresArmatoste(string text)
    {
        var folded = RulesText.Fold(text);
        return folded.Contains("ignora", StringComparison.Ordinal) && folded.Contains("armatoste", StringComparison.Ordinal);
    }

    /// <summary>"+2 a Fuerza." o "+1 Fuerza y Dureza." → cambios. Cualquier texto con más condiciones no cuenta.</summary>
    private IEnumerable<(string Stat, int Delta)> FlatBonuses(string? text)
    {
        if (text is null || FlatBonus().Match(text.Trim()) is not { Success: true } match)
        {
            yield break;
        }

        var delta = int.Parse(match.Groups[2].Value) * (match.Groups[1].Value == "+" ? 1 : -1);
        foreach (var name in Regex.Split(match.Groups[3].Value, @"\s*(?:,|\by\b)\s*"))
        {
            var attribute = book.Attributes.FirstOrDefault(a => RulesText.Fold(a.Name) == RulesText.Fold(name.Trim()));
            if (attribute is not null)
            {
                yield return (attribute.Key, delta);
            }
        }
    }

    [GeneratedRegex(@"^([+\-−])(\d+)\s+(?:a\s+(?:la\s+)?)?((?:Acciones|Combate|Precisión|Agilidad|Fuerza|Dureza|Técnica)(?:\s*(?:,|y)\s*(?:Acciones|Combate|Precisión|Agilidad|Fuerza|Dureza|Técnica))*)\.?$")]
    private static partial Regex FlatBonus();

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex FirstNumber();
}
