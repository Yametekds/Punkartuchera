using System.Text.Json.Serialization;

namespace Punkartuchera.Core.Tournaments;

/// <summary>
/// Un torneo por sistema suizo que gestiona un solo móvil. Cada mesa juega en su propio móvil
/// y, al acabar, el organizador recoge el resultado (escaneando un QR o apuntándolo a mano).
/// </summary>
public sealed class Tournament
{
    public const int MinPlayers = 2;
    public const int MaxPlayers = 128;
    public const int MaxNameLength = 60;
    public const int MaxRounds = 12;

    /// <summary>Tope de los puntos de una partida.</summary>
    public const int MaxScore = 999;

    /// <summary>Puntos de torneo por ganar, empatar y perder. El descanso cuenta como victoria.</summary>
    public const int WinPoints = 3;
    public const int DrawPoints = 1;
    public const int LossPoints = 0;

    /// <summary>Tope de Puntos de victoria por partida que recomienda el POTS (entre 15 y 20).</summary>
    public const int DefaultScoreCap = 20;
    public const int MinScoreCap = 10;
    public const int MaxScoreCap = 50;

    /// <summary>Puntos de victoria extra para quien Abate a toda la banda rival (Arrasamiento del POTS).</summary>
    public const int WipeoutBonus = 5;

    /// <summary>Tope de los Puntos de Escenario de una partida.</summary>
    public const int MaxScenarioPoints = 99;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public List<TournamentPlayer> Players { get; set; } = [];

    public List<TournamentRound> Rounds { get; set; } = [];

    /// <summary>Rondas previstas. Se puede acabar antes o añadir alguna más.</summary>
    public int PlannedRounds { get; set; }

    /// <summary>Semilla del sorteo de la primera ronda, para poder enseñar que fue al azar.</summary>
    public uint Seed { get; set; }

    public bool Finished { get; set; }

    /// <summary>
    /// Tope de Puntos de victoria por partida para la clasificación. Sin valor no hay tope
    /// (torneos guardados antes de que existiera).
    /// </summary>
    public int? ScoreCap { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public bool Started => Rounds.Count > 0;

    [JsonIgnore]
    public TournamentRound? CurrentRound => Rounds.Count > 0 ? Rounds[^1] : null;

    public TournamentPlayer? FindPlayer(Guid playerId) => Players.FirstOrDefault(p => p.Id == playerId);

    /// <summary>Rondas recomendadas para que salga un ganador claro: log2 del número de jugadores, redondeado hacia arriba.</summary>
    public static int RecommendedRounds(int players) =>
        players < 2 ? 1 : Math.Min(MaxRounds, (int)Math.Ceiling(Math.Log2(players)));

    /// <summary>
    /// Puntos de victoria de una partida tal como cuentan para la clasificación. Cada bando se queda
    /// como mucho con el tope; si los dos lo pasan, el ganador se lleva el tope más uno y el perdedor el tope.
    /// </summary>
    public static (int A, int B) CappedScores(TournamentResult result, int? cap)
    {
        if (cap is not { } max)
        {
            return (result.ScoreA, result.ScoreB);
        }

        if (result.ScoreA > max && result.ScoreB > max)
        {
            return result.Outcome switch
            {
                MatchOutcome.WinA => (max + 1, max),
                MatchOutcome.WinB => (max, max + 1),
                _ => (max, max),
            };
        }

        return (Math.Min(result.ScoreA, max), Math.Min(result.ScoreB, max));
    }
}

/// <summary>Un jugador del torneo con la banda que lleva.</summary>
public sealed class TournamentPlayer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nombre del jugador. Sale del nickname con el que se pasó la banda, pero el organizador lo puede cambiar.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Banda en el móvil del organizador.</summary>
    public Guid BandId { get; set; }

    /// <summary>
    /// Identificador de la banda en el móvil de su dueño (en las rivales, su SourceId).
    /// Es lo que trae el QR de resultado de la mesa para saber de quién es cada marcador.
    /// </summary>
    public Guid BandOriginId { get; set; }

    public string BandName { get; set; } = string.Empty;

    public string FactionId { get; set; } = string.Empty;

    public int? BandCost { get; set; }

    /// <summary>Abandonó el torneo: no se le empareja más, pero sus partidas siguen contando.</summary>
    public bool Dropped { get; set; }
}

public sealed class TournamentRound
{
    public int Number { get; set; }

    public List<TournamentPairing> Pairings { get; set; } = [];

    [JsonIgnore]
    public bool Complete => Pairings.All(p => p.IsBye || p.Result is not null);
}

/// <summary>Una mesa de una ronda. Sin jugador B es un descanso (bye).</summary>
public sealed class TournamentPairing
{
    public int Table { get; set; }

    public Guid PlayerA { get; set; }

    public Guid? PlayerB { get; set; }

    public TournamentResult? Result { get; set; }

    [JsonIgnore]
    public bool IsBye => PlayerB is null;

    public bool Involves(Guid playerId) => PlayerA == playerId || PlayerB == playerId;

    public Guid? OpponentOf(Guid playerId) =>
        PlayerA == playerId ? PlayerB : PlayerB == playerId ? PlayerA : null;
}

public sealed class TournamentResult
{
    public MatchOutcome Outcome { get; set; }

    public int ScoreA { get; set; }

    public int ScoreB { get; set; }

    /// <summary>Puntos de Escenario (cartas de Escenario y extras del organizador). Son el primer desempate.</summary>
    public int ScenarioA { get; set; }

    public int ScenarioB { get; set; }

    /// <summary>A arrasó a B: ScoreA ya lleva sumados los <see cref="Tournament.WipeoutBonus"/> puntos.</summary>
    public bool WipeoutA { get; set; }

    /// <summary>B arrasó a A: ScoreB ya lleva sumados los <see cref="Tournament.WipeoutBonus"/> puntos.</summary>
    public bool WipeoutB { get; set; }

    /// <summary>Cómo llegó: escaneado de la mesa o apuntado a mano.</summary>
    public ResultSource Source { get; set; }

    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum MatchOutcome
{
    WinA,
    WinB,
    Draw
}

public enum ResultSource
{
    Manual,
    Qr
}
