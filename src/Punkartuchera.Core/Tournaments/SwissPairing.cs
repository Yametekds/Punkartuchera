using Punkartuchera.Core.Maps;
using Punkartuchera.Core.Users;

namespace Punkartuchera.Core.Tournaments;

/// <summary>
/// Empareja una ronda. La primera, por sorteo; las siguientes, por sistema suizo: se ordena por la
/// clasificación y cada jugador se enfrenta al siguiente con su misma puntuación (o la más parecida)
/// contra el que todavía no haya jugado. Si sobran jugadores impares, descansa el peor clasificado
/// que aún no haya descansado. Se intenta además que no se crucen dos bandas del mismo jugador.
/// </summary>
public static class SwissPairing
{
    public static List<TournamentPairing> Pair(Tournament tournament, uint seed)
    {
        var active = tournament.Players.Where(p => !p.Dropped).ToList();
        if (active.Count < Tournament.MinPlayers)
        {
            throw new TournamentException("Hacen falta al menos dos jugadores para emparejar.");
        }

        List<TournamentPlayer> order;
        if (tournament.Rounds.Count == 0)
        {
            order = Shuffle(active, seed);
        }
        else
        {
            // Dentro de cada grupo de puntos se baraja también, para que el desempate no decida siempre los cruces.
            var random = new SeededRandom(seed);
            var standings = TournamentStandings.Compute(tournament).ToDictionary(r => r.Player.Id);
            order = active
                .Select(p => (Player: p, Row: standings[p.Id], Jitter: random.NextDouble()))
                .OrderByDescending(x => x.Row.Points)
                .ThenBy(x => x.Jitter)
                .Select(x => x.Player)
                .ToList();
        }

        var played = PreviousOpponents(tournament);
        var byes = tournament.Rounds
            .SelectMany(r => r.Pairings)
            .Where(p => p.IsBye)
            .Select(p => p.PlayerA)
            .ToHashSet();

        TournamentPlayer? bye = null;
        if (order.Count % 2 == 1)
        {
            bye = order.LastOrDefault(p => !byes.Contains(p.Id)) ?? order[^1];
            order.Remove(bye);
        }

        var pairs = TryPair(order, played, avoidRematch: true, avoidSamePlayer: true)
            ?? TryPair(order, played, avoidRematch: true, avoidSamePlayer: false)
            ?? TryPair(order, played, avoidRematch: false, avoidSamePlayer: true)
            ?? TryPair(order, played, avoidRematch: false, avoidSamePlayer: false)!;

        var result = pairs
            .Select((pair, index) => new TournamentPairing { Table = index + 1, PlayerA = pair.A.Id, PlayerB = pair.B.Id })
            .ToList();

        if (bye is not null)
        {
            result.Add(new TournamentPairing { Table = result.Count + 1, PlayerA = bye.Id });
        }

        return result;
    }

    public static List<TournamentPlayer> Shuffle(List<TournamentPlayer> players, uint seed)
    {
        var random = new SeededRandom(seed);
        var list = players.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(0, i);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    private static Dictionary<Guid, HashSet<Guid>> PreviousOpponents(Tournament tournament)
    {
        var map = tournament.Players.ToDictionary(p => p.Id, _ => new HashSet<Guid>());
        foreach (var pairing in tournament.Rounds.SelectMany(r => r.Pairings))
        {
            if (pairing.PlayerB is { } b && map.ContainsKey(pairing.PlayerA) && map.ContainsKey(b))
            {
                map[pairing.PlayerA].Add(b);
                map[b].Add(pairing.PlayerA);
            }
        }

        return map;
    }

    /// <summary>Empareja de arriba abajo con vuelta atrás. Devuelve null si no hay forma de cumplir las restricciones.</summary>
    private static List<(TournamentPlayer A, TournamentPlayer B)>? TryPair(
        List<TournamentPlayer> order,
        Dictionary<Guid, HashSet<Guid>> played,
        bool avoidRematch,
        bool avoidSamePlayer)
    {
        var pairs = new List<(TournamentPlayer, TournamentPlayer)>();
        var used = new bool[order.Count];
        var budget = 200_000;
        return Search(0) ? pairs : null;

        bool Search(int start)
        {
            if (--budget < 0)
            {
                return false;
            }

            var i = start;
            while (i < order.Count && used[i])
            {
                i++;
            }

            if (i >= order.Count)
            {
                return true;
            }

            used[i] = true;
            for (var j = i + 1; j < order.Count; j++)
            {
                if (used[j] || !Allowed(order[i], order[j]))
                {
                    continue;
                }

                used[j] = true;
                pairs.Add((order[i], order[j]));
                if (Search(i + 1))
                {
                    return true;
                }

                pairs.RemoveAt(pairs.Count - 1);
                used[j] = false;
            }

            used[i] = false;
            return false;
        }

        bool Allowed(TournamentPlayer a, TournamentPlayer b) =>
            (!avoidRematch || !played[a.Id].Contains(b.Id))
            && (!avoidSamePlayer || !SamePlayer(a, b));
    }

    private static bool SamePlayer(TournamentPlayer a, TournamentPlayer b) =>
        !string.IsNullOrWhiteSpace(a.Name)
        && !string.Equals(a.Name.Trim(), UserProfile.DefaultNickname, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Name.Trim(), b.Name.Trim(), StringComparison.CurrentCultureIgnoreCase);
}

public sealed class TournamentException(string message) : Exception(message);
