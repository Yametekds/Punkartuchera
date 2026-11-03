using System.Text.Json;
using System.Text.Json.Serialization;

namespace Punkartuchera.Core.Rules;

/// <summary>
/// Carga el reglamento de los JSON incrustados en el ensamblado:
/// <c>Rules/Data/core.json</c>, un fichero por facción en <c>Rules/Data/factions</c>,
/// sus subfacciones en <c>Rules/Data/subfactions</c> y las listas preconfiguradas de <c>lists.json</c>.
/// </summary>
public static class RulebookLoader
{
    private const string ResourcePrefix = "Punkartuchera.Rules.";
    private const string CoreResource = ResourcePrefix + "core.json";
    private const string FactionPrefix = ResourcePrefix + "factions.";
    private const string SubfactionPrefix = ResourcePrefix + "subfactions.";
    private const string ListsResource = ResourcePrefix + "lists.json";

    /// <summary>Orden en que se muestran las facciones; las que no estén van al final.</summary>
    private static readonly string[] FactionOrder =
    [
        "pandilleros", "mutardos", "chatarreros", "sangre-negra", "v-reich",
        "idos", "irradiados", "troupe", "unami", "craabianos", "banda-del-comic", "mercenarios",
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
            var subfactions = Read(assembly.GetManifestResourceStream($"{SubfactionPrefix}{faction.Id}.json"), RulesJsonContext.Default.ListFactionRules);
            foreach (var subfaction in subfactions ?? [])
            {
                AddSubfaction(book, faction, subfaction);
            }
        }

        var lists = Read(assembly.GetManifestResourceStream(ListsResource), RulesJsonContext.Default.PresetListFile);
        book.Lists.AddRange(lists?.Lists ?? []);

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

    /// <summary>
    /// Cuelga una subfacción de su facción. Su equipo propio va a las listas generales
    /// marcado con la facción madre y la subfacción.
    /// </summary>
    public static void AddSubfaction(Rulebook book, FactionRules parent, FactionRules subfaction)
    {
        subfaction.ParentId = parent.Id;
        subfaction.Source ??= $"subfacciones-{parent.Id}";
        parent.Subfactions.Add(subfaction);
        foreach (var weapon in subfaction.Weapons)
        {
            weapon.FactionId = parent.Id;
            weapon.SubfactionId = subfaction.Id;
            weapon.Source ??= subfaction.Source;
            book.Weapons.Add(weapon);
        }

        foreach (var armor in subfaction.Armors)
        {
            armor.FactionId = parent.Id;
            armor.SubfactionId = subfaction.Id;
            armor.Source ??= subfaction.Source;
            book.Armors.Add(armor);
        }

        foreach (var gear in subfaction.Gear)
        {
            gear.FactionId = parent.Id;
            gear.SubfactionId = subfaction.Id;
            gear.Source ??= subfaction.Source;
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
[JsonSerializable(typeof(List<FactionRules>))]
[JsonSerializable(typeof(PresetListFile))]
public sealed partial class RulesJsonContext : JsonSerializerContext;
