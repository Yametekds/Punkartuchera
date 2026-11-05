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

    /// <summary>Recupera la partida guardada en el dispositivo y, si es en línea, vuelve a conectar.</summary>
    Task<ActiveMatch?> RestoreAsync(CancellationToken cancellationToken = default);

    Task<ActiveMatch> StartLocalAsync(Guid ownBandId, Guid rivalBandId, CancellationToken cancellationToken = default);

    /// <summary>Crea una partida en línea. El código para el rival queda en <see cref="ActiveMatch.Code"/>.</summary>
    Task<ActiveMatch> CreateOnlineAsync(Guid ownBandId, CancellationToken cancellationToken = default);

    Task<ActiveMatch> JoinOnlineAsync(string code, Guid ownBandId, CancellationToken cancellationToken = default);

    /// <summary>Vuelve a conectar con el relé si se perdió la conexión.</summary>
    Task ReconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Cambia de ronda. Al cambiar, nadie ha actuado todavía.</summary>
    Task SetRoundAsync(int round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca a un miembro como activado o como baja. En línea solo se pueden tocar los de la banda propia:
    /// los del rival los marca él en su móvil.
    /// </summary>
    Task SetMemberAsync(MatchSide side, int index, bool? activated = null, bool? down = null, CancellationToken cancellationToken = default);

    /// <summary>Termina la partida. En línea, la cierra también para el rival.</summary>
    Task EndAsync(CancellationToken cancellationToken = default);
}

public sealed class MatchException(string message) : Exception(message);
