using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Rules;

/// <summary>Por qué una miniatura puede pelear cuerpo a cuerpo con algo que no es un arma de combate.</summary>
public enum MeleeStandIn
{
    /// <summary>Arma a distancia A dos manos: cuenta como una maza ligera, gratis.</summary>
    TwoHanded,

    /// <summary>Arma a distancia con Mamotreto: cuenta como una maza pesada.</summary>
    Mamotreto,

    /// <summary>Arma a distancia sin A dos manos y sin arma cuerpo a cuerpo: arma improvisada (-1 al Combate).</summary>
    Improvised,

    /// <summary>Solo lleva bombas, que no sirven como arma improvisada: pelea desarmada.</summary>
    Unarmed,
}

/// <summary>
/// Un arma a distancia (o la falta de armas) usada en Combate: "Escopeta" pelea como "Maza ligera".
/// <see cref="From"/> es nulo cuando no sale de ningún objeto (desarmado).
/// </summary>
public sealed record MeleeFallback(string? From, MeleeStandIn Reason, Weapon Profile, string Explanation);

/// <summary>
/// Cómo pelean en Combate las armas de disparo, según "Llevar varias armas" (Equipo, p. 3) y las reglas
/// A dos manos, Mamotreto y Bomba: las de A dos manos cuentan como maza ligera; las de Mamotreto, como
/// maza pesada; el resto, si no lleva arma cuerpo a cuerpo, como arma improvisada.
/// </summary>
public sealed class CloseCombat(Rulebook book, GearLookup lookup)
{
    private const string TwoHandedRule = "A dos manos";
    private const string MamotretoRule = "Mamotreto";
    private const string BombRule = "Bomba";

    public IReadOnlyList<MeleeFallback> For(Miniature member, string? factionId)
    {
        var weapons = member.Gear
            .Select(g => (Item: g.Name, Weapon: FindWeapon(UnitCostCalculator.Parse(g.Name).Name, factionId)))
            .Where(w => w.Weapon is not null)
            .Select(w => (w.Item, Weapon: w.Weapon!))
            .ToList();

        var ranged = weapons.Where(w => w.Weapon.Kind == WeaponKind.Ranged).ToList();
        if (ranged.Count == 0)
        {
            return [];
        }

        var fallbacks = new List<MeleeFallback>();
        foreach (var (item, weapon) in ranged)
        {
            if (Has(weapon, MamotretoRule))
            {
                Add(item, MeleeStandIn.Mamotreto, "Maza pesada", "Mamotreto: en Combate cuenta como una maza pesada.");
            }
            else if (Has(weapon, TwoHandedRule))
            {
                Add(item, MeleeStandIn.TwoHanded, "Maza ligera", "A dos manos: en Combate cuenta como una maza ligera, gratis.");
            }
        }

        var hasMelee = weapons.Any(w => w.Weapon.Kind == WeaponKind.Melee);
        if (hasMelee || fallbacks.Count > 0)
        {
            return fallbacks;
        }

        var improvised = ranged.Where(w => !Has(w.Weapon, BombRule)).ToList();
        if (improvised.Count > 0)
        {
            Add(string.Join(", ", improvised.Select(w => w.Item).Distinct()), MeleeStandIn.Improvised, "Arma improvisada",
                "Sin A dos manos y sin arma cuerpo a cuerpo: en Combate es un arma improvisada, -1 al Combate.");
        }
        else
        {
            Add(null, MeleeStandIn.Unarmed, "Desarmado", "Las bombas no sirven como arma improvisada: pelea desarmada.");
        }

        return fallbacks;

        void Add(string? from, MeleeStandIn reason, string profileName, string explanation)
        {
            if (book.Weapons.FirstOrDefault(w => w.FactionId is null && w.Kind == WeaponKind.Melee && w.Name == profileName) is { } profile)
            {
                fallbacks.Add(new MeleeFallback(from, reason, profile, explanation));
            }
        }
    }

    private Weapon? FindWeapon(string name, string? factionId)
    {
        var info = lookup.Find(name, factionId);
        if (info?.Kind is not (GearInfoKind.MeleeWeapon or GearInfoKind.RangedWeapon))
        {
            return null;
        }

        var kind = info.Kind == GearInfoKind.MeleeWeapon ? WeaponKind.Melee : WeaponKind.Ranged;
        return book.Weapons
            .Where(w => w.Kind == kind && w.Name == info.Name)
            .OrderBy(w => w.FactionId is null || w.FactionId == factionId ? 0 : 1)
            .FirstOrDefault();
    }

    private static bool Has(Weapon weapon, string rule) =>
        weapon.Rules.Any(r => RulesText.Fold(RulesText.StripParameter(r)) == RulesText.Fold(rule));
}
