using System.Text.RegularExpressions;
using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Rules;

/// <summary>Cómo se gasta la munición de un arma a distancia.</summary>
public enum ShotKind
{
    /// <summary>Arma de fuego: cada disparo gasta una bala comprada aparte.</summary>
    Bullets,

    /// <summary>Lentorra (X): después de disparar hay que recargarla.</summary>
    Reload,

    /// <summary>Usar y tirar o de un solo disparo: se gasta al usarla.</summary>
    SingleUse,
}

/// <summary>Un arma a distancia que lleva un miembro y cómo dispara.</summary>
/// <param name="Name">Nombre tal como va en la ficha ("Pistola de mecha", "Lucy").</param>
/// <param name="ReloadActions">Acciones para recargar (Lentorra); 0 si no se sabe o no recarga.</param>
/// <param name="Quantity">Cuántas lleva (arpones, redes…).</param>
public sealed record RangedWeapon(string Name, ShotKind Kind, int ReloadActions = 0, int Quantity = 1);

/// <summary>
/// Lee el equipo de un miembro para saber qué armas a distancia lleva y cómo gastan la munición.
/// Las balas solo valen para armas con Arma de fuego (Equipo, pág. 3); el resto tiene munición
/// para toda la partida, aunque las de Lentorra hay que recargarlas y las de Usar y tirar se pierden.
/// </summary>
public sealed partial class MemberWeapons(GearLookup lookup)
{
    private const string Firearm = "Arma de fuego";

    /// <summary>Armas del miembro que gastan balas, se recargan o se usan una vez.</summary>
    public IReadOnlyList<RangedWeapon> Of(Miniature member, string? factionId) =>
        Of(member.Gear.Select(g => g.Name), factionId);

    public IReadOnlyList<RangedWeapon> Of(IEnumerable<string> gear, string? factionId)
    {
        var result = new List<RangedWeapon>();
        foreach (var item in gear)
        {
            var (quantity, name) = UnitCostCalculator.Parse(item);
            foreach (var part in Parts(name))
            {
                if (Classify(part, factionId) is { } weapon)
                {
                    var existing = result.FindIndex(w => w.Name == weapon.Name && w.Kind == weapon.Kind);
                    if (existing >= 0)
                    {
                        result[existing] = result[existing] with { Quantity = result[existing].Quantity + quantity };
                    }
                    else
                    {
                        result.Add(weapon with { Quantity = quantity });
                    }
                }
            }
        }

        return result;
    }

    /// <summary>Lleva algún arma con Arma de fuego.</summary>
    public bool UsesBullets(Miniature member, string? factionId) =>
        Of(member, factionId).Any(w => w.Kind == ShotKind.Bullets);

    /// <summary>
    /// Se le pueden comprar balas: lleva un Arma de fuego. Si ninguna pieza de su equipo está en el
    /// reglamento (fichas hechas a mano), no se le quita la opción.
    /// </summary>
    public bool CanBuyAmmo(Miniature member, string? factionId) =>
        UsesBullets(member, factionId) || !member.Gear.Any(g => lookup.Find(g.Name, factionId) is not null);

    /// <summary>Balas que lleva compradas como equipo (balas apañadas, munición irradiada…), además de las normales.</summary>
    public static int SpecialBullets(Miniature member) =>
        member.Gear
            .Select(g => UnitCostCalculator.Parse(g.Name))
            .Where(p => RulesText.Fold(p.Name) is var name && (name.StartsWith("bala", StringComparison.Ordinal) || name.StartsWith("municion", StringComparison.Ordinal)))
            .Sum(p => p.Quantity);

    private RangedWeapon? Classify(string part, string? factionId)
    {
        var open = part.IndexOf('(');
        var label = (open > 0 ? part[..open] : part).Trim();
        var inner = open > 0 ? part[(open + 1)..].TrimEnd(')', ' ') : null;

        // Un perfil escrito en la ficha ("Fusil arponero (F4, Pen 5, …, Lentorra (1))") manda sobre el nombre.
        if (inner is not null && ProfileText().IsMatch(inner))
        {
            return FromText(label, inner);
        }

        var info = lookup.Find(part, factionId);
        if (info is null && inner is not null)
        {
            // "Lucy (pistola)", "Garrapata (ballesta)": el arma va entre paréntesis.
            info = lookup.Find(inner, factionId);
        }

        return info?.Kind switch
        {
            GearInfoKind.RangedWeapon => FromRules(label, info.SpecialRules, info.Summary),

            // Las de cuerpo a cuerpo con Lentorra (puño balístico, guante de boxeo) también se descargan.
            GearInfoKind.MeleeWeapon => FromRules(label, info.SpecialRules, info.Summary) is { Kind: ShotKind.Reload } melee ? melee : null,
            GearInfoKind.Rule => FromText(label, info.Summary),
            _ => null,
        };
    }

    private static RangedWeapon? FromRules(string label, IReadOnlyList<string> rules, string summary)
    {
        if (rules.Any(r => Same(r, Firearm)))
        {
            return new RangedWeapon(label, ShotKind.Bullets);
        }

        if (rules.Any(r => RulesText.Fold(r).StartsWith("lentorra", StringComparison.Ordinal)))
        {
            return new RangedWeapon(label, ShotKind.Reload, ReloadActions(rules.Append(summary)) ?? 0);
        }

        if (rules.Any(r => Same(r, "Usar y tirar")) || OneShot().IsMatch(summary))
        {
            return new RangedWeapon(label, ShotKind.SingleUse);
        }

        return null;
    }

    /// <summary>Armas que solo vienen descritas en un texto (personalidades, reglas de tropa).</summary>
    private static RangedWeapon? FromText(string label, string text)
    {
        var folded = RulesText.Fold(text);
        if (folded.Contains("sin balas", StringComparison.Ordinal))
        {
            return null;
        }

        if (folded.Contains(RulesText.Fold(Firearm), StringComparison.Ordinal) || folded.Contains("gasta balas", StringComparison.Ordinal))
        {
            return new RangedWeapon(label, ShotKind.Bullets);
        }

        if (ReloadActions([text]) is { } actions)
        {
            return new RangedWeapon(label, ShotKind.Reload, actions);
        }

        return folded.Contains("usar y tirar", StringComparison.Ordinal) ? new RangedWeapon(label, ShotKind.SingleUse) : null;
    }

    /// <summary>Las Acciones de Lentorra: el primer número que aparezca; 0 si solo pone "(X)"; nulo si no es Lentorra.</summary>
    private static int? ReloadActions(IEnumerable<string> texts)
    {
        int? found = null;
        foreach (var match in texts.SelectMany(t => Lentorra().Matches(t)))
        {
            if (int.TryParse(match.Groups[1].Value, out var value))
            {
                return value;
            }

            found = 0;
        }

        return found;
    }

    /// <summary>"Pistola y filo medio", "Eva: pistola y filo pequeño" → piezas sueltas, sin partir lo que va entre paréntesis.</summary>
    private static IEnumerable<string> Parts(string text)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth = Math.Max(0, depth - 1);
                    break;
                case ',' or '.' or ':' when depth == 0:
                    yield return text[start..i];
                    start = i + 1;
                    break;
                case ' ' when depth == 0 && i + 2 < text.Length && text[i + 1] == 'y' && text[i + 2] == ' ':
                    yield return text[start..i];
                    start = i + 3;
                    i += 2;
                    break;
            }
        }

        yield return text[start..];
    }

    private static bool Same(string a, string b) => RulesText.Fold(a) == RulesText.Fold(b);

    [GeneratedRegex(@"Lentorra\s*\((\d+|X)", RegexOptions.IgnoreCase)]
    private static partial Regex Lentorra();

    [GeneratedRegex(@"solo dispara una vez", RegexOptions.IgnoreCase)]
    private static partial Regex OneShot();

    [GeneratedRegex(@"\bPen\b|\d+/\d+/\d+")]
    private static partial Regex ProfileText();
}
