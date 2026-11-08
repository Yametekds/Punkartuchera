using System.Text;
using Punkartuchera.Core.Backup;
using Punkartuchera.Core.Matches;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Services;
using Punkartuchera.Core.Sharing;
using Punkartuchera.Core.Storage;
using Punkartuchera.Core.Tournaments;
using Punkartuchera.Core.Users;

namespace Punkartuchera.Core.Tests;

/// <summary>
/// Ataques simulados contra la restauración de copias de seguridad y los QR de resultado del torneo.
/// Una copia puede llegar de otra persona ("restaura esta copia con mis bandas") y estar manipulada a mano.
/// </summary>
public class BackupAttackTests
{
    private static readonly RulebookCatalog Rules = new(RulebookLoader.LoadEmbedded());

    private readonly InMemoryBandRepository _bands = new();
    private readonly InMemoryPhotoStore _photos = new();
    private readonly InMemorySavedMatchStore _matches = new();
    private readonly InMemoryUserProfileStore _profile = new();

    private BackupService Backup => new(_bands, _photos, _matches, _profile, Rules);

    // ---------- Copias rotas ----------

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"format\":\"punkartuchera-band\"}")]
    [InlineData("{\"format\":\"punkartuchera-backup\",\"version\":999}")]
    [InlineData("{\"format\":\"punkartuchera-backup\",\"bands\":[{\"id\":\"no-es-guid\"}]}")]
    [InlineData("{\"format\":\"punkartuchera-backup\",\"settings\":{\"font\":\"Inventada\"}}")]
    public async Task Malformed_backups_are_rejected(string json)
    {
        await Assert.ThrowsAsync<BackupException>(() => ReadAsync(json));
    }

    [Fact]
    public async Task Deeply_nested_backup_is_rejected()
    {
        var json = "{\"format\":\"punkartuchera-backup\",\"nickname\":" + new string('[', 100_000) + new string(']', 100_000) + "}";

        await Assert.ThrowsAsync<BackupException>(() => ReadAsync(json));
    }

    // ---------- Nulos donde no debería haberlos ----------

    [Fact]
    public async Task Nulls_everywhere_are_tidied_and_restored()
    {
        var backup = await ReadAsync("""
            {"format":"punkartuchera-backup","nickname":null,"settings":null,
             "bands":[null,{"id":"55555555-5555-5555-5555-555555555555","name":null,"factionId":null,"members":[null,
               {"name":null,"role":null,"stats":null,"skills":[null],"gear":[null,{"name":null,"notes":null}],"included":null}]}],
             "matches":[null,{"ownBandName":null,"own":[null,{"name":null,"role":null}],"rival":null,"ownCounters":null,"rivalCounters":null,"photos":[null,{"key":null}]}],
             "photos":[null,{"key":null,"contentType":null,"data":null}]}
            """);
        await Backup.RestoreAsync(backup);

        var band = Assert.Single(await _bands.GetAllAsync());
        var member = Assert.Single(band.Members);
        Assert.NotNull(member.Name);
        Assert.All(member.Gear, g => Assert.NotNull(g.Name));
        var match = Assert.Single(await _matches.GetAllAsync());
        Assert.All(match.Own, m => Assert.NotNull(m.Name));
    }

    // ---------- Valores fuera de rango ----------

    [Fact]
    public async Task Out_of_range_values_are_clamped()
    {
        var backup = await ReadAsync("""
            {"format":"punkartuchera-backup","settings":{"fontScale":99999},
             "bands":[{"name":"Chetada","factionId":"pandilleros","members":[{"name":"A","ammo":-5,"cost":-9,"stats":{"c":999}}]}],
             "matches":[{"round":99,"ownScore":-999999,"rivalScore":2147483647,"ownCounters":{"x":-999999}}]}
            """);
        await Backup.RestoreAsync(backup);

        var member = (await _bands.GetAllAsync())[0].Members[0];
        Assert.Equal(0, member.Ammo);
        Assert.Equal(Rules.Stats.Single(s => s.Key == "c").Max, member.Stats["c"]);
        Assert.True(member.Cost is null or >= 0);
        var match = (await _matches.GetAllAsync())[0];
        Assert.InRange(match.Round, 1, ActiveMatch.MaxRound);
        Assert.InRange(match.OwnScore, 0, ActiveMatch.MaxCount);
        Assert.InRange(match.RivalScore, 0, ActiveMatch.MaxCount);
        Assert.All(match.OwnCounters.Values, v => Assert.InRange(v, 0, ActiveMatch.MaxCount));
        Assert.True((await _profile.GetAsync()).FontScale <= UserProfile.MaxFontScale);
    }

    // ---------- Tamaños desproporcionados ----------

    [Fact]
    public async Task Huge_texts_and_lists_are_trimmed()
    {
        var big = new string('A', 200_000);
        var members = string.Join(',', Enumerable.Range(0, 2_000).Select(i => $"{{\"name\":\"M{i}\",\"skills\":[{string.Join(',', Enumerable.Repeat("\"S\"", 300))}]}}"));
        var counters = string.Join(',', Enumerable.Range(0, 5_000).Select(i => $"\"k{i}{big[..50]}\":1"));
        var backup = await ReadAsync($$$"""
            {"format":"punkartuchera-backup",
             "bands":[{"name":"{{{big}}}","factionId":"pandilleros","notes":"{{{big}}}","members":[{{{members}}}]}],
             "matches":[{"ownBandName":"{{{big}}}","ownCounters":{{{{counters}}}}}]}
            """);
        await Backup.RestoreAsync(backup);

        var band = (await _bands.GetAllAsync())[0];
        Assert.True(band.Name.Length <= BandService.MaxNameLength);
        Assert.True(band.Notes!.Length <= RivalBandSanitizer.MaxNotesLength);
        Assert.True(band.Members.Count <= RivalBandSanitizer.MaxMembers);
        Assert.All(band.Members, m => Assert.True(m.Skills.Count <= RivalBandSanitizer.MaxListItems));
        var match = (await _matches.GetAllAsync())[0];
        Assert.True(match.OwnBandName.Length <= BandService.MaxNameLength);
        Assert.True(match.OwnCounters.Count <= BackupService.MaxCounters);
    }

    // ---------- Contenido peligroso ----------

    [Fact]
    public async Task External_photos_fake_images_and_odd_map_links_do_not_get_in()
    {
        var svg = Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg onload='alert(1)'/>"));
        var backup = await ReadAsync($$$"""
            {"format":"punkartuchera-backup",
             "bands":[{"name":"Espía","factionId":"pandilleros","members":[
               {"name":"A","photo":{"source":"Official","key":"https://espia.example/p.gif"}},
               {"name":"B","photo":{"source":"Stored","key":"f"}}]}],
             "matches":[{"map":{"code":"ABC","query":"codigo=ABC&x=\"><script>alert(1)</script>"},"photos":[{"key":"f"}]}],
             "photos":[{"key":"f","contentType":"image/png","data":"{{{svg}}}"}]}
            """);
        await Backup.RestoreAsync(backup);

        var band = (await _bands.GetAllAsync())[0];
        Assert.All(band.Members, m => Assert.Null(m.Photo));
        var match = (await _matches.GetAllAsync())[0];
        Assert.Empty(match.Photos);
        Assert.DoesNotContain("<", match.Map?.Query ?? string.Empty);
    }

    // ---------- QR de resultado del torneo ----------

    [Theory]
    [InlineData("PKR1:")]
    [InlineData("PKR1:a:b:c:d:e:f")]
    [InlineData("PKR1:<script>alert(1)</script>")]
    [InlineData("https://espia.example")]
    public void Garbage_result_qr_is_rejected(string text)
    {
        Assert.Throws<TournamentException>(() => MatchResultCode.Decode(text));
    }

    [Fact]
    public void Result_qr_with_a_changed_score_but_old_checksum_is_rejected()
    {
        var text = new MatchResultCode(Guid.NewGuid(), Guid.NewGuid(), 3, 1, MatchResultWinner.Own).Encode();
        var parts = text.Split(':');
        parts[3] = "9";

        Assert.Throws<TournamentException>(() => MatchResultCode.Decode(string.Join(':', parts)));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("99999999999")]
    [InlineData("1e3")]
    [InlineData(" 3")]
    public void Result_qr_with_hostile_scores_is_rejected(string score)
    {
        var body = $"{Guid.NewGuid():N}:{Guid.NewGuid():N}:{score}:1:G";
        var text = $"{MatchResultCode.Prefix}{body}:{Crc(body)}";

        Assert.Throws<TournamentException>(() => MatchResultCode.Decode(text));
    }

    [Fact]
    public void Absurd_scores_are_clamped()
    {
        var body = $"{Guid.NewGuid():N}:{Guid.NewGuid():N}:2000000000:0:G";
        var code = MatchResultCode.Decode($"{MatchResultCode.Prefix}{body}:{Crc(body)}");

        Assert.Equal(Tournament.MaxScore, code.OwnScore);
    }

    [Fact]
    public void Result_qr_too_long_is_rejected_before_parsing()
    {
        Assert.Throws<TournamentException>(() => MatchResultCode.Decode(MatchResultCode.Prefix + new string(':', 1_000_000)));
    }

    // ---------- Utilidades ----------

    private Task<BackupFile> ReadAsync(string json) => Backup.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private static string Crc(string body)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in Encoding.ASCII.GetBytes(body))
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
        }

        return (crc ^ 0xFFFFFFFFu).ToString("x8");
    }
}
