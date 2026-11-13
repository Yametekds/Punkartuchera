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

    /// <summary>
    /// Tropas que puede reclutar una banda. Sin subfacción: las de su facción y los mercenarios.
    /// Con subfacción: primero las propias de la subfacción, luego las de la facción madre que
    /// permite (con el nombre que reciben allí y sin las que sustituye), sus personalidades
    /// y los mercenarios si los admite.
    /// </summary>
    public IReadOnlyList<TroopChoice> ChoicesFor(string? factionId, string? subfactionId = null)
    {
        var choices = new List<TroopChoice>();
        var faction = book.FindFaction(factionId);
        var subfaction = faction?.Subfactions.FirstOrDefault(s => s.Id == subfactionId);
        if (faction is not null && subfaction is null)
        {
            choices.AddRange(faction.Troops.Select(t => new TroopChoice(faction, t)));
        }
        else if (faction is not null && subfaction is not null)
        {
            choices.AddRange(subfaction.Troops.Where(t => !t.Personality).Select(t => new TroopChoice(subfaction, t)));
            var replaced = subfaction.Troops
                .Where(t => t.Replaces is not null)
                .Select(t => RulesText.Fold(t.Replaces))
                .ToHashSet();
            var allowed = subfaction.BaseTroops.Select(RulesText.Fold).ToHashSet();
            var fromParent = faction.Troops
                .Where(t => allowed.Contains(RulesText.Fold(t.Name)) && !replaced.Contains(RulesText.Fold(t.Name)))
                .ToList();
            foreach (var troop in fromParent.Where(t => !t.Personality))
            {
                var label = subfaction.TroopNames.GetValueOrDefault(troop.Name);
                choices.Add(new TroopChoice(faction, troop, label, $"De {faction.Name}"));
            }

            choices.AddRange(subfaction.Troops.Where(t => t.Personality).Select(t => new TroopChoice(subfaction, t)));
            choices.AddRange(fromParent.Where(t => t.Personality).Select(t => new TroopChoice(faction, t)));
        }

        foreach (var guest in faction?.GuestTroops ?? [])
        {
            var host = book.Factions.FirstOrDefault(f => f != faction && !f.IsMercenaries && f.Troops.Any(t => t.Name == guest));
            if (host?.Troops.First(t => t.Name == guest) is { } troop)
            {
                choices.Add(new TroopChoice(host, troop, null, $"De {host.Name}"));
            }
        }

        if (faction is not { NoMercenaries: true } && subfaction is not { NoMercenaries: true })
        {
            foreach (var mercenaries in book.Factions.Where(f => f.IsMercenaries))
            {
                choices.AddRange(mercenaries.Troops.Select(t => new TroopChoice(mercenaries, t)));
            }
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

    /// <summary>
    /// Un miembro con el perfil, el coste base, el equipo fijo, las reglas de la tropa
    /// y su foto oficial si la hay.
    /// </summary>
    public Miniature FromTroop(Troop troop, string? factionId = null)
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

        member.BaseCost = troop.Cost;
        member.Included = [.. member.Gear.Select(g => g.Name), .. member.Skills];
        member.Photo = BestPhoto(factionId, troop.Name, member.Gear.Select(g => g.Name));
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
        member.Skills.AddRange(preset.Skills.Where(s => !member.Skills.Contains(s)));
        foreach (var name in preset.Weapons.Append(IsNone(preset.Armor) ? null : preset.Armor).Concat(preset.Gear))
        {
            AddGear(member, name);
        }

        // La ficha de control ya da el coste con todo su equipo y sus balas: las balas se apartan del coste base
        // para que, si se quitan o se añaden, el total cambie como con cualquier otra miniatura.
        member.Ammo = preset.Ammo;
        member.BaseCost = preset.Cost - (preset.Ammo * BulletPrice);
        member.Included = [.. member.Gear.Select(g => g.Name), .. member.Skills];
        member.Photo = BestPhoto(list.FactionId, preset.Troop, member.Gear.Select(g => g.Name));
        return member;
    }

    private int BulletPrice =>
        book.Gear.FirstOrDefault(g => g.FactionId is null && RulesText.Fold(g.Name) == "balas")?.Cost ?? UnitCostCalculator.DefaultBulletPrice;

    /// <summary>Fotos oficiales de una tropa; los mercenarios valen para cualquier facción.</summary>
    public IReadOnlyList<OfficialMini> PhotosFor(string? factionId, string? troopName) =>
        string.IsNullOrWhiteSpace(troopName)
            ? []
            : book.Minis
                .Where(m => RulesText.Fold(m.Troop) == RulesText.Fold(troopName.Trim()))
                .Where(m => factionId is null || m.FactionId == factionId || m.FactionId == "mercenarios")
                .ToList();

    /// <summary>
    /// La foto oficial que mejor encaja con el equipo: gana la miniatura cuyo nombre
    /// menciona más armas o piezas que lleva ("Ascua con escopeta" para un Ascua con escopeta).
    /// </summary>
    public PhotoRef? BestPhoto(string? factionId, string troopName, IEnumerable<string> gear)
    {
        var folded = gear.Select(g => RulesText.Fold(g)).Where(g => g.Length > 0).ToList();
        var best = PhotosFor(factionId, troopName)
            .Select((mini, index) => (Mini: mini, Index: index, Score: folded.Count(g => RulesText.Fold(mini.Name).Contains(g, StringComparison.Ordinal))))
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Index)
            .Select(c => c.Mini)
            .FirstOrDefault();
        return best is null ? null : PhotoRef.Official(best.Image);
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
public sealed record TroopChoice(FactionRules Owner, Troop Troop, string? Label = null, string? GroupLabel = null)
{
    public string Key => $"{Owner.Id}/{Troop.Slug}";

    /// <summary>Cómo se llama en la banda: el nombre de la subfacción si lo renombra ("Ninja negro").</summary>
    public string Name => Label ?? Troop.Name;

    public string Group => GroupLabel ?? (Owner.IsMercenaries
        ? "Mercenarios"
        : Troop.Personality ? $"Personalidades · {Owner.Name}" : Owner.Name);
}
