namespace Punkartuchera.Core.Rules;

public enum RuleHitKind
{
    Section,
    SpecialRule,
    Weapon,
    Armor,
    Gear,
    Faction,
    FactionRule,
    Troop,
    Extra,
}

/// <summary>Un resultado de búsqueda con el enlace a su sitio en el navegador de reglas.</summary>
public sealed record RuleHit(RuleHitKind Kind, string Title, string Context, string Snippet, string Href, string? FactionId);

/// <summary>
/// Índice de búsqueda de todo el reglamento. Ignora mayúsculas y tildes y exige que
/// aparezcan todas las palabras de la consulta.
/// </summary>
public sealed class RulesSearch
{
    private static readonly HashSet<string> StopWords =
    [
        "a", "al", "como", "con", "de", "del", "el", "en", "es", "la", "las", "lo", "los",
        "o", "para", "por", "que", "se", "su", "un", "una", "y",
    ];

    private readonly List<Entry> _entries = [];

    public RulesSearch(Rulebook book)
    {
        foreach (var section in book.Sections)
        {
            var text = string.Join(' ', section.Body.Concat(TableText(section.Table)));
            Add(RuleHitKind.Section, section.Title, section.Category, text, $"reglas/basicas#{section.Id}", null, section.Keywords);
        }

        foreach (var rule in book.SpecialRules)
        {
            Add(RuleHitKind.SpecialRule, rule.Name, "Regla especial", rule.Text, $"reglas/especiales#{rule.Slug}", null);
        }

        foreach (var weapon in book.Weapons)
        {
            var kind = weapon.Kind == WeaponKind.Melee ? "Arma cuerpo a cuerpo" : "Arma a distancia";
            var text = string.Join(' ', new[] { weapon.Notes ?? string.Empty }.Concat(weapon.Rules));
            Add(RuleHitKind.Weapon, weapon.Name, Context(book, weapon.FactionId, kind), text, $"reglas/equipo#{weapon.Slug}", weapon.FactionId);
        }

        foreach (var armor in book.Armors)
        {
            var text = string.Join(' ', new[] { $"Blindaje {armor.Armor}", armor.Notes ?? string.Empty }.Concat(armor.Rules));
            Add(RuleHitKind.Armor, armor.Name, Context(book, armor.FactionId, "Blindaje"), text, $"reglas/equipo#{armor.Slug}", armor.FactionId);
        }

        foreach (var gear in book.Gear)
        {
            Add(RuleHitKind.Gear, gear.Name, Context(book, gear.FactionId, "Equipo"), gear.Text, $"reglas/equipo#{gear.Slug}", gear.FactionId);
        }

        foreach (var faction in book.Factions)
        {
            var root = $"reglas/faccion/{faction.Id}";
            Add(RuleHitKind.Faction, faction.Name, "Facción", string.Join(' ', faction.Restrictions.Prepend(faction.Summary)), root, faction.Id);

            foreach (var rule in faction.Rules)
            {
                Add(RuleHitKind.FactionRule, rule.Name, faction.Name, rule.Text + " " + string.Join(' ', TableText(rule.Table)), $"{root}/reglas#{rule.Slug}", faction.Id);
            }

            foreach (var group in faction.Extras)
            {
                foreach (var item in group.Items)
                {
                    Add(RuleHitKind.Extra, item.Name, $"{faction.Name} · {group.Title}", item.Text, $"{root}/reglas#{item.Slug}", faction.Id);
                }
            }

            foreach (var troop in faction.Troops)
            {
                var parts = new List<string>(troop.SpecialRules) { troop.Weapons ?? string.Empty, troop.Armor ?? string.Empty, troop.Mutations ?? string.Empty };
                parts.AddRange(troop.WeaponOptions.Select(o => o.Name));
                parts.AddRange(troop.Rules.Select(r => $"{r.Name} {r.Text}"));
                var context = troop.Personality ? $"{faction.Name} · Personalidad" : faction.Name;
                Add(RuleHitKind.Troop, troop.Name, context, string.Join(' ', parts), $"{root}/miembros#{troop.Slug}", faction.Id);
            }
        }
    }

    public int Count => _entries.Count;

    public IReadOnlyList<RuleHit> Search(string? query, string? factionId = null, int limit = 40)
    {
        var words = RulesText.Fold(query)
            .Split([' ', ',', '.', ';', ':', '/', '(', ')', '¿', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var terms = words.Where(w => !StopWords.Contains(w)).ToArray();
        if (terms.Length == 0)
        {
            terms = words;
        }

        if (terms.Length == 0)
        {
            return [];
        }

        var phrase = string.Join(' ', terms);
        return _entries
            .Where(e => factionId is null || e.Hit.FactionId is null || e.Hit.FactionId == factionId)
            .Select(e => (e.Hit, Score: Score(e, terms, phrase)))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Hit.Title, StringComparer.CurrentCulture)
            .Take(limit)
            .Select(r => r.Hit with { Snippet = MakeSnippet(r.Hit.Snippet, terms) })
            .ToList();
    }

    private static int Score(Entry entry, string[] terms, string phrase)
    {
        var score = 0;
        foreach (var term in terms)
        {
            if (entry.Title.Contains(term, StringComparison.Ordinal))
            {
                score += 20;
            }
            else if (entry.Keywords.Contains(term, StringComparison.Ordinal))
            {
                score += 8;
            }
            else if (entry.Body.Contains(term, StringComparison.Ordinal))
            {
                score += 1;
            }
            else
            {
                return 0;
            }
        }

        if (entry.Title == phrase)
        {
            score += 100;
        }
        else if (entry.Title.StartsWith(phrase, StringComparison.Ordinal))
        {
            score += 40;
        }

        return score;
    }

    /// <summary>Recorta el texto alrededor de la primera palabra encontrada.</summary>
    private static string MakeSnippet(string text, string[] terms)
    {
        const int Width = 140;
        if (text.Length <= Width)
        {
            return text;
        }

        var folded = RulesText.Fold(text);
        var at = terms.Select(t => folded.IndexOf(t, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, at - 40);
        if (start > 0)
        {
            var space = text.IndexOf(' ', start);
            start = space >= 0 && space < at ? space + 1 : start;
        }

        var length = Math.Min(Width, text.Length - start);
        return (start > 0 ? "…" : string.Empty) + text.Substring(start, length).TrimEnd() + (start + length < text.Length ? "…" : string.Empty);
    }

    private void Add(RuleHitKind kind, string title, string context, string text, string href, string? factionId, IEnumerable<string>? keywords = null)
    {
        var clean = text.Replace('\n', ' ').Replace("  ", " ").Trim();
        var hit = new RuleHit(kind, title, context, clean, href, factionId);
        _entries.Add(new Entry(
            hit,
            RulesText.Fold(title),
            RulesText.Fold(string.Join(' ', (keywords ?? []).Append(context))),
            RulesText.Fold(clean)));
    }

    private static string Context(Rulebook book, string? factionId, string kind) =>
        book.FindFaction(factionId) is { } faction ? $"{kind} · {faction.Name}" : kind;

    private static IEnumerable<string> TableText(RuleTable? table) =>
        table is null ? [] : table.Rows.SelectMany(row => row);

    private sealed record Entry(RuleHit Hit, string Title, string Keywords, string Body);
}
