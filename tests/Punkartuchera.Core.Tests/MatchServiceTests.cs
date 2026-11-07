using Punkartuchera.Core.Matches;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Sharing;
using Punkartuchera.Core.Storage;
using Punkartuchera.Relay.Contracts;

namespace Punkartuchera.Core.Tests;

public class MatchServiceTests
{
    private static readonly RulebookCatalog Rules = new(RulebookLoader.LoadEmbedded());

    private readonly FakeRelayServer _server = new();
    private readonly Device _host;
    private readonly Device _guest;

    public MatchServiceTests()
    {
        _host = new Device(_server);
        _guest = new Device(_server);
    }

    [Fact]
    public async Task Players_get_each_others_band_without_ammo()
    {
        var hostBand = await _host.AddBandAsync("Los Oxidados", "Morro", "Chispas");
        var guestBand = await _guest.AddBandAsync("Las Ratas", "Roedora");

        var created = await _host.Matches.CreateOnlineAsync(hostBand.Id);
        Assert.Equal(MatchMode.Host, created.Mode);
        Assert.False(created.HasRival);

        await _guest.Matches.JoinOnlineAsync(created.Code!.ToLowerInvariant(), guestBand.Id);

        var atGuest = _guest.Matches.Current!;
        var atHost = _host.Matches.Current!;
        Assert.Equal("Los Oxidados", atGuest.RivalBandName);
        Assert.Equal(["Morro", "Chispas"], atGuest.Rival.Select(m => m.Name));
        Assert.Equal("Las Ratas", atHost.RivalBandName);
        Assert.Equal(RelayStatus.Online, _guest.Matches.Connection);

        var copy = await _guest.Bands.GetAsync(atGuest.RivalBandId!.Value);
        Assert.True(copy!.IsReadOnly);
        Assert.All(copy.Members, m => Assert.Equal(0, m.Ammo));
    }

    [Fact]
    public async Task Member_status_and_rounds_reach_the_rival()
    {
        await StartOnlineAsync();

        await _host.Matches.SetMemberAsync(MatchSide.Own, 1, down: true);
        await _host.Matches.SetMemberAsync(MatchSide.Own, 0, activated: true);
        Assert.True(_guest.Matches.Current!.Rival[1].Down);
        Assert.True(_guest.Matches.Current.Rival[0].Activated);

        await _guest.Matches.SetRoundAsync(2);

        Assert.Equal(2, _host.Matches.Current!.Round);
        Assert.False(_host.Matches.Current.Own[0].Activated);
        Assert.False(_guest.Matches.Current.Rival[0].Activated);
        Assert.True(_guest.Matches.Current.Rival[1].Down);
    }

    [Fact]
    public async Task Rival_members_can_only_be_marked_by_their_owner()
    {
        await StartOnlineAsync();

        await Assert.ThrowsAsync<MatchException>(() => _guest.Matches.SetMemberAsync(MatchSide.Rival, 0, down: true));
    }

    [Fact]
    public async Task Reconnecting_catches_up_with_what_was_missed()
    {
        await StartOnlineAsync();

        await _guest.Relay.DropAsync();
        await _host.Matches.SetRoundAsync(3);
        await _host.Matches.SetMemberAsync(MatchSide.Own, 0, down: true);
        Assert.Equal(1, _guest.Matches.Current!.Round);
        Assert.Equal(RelayStatus.Offline, _guest.Matches.Connection);

        await _guest.Matches.ReconnectAsync();

        Assert.Equal(3, _guest.Matches.Current.Round);
        Assert.True(_guest.Matches.Current.Rival[0].Down);
    }

    [Fact]
    public async Task Concurrent_round_changes_settle_on_the_same_round()
    {
        await StartOnlineAsync();

        await _guest.Relay.DropAsync();
        await _host.Matches.SetRoundAsync(2);
        await _guest.Matches.SetRoundAsync(4);
        Assert.Equal(2, _host.Matches.Current!.Round);
        await _guest.Matches.ReconnectAsync();

        // Mismo reloj en los dos: gana la ronda más alta, y los dos acaban igual.
        Assert.Equal(4, _guest.Matches.Current!.Round);
        Assert.Equal(4, _host.Matches.Current!.Round);
    }

    [Fact]
    public async Task Ending_the_match_closes_it_for_the_rival()
    {
        await StartOnlineAsync();

        await _host.Matches.EndAsync();

        Assert.Null(_host.Matches.Current);
        Assert.True(_guest.Matches.Current!.ClosedByRival);
        Assert.Equal(RelayStatus.Offline, _guest.Matches.Connection);
        Assert.Null(await _host.Store.GetCurrentIdAsync());
        Assert.Empty(await _host.Store.GetAllAsync());
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("{\"type\":\"hack\"}")]
    [InlineData("{\"type\":\"round\",\"round\":500,\"stamp\":99}")]
    [InlineData("{\"type\":\"round\",\"round\":-1,\"stamp\":99}")]
    [InlineData("{\"type\":\"hello\",\"band\":\"PKB1:ESTONOESUNABANDA\"}")]
    [InlineData("{\"type\":\"status\"}")]
    [InlineData("{\"type\":\"round\",\"round\":7,\"stamp\":99}")]
    [InlineData("{\"type\":\"status\",\"members\":[],\"score\":-3}")]
    [InlineData("{\"type\":\"status\",\"members\":[],\"counters\":{\"ambrosia\":5000}}")]
    public async Task Bad_messages_from_the_rival_are_ignored(string message)
    {
        await StartOnlineAsync();
        var before = System.Text.Json.JsonSerializer.Serialize(_guest.Matches.Current, MatchJsonContext.Default.ActiveMatch!);

        await _guest.Relay.DeliverAsync(message);

        var after = System.Text.Json.JsonSerializer.Serialize(_guest.Matches.Current, MatchJsonContext.Default.ActiveMatch!);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Too_many_members_in_a_status_are_rejected()
    {
        await StartOnlineAsync();
        var flags = string.Join(',', Enumerable.Repeat("{\"a\":true,\"d\":true}", MatchMessage.MaxMembers + 1));

        await _guest.Relay.DeliverAsync($"{{\"type\":\"status\",\"members\":[{flags}]}}");

        Assert.All(_guest.Matches.Current!.Rival, m => Assert.False(m.Down));
    }

    [Fact]
    public async Task The_rival_cannot_swap_bands_mid_match()
    {
        await StartOnlineAsync();
        var other = await _host.AddBandAsync("Otra banda", "Intruso");
        var payload = BandQrCodec.Encode(other, Rules.Stats.Select(s => s.Key).ToList()).Text;
        var bandsBefore = (await _guest.Bands.GetAllAsync()).Count;

        await _guest.Relay.DeliverAsync(MatchMessage.Serialize(new MatchMessage { Type = MessageTypes.Hello, Band = payload }));

        Assert.Equal("Los Oxidados", _guest.Matches.Current!.RivalBandName);
        Assert.Equal(bandsBefore, (await _guest.Bands.GetAllAsync()).Count);
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("ABCDE0")]
    [InlineData("ABCDEFG")]
    public async Task Malformed_codes_are_rejected_before_asking_the_relay(string code)
    {
        var band = await _guest.AddBandAsync("Las Ratas", "Roedora");

        await Assert.ThrowsAsync<MatchException>(() => _guest.Matches.JoinOnlineAsync(code, band.Id));
        Assert.Equal(0, _server.JoinAttempts);
    }

    [Fact]
    public async Task Unknown_code_gives_a_clear_error()
    {
        var band = await _guest.AddBandAsync("Las Ratas", "Roedora");

        var error = await Assert.ThrowsAsync<MatchException>(() => _guest.Matches.JoinOnlineAsync("ABCDEF", band.Id));
        Assert.Contains("código", error.Message);
    }

    [Fact]
    public async Task Rival_bands_cannot_play_online()
    {
        var band = await _host.AddBandAsync("Prestada", "Alguien");
        band.Ownership = BandOwnership.Rival;
        await _host.Bands.SaveAsync(band);

        await Assert.ThrowsAsync<MatchException>(() => _host.Matches.CreateOnlineAsync(band.Id));
    }

    [Fact]
    public async Task Local_match_tracks_both_bands_and_survives_a_reload()
    {
        var own = await _host.AddBandAsync("Los Oxidados", "Morro", "Chispas");
        var rival = await _host.AddBandAsync("Las Ratas", "Roedora");

        await _host.Matches.StartLocalAsync(own.Id, rival.Id);
        await _host.Matches.SetMemberAsync(MatchSide.Rival, 0, down: true);
        await _host.Matches.SetMemberAsync(MatchSide.Own, 0, activated: true);
        await _host.Matches.SetRoundAsync(2);

        var reloaded = new MatchService(_host.Store, new FakeRelay(_server), _host.Bands, _host.Exchange, _host.Photos);
        var match = await reloaded.RestoreAsync();

        Assert.Equal(MatchMode.Local, match!.Mode);
        Assert.Equal(2, match.Round);
        Assert.True(match.Rival[0].Down);
        Assert.False(match.Own[0].Activated);
    }

    [Fact]
    public async Task Without_a_relay_only_local_matches_are_offered()
    {
        var bands = new InMemoryBandRepository();
        var photos = new InMemoryPhotoStore();
        var service = new MatchService(new InMemorySavedMatchStore(), new DisabledMatchRelay(), bands, new BandExchangeService(bands, photos, Rules), photos);

        Assert.False(service.SupportsOnlineMatches);
        await Assert.ThrowsAsync<MatchException>(() => service.CreateOnlineAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Score_and_counters_reach_the_rival_but_only_the_owner_changes_them()
    {
        await StartOnlineAsync();

        await _host.Matches.SetScoreAsync(MatchSide.Own, 3);
        await _host.Matches.SetCountersAsync(MatchSide.Own, new Dictionary<string, int> { ["ambrosia"] = 4, ["dosis@0"] = 1 });

        var atGuest = _guest.Matches.Current!;
        Assert.Equal(3, atGuest.RivalScore);
        Assert.Equal(4, atGuest.RivalCounters["ambrosia"]);
        Assert.Equal(1, atGuest.RivalCounters["dosis@0"]);
        Assert.Equal(0, atGuest.OwnScore);

        await Assert.ThrowsAsync<MatchException>(() => _guest.Matches.SetScoreAsync(MatchSide.Rival, 0));
        await Assert.ThrowsAsync<MatchException>(() => _guest.Matches.SetCountersAsync(MatchSide.Rival, new Dictionary<string, int> { ["ambrosia"] = 0 }));
    }

    [Fact]
    public async Task A_match_lasts_six_rounds_and_closing_the_last_one_ends_it_for_both()
    {
        await StartOnlineAsync();

        await _host.Matches.SetRoundAsync(9);
        Assert.Equal(ActiveMatch.MaxRound, _host.Matches.Current!.Round);
        Assert.Equal(6, ActiveMatch.MaxRound);

        await _guest.Matches.FinishAsync();
        Assert.True(_guest.Matches.Current!.Over);
        Assert.True(_host.Matches.Current.Over);

        // Reabrir la última ronda deshace el final en los dos móviles.
        await _host.Matches.SetRoundAsync(6);
        Assert.False(_host.Matches.Current.Over);
        Assert.False(_guest.Matches.Current.Over);
    }

    [Fact]
    public async Task Local_match_keeps_both_scores_and_counters()
    {
        var own = await _host.AddBandAsync("Los Oxidados", "Morro");
        var rival = await _host.AddBandAsync("Las Ratas", "Roedora");
        await _host.Matches.StartLocalAsync(own.Id, rival.Id);

        await _host.Matches.SetScoreAsync(MatchSide.Rival, 2);
        await _host.Matches.SetCountersAsync(MatchSide.Rival, new Dictionary<string, int> { ["mojo"] = 6 });

        var reloaded = new MatchService(_host.Store, new FakeRelay(_server), _host.Bands, _host.Exchange, _host.Photos);
        var match = await reloaded.RestoreAsync();
        Assert.Equal(2, match!.RivalScore);
        Assert.Equal(6, match.RivalCounters["mojo"]);
    }

    [Fact]
    public async Task Starting_another_match_keeps_the_first_one_saved_to_resume_later()
    {
        var own = await _host.AddBandAsync("Los Oxidados", "Morro", "Chispas");
        var rival = await _host.AddBandAsync("Las Ratas", "Roedora");
        var third = await _host.AddBandAsync("Los Mutis", "Bulto");

        var first = await _host.Matches.StartLocalAsync(own.Id, rival.Id);
        await _host.Matches.SetRoundAsync(3);
        await _host.Matches.SetMemberAsync(MatchSide.Rival, 0, down: true);
        var second = await _host.Matches.StartLocalAsync(own.Id, third.Id);

        var saved = await _host.Matches.GetSavedAsync();
        Assert.Equal([second.Id, first.Id], saved.Select(m => m.Id));

        var resumed = await _host.Matches.ResumeAsync(first.Id);
        Assert.Equal(3, resumed.Round);
        Assert.True(resumed.Rival[0].Down);

        // Al abrir la app otra vez sale la última que estaba en pantalla.
        var reloaded = new MatchService(_host.Store, new FakeRelay(_server), _host.Bands, _host.Exchange, _host.Photos);
        Assert.Equal(first.Id, (await reloaded.RestoreAsync())!.Id);
        Assert.Equal(2, (await reloaded.GetSavedAsync()).Count);
    }

    [Fact]
    public async Task Leaving_a_match_for_later_keeps_it_saved_but_off_screen()
    {
        var own = await _host.AddBandAsync("Los Oxidados", "Morro");
        var rival = await _host.AddBandAsync("Las Ratas", "Roedora");
        var match = await _host.Matches.StartLocalAsync(own.Id, rival.Id);

        await _host.Matches.PauseAsync();

        Assert.Null(_host.Matches.Current);
        Assert.Null(await _host.Store.GetCurrentIdAsync());
        Assert.Equal(match.Id, Assert.Single(await _host.Matches.GetSavedAsync()).Id);
    }

    [Fact]
    public async Task Map_photos_and_notes_are_saved_with_the_match_and_deleted_with_it()
    {
        var own = await _host.AddBandAsync("Los Oxidados", "Morro");
        var rival = await _host.AddBandAsync("Las Ratas", "Roedora");
        var match = await _host.Matches.StartLocalAsync(own.Id, rival.Id);
        await _host.Matches.SetRoundAsync(2);

        await _host.Matches.SetMapAsync(MatchMap.FromQuery("codigo=k7q2xm&mesa=90x90&despliegue=v&terreno=ruina-2&otra=x"));
        await _host.Matches.SetNotesAsync("Morro tiene el bidón.  ");
        var key = await _host.Matches.AddPhotoAsync([1, 2, 3]);

        var saved = (await _host.Store.GetAsync(match.Id))!;
        Assert.Equal("K7Q2XM", saved.Map!.Code);
        Assert.Equal("mapa?codigo=K7Q2XM&mesa=90x90&despliegue=v&terreno=ruina-2", saved.Map.Link);
        Assert.Equal("Morro tiene el bidón.", saved.Notes);
        var photo = Assert.Single(saved.Photos);
        Assert.Equal(key, photo.Key);
        Assert.Equal(2, photo.Round);
        Assert.NotNull(await _host.Photos.GetAsync(key));

        await _host.Matches.PauseAsync();
        await _host.Matches.DeleteAsync(match.Id);

        Assert.Empty(await _host.Matches.GetSavedAsync());
        Assert.Null(await _host.Photos.GetAsync(key));
    }

    [Fact]
    public async Task A_match_holds_a_limited_number_of_photos()
    {
        var own = await _host.AddBandAsync("Los Oxidados", "Morro");
        var rival = await _host.AddBandAsync("Las Ratas", "Roedora");
        await _host.Matches.StartLocalAsync(own.Id, rival.Id);

        for (var i = 0; i < ActiveMatch.MaxPhotos; i++)
        {
            await _host.Matches.AddPhotoAsync([1]);
        }

        await Assert.ThrowsAsync<MatchException>(() => _host.Matches.AddPhotoAsync([1]));

        var first = _host.Matches.Current!.Photos[0].Key;
        await _host.Matches.RemovePhotoAsync(first);
        Assert.Equal(ActiveMatch.MaxPhotos - 1, _host.Matches.Current.Photos.Count);
        Assert.Null(await _host.Photos.GetAsync(first));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("mesa=90x90")]
    [InlineData("codigo=")]
    public void A_map_needs_a_code(string? query) => Assert.Null(MatchMap.FromQuery(query));

    [Fact]
    public void A_dictated_code_alone_is_a_map()
    {
        var map = MatchMap.FromCode(" k7q2-xm ")!;
        Assert.Equal("K7Q2XM", map.Code);
        Assert.Equal("mapa?codigo=K7Q2XM", map.Link);
    }

    [Fact]
    public async Task Resuming_an_online_match_reconnects_and_catches_up()
    {
        await StartOnlineAsync();
        var online = _guest.Matches.Current!;
        var local = await _guest.AddBandAsync("Los Mutis", "Bulto");
        var other = await _guest.AddBandAsync("Las Hienas", "Risas");
        await _guest.Matches.StartLocalAsync(local.Id, other.Id);
        Assert.Equal(RelayStatus.Offline, _guest.Matches.Connection);

        await _host.Matches.SetRoundAsync(4);
        await _guest.Matches.ResumeAsync(online.Id);

        Assert.Equal(RelayStatus.Online, _guest.Matches.Connection);
        Assert.Equal(4, _guest.Matches.Current!.Round);
    }

    private async Task StartOnlineAsync()
    {
        var hostBand = await _host.AddBandAsync("Los Oxidados", "Morro", "Chispas");
        var guestBand = await _guest.AddBandAsync("Las Ratas", "Roedora");
        var created = await _host.Matches.CreateOnlineAsync(hostBand.Id);
        await _guest.Matches.JoinOnlineAsync(created.Code!, guestBand.Id);
    }

    /// <summary>Un móvil: sus bandas, su partida guardada y su conexión con el relé.</summary>
    private sealed class Device
    {
        public Device(FakeRelayServer server)
        {
            Exchange = new BandExchangeService(Bands, Photos, Rules);
            Relay = new FakeRelay(server);
            Matches = new MatchService(Store, Relay, Bands, Exchange, Photos);
        }

        public InMemoryBandRepository Bands { get; } = new();

        public InMemoryPhotoStore Photos { get; } = new();

        public InMemorySavedMatchStore Store { get; } = new();

        public BandExchangeService Exchange { get; }

        public FakeRelay Relay { get; }

        public MatchService Matches { get; }

        public async Task<Band> AddBandAsync(string name, params string[] members)
        {
            var band = new Band
            {
                Name = name,
                FactionId = "chatarreros",
                Members = members.Select(m => new Miniature { Name = m, Role = "Pandillero", Ammo = 5 }).ToList(),
            };
            await Bands.SaveAsync(band);
            return band;
        }
    }

    /// <summary>Relé en memoria: empareja jugadores por código y entrega los mensajes al momento.</summary>
    private sealed class FakeRelayServer
    {
        private readonly Dictionary<string, List<FakeRelay>> _matches = [];

        public int JoinAttempts { get; private set; }

        public RelayTicket Create(FakeRelay host)
        {
            var code = MatchCode.Generate();
            _matches[code] = [host];
            return new RelayTicket(code, Guid.NewGuid().ToString("N"), "llave-anfitrion", true, DateTimeOffset.UtcNow.AddHours(1));
        }

        public RelayTicket? Join(string code, FakeRelay guest)
        {
            JoinAttempts++;
            if (!_matches.TryGetValue(code, out var players) || players.Count != 1)
            {
                return null;
            }

            players.Add(guest);
            return new RelayTicket(code, Guid.NewGuid().ToString("N"), "llave-rival", false, DateTimeOffset.UtcNow.AddHours(1));
        }

        public bool Exists(string code) => _matches.ContainsKey(code);

        public FakeRelay? OpponentOf(string code, FakeRelay player) =>
            _matches.TryGetValue(code, out var players) ? players.FirstOrDefault(p => p != player) : null;

        public void Remove(string code) => _matches.Remove(code);
    }

    private sealed class FakeRelay(FakeRelayServer server) : IMatchRelay
    {
        private Func<string, Task>? _onMessage;
        private Func<RelayStatus, Task>? _onStatus;

        public bool IsConfigured => true;

        public bool Connected => _onMessage is not null;

        public Task<RelayTicket> CreateAsync(CancellationToken cancellationToken = default) => Task.FromResult(server.Create(this));

        public Task<RelayTicket?> JoinAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult(server.Join(code, this));

        public async Task ConnectAsync(RelayTicket ticket, Func<string, Task> onMessage, Func<RelayStatus, Task> onStatus, CancellationToken cancellationToken = default)
        {
            if (!server.Exists(ticket.Code))
            {
                throw new RelayRejectedException();
            }

            _onMessage = onMessage;
            _onStatus = onStatus;
            await onStatus(RelayStatus.Online);
        }

        public async Task<bool> SendAsync(RelayTicket ticket, string message, CancellationToken cancellationToken = default)
        {
            if (!server.Exists(ticket.Code))
            {
                throw new RelayRejectedException();
            }

            if (server.OpponentOf(ticket.Code, this) is not { } opponent)
            {
                return false;
            }

            if (opponent.Connected)
            {
                await opponent.DeliverAsync(message);
            }

            return true;
        }

        public async Task LeaveAsync(RelayTicket ticket, CancellationToken cancellationToken = default)
        {
            var opponent = server.OpponentOf(ticket.Code, this);
            server.Remove(ticket.Code);
            if (opponent is { Connected: true })
            {
                await opponent.DeliverAsync("{\"type\":\"bye\"}");
            }
        }

        public Task DisconnectAsync()
        {
            Disconnect();
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            _onMessage = null;
            _onStatus = null;
        }

        /// <summary>Se corta la conexión (túnel, móvil en segundo plano...).</summary>
        public async Task DropAsync()
        {
            var onStatus = _onStatus;
            Disconnect();
            if (onStatus is not null)
            {
                await onStatus(RelayStatus.Offline);
            }
        }

        public Task DeliverAsync(string message) => _onMessage?.Invoke(message) ?? Task.CompletedTask;
    }
}
