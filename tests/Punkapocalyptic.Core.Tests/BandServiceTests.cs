using Punkapocalyptic.Core.Models;
using Punkapocalyptic.Core.Rules;
using Punkapocalyptic.Core.Services;
using Punkapocalyptic.Core.Storage;

namespace Punkapocalyptic.Core.Tests;

public class BandServiceTests
{
    private readonly InMemoryBandRepository _bands = new();
    private readonly InMemoryPhotoStore _photos = new();
    private readonly PlaceholderRulesCatalog _rules = new();
    private readonly BandService _service;

    public BandServiceTests() => _service = new BandService(_bands, _photos, _rules);

    [Fact]
    public async Task CreateAsync_persists_trimmed_band()
    {
        var band = await _service.CreateAsync("  Los Oxidados  ", "chatarreros");

        var stored = await _bands.GetAsync(band.Id);
        Assert.NotNull(stored);
        Assert.Equal("Los Oxidados", stored.Name);
        Assert.Equal(BandOwnership.Own, stored.Ownership);
    }

    [Theory]
    [InlineData("", "chatarreros")]
    [InlineData("Banda", "faccion-inexistente")]
    public async Task CreateAsync_rejects_invalid_input(string name, string faction)
    {
        await Assert.ThrowsAsync<BandValidationException>(() => _service.CreateAsync(name, faction));
    }

    [Fact]
    public void NewMember_uses_default_stats()
    {
        var member = _service.NewMember();

        Assert.All(_rules.Stats, stat => Assert.Equal(stat.Default, member.Stats[stat.Key]));
    }

    [Fact]
    public async Task SaveMemberAsync_adds_then_updates_and_clamps()
    {
        var band = await _service.CreateAsync("Banda", "mutardos");
        var member = _service.NewMember();
        member.Name = "Morro";
        member.Ammo = -3;
        member.Stats["mov"] = 999;

        await _service.SaveMemberAsync(band, member);
        member.Name = "Morro Chungo";
        await _service.SaveMemberAsync(band, member);

        var stored = await _bands.GetAsync(band.Id);
        var saved = Assert.Single(stored!.Members);
        Assert.Equal("Morro Chungo", saved.Name);
        Assert.Equal(0, saved.Ammo);
        Assert.Equal(_rules.Stats.Single(s => s.Key == "mov").Max, saved.Stats["mov"]);
    }

    [Fact]
    public async Task SaveMemberAsync_requires_name()
    {
        var band = await _service.CreateAsync("Banda", "mutardos");

        await Assert.ThrowsAsync<BandValidationException>(() => _service.SaveMemberAsync(band, _service.NewMember()));
    }

    [Fact]
    public async Task Replacing_photo_deletes_previous_stored_photo()
    {
        var band = await _service.CreateAsync("Banda", "idos");
        var oldKey = await _photos.SaveAsync([1, 2, 3], "image/jpeg");
        var member = _service.NewMember();
        member.Name = "Foto";
        member.Photo = PhotoRef.Stored(oldKey);
        await _service.SaveMemberAsync(band, member);

        member.Photo = PhotoRef.Stock("calavera");
        await _service.SaveMemberAsync(band, member);

        Assert.Null(await _photos.GetAsync(oldKey));
    }

    [Fact]
    public async Task DeleteAsync_removes_band_and_photos()
    {
        var band = await _service.CreateAsync("Banda", "idos");
        var key = await _photos.SaveAsync([9], "image/jpeg");
        var member = _service.NewMember();
        member.Name = "Uno";
        member.Photo = PhotoRef.Stored(key);
        await _service.SaveMemberAsync(band, member);

        await _service.DeleteAsync(band.Id);

        Assert.Null(await _bands.GetAsync(band.Id));
        Assert.Null(await _photos.GetAsync(key));
    }

    [Fact]
    public async Task DuplicateAsync_creates_independent_copy()
    {
        var band = await _service.CreateAsync("Original", "unami");
        var key = await _photos.SaveAsync([7, 7], "image/jpeg");
        var member = _service.NewMember();
        member.Name = "Uno";
        member.Photo = PhotoRef.Stored(key);
        await _service.SaveMemberAsync(band, member);

        var copy = await _service.DuplicateAsync(band.Id);

        Assert.NotEqual(band.Id, copy.Id);
        Assert.Equal("Original (copia)", copy.Name);
        var copiedMember = Assert.Single(copy.Members);
        Assert.NotEqual(member.Id, copiedMember.Id);
        Assert.NotEqual(key, copiedMember.Photo!.Key);

        await _service.DeleteAsync(copy.Id);
        Assert.NotNull(await _photos.GetAsync(key));
    }

    [Fact]
    public async Task Rival_bands_are_read_only()
    {
        var rival = new Band { Name = "Rival", FactionId = "v-reich", Ownership = BandOwnership.Rival };
        await _bands.SaveAsync(rival);

        await Assert.ThrowsAsync<BandValidationException>(() => _service.SaveAsync(rival));
        await Assert.ThrowsAsync<BandValidationException>(() => _service.RemoveMemberAsync(rival, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAllAsync_lists_own_bands_before_rivals()
    {
        await _bands.SaveAsync(new Band { Name = "Rival", FactionId = "idos", Ownership = BandOwnership.Rival });
        await _service.CreateAsync("Propia", "idos");

        var all = await _service.GetAllAsync();

        Assert.Equal(["Propia", "Rival"], all.Select(b => b.Name));
    }
}
