using Punkartuchera.Core.Matches;
using Punkartuchera.UI.Pages.Partida;

namespace Punkartuchera.UI.Tests;

public class MatchPageTests : UiTestContext
{
    private async Task<IMatchService> StartAsync()
    {
        var own = await CreateBandAsync("Los Míos", "pandilleros-1");
        var rival = await CreateBandAsync("Los Otros", "mutardos-1");
        var matches = Get<IMatchService>();
        await matches.StartLocalAsync(own.Id, rival.Id);
        return matches;
    }

    [Fact]
    public void Without_bands_it_asks_to_build_one_first()
    {
        var page = Render<MatchPage>();

        page.WaitForAssertion(() => Assert.Contains("Sin banda no hay bronca", page.Markup, StringComparison.Ordinal));
        Assert.Equal("bandas/nueva", page.Find(".punk-empty a").GetAttribute("href"));
    }

    [Fact]
    public async Task The_match_cannot_start_until_the_rival_is_chosen()
    {
        await CreateBandAsync("Los Míos", "pandilleros-1");
        var page = Render<MatchPage>();

        page.WaitForAssertion(() => page.Find("button.btn--primary"));
        var start = page.FindAll("button.btn--primary").Single(b => b.TextContent.Contains("A pelear", StringComparison.Ordinal));
        Assert.True(start.HasAttribute("disabled"));
    }

    [Fact]
    public async Task A_local_match_shows_both_bands_with_everyone_pending()
    {
        var matches = await StartAsync();
        var page = Render<MatchPage>();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("section.match-side").Count));
        var own = matches.Current!.Own;
        var count = page.Find("#match-side-own .match-side__count").TextContent;
        Assert.Contains($"{own.Count} en pie de {own.Count}", count, StringComparison.Ordinal);
        Assert.Contains($"{own.Count} por actuar", count, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Marking_a_member_as_done_updates_the_match_and_the_pending_count()
    {
        var matches = await StartAsync();
        var page = Render<MatchPage>();
        page.WaitForAssertion(() => page.Find("#match-side-own .match-toggle--pending"));

        page.Find("#match-side-own .match-toggle--pending").Click();

        page.WaitForAssertion(() => Assert.True(matches.Current!.Own[0].Activated));
        var own = matches.Current!.Own;
        Assert.Contains($"{own.Count - 1} por actuar", page.Find("#match-side-own .match-side__count").TextContent, StringComparison.Ordinal);
        Assert.Equal("true", page.FindAll("#match-side-own .match-toggle")[0].GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task A_downed_member_no_longer_counts_as_standing()
    {
        var matches = await StartAsync();
        var page = Render<MatchPage>();
        page.WaitForAssertion(() => page.Find("#match-side-own .match-toggle--down"));

        page.Find("#match-side-own .match-toggle--down").Click();

        page.WaitForAssertion(() => Assert.True(matches.Current!.Own[0].Down));
        var own = matches.Current!.Own;
        Assert.Contains($"{own.Count - 1} en pie de {own.Count}", page.Find("#match-side-own .match-side__count").TextContent, StringComparison.Ordinal);
    }
}
