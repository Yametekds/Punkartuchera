using System.Text;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;
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
    private static readonly RulebookCatalog Rules = new(RulebookLoader.LoadEmbedded());

    public BandExchangeTests()
    {
        _mine = new BandExchangeService(_myBands, _myPhotos, Rules);
        _host = new BandExchangeService(_hostBands, _hostPhotos, Rules);
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
        Assert.Equal(["Duro", "Retraso (2)"], leader.Skills);
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

    [Fact]
    public async Task Qr_carries_the_whole_sheet_without_photos_or_ammo()
    {
        var band = await CreateBandAsync();

        var qr = await _mine.ExportQrAsync(band.Id);
        var rival = await _host.ImportQrAsync(qr.Text);

        Assert.StartsWith(BandQrCodec.Prefix, qr.Text);
        Assert.False(qr.NotesDropped);
        Assert.DoesNotContain("Plan secreto", qr.Text);
        Assert.Equal("Los Oxidados", rival.Name);
        Assert.Equal("chatarreros", rival.FactionId);
        Assert.Equal(band.Id, rival.SourceId);
        Assert.True(rival.IsReadOnly);
        Assert.Null(rival.Notes);
        Assert.All(rival.Members, m => Assert.Equal(0, m.Ammo));

        var leader = rival.Members[0];
        Assert.Equal("Morro", leader.Name);
        Assert.Equal("Líder", leader.Role);
        Assert.Equal(5, leader.Stats["mov"]);
        Assert.Equal(["Duro", "Retraso (2)"], leader.Skills);
        Assert.Equal("Tubería", leader.Gear[0].Name);
        Assert.Equal("Oxidada", leader.Gear[0].Notes);
        Assert.Null(leader.Cost);
        Assert.Equal(rival.TotalCost, band.Members.Sum(m => m.Cost ?? 0));
        Assert.Equal("Cojea", leader.Notes);
        Assert.Null(leader.Photo);
        Assert.Equal(PhotoRef.Stock("cresta"), rival.Members[1].Photo);
    }

    [Fact]
    public async Task Qr_after_the_file_replaces_the_band_but_keeps_its_photos()
    {
        var band = await CreateBandAsync();
        var fromFile = await _host.ImportAsync(new MemoryStream((await _mine.ExportAsync(band.Id)).Content));
        var photoKey = fromFile.Members[0].Photo!.Key;

        band.Members[0].Stats["mov"] = 6;
        await _myBands.SaveAsync(band);
        var fromQr = await _host.ImportQrAsync((await _mine.ExportQrAsync(band.Id)).Text);

        Assert.Equal(fromFile.Id, fromQr.Id);
        Assert.Single(await _hostBands.GetAllAsync());
        Assert.Equal(6, fromQr.Members[0].Stats["mov"]);
        Assert.Equal(PhotoRef.Stored(photoKey), fromQr.Members[0].Photo);
        Assert.NotNull(await _hostPhotos.GetAsync(photoKey));
    }

    [Fact]
    public async Task Damaged_qr_is_rejected()
    {
        var band = await CreateBandAsync();
        var text = (await _mine.ExportQrAsync(band.Id)).Text;
        var middle = text.Length / 2;
        var damaged = text[..middle] + (text[middle] == 'A' ? 'B' : 'A') + text[(middle + 1)..];

        await Assert.ThrowsAsync<BandExchangeException>(() => _host.ImportQrAsync(damaged));
        await Assert.ThrowsAsync<BandExchangeException>(() => _host.ImportQrAsync("https://example.com"));
        await Assert.ThrowsAsync<BandExchangeException>(() => _host.ImportQrAsync(text[..^7]));
        Assert.Empty(await _hostBands.GetAllAsync());
    }

    [Fact]
    public async Task Big_band_fits_in_a_qr_and_huge_notes_are_dropped_first()
    {
        var band = new Band { Name = "Horda", FactionId = "chatarreros" };
        for (var i = 0; i < 15; i++)
        {
            band.Members.Add(new Miniature
            {
                Name = $"Pandillero {i}",
                Role = "Pandillero",
                Stats = Rules.Stats.ToDictionary(s => s.Key, s => s.Default),
                Skills = ["Duro", "Sanguinario"],
                Gear = [new Equipment { Name = "Pistola", Notes = string.Concat(Enumerable.Range(0, 8).Select(_ => Guid.NewGuid().ToString("N"))) }, new Equipment { Name = "Chupa de cuero" }],
                Notes = $"Nota única número {i} con {Guid.NewGuid()}",
            });
        }

        await _myBands.SaveAsync(band);

        var qr = await _mine.ExportQrAsync(band.Id);
        var rival = await _host.ImportQrAsync(qr.Text);

        Assert.True(qr.Text.Length <= BandQrCodec.MaxLength);
        Assert.Equal(15, rival.Members.Count);
        Assert.True(qr.NotesDropped);
        Assert.Equal("Chupa de cuero", rival.Members[14].Gear[1].Name);
    }

    [Fact]
    public void Base45_round_trips_rfc_examples()
    {
        Assert.Equal("BB8", Base45.Encode("AB"u8));
        Assert.Equal("%69 VD92EX0", Base45.Encode("Hello!!"u8));
        Assert.Equal("ietf!"u8.ToArray(), Base45.Decode("QED8WEX0"));
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
                    Role = "Líder",
                    Ammo = 4,
                    Cost = 12,
                    Notes = "Cojea",
                    Photo = PhotoRef.Stored(photoKey),
                    Stats = { ["mov"] = 5 },
                    Skills = ["Duro", "Retraso (2)"],
                    Gear = [new Equipment { Name = "Tubería", Notes = "Oxidada" }],
                },
                new Miniature { Name = "Chispas", Ammo = 2, Photo = PhotoRef.Stock("cresta") },
            ],
        };
        await _myBands.SaveAsync(band);
        return band;
    }
}
