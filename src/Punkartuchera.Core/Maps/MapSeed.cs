namespace Punkartuchera.Core.Maps;

/// <summary>
/// Códigos de mapa cortos y fáciles de dictar. El mismo código con la misma configuración
/// genera siempre el mismo mapa, en cualquier dispositivo.
/// </summary>
public static class MapSeed
{
    /// <summary>Alfabeto sin caracteres que se confunden al dictarlos (0/O, 1/I).</summary>
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public const int Length = 6;

    public static string NewCode(Random? random = null)
    {
        random ??= Random.Shared;
        Span<char> code = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            code[i] = Alphabet[random.Next(Alphabet.Length)];
        }

        return new string(code);
    }

    /// <summary>Pasa a mayúsculas y quita lo que no pertenece al alfabeto. Devuelve null si no queda nada.</summary>
    public static string? Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var clean = new string(code.ToUpperInvariant().Where(c => Alphabet.Contains(c)).Take(12).ToArray());
        return clean.Length == 0 ? null : clean;
    }

    /// <summary>Hash FNV-1a: estable entre versiones de .NET, a diferencia de string.GetHashCode.</summary>
    public static uint ToNumber(string code)
    {
        var hash = 2166136261u;
        foreach (var c in code)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }
}
