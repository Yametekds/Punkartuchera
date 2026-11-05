using System.Text.RegularExpressions;

namespace Punkartuchera.Core.Rules;

/// <summary>Un trozo de texto: o texto normal, o la mención de una regla especial.</summary>
public sealed record TextPiece(string Text, SpecialRule? Rule = null);

/// <summary>
/// Encuentra en los textos del reglamento las reglas especiales que se nombran
/// ("Gana Escurridizo", "Ímpetu (2)") para poder explicarlas y enlazarlas.
/// Solo cuenta el nombre tal como lo escribe el reglamento, con su mayúscula.
/// </summary>
public sealed class RuleMentions
{
    private readonly Regex? _pattern;
    private readonly Dictionary<string, SpecialRule> _byName = new(StringComparer.Ordinal);

    public RuleMentions(Rulebook book)
    {
        foreach (var rule in book.SpecialRules)
        {
            _byName.TryAdd(RulesText.StripParameter(rule.Name), rule);
        }

        if (_byName.Count == 0)
        {
            return;
        }

        var names = _byName.Keys.OrderByDescending(n => n.Length).Select(Regex.Escape);
        _pattern = new Regex(
            $@"(?<![\p{{L}}\p{{N}}])(?<name>{string.Join('|', names)})(?<param>\s\([^)]*\))?(?![\p{{L}}\p{{N}}])",
            RegexOptions.CultureInvariant);
    }

    public IReadOnlyList<TextPiece> Split(string? text, string? exceptRule = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        if (_pattern is null)
        {
            return [new TextPiece(text)];
        }

        var pieces = new List<TextPiece>();
        var last = 0;
        foreach (Match match in _pattern.Matches(text))
        {
            var rule = _byName[match.Groups["name"].Value];
            if (exceptRule is not null && RulesText.StripParameter(exceptRule) == RulesText.StripParameter(rule.Name))
            {
                continue;
            }

            if (match.Index > last)
            {
                pieces.Add(new TextPiece(text[last..match.Index]));
            }

            pieces.Add(new TextPiece(match.Value, rule));
            last = match.Index + match.Length;
        }

        if (last < text.Length)
        {
            pieces.Add(new TextPiece(text[last..]));
        }

        return pieces;
    }

    /// <summary>Resumen corto de una regla para ponerlo al lado de su nombre.</summary>
    public static string Gloss(SpecialRule rule, int max = 110)
    {
        var text = rule.Text.Replace('\n', ' ').Trim();
        if (text.Length <= max)
        {
            return text.TrimEnd('.');
        }

        var cut = text.LastIndexOf(' ', max);
        return text[..(cut > 40 ? cut : max)].TrimEnd(',', ';', ':', '.') + "…";
    }
}
