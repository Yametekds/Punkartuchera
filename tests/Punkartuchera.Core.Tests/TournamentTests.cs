using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Sharing;
using Punkartuchera.Core.Storage;
using Punkartuchera.Core.Tournaments;
using Punkartuchera.Core.Users;

namespace Punkartuchera.Core.Tests;

public class TournamentTests
{
    private static readonly RulebookCatalog Rules = new(RulebookLoader.LoadEmbedded());

    private readonly InMemoryBandRepository _bands = new();
    private readonly InMemoryUserProfileStore _profile = new();
    private readonly InMemoryTournamentStore _store = new();
    private readonly TournamentService _service;

    public TournamentTests()
    {
        _service = new TournamentService(_store, _bands, _profile);
    }

    [Fact]
    public async Task Exported_band_carries_the_sender_nickname_in_file_and_qr()
    {
        var senderBands = new InMemoryBandRepository();
        var senderProfile = new InMemoryUserProfileStore();
        await senderProfile.SaveAsync(new UserProfile { Nickname = "Chusma" });
        var band = new Band { Name = "Los Oxidados", FactionId = "chatarreros" };
        await senderBands.SaveAsync(band);
        var sender = new BandExchangeService(senderBands, new InMemoryPhotoStore(), Rules, senderProfile);
        var receiver = new BandExchangeService(_bands, new InMemoryPhotoStore(), Rules);

        var fromFile = await receiver.ImportAsync(new MemoryStream((await sender.ExportAsync(band.Id)).Content));
        var fromQr = await receiver.ImportQrAsync((await sender.ExportQrAsync(band.Id)).Text);

        Assert.Equal("Chusma", fromFile.PlayerName);
        Assert.Equal("Chusma", fromQr.PlayerName);
        Assert.Equal("Chusma", await _service.SuggestPlayerNameAsync(fromQr));
    }

    [Fact]
    public async Task Sender_without_nickname_exports_the_default_one()
    {
        var bands = new InMemoryBandRepository();
        var band = new Band { Name = "Sin nombre", FactionId = "chatarreros" };
        await bands.SaveAsync(band);
        var sender = new BandExchangeService(bands, new InMemoryPhotoStore(), Rules, new InMemoryUserProfileStore());

        var imported = await new BandExchangeService(_bands, new InMemoryPhotoStore(), Rules)
            .ImportAsync(new MemoryStream((await sender.ExportAsync(band.Id)).Content));

        Assert.Equal(UserProfile.DefaultNickname, imported.PlayerName);
    }

    [Fact]
    public async Task Own_bands_suggest_this_phone_nickname_and_player_name_can_be_changed()
    {
        await _profile.SaveAsync(new UserProfile { Nickname = "Organizador" });
        var t = await CreateAsync(4);
        Assert.All(t.Players, p => Assert.Equal("Organizador", p.Name));

        t = await _service.RenamePlayerAsync(t.Id, t.Players[0].Id, "  Paco  ");
        Assert.Equal("Paco", t.Players[0].Name);
    }

    [Fact]
    public async Task First_round_pairs_everyone_once_and_gives_a_bye_when_odd()
    {
        var t = await CreateAsync(7);
        var round = Assert.Single(t.Rounds);

        Assert.Equal(4, round.Pairings.Count);
        Assert.Single(round.Pairings, p => p.IsBye);
        var seen = round.Pairings.SelectMany(p => new[] { p.PlayerA, p.PlayerB }).OfType<Guid>().ToList();
        Assert.Equal(7, seen.Distinct().Count());
        Assert.Equal(3, t.PlannedRounds);
    }

    [Fact]
    public void First_round_draw_depends_on_the_seed_and_is_reproducible()
    {
        var t = new Tournament();
        for (var i = 0; i < 8; i++)
        {
            t.Players.Add(new TournamentPlayer { Name = $"J{i}" });
        }

        var a = SwissPairing.Pair(t, 12345).Select(p => (p.PlayerA, p.PlayerB)).ToList();
        var b = SwissPairing.Pair(t, 12345).Select(p => (p.PlayerA, p.PlayerB)).ToList();
        var c = Enumerable.Range(1, 20).Select(s => string.Join(",", SwissPairing.Pair(t, (uint)s).Select(p => p.PlayerA))).Distinct().Count();

        Assert.Equal(a, b);
        Assert.True(c > 10);
    }

    [Fact]
    public void Shuffle_is_fair()
    {
        // Cada jugador debería caer en cada posición más o menos igual de veces.
        var players = Enumerable.Range(0, 4).Select(i => new TournamentPlayer { Name = $"J{i}" }).ToList();
        var counts = new int[4, 4];
        for (uint seed = 1; seed <= 8000; seed++)
        {
            var order = SwissPairing.Shuffle(players, seed * 2654435761u);
            for (var pos = 0; pos < 4; pos++)
            {
                counts[players.IndexOf(order[pos]), pos]++;
            }
        }

        foreach (var count in counts)
        {
            Assert.InRange(count, 1800, 2200);
        }
    }

    [Fact]
    public void Swiss_rounds_never_repeat_opponents_while_possible_and_pair_by_points()
    {
        var t = new Tournament();
        for (var i = 0; i < 8; i++)
        {
            t.Players.Add(new TournamentPlayer { Name = $"J{i}" });
        }

        t.Rounds.Add(new TournamentRound { Number = 1, Pairings = SwissPairing.Pair(t, 7) });
        for (var r = 1; r <= 3; r++)
        {
            foreach (var p in t.CurrentRound!.Pairings)
            {
                // Gana siempre el de nombre más bajo: así hay una clasificación clara.
                var a = t.FindPlayer(p.PlayerA)!;
                var b = t.FindPlayer(p.PlayerB!.Value)!;
                TournamentService.RecordResult(t, r, p.Table, string.CompareOrdinal(a.Name, b.Name) < 0 ? MatchOutcome.WinA : MatchOutcome.WinB, 10, 5, ResultSource.Manual);
            }

            if (r < 3)
            {
                TournamentService.NextRound(t, (uint)(100 + r));
                var points = TournamentStandings.Compute(t).ToDictionary(s => s.Player.Id, s => s.Points);
                Assert.All(t.CurrentRound!.Pairings, p => Assert.Equal(points[p.PlayerA], points[p.PlayerB!.Value]));
            }
        }

        var matches = t.Rounds.SelectMany(r => r.Pairings).Select(p => string.Join("|", new[] { p.PlayerA, p.PlayerB!.Value }.Order())).ToList();
        Assert.Equal(matches.Count, matches.Distinct().Count());

        var standings = TournamentStandings.Compute(t);
        Assert.Equal("J0", standings[0].Player.Name);
        Assert.Equal(9, standings[0].Points);
        Assert.Equal(3, standings[0].Wins);
    }

    [Fact]
    public void Bye_goes_to_a_different_low_ranked_player_each_round()
    {
        var t = new Tournament();
        for (var i = 0; i < 5; i++)
        {
            t.Players.Add(new TournamentPlayer { Name = $"J{i}" });
        }

        t.Rounds.Add(new TournamentRound { Number = 1, Pairings = SwissPairing.Pair(t, 3) });
        for (var r = 1; r <= 4; r++)
        {
            foreach (var p in t.CurrentRound!.Pairings.Where(p => !p.IsBye))
            {
                TournamentService.RecordResult(t, r, p.Table, MatchOutcome.WinA, 3, 1, ResultSource.Manual);
            }

            if (r < 4)
            {
                TournamentService.NextRound(t, (uint)r);
            }
        }

        var byes = t.Rounds.Select(r => r.Pairings.Single(p => p.IsBye).PlayerA).ToList();
        Assert.Equal(byes.Count, byes.Distinct().Count());
    }

    [Fact]
    public void Two_bands_of_the_same_player_are_kept_apart_if_possible()
    {
        var t = new Tournament();
        t.Players.Add(new TournamentPlayer { Name = "Ana" });
        t.Players.Add(new TournamentPlayer { Name = "ana " });
        t.Players.Add(new TournamentPlayer { Name = "Luis" });
        t.Players.Add(new TournamentPlayer { Name = "Eva" });

        for (uint seed = 1; seed < 50; seed++)
        {
            var pairs = SwissPairing.Pair(t, seed);
            Assert.DoesNotContain(pairs, p => (p.PlayerA == t.Players[0].Id && p.PlayerB == t.Players[1].Id) || (p.PlayerA == t.Players[1].Id && p.PlayerB == t.Players[0].Id));
        }
    }

    [Fact]
    public void Standings_use_buchholz_then_scored_points()
    {
        var t = new Tournament();
        var a = new TournamentPlayer { Name = "A" };
        var b = new TournamentPlayer { Name = "B" };
        var c = new TournamentPlayer { Name = "C" };
        var d = new TournamentPlayer { Name = "D" };
        t.Players.AddRange([a, b, c, d]);
        t.Rounds.Add(new TournamentRound
        {
            Number = 1,
            Pairings =
            [
                new() { Table = 1, PlayerA = a.Id, PlayerB = b.Id, Result = new() { Outcome = MatchOutcome.WinA, ScoreA = 12, ScoreB = 4 } },
                new() { Table = 2, PlayerA = c.Id, PlayerB = d.Id, Result = new() { Outcome = MatchOutcome.Draw, ScoreA = 6, ScoreB = 6 } },
            ],
        });
        t.Rounds.Add(new TournamentRound
        {
            Number = 2,
            Pairings =
            [
                new() { Table = 1, PlayerA = a.Id, PlayerB = c.Id, Result = new() { Outcome = MatchOutcome.WinB, ScoreA = 3, ScoreB = 9 } },
                new() { Table = 2, PlayerA = b.Id, PlayerB = d.Id, Result = new() { Outcome = MatchOutcome.WinA, ScoreA = 8, ScoreB = 2 } },
            ],
        });

        var rows = TournamentStandings.Compute(t);

        Assert.Equal(["C", "A", "B", "D"], rows.Select(r => r.Player.Name));
        Assert.Equal(4, rows[0].Points);
        Assert.Equal(3, rows[1].Points);
        Assert.Equal(3, rows[2].Points);
        Assert.True(rows[1].Buchholz > rows[2].Buchholz);
        Assert.Equal(15, rows[1].ScoreFor);
    }

    [Fact]
    public async Task Next_round_needs_every_result()
    {
        var t = await CreateAsync(4);
        await Assert.ThrowsAsync<TournamentException>(() => _service.NextRoundAsync(t.Id));
    }

    [Fact]
    public async Task Result_qr_from_the_table_lands_on_its_pairing_in_either_order()
    {
        var t = await CreateAsync(4);
        var pairing = t.Rounds[0].Pairings[0];
        var a = t.FindPlayer(pairing.PlayerA)!;
        var b = t.FindPlayer(pairing.PlayerB!.Value)!;

        // La mesa juega en el móvil de B: su banda es la propia y la de A, la rival.
        var code = new MatchResultCode(b.BandOriginId, a.BandOriginId, OwnScore: 7, RivalScore: 11, MatchResultWinner.Rival).Encode();
        var scanned = TournamentService.Locate(t, code);

        Assert.Equal(1, scanned.Round);
        Assert.Same(pairing, scanned.Pairing);
        Assert.Equal(MatchOutcome.WinA, scanned.Outcome);
        Assert.Equal(11, scanned.ScoreA);
        Assert.Equal(7, scanned.ScoreB);
    }

    [Fact]
    public async Task Imported_rival_band_is_matched_by_its_original_id()
    {
        var original = Guid.NewGuid();
        var rival = new Band { Name = "Rival", FactionId = "chatarreros", Ownership = BandOwnership.Rival, SourceId = original, PlayerName = "Chusma" };
        var own = new Band { Name = "Mía", FactionId = "chatarreros" };
        await _bands.SaveAsync(rival);
        await _bands.SaveAsync(own);

        var t = await _service.CreateAsync("Liga", [new(rival.Id, ""), new(own.Id, "Yo")]);
        var code = new MatchResultCode(original, own.Id, 5, 5, MatchResultWinner.Draw).Encode();

        Assert.Equal("Chusma", t.Players.Single(p => p.BandId == rival.Id).Name);
        Assert.Equal(MatchOutcome.Draw, TournamentService.Locate(t, code).Outcome);
    }

    [Theory]
    [InlineData("hola")]
    [InlineData("PKR1:roto")]
    [InlineData("PKB1:ABC")]
    public void Bad_result_codes_are_rejected(string text) =>
        Assert.Throws<TournamentException>(() => MatchResultCode.Decode(text));

    [Fact]
    public void Tampered_result_code_is_rejected()
    {
        var code = new MatchResultCode(Guid.NewGuid(), Guid.NewGuid(), 3, 2, MatchResultWinner.Own).Encode();
        var tampered = code.Replace(":3:2:", ":9:2:");

        Assert.Throws<TournamentException>(() => MatchResultCode.Decode(tampered));
        Assert.Equal(3, MatchResultCode.Decode(code).OwnScore);
    }

    [Fact]
    public async Task Result_for_a_band_outside_the_tournament_is_rejected()
    {
        var t = await CreateAsync(4);
        var code = new MatchResultCode(Guid.NewGuid(), Guid.NewGuid(), 1, 0, MatchResultWinner.Own).Encode();
        Assert.Throws<TournamentException>(() => TournamentService.Locate(t, code));
    }

    [Fact]
    public async Task Dropped_players_are_not_paired_again()
    {
        var t = await CreateAsync(4);
        foreach (var p in t.Rounds[0].Pairings)
        {
            t = await _service.RecordResultAsync(t.Id, 1, p.Table, MatchOutcome.WinA, 1, 0);
        }

        var dropped = t.Players[0].Id;
        await _service.SetDroppedAsync(t.Id, dropped, true);
        t = await _service.NextRoundAsync(t.Id);

        Assert.DoesNotContain(t.Rounds[1].Pairings, p => p.Involves(dropped));
        Assert.Single(t.Rounds[1].Pairings, p => p.IsBye);
    }

    [Fact]
    public async Task Tournament_survives_a_round_trip_through_the_store()
    {
        var t = await CreateAsync(5);
        var loaded = await _service.GetAsync(t.Id);

        Assert.NotNull(loaded);
        Assert.Equal(5, loaded.Players.Count);
        Assert.Equal(t.Rounds[0].Pairings.Count, loaded.Rounds[0].Pairings.Count);
        Assert.Equal(t.Seed, loaded.Seed);
    }

    private async Task<Tournament> CreateAsync(int count)
    {
        var entries = new List<TournamentEntry>();
        for (var i = 0; i < count; i++)
        {
            var band = new Band { Name = $"Banda {i}", FactionId = "chatarreros" };
            await _bands.SaveAsync(band);
            entries.Add(new TournamentEntry(band.Id, string.Empty));
        }

        return await _service.CreateAsync("Torneo de prueba", entries);
    }
}
