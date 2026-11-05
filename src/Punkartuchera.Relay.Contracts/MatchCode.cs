using System.Security.Cryptography;

namespace Punkartuchera.Relay.Contracts;

/// <summary>
/// Código de seis caracteres que un jugador le pasa al otro para entrar en su partida.
/// El alfabeto quita las letras y números que se confunden al leerlos en voz alta
/// o en una pantalla (0/O, 1/I/L, U/V).
/// </summary>
public static class MatchCode
{
    public const string Alphabet = "ABCDEFGHJKMNPQRSTWXYZ23456789";

    public const int Length = 6;

    /// <summary>Código nuevo con un generador criptográfico: no se puede predecir el siguiente.</summary>
    public static string Generate()
    {
        Span<char> code = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }

    /// <summary>
    /// Pasa a mayúsculas y quita espacios y guiones. Devuelve false si queda algo
    /// que no puede ser un código, sin llegar a buscarlo.
    /// </summary>
    public static bool TryNormalize(string? input, out string code)
    {
        code = string.Empty;
        if (string.IsNullOrWhiteSpace(input) || input.Length > 20)
        {
            return false;
        }

        Span<char> buffer = stackalloc char[Length];
        var count = 0;
        foreach (var raw in input)
        {
            if (raw is ' ' or '-')
            {
                continue;
            }

            var c = char.ToUpperInvariant(raw);
            if (count == Length || !Alphabet.Contains(c))
            {
                return false;
            }

            buffer[count++] = c;
        }

        if (count != Length)
        {
            return false;
        }

        code = new string(buffer);
        return true;
    }
}
