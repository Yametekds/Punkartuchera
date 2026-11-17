using System.Text.RegularExpressions;
using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Rules;

/// <summary>
/// El Blindaje que lleva un miembro. <see cref="Value"/> es el que cuenta siempre; <see cref="Situational"/>
/// recoge lo que solo vale en ciertos casos ("+2 en cuerpo a cuerpo" del escudo, "5/6/7 contra Arma de fuego").
/// </summary>
public sealed record ArmorRating(int Value, IReadOnlyList<string> Sources, IReadOnlyList<string> Situational)
{
    public static ArmorRating None { get; } = new(0, [], []);
}

/// <summary>
/// Calcula el Blindaje a partir de lo que lleva el miembro: el blindaje que viste (Equipo pág. 3 o el
/// de su facción), las compras que dan un Blindaje propio ("Piel dura 3: Blindaje 3"), las que lo cambian
/// ("Pasa a Blindaje 5") y las que suman siempre ("+2 Blindaje"). Solo se lleva un Blindaje a la vez:
/// el último que se le pone reemplaza al anterior, y cualquiera del equipo reemplaza al que da una regla de la tropa.
/// </summary>
public sealed partial class MemberArmor(Rulebook book, GearLookup lookup, UnitCostCalculator costs)
{
    public ArmorRating For(Miniature member, string? factionId, string? subfactionId = null)
    {
        var troop = costs.FindTroop(member.Role, factionId, subfactionId);
        if (troop?.Rules.Any(r => NoArmor().IsMatch(r.Text)) == true)
        {
            var rule = troop.Rules.First(r => NoArmor().IsMatch(r.Text));
            return new ArmorRating(0, [rule.Name], []);
        }

        (int Value, string Source)? worn = null;
        (int Value, string Source)? replaced = null;
        var bonuses = new List<(int Value, string Source, bool NeedsArmor)>();
        var situational = new List<string>();

        foreach (var name in member.Gear.Select(g => g.Name).Concat(member.Skills).Select(n => n.Trim()).Where(n => n.Length > 0))
        {
            // Lo lleva en el nombre: "Cuerpo mecánico (Blindaje 3)".
            if (InName().Match(name) is { Success: true } inName)
            {
                worn = (int.Parse(inName.Groups[1].Value), name);
                continue;
            }

            var info = lookup.Find(name, factionId);
            // "Capa de ratas (cuero endurecido)": un blindaje del reglamento con otro nombre.
            if (info?.Kind != GearInfoKind.Armor && Parenthesis().Match(name) is { Success: true } inner
                && lookup.Find(inner.Groups[1].Value, factionId) is { Kind: GearInfoKind.Armor } alias)
            {
                info = alias;
            }

            if (info?.Kind == GearInfoKind.Armor)
            {
                var item = FindArmor(info.Name, factionId);
                if (item is not null && LeadingNumber().Match(item.Armor) is { Success: true } number)
                {
                    worn = (int.Parse(number.Groups[1].Value), name);
                    var rest = item.Armor[number.Length..].Trim(' ', '·', ',');
                    if (rest.Length > 0)
                    {
                        situational.Add($"{rest} ({name})");
                    }
                }

                continue;
            }

            if (info is null || info.Kind is GearInfoKind.MeleeWeapon or GearInfoKind.RangedWeapon)
            {
                continue;
            }

            var text = info.Summary.Trim();
            if (OwnArmor().Match(text) is { Success: true } own)
            {
                worn = (int.Parse(own.Groups[1].Value), name);
                if (own.Groups[2].Success)
                {
                    situational.Add($"{name}: {Shorten(text)}");
                }
            }
            else if (info.Kind == GearInfoKind.Rule)
            {
                continue;
            }
            else if (BecomesArmor().Match(text) is { Success: true } becomes)
            {
                Keep(ref replaced, int.Parse(becomes.Groups[1].Value), name);
            }
            else if (FlatBonus().Match(text) is { Success: true } flat)
            {
                bonuses.Add((int.Parse(flat.Groups[1].Value), name, false));
            }
            else if (AnyArmorBonus().Match(text) is { Success: true } any)
            {
                bonuses.Add((int.Parse(any.Groups[1].Value), name, true));
            }
            else if (text.Contains("Blindaje", StringComparison.OrdinalIgnoreCase) && !text.Contains("ignora", StringComparison.OrdinalIgnoreCase))
            {
                situational.Add($"{name}: {Shorten(text)}");
            }
        }

        // Reglas de la propia tropa: "Piel escamosa: Escurridizo y +1 a cualquier Blindaje que lleve".
        foreach (var rule in troop?.Rules ?? [])
        {
            var text = rule.Text.Trim();
            if (bonuses.Any(b => b.Source == rule.Name))
            {
                continue;
            }

            if (OwnArmor().Match(text) is { Success: true, Groups: var own } && !own[2].Success)
            {
                worn ??= (int.Parse(own[1].Value), rule.Name);
            }
            else if (FlatBonus().Match(text) is { Success: true } flat)
            {
                bonuses.Add((int.Parse(flat.Groups[1].Value), rule.Name, false));
            }
            else if (AnyArmorBonus().Match(text) is { Success: true } any)
            {
                bonuses.Add((int.Parse(any.Groups[1].Value), rule.Name, true));
            }
        }

        var sources = new List<string>();
        var value = 0;
        if (replaced is { } r)
        {
            value = r.Value;
            sources.Add(r.Source);
        }
        else if (worn is { } w)
        {
            value = w.Value;
            sources.Add(w.Source);
        }

        foreach (var bonus in bonuses.Where(b => !b.NeedsArmor || value > 0))
        {
            value += bonus.Value;
            sources.Add($"+{bonus.Value} {bonus.Source}");
        }

        return value == 0 && situational.Count == 0 && sources.Count == 0
            ? ArmorRating.None
            : new ArmorRating(value, sources, situational);
    }

    private ArmorItem? FindArmor(string name, string? factionId) =>
        book.Armors.FirstOrDefault(a => a.Name == name && (a.FactionId is null || a.FactionId == factionId))
        ?? book.Armors.FirstOrDefault(a => a.Name == name);

    private static void Keep(ref (int Value, string Source)? current, int value, string source)
    {
        if (current is null || value > current.Value.Value)
        {
            current = (value, source);
        }
    }

    private static string Shorten(string text)
    {
        var end = text.IndexOf(". ", StringComparison.Ordinal);
        return (end > 0 ? text[..end] : text).TrimEnd('.');
    }

    [GeneratedRegex(@"^(\d+)")]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"\(Blindaje (\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex InName();

    [GeneratedRegex(@"\(([^)]+)\)")]
    private static partial Regex Parenthesis();


    /// <summary>
    /// "Blindaje 3. No se acumula…", "Blindaje 3 con Ignífugo…", "Blindaje 2 que no reduce…";
    /// "Blindaje 2 contra disparos y 5 cuerpo a cuerpo" deja lo de después en el grupo 2.
    /// </summary>
    [GeneratedRegex(@"^Blindaje (\d+)(?:\.|\s+(?:con|que)\s|$|\s+(contra .+))")]
    private static partial Regex OwnArmor();

    [GeneratedRegex(@"^Pasa a Blindaje (\d+)\.?$")]
    private static partial Regex BecomesArmor();

    [GeneratedRegex(@"^\+(\d+) (?:al )?Blindaje\.?$")]
    private static partial Regex FlatBonus();

    [GeneratedRegex(@"\+(\d+) a cualquier Blindaje")]
    private static partial Regex AnyArmorBonus();

    [GeneratedRegex(@"no llevan? Blindaje", RegexOptions.IgnoreCase)]
    private static partial Regex NoArmor();
}
