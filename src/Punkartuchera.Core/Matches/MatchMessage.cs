using System.Text.Json;
using System.Text.Json.Serialization;
using Punkartuchera.Relay.Contracts;

namespace Punkartuchera.Core.Matches;

/// <summary>
/// Lo que un móvil le manda al otro a través del relé. Todo lo que llega se trata como dato no fiable:
/// se valida aquí antes de tocar la partida.
/// <list type="bullet">
/// <item><c>hello</c>: la banda en formato QR, la ronda y el estado de cada miembro. Se manda al conectar;
/// con <see cref="Reply"/> pide al otro que conteste con el suyo.</item>
/// <item><c>status</c>: estado de cada miembro propio, en el orden de la banda enviada.</item>
/// <item><c>round</c>: cambio de ronda.</item>
/// <item><c>bye</c>: lo manda el relé cuando el rival cierra la partida.</item>
/// </list>
/// </summary>
public sealed class MatchMessage
{
    public const int MaxMembers = 60;

    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;

    [JsonPropertyName("band")] public string? Band { get; set; }

    [JsonPropertyName("reply")] public bool Reply { get; set; }

    [JsonPropertyName("round")] public int? Round { get; set; }

    [JsonPropertyName("stamp")] public long? Stamp { get; set; }

    [JsonPropertyName("members")] public List<MemberFlags>? Members { get; set; }

    public static string Serialize(MatchMessage message) =>
        JsonSerializer.Serialize(message, MatchJsonContext.Default.MatchMessage);

    /// <summary>Devuelve null si no es un mensaje válido.</summary>
    public static MatchMessage? TryParse(string json)
    {
        if (json.Length > RelayProtocol.MaxMessageBytes)
        {
            return null;
        }

        try
        {
            var message = JsonSerializer.Deserialize(json, MatchJsonContext.Default.MatchMessage);
            if (message is null)
            {
                return null;
            }

            var valid = message.Type switch
            {
                MessageTypes.Hello => message.Band is { Length: > 0 and <= 4096 } && ValidRound(message) && ValidMembers(message),
                MessageTypes.Status => message.Members is not null && ValidMembers(message),
                MessageTypes.Round => message.Round is not null && message.Stamp is not null && ValidRound(message),
                MessageTypes.Bye => true,
                _ => false,
            };
            return valid ? message : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool ValidRound(MatchMessage m) =>
        m.Round is null or (>= 1 and <= ActiveMatch.MaxRound) && m.Stamp is null or >= 0;

    private static bool ValidMembers(MatchMessage m) =>
        m.Members is null || m.Members.Count <= MaxMembers;
}

public sealed class MemberFlags
{
    [JsonPropertyName("a")] public bool Activated { get; set; }

    [JsonPropertyName("d")] public bool Down { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    MaxDepth = 8)]
[JsonSerializable(typeof(MatchMessage))]
[JsonSerializable(typeof(ActiveMatch))]
public sealed partial class MatchJsonContext : JsonSerializerContext;
