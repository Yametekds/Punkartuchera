using Punkartuchera.Relay.Contracts;

namespace Punkartuchera.Core.Matches;

public sealed class ActiveMatch
{
    /// <summary>Una partida dura seis Turnos (en la app, rondas).</summary>
    public const int MaxRound = 6;

    /// <summary>Tope de los puntos del marcador y de cualquier contador.</summary>
    public const int MaxCount = 999;

    /// <summary>Fotos de la mesa que se pueden guardar en cada partida.</summary>
    public const int MaxPhotos = 12;

    /// <summary>Largo máximo de las notas.</summary>
    public const int MaxNotes = 4000;

    public Guid Id { get; set; } = Guid.NewGuid();

    public MatchMode Mode { get; set; }

    public Guid OwnBandId { get; set; }

    /// <summary>En línea es nulo hasta que llega la banda del rival.</summary>
    public Guid? RivalBandId { get; set; }

    public string OwnBandName { get; set; } = string.Empty;

    public string? RivalBandName { get; set; }

    public int Round { get; set; } = 1;

    /// <summary>
    /// Reloj de Lamport de la ronda: sube con cada cambio. Si los dos móviles cambian la ronda
    /// a la vez (o uno estaba sin conexión), gana el cambio con el reloj más alto y, si empatan, la ronda más alta.
    /// </summary>
    public long RoundStamp { get; set; }

    /// <summary>Se cerró la última ronda: la partida ha acabado y queda el resultado.</summary>
    public bool Over { get; set; }

    /// <summary>Puntos de la banda propia en el marcador (escenario y victoria).</summary>
    public int OwnScore { get; set; }

    public int RivalScore { get; set; }

    /// <summary>
    /// Contadores de la banda propia: dosis de Ambrosía, Puntos de Mojo, reglas de un solo uso…
    /// Las claves son las de <see cref="MatchAid.Key"/>; las de un miembro llevan detrás "@" y su índice.
    /// Solo se guardan los que han cambiado: el resto vale lo que diga la ayuda.
    /// </summary>
    public Dictionary<string, int> OwnCounters { get; set; } = [];

    public Dictionary<string, int> RivalCounters { get; set; } = [];

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Último cambio, para ordenar la lista de partidas guardadas.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Mapa en el que se juega, para poder volver a generarlo igual. Solo en este dispositivo.</summary>
    public MatchMap? Map { get; set; }

    /// <summary>Fotos de la mesa para recordar dónde estaba cada miniatura. Solo en este dispositivo.</summary>
    public List<MatchPhoto> Photos { get; set; } = [];

    /// <summary>Notas libres de la partida. Solo en este dispositivo.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Miembros propios en el orden en que se mandaron al rival. Se congela al empezar,
    /// así los índices de los mensajes siguen valiendo aunque se edite la banda durante la partida.
    /// </summary>
    public List<MatchMember> Own { get; set; } = [];

    public List<MatchMember> Rival { get; set; } = [];

    /// <summary>Llave para el relé. Solo en partidas en línea; no sale del dispositivo salvo hacia el relé.</summary>
    public RelayTicket? Ticket { get; set; }

    /// <summary>La banda propia en formato QR (sin fotos ni balas), tal como se manda al rival.</summary>
    public string? OwnPayload { get; set; }

    /// <summary>Última banda recibida del rival, para no volver a importarla si no cambia.</summary>
    public string? RivalPayload { get; set; }

    /// <summary>Identificador de la banda del rival en su móvil. No puede cambiar durante la partida.</summary>
    public Guid? RivalSourceId { get; set; }

    /// <summary>El rival cerró la partida o el relé ya no la reconoce.</summary>
    public bool ClosedByRival { get; set; }

    public string? Code => Ticket?.Code;

    public bool IsOnline => Mode != MatchMode.Local;

    public bool HasRival => RivalBandId is not null;

    public List<MatchMember> MembersOf(MatchSide side) => side == MatchSide.Own ? Own : Rival;

    public Dictionary<string, int> CountersOf(MatchSide side) => side == MatchSide.Own ? OwnCounters : RivalCounters;

    public int ScoreOf(MatchSide side) => side == MatchSide.Own ? OwnScore : RivalScore;
}

/// <summary>
/// Mapa de la partida: el código que se ve y el resto de la consulta del generador
/// (mesa, despliegue y chatarra), que con el mismo código dan el mismo mapa.
/// </summary>
public sealed class MatchMap
{
    public string Code { get; set; } = string.Empty;

    /// <summary>Consulta de la página del mapa, sin el "?" (codigo=…&amp;mesa=…&amp;despliegue=…&amp;terreno=…).</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Enlace relativo a la página del mapa.</summary>
    public string Link => $"mapa?{Query}";

    /// <summary>
    /// Mapa a partir de la consulta del generador. Devuelve null si no lleva un código válido.
    /// Solo se quedan los parámetros que entiende el generador.
    /// </summary>
    public static MatchMap? FromQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var parts = new List<string>();
        string? code = null;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var name = pair[..separator];
            var value = Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' '));
            if (name == "codigo")
            {
                code = Maps.MapSeed.Normalize(value);
                value = code ?? string.Empty;
            }
            else if (name is not ("mesa" or "despliegue" or "terreno") || value.Length > 300)
            {
                continue;
            }

            parts.Add($"{name}={Uri.EscapeDataString(value)}");
        }

        return code is null ? null : new MatchMap { Code = code, Query = string.Join('&', parts) };
    }

    /// <summary>Mapa con solo el código (por ejemplo, el que dicta el rival): el resto, lo de por defecto.</summary>
    public static MatchMap? FromCode(string? code) =>
        Maps.MapSeed.Normalize(code) is { } clean ? new MatchMap { Code = clean, Query = $"codigo={clean}" } : null;
}

/// <summary>Foto de la mesa guardada en el almacén de fotos.</summary>
public sealed class MatchPhoto
{
    public string Key { get; set; } = string.Empty;

    /// <summary>Ronda en la que se hizo.</summary>
    public int Round { get; set; }

    public DateTimeOffset TakenAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MatchMember
{
    /// <summary>Miembro de la banda en este dispositivo, si se conoce.</summary>
    public Guid? MemberId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    /// <summary>Ya ha actuado esta ronda.</summary>
    public bool Activated { get; set; }

    /// <summary>Fuera de combate.</summary>
    public bool Down { get; set; }
}

public enum MatchMode
{
    Local,
    Host,
    Guest,
}

public enum MatchSide
{
    Own,
    Rival,
}

public enum RelayStatus
{
    Offline,
    Connecting,
    Online,
    Reconnecting,
}
