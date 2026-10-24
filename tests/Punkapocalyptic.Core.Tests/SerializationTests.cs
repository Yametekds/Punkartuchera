using Punkapocalyptic.Core.Matches;
using Punkapocalyptic.Core.Models;
using Punkapocalyptic.Core.Serialization;

namespace Punkapocalyptic.Core.Tests;

public class SerializationTests
{
    [Fact]
    public void Band_round_trips_through_json()
    {
        var band = new Band
        {
            Name = "Los Oxidados",
            FactionId = "chatarreros",
            Ownership = BandOwnership.Rival,
            Members =
            [
                new Miniature
                {
                    Name = "Morro",
                    Role = "Líder",
                    Ammo = 4,
                    Photo = PhotoRef.Stock("cresta"),
                    Stats = { ["mov"] = 5 },
                    Skills = ["Duro"],
                    Gear = [new Equipment { Name = "Tubería", Notes = "Oxidada" }],
                },
            ],
        };

        var json = BandSerializer.Serialize(band);
        var copy = BandSerializer.Deserialize(json);

        Assert.Contains("\"ownership\":\"Rival\"", json);
        Assert.Equal(band.Name, copy.Name);
        var member = Assert.Single(copy.Members);
        Assert.Equal(PhotoRef.Stock("cresta"), member.Photo);
        Assert.Equal(5, member.Stats["mov"]);
        Assert.Equal("Oxidada", member.Gear[0].Notes);
        Assert.Equal(4, member.Ammo);
    }

    [Fact]
    public async Task Local_match_service_has_no_online_mode()
    {
        var matches = new LocalMatchService();

        var match = await matches.StartLocalAsync(Guid.NewGuid(), null);

        Assert.Same(match, matches.Current);
        Assert.False(matches.SupportsOnlineMatches);
        await Assert.ThrowsAsync<OnlineMatchesUnavailableException>(() => matches.CreateOnlineAsync(Guid.NewGuid()));
    }
}
