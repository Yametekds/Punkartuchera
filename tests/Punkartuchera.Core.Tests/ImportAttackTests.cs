using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Services;
using Punkartuchera.Core.Sharing;
using Punkartuchera.Core.Storage;

namespace Punkartuchera.Core.Tests;

/// <summary>
/// Ataques simulados contra la importación de bandas rivales: archivos <c>.punkband</c> y QR
/// manipulados a mano por alguien que quiere romper la app, colar contenido o espiar al que la abre.
/// </summary>
public class ImportAttackTests
{
    private static readonly RulebookCatalog Rules = new(RulebookLoader.LoadEmbedded());
    private static readonly string Jpeg = Convert.ToBase64String([0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46]);

    private readonly InMemoryBandRepository _bands = new();
    private readonly InMemoryPhotoStore _photos = new();
    private readonly BandExchangeService _exchange;

    public ImportAttackTests()
    {
        _exchange = new BandExchangeService(_bands, _photos, Rules);
    }

    // ---------- Archivos rotos o que no son bandas ----------

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("\"punkartuchera-band\"")]
    [InlineData("{\"format\":\"punkartuchera-band\",\"band\":null}")]
    [InlineData("{\"format\":\"otra-cosa\",\"band\":{}}")]
    [InlineData("{\"format\":\"punkartuchera-band\",\"version\":\"uno\",\"band\":{}}")]
    [InlineData("{\"format\":\"punkartuchera-band\",\"band\":{\"members\":[{\"stats\":{\"mov\":\"9999999999999\"}}]}}")]
    [InlineData("{\"format\":\"punkartuchera-band\",\"band\":{\"id\":\"no-es-un-guid\"}}")]
    [InlineData("{\"format\":\"punkartuchera-band\",\"band\":{},\"photos\":[{\"data\":\"%%%no-es-base64\"}]}")]
    public async Task Malformed_files_are_rejected_with_a_friendly_message(string json)
    {
        await Assert.ThrowsAsync<BandExchangeException>(() => ImportAsync(json));
        Assert.Empty(await _bands.GetAllAsync());
    }

    [Fact]
    public async Task Binary_garbage_is_rejected()
    {
        var garbage = new byte[64 * 1024];
        new Random(7).NextBytes(garbage);

        await Assert.ThrowsAsync<BandExchangeException>(() => _exchange.ImportAsync(new MemoryStream(garbage)));
    }

    [Fact]
    public async Task Deeply_nested_json_is_rejected_without_blowing_the_stack()
    {
        var json = "{\"format\":\"punkartuchera-band\",\"band\":{\"notes\":" + new string('[', 100_000) + new string(']', 100_000) + "}}";

        await Assert.ThrowsAsync<BandExchangeException>(() => ImportAsync(json));
    }

    [Fact]
    public async Task Newer_versions_are_rejected()
    {
        var ex = await Assert.ThrowsAsync<BandExchangeException>(() =>
            ImportAsync("{\"format\":\"punkartuchera-band\",\"version\":999,\"band\":{\"name\":\"X\"}}"));
        Assert.Contains("versión", ex.Message);
    }

    [Fact]
    public async Task Null_entries_inside_lists_do_not_crash_the_import()
    {
        var rival = await ImportAsync("""
            {"format":"punkartuchera-band","band":{"name":"Nulos","factionId":"pandilleros",
              "members":[null,{"name":"Uno","stats":null,"skills":[null,"Duro"],"gear":[null,{"name":null},{"name":"Tubería"}],"photo":null}]},
             "photos":[null,{"key":null,"contentType":null,"data":null}]}
            """);

        var member = Assert.Single(rival.Members);
        Assert.Equal("Uno", member.Name);
        Assert.Equal(["Duro"], member.Skills);
        Assert.Equal("Tubería", Assert.Single(member.Gear).Name);
    }

    // ---------- Intentos de saltarse las reglas del intercambio ----------

    [Fact]
    public async Task A_file_cannot_make_itself_an_editable_band_or_bring_ammo_or_notes()
    {
        var rival = await ImportAsync("""
            {"format":"punkartuchera-band","band":{"name":"Trampa","factionId":"pandilleros","ownership":"Own","notes":"Mis planes",
              "sourceId":"11111111-1111-1111-1111-111111111111","schemaVersion":99,
              "members":[{"name":"Tramposo","ammo":99}]}}
            """);

        Assert.Equal(BandOwnership.Rival, rival.Ownership);
        Assert.True(rival.IsReadOnly);
        Assert.Null(rival.Notes);
        Assert.Equal(0, rival.Members[0].Ammo);
        Assert.Equal(Band.CurrentSchemaVersion, rival.SchemaVersion);
    }

    [Fact]
    public async Task Stats_out_of_range_are_clamped_and_unknown_keys_dropped()
    {
        var rival = await ImportAsync("""
            {"format":"punkartuchera-band","band":{"name":"Chetados","factionId":"pandilleros",
              "members":[{"name":"Hulk","cost":-500,"stats":{"c":999,"a":-999,"<img src=x onerror=alert(1)>":5}}]}}
            """);

        var member = rival.Members[0];
        var c = Rules.Stats.Single(s => s.Key == "c");
        var a = Rules.Stats.Single(s => s.Key == "a");
        Assert.Equal(c.Max, member.Stats["c"]);
        Assert.Equal(a.Min, member.Stats["a"]);
        Assert.All(member.Stats.Keys, k => Assert.Contains(Rules.Stats, s => s.Key == k));
        Assert.Equal(0, member.Cost);
    }

    [Fact]
    public async Task Duplicated_member_ids_are_replaced()
    {
        var rival = await ImportAsync("""
            {"format":"punkartuchera-band","band":{"name":"Clones","factionId":"pandilleros",
              "members":[{"id":"22222222-2222-2222-2222-222222222222","name":"A"},{"id":"22222222-2222-2222-2222-222222222222","name":"B"}]}}
            """);

        Assert.Equal(2, rival.Members.Select(m => m.Id).Distinct().Count());
    }

    // ---------- Tamaños desproporcionados ----------

    [Fact]
    public async Task Huge_bands_are_trimmed()
    {
        var hugeName = new string('A', 1_000_000);
        var members = string.Join(',', Enumerable.Range(0, 2_000).Select(i =>
            $"{{\"name\":\"M{i}\",\"role\":\"{hugeName[..5000]}\",\"skills\":[{string.Join(',', Enumerable.Repeat("\"S\"", 500))}]}}"));
        var json = $"{{\"format\":\"punkartuchera-band\",\"band\":{{\"name\":\"{hugeName}\",\"factionId\":\"pandilleros\",\"members\":[{members}]}}}}";

        var rival = await ImportAsync(json);

        Assert.True(rival.Name.Length <= BandService.MaxNameLength);
        Assert.True(rival.Members.Count <= RivalBandSanitizer.MaxMembers);
        Assert.All(rival.Members, m =>
        {
            Assert.True(m.Role.Length <= RivalBandSanitizer.MaxTextLength);
            Assert.True(m.Skills.Count <= RivalBandSanitizer.MaxListItems);
        });
    }

    [Fact]
    public async Task Files_over_the_size_limit_are_rejected()
    {
        // JSON válido, pero más grande de lo permitido.
        var head = "{\"format\":\"punkartuchera-band\",\"band\":{\"name\":\"Gorda\",\"factionId\":\"pandilleros\",\"notes\":\"";
        var json = head + new string('A', (int)BandExchangeService.MaxFileBytes) + "\"}}";

        await Assert.ThrowsAsync<BandExchangeException>(() => ImportAsync(json));
    }

    // ---------- Imágenes manipuladas ----------

    [Theory]
    [InlineData("image/png", "<svg xmlns='http://www.w3.org/2000/svg' onload='alert(1)'/>")]
    [InlineData("image/jpeg", "<html><script>alert(1)</script></html>")]
    [InlineData("image/webp", "MZ\u0090\u0000 ejecutable de Windows")]
    public async Task Photos_whose_bytes_are_not_the_declared_image_are_dropped(string contentType, string payload)
    {
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        var rival = await ImportAsync($$$"""
            {"format":"punkartuchera-band","band":{"name":"Fotos","factionId":"pandilleros",
              "members":[{"name":"Uno","photo":{"source":"Stored","key":"f"}}]},
             "photos":[{"key":"f","contentType":"{{{contentType}}}","data":"{{{data}}}"}]}
            """);

        Assert.Null(rival.Members[0].Photo);
    }

    [Fact]
    public async Task Svg_photos_are_dropped()
    {
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg onload='alert(1)'/>"));
        var rival = await ImportAsync($$$"""
            {"format":"punkartuchera-band","band":{"name":"Fotos","factionId":"pandilleros",
              "members":[{"name":"Uno","photo":{"source":"Stored","key":"f"}}]},
             "photos":[{"key":"f","contentType":"image/svg+xml","data":"{{{data}}}"}]}
            """);

        Assert.Null(rival.Members[0].Photo);
    }

    [Fact]
    public async Task Real_images_still_get_through()
    {
        var rival = await ImportAsync($$$"""
            {"format":"punkartuchera-band","band":{"name":"Fotos","factionId":"pandilleros",
              "members":[{"name":"Uno","photo":{"source":"Stored","key":"f"}}]},
             "photos":[{"key":"f","contentType":"image/jpeg","data":"{{{Jpeg}}}"}]}
            """);

        Assert.Equal(PhotoSource.Stored, rival.Members[0].Photo!.Source);
    }

    [Fact]
    public async Task Oversized_photos_are_dropped()
    {
        var big = new byte[RivalBandSanitizer.MaxPhotoBytes + 1];
        big[0] = 0xFF; big[1] = 0xD8; big[2] = 0xFF;
        var rival = await ImportAsync($$$"""
            {"format":"punkartuchera-band","band":{"name":"Fotos","factionId":"pandilleros",
              "members":[{"name":"Uno","photo":{"source":"Stored","key":"f"}}]},
             "photos":[{"key":"f","contentType":"image/jpeg","data":"{{{Convert.ToBase64String(big)}}}"}]}
            """);

        Assert.Null(rival.Members[0].Photo);
    }

    [Theory]
    [InlineData("Official", "https://espia.example/pixel.gif?victima=1")]
    [InlineData("Official", "javascript:alert(1)")]
    [InlineData("Official", "data:image/svg+xml,<svg onload=alert(1)>")]
    [InlineData("Official", "http://www.badrollgames.com/wp-content/uploads/2026/11/BR1CR07A.webp")]
    [InlineData("Stock", "../../../../index.html")]
    [InlineData("Stock", "https://espia.example/x")]
    [InlineData("Stock", "no-existe")]
    public async Task Photo_references_outside_the_app_are_dropped(string source, string key)
    {
        var rival = await ImportAsync($$$"""
            {"format":"punkartuchera-band","band":{"name":"Espía","factionId":"pandilleros",
              "members":[{"name":"Uno","photo":{"source":"{{{source}}}","key":"{{{key}}}"}}]}}
            """);

        Assert.Null(rival.Members[0].Photo);
    }

    [Fact]
    public async Task Official_photos_from_the_catalog_are_kept()
    {
        var url = Rules.Book.Minis[0].Image;
        var rival = await ImportAsync($$$"""
            {"format":"punkartuchera-band","band":{"name":"Oficial","factionId":"pandilleros",
              "members":[{"name":"Uno","photo":{"source":"Official","key":"{{{url}}}"}},{"name":"Dos","photo":{"source":"Stock","key":"cresta"}}]}}
            """);

        Assert.Equal(PhotoRef.Official(url), rival.Members[0].Photo);
        Assert.Equal(PhotoRef.Stock("cresta"), rival.Members[1].Photo);
    }

    // ---------- QR manipulados ----------

    [Fact]
    public void Qr_with_a_tampered_checksum_is_rejected()
    {
        var band = new Band { Name = "Buena", FactionId = "pandilleros", Members = [new Miniature { Name = "A" }] };
        var text = BandQrCodec.Encode(band, Rules.Stats.Select(s => s.Key).ToList()).Text;
        var bytes = Base45.Decode(text[BandQrCodec.Prefix.Length..]);
        bytes[^1] ^= 0xFF;

        Assert.Throws<BandExchangeException>(() => BandQrCodec.Decode(BandQrCodec.Prefix + Base45.Encode(bytes)));
    }

    [Theory]
    [InlineData("PKB1:")]
    [InlineData("PKB1:A")]
    [InlineData("PKB1:abc")]
    [InlineData("PKB1:<script>alert(1)</script>")]
    [InlineData("PKB1:GGW")]
    [InlineData("https://espia.example")]
    public void Garbage_qr_text_is_rejected(string text)
    {
        Assert.Throws<BandExchangeException>(() => BandQrCodec.Decode(text));
    }

    [Fact]
    public void Absurdly_long_qr_text_is_rejected_before_decoding()
    {
        var text = BandQrCodec.Prefix + new string('0', 3_000_000);

        Assert.Throws<BandExchangeException>(() => BandQrCodec.Decode(text));
    }

    [Fact]
    public void Qr_decompression_bomb_is_rejected()
    {
        // Un QR pequeño que, descomprimido, serían megas de JSON.
        var json = "{\"n\":\"" + new string('A', 20_000_000) + "\",\"m\":[]}";
        var text = PackRawQr(Encoding.UTF8.GetBytes(json));
        Assert.True(text.Length < 100_000);

        Assert.Throws<BandExchangeException>(() => BandQrCodec.Decode(text));
    }

    [Fact]
    public async Task Qr_with_nulls_and_hostile_values_imports_safely()
    {
        var json = """
            {"i":"33333333333333333333333333333333","n":"<script>alert(1)</script>","f":"pandilleros",
             "k":["c","zz"],"m":[null,{"n":"A","s":[999,5,7,8],"h":[null,"Duro"],"g":[[],[null],["Tubería",null]],"c":-1,"p":"../../x"}]}
            """;

        var rival = await _exchange.ImportQrAsync(PackRawQr(Encoding.UTF8.GetBytes(json)));

        Assert.Equal("<script>alert(1)</script>", rival.Name); // se guarda tal cual; Blazor lo escapa al pintarlo
        var member = Assert.Single(rival.Members);
        Assert.Equal(Rules.Stats.Single(s => s.Key == "c").Max, member.Stats["c"]);
        Assert.False(member.Stats.ContainsKey("zz"));
        Assert.Equal(["Duro"], member.Skills);
        Assert.Equal("Tubería", Assert.Single(member.Gear).Name);
        Assert.Null(member.Photo);
        Assert.Equal(0, member.Cost);
    }

    // ---------- Utilidades ----------

    private Task<Band> ImportAsync(string json) => _exchange.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private static string PackRawQr(byte[] json)
    {
        using var buffer = new MemoryStream();
        buffer.Write(new byte[4]);
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(json);
        }

        var bytes = buffer.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(bytes, Crc32For(bytes.AsSpan(4)));
        return BandQrCodec.Prefix + Base45.Encode(bytes);
    }

    private static uint Crc32For(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
