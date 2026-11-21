using Microsoft.AspNetCore.Components;
using Punkartuchera.Core.Services;
using Punkartuchera.UI.Pages.Bands;

namespace Punkartuchera.UI.Tests;

public class MemberEditorTests : UiTestContext
{
    private async Task<IRenderedComponent<MemberEditor>> RecruitAsync(Guid? bandId = null)
    {
        bandId ??= (await Get<BandService>().CreateAsync("Los Míos", "pandilleros")).Id;
        var page = Render<MemberEditor>(p => p.Add(e => e.BandId, bandId.Value));
        page.WaitForAssertion(() => page.Find(".troop-trigger"));
        return page;
    }

    [Fact]
    public async Task Without_a_type_the_profile_can_be_edited_by_hand()
    {
        var page = await RecruitAsync();

        Assert.Empty(page.FindAll(".stat-grid--locked"));
        Assert.NotEmpty(page.FindAll(".stat-grid .stepper"));
    }

    [Fact]
    public async Task Picking_an_official_troop_locks_its_profile()
    {
        var page = await RecruitAsync();

        page.Find(".troop-trigger").Click();
        page.WaitForAssertion(() => page.Find(".troop-option"));
        page.Find(".troop-option").Click();

        page.WaitForAssertion(() => page.Find(".stat-grid--locked"));
        Assert.Empty(page.FindAll(".stat-grid .stepper"));
        Assert.Empty(page.FindAll(".troop-picker"));
    }

    [Fact]
    public async Task Saving_adds_the_member_to_the_band_and_goes_back_to_it()
    {
        var band = await Get<BandService>().CreateAsync("Los Míos", "pandilleros");
        var page = await RecruitAsync(band.Id);

        page.Find(".troop-trigger").Click();
        page.WaitForAssertion(() => page.Find(".troop-option"));
        page.Find(".troop-option").Click();
        page.Find("input[placeholder='Morro Chungo']").Input("Morro Fino");
        page.FindAll("button").Single(b => b.TextContent.Trim() == "Guardar").Click();

        page.WaitForAssertion(() => Assert.EndsWith($"bandas/{band.Id}", Get<NavigationManager>().Uri, StringComparison.Ordinal));
        var saved = await Get<BandService>().GetAsync(band.Id);
        var member = Assert.Single(saved!.Members);
        Assert.Equal("Morro Fino", member.Name);
        Assert.True(member.BaseCost > 0);
    }
}
