using System.Globalization;
using System.Text;

namespace Punkartuchera.Core.Rules;

/// <summary>Utilidades de texto para buscar y enlazar reglas.</summary>
public static class RulesText
{
    /// <summary>Minúsculas sin tildes ni diéresis, para comparar sin importar cómo se escriba.</summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Identificador para anclas de URL: "Ímpetu (X)" → "impetu-x".</summary>
    public static string Slug(string? text)
    {
        var folded = Fold(text);
        var builder = new StringBuilder(folded.Length);
        foreach (var c in folded)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().TrimEnd('-');
    }

    /// <summary>Quita el parámetro final: "Retraso (2)" → "Retraso".</summary>
    public static string StripParameter(string name)
    {
        var open = name.LastIndexOf('(');
        return open > 0 && name.EndsWith(')') ? name[..open].TrimEnd() : name;
    }
}
