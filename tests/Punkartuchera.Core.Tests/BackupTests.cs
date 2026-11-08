using System.Text;
using Punkartuchera.Core.Backup;
using Punkartuchera.Core.Matches;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Storage;
using Punkartuchera.Core.Users;

namespace Punkartuchera.Core.Tests;

public class BackupTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
    private static readonly byte[] TablePhoto = [0xFF, 0xD8, 0xFF, 0xE0, 9, 9, 9];
    private static readonly RulebookCatalog Rules = new(RulebookLoader.LoadEmbedded());

    // El móvil viejo, donde se hace la copia, y el nuevo, donde se restaura.
    private readonly Device _old = new();
    private readonly Device _new = new();

    private sealed class Device
    {
        public InMemoryBandRepository Bands { get; } = new();
        public InMemoryPhotoStore Photos { get; } = new();
        public InMemorySavedMatchStore Matches { get; } = new();
        public InMemoryUserProfileStore Profile { get; } = new();

        public BackupService Backup => new(Bands, Photos, Matches, Profile, Rules);
    }

    [Fact]
    public async Task Backup_keeps_the_ammo_the_date_and_the_nickname()
    {
        await _old.Profile.SaveAsync(new UserProfile { Nickname = "Morralla", FontScale = 120, Font = ReadingFont.OpenDyslexic });
        await SeedAsync(_old);

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var backup = await _old.Backup.CreateAsync();
        var json = Encoding.UTF8.GetString(backup.Content);

        Assert.StartsWith("punkartuchera-Morralla-", backup.FileName);
        Assert.EndsWith(".json", backup.FileName);
        Assert.Contains("\"format\": \"punkartuchera-backup\"", json);
        Assert.Contains("\"nickname\": \"Morralla\"", json);
        Assert.Contains("\"ammo\": 4", json);
        Assert.Contains("Plan secreto", json);
        Assert.Contains("\"font\": \"OpenDyslexic\"", json);

        var read = await _new.Backup.ReadAsync(new MemoryStream(backup.Content));
        Assert.True(read.CreatedAt >= before);
        Assert.Equal(2, read.Photos.Count);
        Assert.Equal(4, backup.Summary.Ammo);
        Assert.Equal(1, backup.Summary.OwnBands);
        Assert.Equal(1, backup.Summary.Matches);
    }

    [Fact]
    public async Task Without_nickname_the_backup_is_signed_as_pringao()
    {
        await SeedAsync(_old);

        var backup = await _old.Backup.CreateAsync();

        Assert.Equal(UserProfile.DefaultNickname, backup.Summary.Nickname);
        Assert.StartsWith("punkartuchera-PringaoSinNickname-", backup.FileName);
        Assert.Contains("\"nickname\": \"PringaoSinNickname\"", Encoding.UTF8.GetString(backup.Content));
    }

    [Fact]
    public async Task Restore_brings_back_bands_matches_photos_and_settings()
    {
        await _old.Profile.SaveAsync(new UserProfile { Nickname = "Morralla", FontScale = 130, Font = ReadingFont.Hyperlegible });
        var (band, match) = await SeedAsync(_old);
        var backup = await _old.Backup.CreateAsync();

        var summary = await _new.Backup.RestoreAsync(await _new.Backup.ReadAsync(new MemoryStream(backup.Content)));

        Assert.Equal(2, summary.Photos);
        var restored = Assert.Single(await _new.Bands.GetAllAsync());
        Assert.Equal(band.Id, restored.Id);
        Assert.Equal("Plan secreto", restored.Notes);
        Assert.Equal(4, restored.Members[0].Ammo);
        var photo = await _new.Photos.GetAsync(restored.Members[0].Photo!.Key);
        Assert.Equal(Jpeg, photo!.Data);
        Assert.Equal(PhotoRef.Stock("cresta"), restored.Members[1].Photo);

        var restoredMatch = Assert.Single(await _new.Matches.GetAllAsync());
        Assert.Equal(match.Id, restoredMatch.Id);
        Assert.Equal(band.Id, restoredMatch.OwnBandId);
        Assert.Equal("Ganamos por los pelos", restoredMatch.Notes);
        Assert.Equal(TablePhoto, (await _new.Photos.GetAsync(Assert.Single(restoredMatch.Photos).Key))!.Data);

        var profile = await _new.Profile.GetAsync();
        Assert.Equal("Morralla", profile.Nickname);
        Assert.Equal(130, profile.FontScale);
        Assert.Equal(ReadingFont.Hyperlegible, profile.Font);
    }

    [Fact]
    public async Task Restore_replaces_what_is_in_the_backup_and_keeps_the_rest()
    {
        var (band, _) = await SeedAsync(_old);
        var backup = await _old.Backup.CreateAsync();

        // En el móvil nuevo ya había otra banda y una versión vieja de la misma.
        var other = new Band { Name = "Otra", FactionId = "craabianos" };
        await _new.Bands.SaveAsync(other);
        var oldKey = await _new.Photos.SaveAsync(Jpeg, "image/jpeg");
        await _new.Bands.SaveAsync(new Band { Id = band.Id, Name = "Versión vieja", Members = [new Miniature { Name = "Viejo", Photo = PhotoRef.Stored(oldKey) }] });
        await _new.Profile.SaveAsync(new UserProfile { Nickname = "YaTenía" });

        await _new.Backup.RestoreAsync(await _new.Backup.ReadAsync(new MemoryStream(backup.Content)));

        var all = await _new.Bands.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, b => b.Id == other.Id);
        Assert.Equal("Los Oxidados", all.Single(b => b.Id == band.Id).Name);
        Assert.Null(await _new.Photos.GetAsync(oldKey));
        // La copia no tenía nickname: no pisa el que ya había.
        Assert.Equal("YaTenía", (await _new.Profile.GetAsync()).Nickname);
    }

    [Theory]
    [InlineData("esto no es json")]
    [InlineData("{\"format\":\"punkartuchera-band\",\"version\":1}")]
    [InlineData("{\"format\":\"punkartuchera-backup\",\"version\":99}")]
    public async Task Read_rejects_files_that_are_not_backups(string content)
    {
        await Assert.ThrowsAsync<BackupException>(() => _new.Backup.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(content))));
    }

    [Fact]
    public async Task Restore_drops_fake_photos_and_strange_links()
    {
        await SeedAsync(_old);
        var json = Encoding.UTF8.GetString((await _old.Backup.CreateAsync()).Content)
            .Replace("image/jpeg", "text/html")
            .Replace("\"key\": \"cresta\"", "\"key\": \"https://malo.example/x.svg\"");

        await _new.Backup.RestoreAsync(await _new.Backup.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json))));

        var band = Assert.Single(await _new.Bands.GetAllAsync());
        Assert.All(band.Members, m => Assert.Null(m.Photo));
        Assert.Empty(Assert.Single(await _new.Matches.GetAllAsync()).Photos);
    }

    [Fact]
    public async Task Rival_bands_in_a_backup_never_carry_ammo()
    {
        await SeedAsync(_old);
        var json = Encoding.UTF8.GetString((await _old.Backup.CreateAsync()).Content)
            .Replace("\"ownership\": \"Own\"", "\"ownership\": \"Rival\"");

        await _new.Backup.RestoreAsync(await _new.Backup.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json))));

        var rival = Assert.Single(await _new.Bands.GetAllAsync());
        Assert.True(rival.IsReadOnly);
        Assert.All(rival.Members, m => Assert.Equal(0, m.Ammo));
    }

    [Theory]
    [InlineData("  Morralla   de   barrio ", "Morralla de barrio")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Nicknames_are_tidied(string? input, string? expected)
    {
        Assert.Equal(expected, UserProfile.CleanNickname(input));
    }

    [Fact]
    public void Font_scale_stays_in_range()
    {
        Assert.Equal(150, new UserProfile { FontScale = 400 }.Normalize().FontScale);
        Assert.Equal(90, new UserProfile { FontScale = 10 }.Normalize().FontScale);
        Assert.Equal(110, new UserProfile { FontScale = 112 }.Normalize().FontScale);
    }

    private static async Task<(Band Band, ActiveMatch Match)> SeedAsync(Device device)
    {
        var key = await device.Photos.SaveAsync(Jpeg, "image/jpeg");
        var band = new Band
        {
            Name = "Los Oxidados",
            FactionId = "craabianos",
            Notes = "Plan secreto",
            Members =
            [
                new Miniature { Name = "Morro", Role = "Jefe", Ammo = 4, Photo = PhotoRef.Stored(key) },
                new Miniature { Name = "Chispa", Role = "Pringao", Photo = PhotoRef.Stock("cresta") },
            ],
        };
        await device.Bands.SaveAsync(band);

        var tableKey = await device.Photos.SaveAsync(TablePhoto, "image/jpeg");
        var match = new ActiveMatch
        {
            Mode = MatchMode.Local,
            OwnBandId = band.Id,
            OwnBandName = band.Name,
            Round = 3,
            Notes = "Ganamos por los pelos",
            Photos = [new MatchPhoto { Key = tableKey, Round = 2 }],
            Own = [new MatchMember { MemberId = band.Members[0].Id, Name = "Morro", Role = "Jefe" }],
        };
        await device.Matches.SaveAsync(match);
        return (band, match);
    }
}
