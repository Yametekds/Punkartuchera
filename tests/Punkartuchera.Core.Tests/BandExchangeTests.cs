using System.Text;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Sharing;
using Punkartuchera.Core.Storage;

namespace Punkartuchera.Core.Tests;

public class BandExchangeTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    // Cada jugador tiene su propio dispositivo: almacenes separados.
    private readonly InMemoryBandRepository _myBands = new();
    private readonly InMemoryPhotoStore _myPhotos = new();
    private readonly InMemoryBandRepository _hostBands = new();
    private readonly InMemoryPhotoStore _hostPhotos = new();
    private readonly BandExchangeService _mine;
    private readonly BandExchangeService _host;

    public BandExchangeTests()
    {
        _mine = new BandExchangeService(_myBands, _myPhotos);
        _host = new BandExchangeService(_hostBands, _hostPhotos);
    }

    [Fact]
    public async Task Export_and_import_keeps_the_sheet_but_not_the_ammo()
    {
        var band = await CreateBandAsync();

        var export = await _mine.ExportAsync(band.Id);
        var json = Encoding.UTF8.GetString(export.Content);
        var rival = await _host.ImportAsync(new MemoryStream(export.Content));

        Assert.Equal("Los Oxidados.punkband", export.FileName);
        Assert.DoesNotContain("Plan secreto", json);
        Assert.Equal(BandOwnership.Rival, rival.Ownership);
        Assert.True(rival.IsReadOnly);
        Assert.NotEqual(band.Id, rival.Id);
        Assert.Equal(band.Id, rival.SourceId);
        Assert.Null(rival.Notes);
        Assert.All(rival.Members, m => Assert.Equal(0, m.Ammo));

        var leader = rival.Members[0];
        Assert.Equal("Morro", leader.Name);
        Assert.Equal(5, leader.Stats["mov"]);
        Assert.Equal(["Duro"], leader.Skills);
        Assert.Equal("Tubería", leader.Gear[0].Name);
        Assert.Equal(PhotoSource.Stored, leader.Photo!.Source);
        var photo = await _hostPhotos.GetAsync(leader.Photo.Key);
        Assert.Equal(Jpeg, photo!.Data);
        Assert.Equal(PhotoRef.Stock("cresta"), rival.Members[1].Photo);

        // La banda original no se toca.
        var original = await _myBands.GetAsync(band.Id);
        Assert.Equal(4, original!.Members[0].Ammo);
    }

    [Fact]
    public async Task Importing_the_same_band_again_replaces_the_old_copy()
    {
        var band = await CreateBandAsync();
        var first = await _host.ImportAsync(new MemoryStream((await _mine.ExportAsync(band.Id)).Content));
        var oldPhotoKey = first.Members[0].Photo!.Key;

        band.Members.RemoveAt(1);
        await _myBands.SaveAsync(band);
        var second = await _host.ImportAsync(new MemoryStream((await _mine.ExportAsync(band.Id)).Content));

        Assert.Equal(first.Id, second.Id);
        var stored = Assert.Single(await _hostBands.GetAllAsync());
        Assert.Single(stored.Members);
        Assert.Null(await _hostPhotos.GetAsync(oldPhotoKey));
        Assert.NotNull(await _hostPhotos.GetAsync(stored.Members[0].Photo!.Key));
    }

    [Fact]
    public async Task Rival_bands_cannot_be_passed_on()
    {
        var band = await CreateBandAsync();
        var rival = await _host.ImportAsync(new MemoryStream((await _mine.ExportAsync(band.Id)).Content));

        await Assert.ThrowsAsync<BandExchangeException>(() => _host.ExportAsync(rival.Id));
    }

    [Theory]
    [InlineData("esto no es json")]
    [InlineData("{\"format\":\"otra-cosa\",\"version\":1,\"band\":{\"name\":\"X\"}}")]
    [InlineData("{\"format\":\"punkartuchera-band\",\"version\":99,\"band\":{\"name\":\"X\"}}")]
    public async Task Import_rejects_unknown_files(string content)
    {
        await Assert.ThrowsAsync<BandExchangeException>(() => _host.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(content))));
        Assert.Empty(await _hostBands.GetAllAsync());
    }

    [Fact]
    public async Task Import_drops_photos_that_are_not_images()
    {
        var band = await CreateBandAsync();
        var package = Encoding.UTF8.GetString((await _mine.ExportAsync(band.Id)).Content)
            .Replace("image/jpeg", "text/html");

        var rival = await _host.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(package)));

        Assert.Null(rival.Members[0].Photo);
    }

    private async Task<Band> CreateBandAsync()
    {
        var photoKey = await _myPhotos.SaveAsync(Jpeg, "image/jpeg");
        var band = new Band
        {
            Name = "Los Oxidados",
            FactionId = "chatarreros",
            Notes = "Plan secreto: flanquear por la izquierda",
            Members =
            [
                new Miniature
                {
                    Name = "Morro",
                    Ammo = 4,
                    Photo = PhotoRef.Stored(photoKey),
                    Stats = { ["mov"] = 5 },
                    Skills = ["Duro"],
                    Gear = [new Equipment { Name = "Tubería" }],
                },
                new Miniature { Name = "Chispas", Ammo = 2, Photo = PhotoRef.Stock("cresta") },
            ],
        };
        await _myBands.SaveAsync(band);
        return band;
    }
}
