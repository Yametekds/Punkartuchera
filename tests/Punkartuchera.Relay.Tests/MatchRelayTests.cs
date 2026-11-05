using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Punkartuchera.Relay.Contracts;
using Punkartuchera.Relay.Matches;
using Punkartuchera.Relay.Realtime;

namespace Punkartuchera.Relay.Tests;

public class MatchRelayTests
{
    private readonly InMemoryMatchStore _store = new();
    private readonly FakeChannel _channel = new();
    private readonly ManualClock _clock = new();
    private readonly MatchRelay _relay;

    public MatchRelayTests()
    {
        _relay = new MatchRelay(_store, _channel, _clock, NullLogger<MatchRelay>.Instance);
    }

    [Fact]
    public async Task Creating_gives_a_readable_code_and_stores_only_the_token_hash()
    {
        var ticket = await _relay.CreateAsync();

        Assert.True(MatchCode.TryNormalize(ticket.Code, out var normalized));
        Assert.Equal(ticket.Code, normalized);
        Assert.True(ticket.IsHost);
        Assert.True(ticket.Token.Length >= 43);

        var stored = await _store.GetAsync(ticket.Code);
        Assert.NotEqual(ticket.Token, stored!.HostTokenHash);
        Assert.DoesNotContain(ticket.Token, stored.HostTokenHash);
    }

    [Fact]
    public async Task Only_one_rival_can_join()
    {
        var host = await _relay.CreateAsync();

        var guest = await _relay.JoinAsync(host.Code);
        var intruder = await _relay.JoinAsync(host.Code);

        Assert.NotNull(guest);
        Assert.False(guest.IsHost);
        Assert.Null(intruder);
    }

    [Fact]
    public async Task Simultaneous_joins_let_exactly_one_player_in()
    {
        var host = await _relay.CreateAsync();

        var attempts = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => Task.Run(() => _relay.JoinAsync(host.Code))));

        Assert.Single(attempts, t => t is not null);
    }

    [Fact]
    public async Task An_open_match_expires_if_nobody_joins()
    {
        var host = await _relay.CreateAsync();

        _clock.Advance(MatchRelay.OpenFor + TimeSpan.FromSeconds(1));

        Assert.Null(await _relay.JoinAsync(host.Code));
        Assert.Null(await _relay.AuthenticateAsync(host.Code, host.Token));
    }

    [Fact]
    public async Task Joining_extends_the_match_for_the_whole_game()
    {
        var host = await _relay.CreateAsync();
        var guest = await _relay.JoinAsync(host.Code);

        _clock.Advance(TimeSpan.FromHours(11));

        Assert.NotNull(await _relay.AuthenticateAsync(host.Code, host.Token));
        Assert.NotNull(await _relay.AuthenticateAsync(host.Code, guest!.Token));

        _clock.Advance(TimeSpan.FromHours(2));
        Assert.Null(await _relay.AuthenticateAsync(host.Code, guest.Token));
    }

    [Fact]
    public async Task Tokens_only_open_their_own_match()
    {
        var first = await _relay.CreateAsync();
        var second = await _relay.CreateAsync();

        Assert.NotNull(await _relay.AuthenticateAsync(first.Code, first.Token));
        Assert.Null(await _relay.AuthenticateAsync(second.Code, first.Token));
        Assert.Null(await _relay.AuthenticateAsync(first.Code, "inventada"));
        Assert.Null(await _relay.AuthenticateAsync(first.Code, ""));
        Assert.Null(await _relay.AuthenticateAsync(first.Code, null));
        Assert.Null(await _relay.AuthenticateAsync(first.Code, new string('x', 5000)));
    }

    [Fact]
    public async Task Messages_go_only_to_the_opponent()
    {
        var (host, guest) = await StartAsync();
        var player = await _relay.AuthenticateAsync(host.Code, host.Token);

        var result = await _relay.ForwardAsync(player!, Body("""{"type":"status","members":[]}"""));

        Assert.Equal(ForwardResult.Sent, result);
        var sent = Assert.Single(_channel.Sent);
        Assert.Equal(guest.PlayerId, sent.PlayerId);
        Assert.Equal("""{"type":"status","members":[]}""", sent.Message);
    }

    [Fact]
    public async Task Without_a_rival_there_is_nobody_to_send_to()
    {
        var host = await _relay.CreateAsync();
        var player = await _relay.AuthenticateAsync(host.Code, host.Token);

        Assert.Equal(ForwardResult.NoOpponent, await _relay.ForwardAsync(player!, Body("""{"type":"hello","band":"x"}""")));
        Assert.Empty(_channel.Sent);
    }

    [Theory]
    [InlineData("""{"type":"bye"}""")]
    [InlineData("""{"type":"admin"}""")]
    [InlineData("""{"tipo":"hello"}""")]
    [InlineData("""{"type":7}""")]
    [InlineData("""["type","hello"]""")]
    [InlineData("""{"type":"hello"} {"type":"bye"}""")]
    [InlineData("""{"type":"hello",""")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("""{"type":"hello","a":{"b":{"c":{"d":{"e":{"f":{"g":{"h":{"i":1}}}}}}}}}""")]
    public async Task Forged_or_malformed_messages_are_dropped(string body)
    {
        var (host, _) = await StartAsync();
        var player = await _relay.AuthenticateAsync(host.Code, host.Token);

        Assert.Equal(ForwardResult.Invalid, await _relay.ForwardAsync(player!, Body(body)));
        Assert.Empty(_channel.Sent);
    }

    [Fact]
    public async Task Oversized_messages_are_dropped()
    {
        var (host, _) = await StartAsync();
        var player = await _relay.AuthenticateAsync(host.Code, host.Token);
        var huge = $$"""{"type":"hello","band":"{{new string('A', RelayProtocol.MaxMessageBytes)}}"}""";

        Assert.Equal(ForwardResult.TooLarge, await _relay.ForwardAsync(player!, Body(huge)));
        Assert.Equal(ForwardResult.TooLarge, await _relay.ForwardAsync(player!, ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public async Task Leaving_closes_the_match_and_tells_the_rival()
    {
        var (host, guest) = await StartAsync();
        var player = await _relay.AuthenticateAsync(guest.Code, guest.Token);

        await _relay.LeaveAsync(player!);

        var sent = Assert.Single(_channel.Sent);
        Assert.Equal(host.PlayerId, sent.PlayerId);
        Assert.Contains("bye", sent.Message);
        Assert.Null(await _relay.AuthenticateAsync(host.Code, host.Token));
    }

    [Fact]
    public async Task Connection_tokens_never_outlive_the_match()
    {
        var host = await _relay.CreateAsync();
        var player = await _relay.AuthenticateAsync(host.Code, host.Token);

        await _relay.ConnectAsync(player!);

        var lifetime = Assert.Single(_channel.Negotiated).Lifetime;
        Assert.True(lifetime <= MatchRelay.OpenFor);
    }

    [Fact]
    public async Task Cleanup_removes_expired_matches()
    {
        await _relay.CreateAsync();
        await StartAsync();

        _clock.Advance(MatchRelay.OpenFor + TimeSpan.FromMinutes(1));

        Assert.Equal(1, await _relay.CleanupAsync());
        Assert.Equal(1, _store.Count);
    }

    private async Task<(RelayTicket Host, RelayTicket Guest)> StartAsync()
    {
        var host = await _relay.CreateAsync();
        var guest = await _relay.JoinAsync(host.Code);
        return (host, guest!);
    }

    private static ReadOnlyMemory<byte> Body(string json) => Encoding.UTF8.GetBytes(json);

    private sealed class FakeChannel : IRealtimeChannel
    {
        public List<(string PlayerId, string Message)> Sent { get; } = [];

        public List<(string PlayerId, TimeSpan Lifetime)> Negotiated { get; } = [];

        public Task<RelayConnectionInfo> NegotiateAsync(string playerId, TimeSpan lifetime, CancellationToken cancellationToken = default)
        {
            Negotiated.Add((playerId, lifetime));
            return Task.FromResult(new RelayConnectionInfo("https://ejemplo.service.signalr.net/client/?hub=partida", "token"));
        }

        public Task SendAsync(string playerId, string message, CancellationToken cancellationToken = default)
        {
            lock (Sent)
            {
                Sent.Add((playerId, message));
            }

            return Task.CompletedTask;
        }
    }
}

internal sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 5, 18, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
