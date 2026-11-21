using Punkartuchera.UI.Pages.Bands;

namespace Punkartuchera.UI.Tests;

public class BandDetailTests : UiTestContext
{
    [Fact]
    public async Task Shows_a_card_for_every_member()
    {
        var band = await CreateBandAsync("Los Míos", "pandilleros-1");

        var page = Render<BandDetail>(p => p.Add(d => d.Id, band.Id));

        page.WaitForAssertion(() => Assert.Equal(band.Members.Count, page.FindAll("article.datacard").Count));
        foreach (var member in band.Members)
        {
            Assert.Contains(member.Name, page.Markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void An_unknown_band_does_not_break_the_page()
    {
        var page = Render<BandDetail>(p => p.Add(d => d.Id, Guid.NewGuid()));

        page.WaitForAssertion(() => Assert.Empty(page.FindAll("article.datacard")));
        Assert.NotNull(page.Find("h1"));
    }
}
