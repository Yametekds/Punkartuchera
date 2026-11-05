using System.Text.Json.Serialization;

namespace Punkartuchera.Relay.Contracts;

/// <summary>
/// Lo que el relé devuelve al crear una partida o entrar en ella.
/// <see cref="Token"/> es la llave del jugador: solo la conoce su móvil y el relé guarda su huella (SHA-256).
/// </summary>
public sealed record RelayTicket(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("playerId")] string PlayerId,
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("host")] bool IsHost,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt);

/// <summary>Datos para abrir la conexión en tiempo real con Azure SignalR Service.</summary>
public sealed record RelayConnectionInfo(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("accessToken")] string AccessToken);

/// <summary>Rutas, nombres y límites que comparten el relé y la app.</summary>
public static class RelayProtocol
{
    /// <summary>Hub de SignalR. Cada jugador se conecta con su <see cref="RelayTicket.PlayerId"/> como usuario.</summary>
    public const string Hub = "partida";

    /// <summary>Método del cliente que recibe los mensajes del rival.</summary>
    public const string ClientMethod = "mensaje";

    /// <summary>Tamaño máximo de un mensaje. La banda en formato QR ocupa como mucho 2400 caracteres.</summary>
    public const int MaxMessageBytes = 8 * 1024;

    /// <summary>Tipos que puede mandar un jugador. "bye" solo lo manda el relé al salir el rival.</summary>
    public static readonly IReadOnlySet<string> ClientMessageTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        MessageTypes.Hello,
        MessageTypes.Status,
        MessageTypes.Round,
    };

    public static string CreatePath => "api/partidas";

    public static string JoinPath(string code) => $"api/partidas/{code}/entrar";

    public static string NegotiatePath(string code) => $"api/partidas/{code}/conexion";

    public static string MessagesPath(string code) => $"api/partidas/{code}/mensajes";

    public static string LeavePath(string code) => $"api/partidas/{code}/salir";
}

public static class MessageTypes
{
    public const string Hello = "hello";
    public const string Status = "status";
    public const string Round = "round";
    public const string Bye = "bye";
}

[JsonSerializable(typeof(RelayTicket))]
[JsonSerializable(typeof(RelayConnectionInfo))]
public sealed partial class RelayJsonContext : JsonSerializerContext;
