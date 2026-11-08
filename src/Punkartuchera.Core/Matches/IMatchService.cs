namespace Punkartuchera.Core.Matches;

/// <summary>
/// Partida activa: ronda, quién ha actuado y quién ha caído en cada banda.
/// Puede jugarse en un solo móvil (las dos bandas en el mismo dispositivo) o en dos,
/// unidos con un código a través del relé en línea.
/// </summary>
public interface IMatchService
{
    /// <summary>Indica si hay relé configurado para crear partidas o entrar con código.</summary>
    bool SupportsOnlineMatches { get; }

    ActiveMatch? Current { get; }

    /// <summary>Estado de la conexión con el relé (solo en partidas en línea).</summary>
    RelayStatus Connection { get; }

    /// <summary>Se lanza cuando cambia la partida o la conexión, también por mensajes del rival.</summary>
    event Action? Changed;

    /// <summary>Recupera la partida que estaba en pantalla y, si es en línea, vuelve a conectar.</summary>
    Task<ActiveMatch?> RestoreAsync(CancellationToken cancellationToken = default);

    /// <summary>Todas las partidas guardadas en el dispositivo, de la más reciente a la más antigua.</summary>
    Task<IReadOnlyList<ActiveMatch>> GetSavedAsync(CancellationToken cancellationToken = default);

    /// <summary>Retoma una partida guardada. La que hubiera en pantalla queda guardada para luego.</summary>
    Task<ActiveMatch> ResumeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deja la partida de pantalla guardada para seguirla otro día.</summary>
    Task PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>Borra una partida guardada con sus fotos. En línea, la cierra también para el rival.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Empieza una partida en un móvil. La que hubiera en pantalla queda guardada para luego.</summary>
    Task<ActiveMatch> StartLocalAsync(Guid ownBandId, Guid rivalBandId, CancellationToken cancellationToken = default);

    /// <summary>Crea una partida en línea. El código para el rival queda en <see cref="ActiveMatch.Code"/>.</summary>
    Task<ActiveMatch> CreateOnlineAsync(Guid ownBandId, CancellationToken cancellationToken = default);

    Task<ActiveMatch> JoinOnlineAsync(string code, Guid ownBandId, CancellationToken cancellationToken = default);

    /// <summary>Vuelve a conectar con el relé si se perdió la conexión.</summary>
    Task ReconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Cambia de ronda. Al cambiar, nadie ha actuado todavía.</summary>
    Task SetRoundAsync(int round, CancellationToken cancellationToken = default);

    /// <summary>Cierra la última ronda: la partida queda acabada con el resultado del marcador.</summary>
    Task FinishAsync(CancellationToken cancellationToken = default);

    /// <summary>Puntos de un bando en el marcador. En línea, solo los propios.</summary>
    Task SetScoreAsync(MatchSide side, int score, CancellationToken cancellationToken = default);

    /// <summary>Cambia a la vez varios contadores de un bando (por ejemplo, gastar una dosis de la reserva). En línea, solo los propios.</summary>
    Task SetCountersAsync(MatchSide side, IReadOnlyDictionary<string, int> values, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca a un miembro como activado o como baja. En línea solo se pueden tocar los de la banda propia:
    /// los del rival los marca él en su móvil.
    /// </summary>
    Task SetMemberAsync(MatchSide side, int index, bool? activated = null, bool? down = null, CancellationToken cancellationToken = default);

    /// <summary>Termina la partida de pantalla y la borra con sus fotos. En línea, la cierra también para el rival.</summary>
    Task EndAsync(CancellationToken cancellationToken = default);

    /// <summary>Apunta el mapa de la partida (o lo quita con null). Solo en este dispositivo.</summary>
    Task SetMapAsync(MatchMap? map, CancellationToken cancellationToken = default);

    Task SetNotesAsync(string? notes, CancellationToken cancellationToken = default);

    /// <summary>Guarda una foto de la mesa (JPEG ya reducido) y devuelve su clave en el almacén de fotos.</summary>
    Task<string> AddPhotoAsync(byte[] jpeg, CancellationToken cancellationToken = default);

    Task RemovePhotoAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class MatchException(string message) : Exception(message);
