namespace Punkartuchera.Relay.Matches;

/// <summary>
/// Una partida guardada en el relé. No contiene nada del juego: solo quién puede hablar con quién y hasta cuándo.
/// Las llaves de los jugadores se guardan como huella SHA-256, nunca en claro.
/// </summary>
public sealed class MatchRecord
{
    public required string Code { get; init; }

    public required string HostId { get; init; }

    public required string HostTokenHash { get; init; }

    public string? GuestId { get; set; }

    public string? GuestTokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Versión del registro (ETag en Table Storage). Si dos jugadores intentan entrar a la vez,
    /// solo se guarda el primero: el segundo encuentra la versión cambiada.
    /// </summary>
    public string? Version { get; set; }

    public bool IsFull => GuestId is not null;

    public string? OpponentOf(string playerId) =>
        playerId == HostId ? GuestId : playerId == GuestId ? HostId : null;
}
