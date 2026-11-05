using System.Text.RegularExpressions;

namespace Punkartuchera.Core.Rules;

/// <summary>
/// Resumen de una facción o subfacción para elegirla de un vistazo: qué tropas tiene,
/// qué reglas la definen y qué la limita.
/// </summary>
public sealed partial class FactionBrief
{
    private static readonly HashSet<string> IgnoredSpecialRules = ["Personalidad", "Mercenario"];

    private FactionBrief(FactionRules faction, FactionRules? parent)
    {
        Faction = faction;
        Parent = parent;
        var troops = parent is null
            ? faction.Troops
            : [.. BaseTroopsFor(parent, faction), .. faction.Troops];
        Troops = [.. troops.Where(t => !t.Personality).OrderBy(t => t.Cost)];
        Personalities = [.. troops.Where(t => t.Personality).OrderBy(t => t.Cost)];
        OwnTroops = parent is null ? [] : faction.Troops;
        CommonSpecialRules = [.. Troops
            .SelectMany(t => t.SpecialRules.Select(BaseRuleName).Distinct())
            .Where(r => !IgnoredSpecialRules.Contains(r))
            .GroupBy(r => r)
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)];
        KeyRules = [.. faction.Rules.Select(r => new BriefRule(r.Name, FirstSentence(r.Text), r.Slug))];
        Restrictions = parent is null ? faction.Restrictions : [.. faction.Restrictions.Where(r => !IsTroopLine(r))];
        TroopLine = parent is null ? null : faction.Restrictions.FirstOrDefault(IsTroopLine);
    }

    public FactionRules Faction { get; }

    /// <summary>La facción base, si <see cref="Faction"/> es una subfacción.</summary>
    public FactionRules? Parent { get; }

    /// <summary>Tropas que puede reclutar (sin personalidades), de la más barata a la más cara.</summary>
    public IReadOnlyList<Troop> Troops { get; }

    public IReadOnlyList<Troop> Personalities { get; }

    /// <summary>Subfacción: las tropas que solo tiene ella.</summary>
    public IReadOnlyList<Troop> OwnTroops { get; }

    /// <summary>Reglas especiales que comparten varias de sus tropas (Retraso, Radioinmune…).</summary>
    public IReadOnlyList<string> CommonSpecialRules { get; }

    /// <summary>Reglas de la facción o de la subfacción, con su primera frase.</summary>
    public IReadOnlyList<BriefRule> KeyRules { get; }

    /// <summary>Restricciones, sin la línea de tropas en las subfacciones (va en <see cref="TroopLine"/>).</summary>
    public IReadOnlyList<string> Restrictions { get; }

    /// <summary>Subfacción: la restricción que dice qué tropas de la facción usa y cuáles no.</summary>
    public string? TroopLine { get; }

    public (int Min, int Max)? CostRange => Troops.Count == 0 ? null : (Troops.Min(t => t.Cost), Troops.Max(t => t.Cost));

    public string RulesHref => Parent is null
        ? $"reglas/faccion/{Faction.Id}"
        : $"reglas/faccion/{Parent.Id}/subfaccion/{Faction.Id}";

    public static FactionBrief For(FactionRules faction, FactionRules? parent = null) => new(faction, parent);

    /// <summary>Busca la facción o subfacción por id y monta su resumen.</summary>
    public static FactionBrief? For(Rulebook book, string? factionId, string? subfactionId = null)
    {
        var faction = book.FindFaction(factionId);
        if (faction is null)
        {
            return null;
        }

        var subfaction = faction.Subfactions.FirstOrDefault(s => s.Id == subfactionId);
        return subfaction is null ? new(faction, null) : new(subfaction, faction);
    }

    /// <summary>Tropas de la facción base que admite la subfacción; sin subfacción, todas.</summary>
    public static IEnumerable<Troop> BaseTroopsFor(FactionRules faction, FactionRules? subfaction) =>
        subfaction is null || subfaction.BaseTroops.Count == 0
            ? faction.Troops
            : faction.Troops.Where(t => subfaction.BaseTroops.Contains(t.Name));

    /// <summary>«Retraso (2)» y «Retraso (3)» cuentan como la misma regla.</summary>
    public static string BaseRuleName(string rule) => ParenSuffix().Replace(rule, string.Empty).Trim();

    /// <summary>La primera frase de un texto de reglas, sin listas ni tablas.</summary>
    public static string FirstSentence(string text)
    {
        var line = text.Split('\n', 2)[0].Trim();
        var match = SentenceEnd().Match(line);
        var sentence = match.Success ? line[..(match.Index + 1)] : line;
        return sentence.EndsWith(':') ? sentence[..^1] + "." : sentence;
    }

    private static bool IsTroopLine(string restriction) =>
        restriction.StartsWith("Usa ", StringComparison.Ordinal) || restriction.StartsWith("Solo ", StringComparison.Ordinal);

    [GeneratedRegex(@"\s*\([^)]*\)$")]
    private static partial Regex ParenSuffix();

    [GeneratedRegex(@"[.!?…](?=\s+\p{Lu})")]
    private static partial Regex SentenceEnd();
}

/// <summary>Una regla resumida a su primera frase.</summary>
public sealed record BriefRule(string Name, string Summary, string Slug);
