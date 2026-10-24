using System.Text.Json;
using System.Text.Json.Serialization;

namespace Punkartuchera.Core.Rules;

/// <summary>
/// Carga el reglamento de los JSON incrustados en el ensamblado:
/// <c>Rules/Data/core.json</c> y un fichero por facción en <c>Rules/Data/factions</c>.
/// </summary>
public static class RulebookLoader
{
    private const string ResourcePrefix = "Punkartuchera.Rules.";
    private const string CoreResource = ResourcePrefix + "core.json";
    private const string FactionPrefix = ResourcePrefix + "factions.";

    /// <summary>Orden en que se muestran las facciones; las que no estén van al final.</summary>
    private static readonly string[] FactionOrder =
    [
        "pandilleros", "mutardos", "chatarreros", "sangre-negra", "v-reich",
        "idos", "irradiados", "troupe", "unami", "banda-del-comic", "mercenarios",
    ];

    public static Rulebook LoadEmbedded()
    {
        var assembly = typeof(RulebookLoader).Assembly;
        var book = Read(assembly.GetManifestResourceStream(CoreResource), RulesJsonContext.Default.Rulebook)
            ?? throw new InvalidOperationException("Falta el reglamento base.");

        var factions = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(FactionPrefix, StringComparison.Ordinal))
            .Select(name => Read(assembly.GetManifestResourceStream(name), RulesJsonContext.Default.FactionRules)
                ?? throw new InvalidOperationException($"Facción vacía: {name}"))
            .OrderBy(f => Array.IndexOf(FactionOrder, f.Id) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(f => f.Name, StringComparer.CurrentCulture);

        foreach (var faction in factions)
        {
            Add(book, faction);
        }

        return book;
    }

    /// <summary>Añade una facción y copia su equipo propio a las listas generales del reglamento.</summary>
    public static void Add(Rulebook book, FactionRules faction)
    {
        book.Factions.Add(faction);
        foreach (var weapon in faction.Weapons)
        {
            weapon.FactionId = faction.Id;
            weapon.Source ??= faction.Source;
            book.Weapons.Add(weapon);
        }

        foreach (var armor in faction.Armors)
        {
            armor.FactionId = faction.Id;
            armor.Source ??= faction.Source;
            book.Armors.Add(armor);
        }

        foreach (var gear in faction.Gear)
        {
            gear.FactionId = faction.Id;
            gear.Source ??= faction.Source;
            book.Gear.Add(gear);
        }
    }

    private static T? Read<T>(Stream? stream, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type)
    {
        if (stream is null)
        {
            return default;
        }

        using (stream)
        {
            return JsonSerializer.Deserialize(stream, type);
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(Rulebook))]
[JsonSerializable(typeof(FactionRules))]
public sealed partial class RulesJsonContext : JsonSerializerContext;
