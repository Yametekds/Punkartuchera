using System.Security.Cryptography;
using System.Text;

namespace Punkartuchera.Relay.Security;

/// <summary>
/// Llaves de jugador: 32 bytes aleatorios (256 bits) en Base64 apto para URL.
/// Se guarda solo su SHA-256. Con tanta entropía no hace falta sal ni un hash lento:
/// no hay diccionario posible, a diferencia de una contraseña elegida por una persona.
/// </summary>
public static class PlayerTokens
{
    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>Identificador público del jugador (para SignalR). No da acceso a nada por sí solo.</summary>
    public static string NewPlayerId() => Base64Url(RandomNumberGenerator.GetBytes(16));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>
    /// Compara en tiempo constante, para que lo que tarda la respuesta no revele
    /// cuántos caracteres de la huella coinciden.
    /// </summary>
    public static bool Matches(string token, string? storedHash) =>
        storedHash is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(token)), Encoding.ASCII.GetBytes(storedHash));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
