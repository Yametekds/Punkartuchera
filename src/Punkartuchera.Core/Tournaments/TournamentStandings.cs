namespace Punkartuchera.Core.Tournaments;

/// <summary>Una fila de la clasificación.</summary>
public sealed record StandingRow(
    int Position,
    TournamentPlayer Player,
    int Points,
    int Wins,
    int Draws,
    int Losses,
    int Byes,
    int ScenarioPoints,
    int ScoreFor,
    int ScoreAgainst)
{
    public int Played => Wins + Draws + Losses;

    public int ScoreDifference => ScoreFor - ScoreAgainst;
}

/// <summary>
/// Clasificación del torneo según el POTS. Orden: Puntos de torneo (victoria 3, empate 1, derrota 0,
/// descanso 3), luego Puntos de Escenario, luego diferencia de Puntos de victoria (a favor menos en contra)
/// y, al final, Puntos de victoria a favor. Los Puntos de victoria cuentan con el tope del torneo.
/// </summary>
public static class TournamentStandings
{
    public static IReadOnlyList<StandingRow> Compute(Tournament tournament)
    {
        var stats = tournament.Players.ToDictionary(p => p.Id, _ => new Tally());

        // El descanso de una ronda cuenta cuando la ronda se ha jugado entera, no al emparejarla.
        foreach (var pairing in tournament.Rounds.SelectMany(r => r.Pairings.Where(p => !p.IsBye || r.Complete)))
        {
            if (!stats.TryGetValue(pairing.PlayerA, out var a))
            {
                continue;
            }

            if (pairing.PlayerB is not { } bId)
            {
                a.Byes++;
                a.Points += Tournament.WinPoints;
                continue;
            }

            if (pairing.Result is not { } result || !stats.TryGetValue(bId, out var b))
            {
                continue;
            }

            var (scoreA, scoreB) = Tournament.CappedScores(result, tournament.ScoreCap);
            a.ScoreFor += scoreA;
            a.ScoreAgainst += scoreB;
            b.ScoreFor += scoreB;
            b.ScoreAgainst += scoreA;
            a.Scenario += result.ScenarioA;
            b.Scenario += result.ScenarioB;

            switch (result.Outcome)
            {
                case MatchOutcome.WinA:
                    a.Wins++;
                    b.Losses++;
                    a.Points += Tournament.WinPoints;
                    b.Points += Tournament.LossPoints;
                    break;
                case MatchOutcome.WinB:
                    b.Wins++;
                    a.Losses++;
                    b.Points += Tournament.WinPoints;
                    a.Points += Tournament.LossPoints;
                    break;
                default:
                    a.Draws++;
                    b.Draws++;
                    a.Points += Tournament.DrawPoints;
                    b.Points += Tournament.DrawPoints;
                    break;
            }
        }

        var ordered = tournament.Players
            .Select(p => (Player: p, Tally: stats[p.Id]))
            .OrderByDescending(x => x.Tally.Points)
            .ThenByDescending(x => x.Tally.Scenario)
            .ThenByDescending(x => x.Tally.ScoreFor - x.Tally.ScoreAgainst)
            .ThenByDescending(x => x.Tally.ScoreFor)
            .ThenBy(x => x.Player.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var rows = new List<StandingRow>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var (player, t) = ordered[i];
            var tiedWithPrevious = i > 0
                && rows[i - 1].Points == t.Points
                && rows[i - 1].ScenarioPoints == t.Scenario
                && rows[i - 1].ScoreDifference == t.ScoreFor - t.ScoreAgainst
                && rows[i - 1].ScoreFor == t.ScoreFor;
            var position = tiedWithPrevious ? rows[i - 1].Position : i + 1;
            rows.Add(new StandingRow(position, player, t.Points, t.Wins, t.Draws, t.Losses, t.Byes, t.Scenario, t.ScoreFor, t.ScoreAgainst));
        }

        return rows;
    }

    private sealed class Tally
    {
        public int Points;
        public int Wins;
        public int Draws;
        public int Losses;
        public int Byes;
        public int ScoreFor;
        public int ScoreAgainst;
        public int Scenario;
    }
}
