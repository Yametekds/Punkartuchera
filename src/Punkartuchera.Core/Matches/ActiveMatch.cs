using Punkartuchera.Relay.Contracts;

namespace Punkartuchera.Core.Matches;

public sealed class ActiveMatch
{
    public const int MaxRound = 99;

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

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

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
