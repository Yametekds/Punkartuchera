using System.Text.RegularExpressions;
using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Rules;

/// <summary>
/// Convierte tropas, personalidades, mercenarios y listas preconfiguradas del reglamento
/// en miembros de banda listos para guardar.
/// </summary>
public sealed partial class RosterBuilder(Rulebook book, IReadOnlyList<StatDefinition> stats)
{
    public RosterBuilder(Rulebook book, IRulesCatalog catalog)
        : this(book, catalog.Stats)
    {
    }

    /// <summary>Tropas que puede reclutar una banda: las de su facción, las de su subfacción y los mercenarios.</summary>
    public IReadOnlyList<TroopChoice> ChoicesFor(string? factionId, string? subfactionId = null)
    {
        var choices = new List<TroopChoice>();
        var faction = book.FindFaction(factionId);
        if (faction is not null)
        {
            choices.AddRange(faction.Troops.Select(t => new TroopChoice(faction, t)));
            var subfaction = faction.Subfactions.FirstOrDefault(s => s.Id == subfactionId);
            if (subfaction is not null)
            {
                choices.AddRange(subfaction.Troops.Select(t => new TroopChoice(subfaction, t)));
            }
        }

        foreach (var mercenaries in book.Factions.Where(f => f.IsMercenaries))
        {
            choices.AddRange(mercenaries.Troops.Select(t => new TroopChoice(mercenaries, t)));
        }

        return choices;
    }

    /// <summary>Busca una tropa por la clave de <see cref="TroopChoice.Key"/>.</summary>
    public TroopChoice? FindChoice(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Split('/') is not [var owner, var troopSlug])
        {
            return null;
        }

        var faction = book.FindFaction(owner) ?? book.FindSubfaction(owner);
        var troop = faction?.Troops.FirstOrDefault(t => t.Slug == troopSlug);
        return faction is null || troop is null ? null : new TroopChoice(faction, troop);
    }

    /// <summary>Un miembro con el perfil, el coste base, el equipo fijo y las reglas de la tropa.</summary>
    public Miniature FromTroop(Troop troop)
    {
        var member = new Miniature
        {
            Name = troop.Name,
            Role = troop.Name,
            Cost = troop.Cost,
            Stats = ParseStats(troop.Stats),
            Skills = [.. troop.SpecialRules.Where(r => r is not "Personalidad" and not "Mercenario")],
        };

        AddGear(member, IsNone(troop.Weapons) ? null : troop.Weapons);
        AddGear(member, IsNone(troop.Armor) ? null : troop.Armor);
        AddGear(member, troop.Equipment);
        if (troop.Rules.Count > 0)
        {
            member.Notes = string.Join(", ", troop.Rules.Select(r => r.Name));
        }

        return member;
    }

    /// <summary>Un miembro tal como viene en una ficha de control.</summary>
    public Miniature FromPreset(PresetList list, PresetMember preset)
    {
        var troop = book.FindFaction(list.FactionId)?.Troops.FirstOrDefault(t => t.Name == preset.Troop);
        var member = new Miniature
        {
            Name = preset.Name ?? preset.Troop,
            Role = preset.Troop,
            Cost = preset.Cost,
            Stats = ParseStats(preset.Stats ?? troop?.Stats ?? []),
        };

        member.Skills.AddRange(troop?.SpecialRules.Where(r => r is not "Personalidad" and not "Mercenario") ?? []);
        member.Skills.AddRange(preset.Skills);
        foreach (var name in preset.Weapons.Append(IsNone(preset.Armor) ? null : preset.Armor).Concat(preset.Gear))
        {
            AddGear(member, name);
        }

        return member;
    }

    public IReadOnlyList<Miniature> FromList(PresetList list) =>
        list.Members.Select(m => FromPreset(list, m)).ToList();

    /// <summary>
    /// Convierte los atributos del reglamento en números. "4 (6)" usa el valor entre paréntesis
    /// (el que ya cuenta el equipo fijo); "-" o "X" dejan el mínimo, o el valor por defecto si el mínimo no es 0.
    /// </summary>
    public Dictionary<string, int> ParseStats(IReadOnlyDictionary<string, string> source)
    {
        var result = new Dictionary<string, int>();
        foreach (var stat in stats)
        {
            var text = source.TryGetValue(stat.Key, out var value) ? value : string.Empty;
            var match = ParenthesisValue().Match(text) is { Success: true } inner ? inner : FirstNumber().Match(text);
            var number = match.Success ? int.Parse(match.Groups[1].Value) : stat.Min > 0 ? stat.Default : stat.Min;
            result[stat.Key] = Math.Clamp(number, stat.Min, stat.Max);
        }

        return result;
    }

    private static bool IsNone(string? text) =>
        string.IsNullOrWhiteSpace(text) || text.Trim().TrimEnd('.') is "Ninguno" or "Ninguna" or "ninguno" or "ninguna";

    private static void AddGear(Miniature member, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            member.Gear.Add(new Equipment { Name = name.Trim() });
        }
    }

    [GeneratedRegex(@"\((\d+)\)")]
    private static partial Regex ParenthesisValue();

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex FirstNumber();
}

/// <summary>Una tropa reclutable y de dónde sale (facción, subfacción o mercenarios).</summary>
public sealed record TroopChoice(FactionRules Owner, Troop Troop)
{
    public string Key => $"{Owner.Id}/{Troop.Slug}";

    public string Group => Owner.IsMercenaries
        ? "Mercenarios"
        : Troop.Personality ? $"Personalidades · {Owner.Name}" : Owner.Name;
}
