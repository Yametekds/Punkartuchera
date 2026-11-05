using System.Text;
using Microsoft.AspNetCore.Http;
using Punkartuchera.Relay.Contracts;
using Punkartuchera.Relay.Functions;
using Punkartuchera.Relay.Security;

namespace Punkartuchera.Relay.Tests;

public class SecurityTests
{
    [Theory]
    [InlineData("k7q2xm", "K7Q2XM")]
    [InlineData(" K7Q-2XM ", "K7Q2XM")]
    [InlineData("k7 q2 xm", "K7Q2XM")]
    public void Codes_are_forgiving_with_case_spaces_and_dashes(string input, string expected)
    {
        Assert.True(MatchCode.TryNormalize(input, out var code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("K7Q2X")]
    [InlineData("K7Q2XMM")]
    [InlineData("K7Q2X0")]
    [InlineData("K7Q2XI")]
    [InlineData("K7Q2X%")]
    [InlineData("../../x")]
    [InlineData("K7Q2XM' or '1'='1")]
    public void Anything_that_is_not_a_code_is_rejected(string? input)
    {
        Assert.False(MatchCode.TryNormalize(input, out _));
    }

    [Fact]
    public void Generated_codes_use_only_the_alphabet_and_vary()
    {
        var codes = Enumerable.Range(0, 2000).Select(_ => MatchCode.Generate()).ToList();

        Assert.All(codes, c => Assert.True(MatchCode.TryNormalize(c, out _)));
        Assert.True(codes.Distinct().Count() > 1990);
    }

    [Fact]
    public void Tokens_are_long_random_and_checked_by_hash()
    {
        var token = PlayerTokens.NewToken();
        var hash = PlayerTokens.Hash(token);

        Assert.NotEqual(token, PlayerTokens.NewToken());
        Assert.True(PlayerTokens.Matches(token, hash));
        Assert.False(PlayerTokens.Matches(token + "x", hash));
        Assert.False(PlayerTokens.Matches(token, null));
    }

    [Fact]
    public void Throttle_stops_brute_force_per_address()
    {
        using var throttle = new RequestThrottle();

        var allowed = Enumerable.Range(0, 50).Count(_ => throttle.TryAcquire(RequestThrottle.Bucket.Join, "1.2.3.4"));

        Assert.Equal(12, allowed);
        Assert.True(throttle.TryAcquire(RequestThrottle.Bucket.Join, "5.6.7.8"));
        Assert.True(throttle.TryAcquire(RequestThrottle.Bucket.Create, "1.2.3.4"));
    }

    [Fact]
    public async Task Body_reader_stops_at_the_limit_even_if_content_length_lies()
    {
        var big = new MemoryStream(new byte[RelayProtocol.MaxMessageBytes + 1]);
        var small = new MemoryStream(Encoding.UTF8.GetBytes("""{"type":"status"}"""));

        Assert.Null(await MatchFunctions.ReadLimitedAsync(big, RelayProtocol.MaxMessageBytes, CancellationToken.None));
        Assert.Equal(17, (await MatchFunctions.ReadLimitedAsync(small, RelayProtocol.MaxMessageBytes, CancellationToken.None))!.Value.Length);
    }

    [Theory]
    [InlineData("6.6.6.6, 81.2.3.4:51234", "81.2.3.4")]
    [InlineData("81.2.3.4", "81.2.3.4")]
    [InlineData("[2001:db8::1]:443", "2001:db8::1")]
    public void Client_address_uses_the_entry_added_by_azure(string forwarded, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = forwarded;

        Assert.Equal(expected, MatchFunctions.ClientAddress(context.Request));
    }
}
