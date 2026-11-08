using System.Security.Cryptography;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Storage;
using Punkartuchera.Core.Users;

namespace Punkartuchera.Core.Tournaments;

/// <summary>Una banda que entra al torneo con el nombre de su jugador.</summary>
public sealed record TournamentEntry(Guid BandId, string PlayerName);

/// <summary>Lo que dice un QR de resultado una vez colocado en su mesa: ya en el orden de la mesa (A y B).</summary>
public sealed record ScannedResult(int Round, TournamentPairing Pairing, MatchOutcome Outcome, int ScoreA, int ScoreB);

/// <summary>
/// Monta y lleva un torneo suizo: inscripción de bandas, sorteo de la primera ronda,
/// resultados (escaneados o a mano), emparejamientos suizos y clasificación.
/// </summary>
public sealed class TournamentService(ITournamentStore store, IBandRepository bands, IUserProfileStore profiles)
{
    public async Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await store.GetAllAsync(cancellationToken)).OrderByDescending(t => t.UpdatedAt).ToList();

    public Task<Tournament?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        store.GetAsync(id, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        store.DeleteAsync(id, cancellationToken);

    /// <summary>
    /// Nombre de jugador que se propone para una banda: en las rivales, el nickname de quien la pasó;
    /// en las propias, el del usuario de este móvil.
    /// </summary>
    public async Task<string> SuggestPlayerNameAsync(Band band, CancellationToken cancellationToken = default)
    {
        if (band.IsReadOnly)
        {
            return UserProfile.CleanNickname(band.PlayerName) ?? UserProfile.DefaultNickname;
        }

        return (await profiles.GetAsync(cancellationToken)).DisplayName;
    }

    /// <summary>Crea el torneo con sus jugadores y sortea la primera ronda.</summary>
    /// <exception cref="TournamentException">Si faltan jugadores, sobran o alguna banda no existe.</exception>
    public async Task<Tournament> CreateAsync(
        string name,
        IReadOnlyList<TournamentEntry> entries,
        int? plannedRounds = null,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count < Tournament.MinPlayers)
        {
            throw new TournamentException("Un torneo necesita al menos dos bandas.");
        }

        if (entries.Count > Tournament.MaxPlayers)
        {
            throw new TournamentException($"Como mucho caben {Tournament.MaxPlayers} bandas.");
        }

        if (entries.Select(e => e.BandId).Distinct().Count() != entries.Count)
        {
            throw new TournamentException("La misma banda no puede entrar dos veces.");
        }

        var tournament = new Tournament { Name = CleanName(name) ?? $"Torneo {DateTime.Now:dd/MM}" };
        foreach (var entry in entries)
        {
            var band = await bands.GetAsync(entry.BandId, cancellationToken)
                ?? throw new TournamentException("Una de las bandas ya no está en el móvil.");
            tournament.Players.Add(new TournamentPlayer
            {
                Name = CleanName(entry.PlayerName) ?? await SuggestPlayerNameAsync(band, cancellationToken),
                BandId = band.Id,
                BandOriginId = OriginOf(band),
                BandName = band.Name,
                FactionId = band.FactionId,
                BandCost = band.Cost,
            });
        }

        tournament.PlannedRounds = Math.Clamp(
            plannedRounds ?? Tournament.RecommendedRounds(entries.Count),
            1,
            Math.Min(Tournament.MaxRounds, entries.Count - 1));
        tournament.Seed = NewSeed();
        tournament.Rounds.Add(new TournamentRound { Number = 1, Pairings = SwissPairing.Pair(tournament, tournament.Seed) });

        await SaveAsync(tournament, cancellationToken);
        return tournament;
    }

    /// <summary>Identificador con el que la mesa nombra a la banda en su QR de resultado.</summary>
    public static Guid OriginOf(Band band) => band.IsReadOnly && band.SourceId is { } source ? source : band.Id;

    public async Task<Tournament> RenamePlayerAsync(Guid tournamentId, Guid playerId, string name, CancellationToken cancellationToken = default)
    {
        var tournament = await LoadAsync(tournamentId, cancellationToken);
        var player = tournament.FindPlayer(playerId) ?? throw new TournamentException("Ese jugador no está en el torneo.");
        player.Name = CleanName(name) ?? player.Name;
        await SaveAsync(tournament, cancellationToken);
        return tournament;
    }

    /// <summary>Retira (o vuelve a meter) a un jugador. Cuenta a partir de la siguiente ronda.</summary>
    public async Task<Tournament> SetDroppedAsync(Guid tournamentId, Guid playerId, bool dropped, CancellationToken cancellationToken = default)
    {
        var tournament = await LoadAsync(tournamentId, cancellationToken);
        var player = tournament.FindPlayer(playerId) ?? throw new TournamentException("Ese jugador no está en el torneo.");
        player.Dropped = dropped;
        await SaveAsync(tournament, cancellationToken);
        return tournament;
    }

    /// <summary>Apunta (o corrige) el resultado de una mesa.</summary>
    public async Task<Tournament> RecordResultAsync(
        Guid tournamentId,
        int round,
        int table,
        MatchOutcome outcome,
        int scoreA,
        int scoreB,
        ResultSource source = ResultSource.Manual,
        CancellationToken cancellationToken = default)
    {
        var tournament = await LoadAsync(tournamentId, cancellationToken);
        RecordResult(tournament, round, table, outcome, scoreA, scoreB, source);
        await SaveAsync(tournament, cancellationToken);
        return tournament;
    }

    public static void RecordResult(Tournament tournament, int round, int table, MatchOutcome outcome, int scoreA, int scoreB, ResultSource source)
    {
        var pairing = tournament.Rounds.FirstOrDefault(r => r.Number == round)?.Pairings.FirstOrDefault(p => p.Table == table)
            ?? throw new TournamentException("Esa mesa no existe.");
        if (pairing.IsBye)
        {
            throw new TournamentException("En un descanso no hay resultado que apuntar.");
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new TournamentException("Resultado no válido.");
        }

        pairing.Result = new TournamentResult
        {
            Outcome = outcome,
            ScoreA = Math.Clamp(scoreA, 0, Tournament.MaxScore),
            ScoreB = Math.Clamp(scoreB, 0, Tournament.MaxScore),
            Source = source,
        };
    }

    public async Task<Tournament> ClearResultAsync(Guid tournamentId, int round, int table, CancellationToken cancellationToken = default)
    {
        var tournament = await LoadAsync(tournamentId, cancellationToken);
        var pairing = tournament.Rounds.FirstOrDefault(r => r.Number == round)?.Pairings.FirstOrDefault(p => p.Table == table);
        if (pairing is not null)
        {
            pairing.Result = null;
            await SaveAsync(tournament, cancellationToken);
        }

        return tournament;
    }

    /// <summary>
    /// Coloca un QR de resultado en su mesa. Busca primero en la ronda en juego y luego en las anteriores
    /// (por si se corrige una). No guarda nada: el organizador lo confirma antes.
    /// </summary>
    /// <exception cref="TournamentException">Si el QR no es válido o no corresponde a ninguna mesa.</exception>
    public static ScannedResult Locate(Tournament tournament, string qrText)
    {
        var code = MatchResultCode.Decode(qrText);
        var playerByOrigin = tournament.Players
            .GroupBy(p => p.BandOriginId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToHashSet());

        foreach (var round in Enumerable.Reverse(tournament.Rounds))
        {
            foreach (var pairing in round.Pairings.Where(p => !p.IsBye))
            {
                var aIsOwn = Matches(pairing.PlayerA, code.OwnBandOrigin) && Matches(pairing.PlayerB!.Value, code.RivalBandOrigin);
                var aIsRival = Matches(pairing.PlayerA, code.RivalBandOrigin) && Matches(pairing.PlayerB!.Value, code.OwnBandOrigin);
                if (!aIsOwn && !aIsRival)
                {
                    continue;
                }

                var (scoreA, scoreB) = aIsOwn ? (code.OwnScore, code.RivalScore) : (code.RivalScore, code.OwnScore);
                var outcome = code.Winner switch
                {
                    MatchResultWinner.Draw => MatchOutcome.Draw,
                    MatchResultWinner.Own => aIsOwn ? MatchOutcome.WinA : MatchOutcome.WinB,
                    _ => aIsOwn ? MatchOutcome.WinB : MatchOutcome.WinA,
                };
                return new ScannedResult(round.Number, pairing, outcome, scoreA, scoreB);
            }
        }

        throw new TournamentException("Ese resultado no es de ninguna mesa de este torneo. ¿Han jugado con las bandas inscritas?");

        bool Matches(Guid playerId, Guid origin) =>
            playerByOrigin.TryGetValue(origin, out var ids) && ids.Contains(playerId);
    }

    /// <summary>Empareja la siguiente ronda por sistema suizo. Hace falta que la actual esté completa.</summary>
    public async Task<Tournament> NextRoundAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await LoadAsync(tournamentId, cancellationToken);
        NextRound(tournament, NewSeed());
        await SaveAsync(tournament, cancellationToken);
        return tournament;
    }

    public static void NextRound(Tournament tournament, uint seed)
    {
        if (tournament.Finished)
        {
            throw new TournamentException("El torneo ya ha acabado.");
        }

        if (tournament.CurrentRound is { Complete: false })
        {
            throw new TournamentException("Faltan resultados de esta ronda.");
        }

        if (tournament.Rounds.Count >= Tournament.MaxRounds)
        {
            throw new TournamentException($"Como mucho se juegan {Tournament.MaxRounds} rondas.");
        }

        var number = tournament.Rounds.Count + 1;
        tournament.Rounds.Add(new TournamentRound { Number = number, Pairings = SwissPairing.Pair(tournament, seed) });
        tournament.PlannedRounds = Math.Max(tournament.PlannedRounds, number);
    }

    /// <summary>Deshace la última ronda si aún no tiene resultados (por ejemplo, para retirar a alguien antes de emparejar).</summary>
    public async Task<Tournament> UndoRoundAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await LoadAsync(tournamentId, cancellationToken);
        if (tournament.Rounds.Count <= 1 || tournament.CurrentRound!.Pairings.Any(p => p.Result is not null))
        {
            throw new TournamentException("Solo se puede deshacer una ronda que todavía no tenga resultados.");
        }

        tournament.Rounds.RemoveAt(tournament.Rounds.Count - 1);
        tournament.Finished = false;
        await SaveAsync(tournament, cancellationToken);
        return tournament;
    }

    public async Task<Tournament> FinishAsync(Guid tournamentId, bool finished = true, CancellationToken cancellationToken = default)
    {
        var tournament = await LoadAsync(tournamentId, cancellationToken);
        if (finished && tournament.CurrentRound is { Complete: false })
        {
            throw new TournamentException("Faltan resultados de esta ronda.");
        }

        tournament.Finished = finished;
        await SaveAsync(tournament, cancellationToken);
        return tournament;
    }

    private async Task<Tournament> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await store.GetAsync(id, cancellationToken) ?? throw new TournamentException("El torneo ya no existe.");

    private Task SaveAsync(Tournament tournament, CancellationToken cancellationToken)
    {
        tournament.UpdatedAt = DateTimeOffset.UtcNow;
        return store.SaveAsync(tournament, cancellationToken);
    }

    private static uint NewSeed() => (uint)RandomNumberGenerator.GetInt32(int.MaxValue);

    private static string? CleanName(string? value)
    {
        var clean = UserProfile.CleanNickname(value);
        return clean is { Length: > Tournament.MaxNameLength } ? clean[..Tournament.MaxNameLength].TrimEnd() : clean;
    }
}
