using System.Globalization;
using System.Text;
using Punkartuchera.Core.Sharing;

namespace Punkartuchera.Core.Tournaments;

/// <summary>
/// Resultado de una partida tal como lo enseña el móvil de la mesa en un QR, para que el organizador
/// del torneo lo escanee. Lleva solo los identificadores de origen de las dos bandas, el marcador y
/// quién ganó; nada de balas ni de equipo. Formato:
/// <c>PKR1:{banda propia}:{banda rival}:{puntos propios}:{puntos rival}:{G|P|E}:{crc}</c>.
/// </summary>
public sealed record MatchResultCode(Guid OwnBandOrigin, Guid RivalBandOrigin, int OwnScore, int RivalScore, MatchResultWinner Winner)
{
    public const string Prefix = "PKR1:";

    /// <summary>El código cabe de sobra en un QR; esto descarta textos que no pueden serlo.</summary>
    public const int MaxLength = 128;

    public static bool LooksLikeResult(string? text) =>
        text is not null && text.TrimStart().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Ganador según el marcador: más puntos gana; igualados, empate.</summary>
    public static MatchResultWinner WinnerFromScore(int own, int rival) =>
        own > rival ? MatchResultWinner.Own : own < rival ? MatchResultWinner.Rival : MatchResultWinner.Draw;

    public string Encode()
    {
        var body = string.Join(':',
            OwnBandOrigin.ToString("N"),
            RivalBandOrigin.ToString("N"),
            Clamp(OwnScore).ToString(CultureInfo.InvariantCulture),
            Clamp(RivalScore).ToString(CultureInfo.InvariantCulture),
            Winner switch { MatchResultWinner.Own => "G", MatchResultWinner.Rival => "P", _ => "E" });
        return $"{Prefix}{body}:{Checksum(body)}";
    }

    /// <exception cref="TournamentException">Si el texto no es un resultado o está roto.</exception>
    public static MatchResultCode Decode(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length > MaxLength || !LooksLikeResult(trimmed))
        {
            throw new TournamentException("Ese QR no es un resultado de partida de Punkartuchera.");
        }

        var parts = trimmed[Prefix.Length..].Split(':');
        if (parts.Length != 6)
        {
            throw new TournamentException("El QR del resultado está incompleto. Vuelve a escanearlo.");
        }

        var body = string.Join(':', parts[..5]);
        if (!string.Equals(parts[5], Checksum(body), StringComparison.OrdinalIgnoreCase))
        {
            throw new TournamentException("El QR del resultado no se ha leído bien. Vuelve a escanearlo.");
        }

        if (!Guid.TryParseExact(parts[0], "N", out var own)
            || !Guid.TryParseExact(parts[1], "N", out var rival)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var ownScore)
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var rivalScore))
        {
            throw new TournamentException("El QR del resultado está roto. Vuelve a escanearlo.");
        }

        var winner = parts[4].ToUpperInvariant() switch
        {
            "G" => MatchResultWinner.Own,
            "P" => MatchResultWinner.Rival,
            "E" => MatchResultWinner.Draw,
            _ => throw new TournamentException("El QR del resultado está roto. Vuelve a escanearlo."),
        };

        return new MatchResultCode(own, rival, Clamp(ownScore), Clamp(rivalScore), winner);
    }

    private static int Clamp(int score) => Math.Clamp(score, 0, Tournament.MaxScore);

    private static string Checksum(string body) =>
        Crc32.Compute(Encoding.ASCII.GetBytes(body)).ToString("x8", CultureInfo.InvariantCulture);
}

public enum MatchResultWinner
{
    Own,
    Rival,
    Draw
}
