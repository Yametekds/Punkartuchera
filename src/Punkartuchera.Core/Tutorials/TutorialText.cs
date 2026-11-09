namespace Punkartuchera.Core.Tutorials;

/// <summary>Trozo de texto de un tutorial; <see cref="Key"/> si va resaltado.</summary>
public readonly record struct TutorialRun(string Text, bool Key);

/// <summary>
/// Marcado mínimo de los textos de los tutoriales: lo que va entre <c>**</c> es una palabra clave.
/// </summary>
public static class TutorialText
{
    public const string Marker = "**";

    /// <summary>Parte el texto en trozos normales y resaltados, sin los marcadores.</summary>
    public static IReadOnlyList<TutorialRun> Split(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var runs = new List<TutorialRun>();
        var key = false;
        foreach (var part in text.Split(Marker))
        {
            if (part.Length > 0)
            {
                runs.Add(new TutorialRun(part, key));
            }

            key = !key;
        }

        return runs;
    }

    /// <summary>Cierto si todos los <c>**</c> van por parejas y no rodean un hueco.</summary>
    public static bool IsBalanced(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        var parts = text.Split(Marker);
        if (parts.Length % 2 == 0)
        {
            return false;
        }

        for (var i = 1; i < parts.Length; i += 2)
        {
            if (string.IsNullOrWhiteSpace(parts[i]) || parts[i] != parts[i].Trim())
            {
                return false;
            }
        }

        return true;
    }
}
