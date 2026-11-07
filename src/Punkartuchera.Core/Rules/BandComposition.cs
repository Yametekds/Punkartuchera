using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Rules;

/// <summary>Una restricción de creación de banda que la banda no cumple.</summary>
public sealed record BandLimitIssue(string Rule, string Detail);

/// <summary>
/// Comprueba las restricciones de creación de banda de su facción o subfacción: máximos
/// (un Jefe), proporciones (no más Piltrafas que el resto), tropas por puntos (un Cosechador
/// por cada 200), Personalidades y Mercenarios… Sólo avisa: la banda se puede guardar igual.
/// </summary>
public sealed class BandComposition(Rulebook book)
{
    /// <summary>Restricciones que se aplican: las de la subfacción si la hay, o las de la facción.</summary>
    public IReadOnlyList<BandLimit> LimitsFor(string? factionId, string? subfactionId)
    {
        var faction = book.FindFaction(factionId);
        var subfaction = faction?.Subfactions.FirstOrDefault(s => s.Id == subfactionId);
        return subfaction?.Limits ?? faction?.Limits ?? [];
    }

    public IReadOnlyList<BandLimitIssue> Check(Band band)
    {
        var faction = book.FindFaction(band.FactionId);
        if (faction is null || band.IsReadOnly)
        {
            return [];
        }

        var subfaction = faction.Subfactions.FirstOrDefault(s => s.Id == band.SubfactionId);
        var names = subfaction?.TroopNames ?? [];
        var roles = band.Members.Select(m => RulesText.Fold(m.Role)).ToList();
        var points = band.Cost ?? 0;
        var issues = new List<BandLimitIssue>();

        // Regla general de los Mercenarios (pág. 81): uno como mucho en cualquier banda.
        var mercenaries = band.Members.Where(m => IsMercenary(m.Role)).ToList();
        if (mercenaries.Count > 0 && (faction.NoMercenaries || subfaction is { NoMercenaries: true }))
        {
            issues.Add(new("Esta banda no admite Mercenarios.", $"Llevas {Join(mercenaries.Select(m => m.Role))}."));
        }

        foreach (var limit in subfaction?.Limits ?? faction.Limits)
        {
            if (Evaluate(limit, band, roles, points, names) is { } detail)
            {
                issues.Add(new(limit.Text, detail));
            }
        }

        if (mercenaries.Count > 1 && !issues.Any(i => i.Rule.Contains("Mercenario", StringComparison.Ordinal)))
        {
            issues.Add(new("Como máximo un Mercenario por banda.", $"Llevas {mercenaries.Count}."));
        }

        return issues;
    }

    private string? Evaluate(BandLimit limit, Band band, List<string> roles, int points, Dictionary<string, string> names)
    {
        int Count(IEnumerable<string> troops)
        {
            var set = troops.Select(RulesText.Fold).ToHashSet();
            return roles.Count(set.Contains);
        }

        var count = Count(limit.Troops);
        switch (limit.Kind)
        {
            case BandLimitKind.Max when count > limit.Max:
                return $"Llevas {count}.";

            // Cada tropa por separado: la lista oficial de la Troupe lleva Domador, Forzudo y Acróbata con 2 Pachachos.
            case BandLimitKind.NotMoreThanRest:
                foreach (var troop in limit.Troops)
                {
                    var own = Count([troop]);
                    if (own > roles.Count - own)
                    {
                        return $"Llevas {own} {Label([troop], names)} y el resto de la banda son {roles.Count - own}.";
                    }
                }

                return null;

            case BandLimitKind.NotMoreThan:
                var than = Count(limit.Than);
                foreach (var troop in limit.Troops)
                {
                    if (Count([troop]) > than)
                    {
                        return $"Llevas {Count([troop])} {Label([troop], names)} y {than} {Label(limit.Than, names)}.";
                    }
                }

                return null;

            case BandLimitKind.PerPoints when count > MatchAidsFractions(points, limit.Points):
                var allowed = MatchAidsFractions(points, limit.Points);
                return $"Con {points} puntos te llega para {allowed} y llevas {count}.";

            case BandLimitKind.PerTroop when count > limit.Max * Count(limit.Than):
                return $"Llevas {count} con {Count(limit.Than)} {Label(limit.Than, names)}.";

            case BandLimitKind.Ladder:
                for (var i = 0; i + 1 < limit.Troops.Count; i++)
                {
                    var upper = Count([limit.Troops[i]]);
                    var lower = Count([limit.Troops[i + 1]]);
                    if (lower > 0 && upper <= lower)
                    {
                        return $"Llevas {upper} {Label([limit.Troops[i]], names)} y {lower} {Label([limit.Troops[i + 1]], names)}.";
                    }
                }

                return null;

            case BandLimitKind.Characters:
                var characters = band.Members
                    .Where(m => IsPersonality(m.Role, band.FactionId, band.SubfactionId) || (limit.Mercenaries && IsMercenary(m.Role)))
                    .ToList();
                if (characters.Count > limit.Max)
                {
                    return $"Llevas {characters.Count}: {Join(characters.Select(m => m.Role))}.";
                }

                if (limit.DistinctMercenaries && characters.Where(m => IsMercenary(m.Role)).GroupBy(m => RulesText.Fold(m.Role)).FirstOrDefault(g => g.Count() > 1) is { } repeated)
                {
                    return $"{repeated.First().Role} está repetido.";
                }

                return null;

            case BandLimitKind.Unique:
                var twice = band.Members.GroupBy(m => RulesText.Fold(m.Role)).FirstOrDefault(g => g.Count() > 1);
                return twice is null ? null : $"{twice.First().Role} está {twice.Count()} veces.";

            case BandLimitKind.Exclusive:
                var present = limit.Troops.Where(t => Count([t]) > 0).ToList();
                return present.Count > 1 ? $"Llevas {Join(present)}." : null;
        }

        return null;
    }

    /// <summary>"Por cada fracción de N puntos": sólo cuentan las completas, como en la partida.</summary>
    private static int MatchAidsFractions(int points, int size) => Matches.MatchAidCatalog.Fractions(points, size);

    private bool IsMercenary(string role) =>
        book.Factions.Where(f => f.IsMercenaries).SelectMany(f => f.Troops).Any(t => Same(t.Name, role));

    private bool IsPersonality(string role, string? factionId, string? subfactionId)
    {
        var faction = book.FindFaction(factionId);
        var subfaction = faction?.Subfactions.FirstOrDefault(s => s.Id == subfactionId);
        var troop = (subfaction?.Troops ?? []).Concat(faction?.Troops ?? []).FirstOrDefault(t => Same(t.Name, role));
        return troop is { Personality: true };
    }

    private static string Label(IEnumerable<string> troops, Dictionary<string, string> names) =>
        Join(troops.Select(t => names.GetValueOrDefault(t, t)));

    private static string Join(IEnumerable<string> items)
    {
        var list = items.Distinct().ToList();
        return list.Count <= 1 ? string.Concat(list) : $"{string.Join(", ", list.Take(list.Count - 1))} y {list[^1]}";
    }

    private static bool Same(string a, string b) => RulesText.Fold(a) == RulesText.Fold(b);
}
