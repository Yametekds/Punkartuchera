namespace Punkartuchera.Core.Sharing;

/// <summary>
/// Codificación Base45 (RFC 9285). Sus 45 caracteres son justo el modo alfanumérico de los QR,
/// así que un QR guarda los datos binarios casi sin desperdicio.
/// </summary>
public static class Base45
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var builder = new System.Text.StringBuilder((data.Length + 1) / 2 * 3);
        for (var i = 0; i + 1 < data.Length; i += 2)
        {
            var value = data[i] * 256 + data[i + 1];
            builder.Append(Alphabet[value % 45]);
            builder.Append(Alphabet[value / 45 % 45]);
            builder.Append(Alphabet[value / 2025]);
        }

        if (data.Length % 2 == 1)
        {
            var value = data[^1];
            builder.Append(Alphabet[value % 45]);
            builder.Append(Alphabet[value / 45]);
        }

        return builder.ToString();
    }

    /// <exception cref="FormatException">Si el texto no es Base45 válido.</exception>
    public static byte[] Decode(string text)
    {
        if (text.Length % 3 == 1)
        {
            throw new FormatException("Longitud Base45 no válida.");
        }

        var output = new byte[text.Length / 3 * 2 + (text.Length % 3 == 2 ? 1 : 0)];
        var o = 0;
        for (var i = 0; i < text.Length; i += 3)
        {
            var c = Digit(text[i]) + Digit(text[i + 1]) * 45;
            if (i + 2 < text.Length)
            {
                c += Digit(text[i + 2]) * 2025;
                if (c > 0xFFFF)
                {
                    throw new FormatException("Valor Base45 fuera de rango.");
                }

                output[o++] = (byte)(c >> 8);
                output[o++] = (byte)c;
            }
            else
            {
                if (c > 0xFF)
                {
                    throw new FormatException("Valor Base45 fuera de rango.");
                }

                output[o++] = (byte)c;
            }
        }

        return output;
    }

    private static int Digit(char c)
    {
        var index = Alphabet.IndexOf(c);
        return index >= 0 ? index : throw new FormatException($"Carácter Base45 no válido: '{c}'.");
    }
}
