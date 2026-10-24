using System.Text.Json.Serialization;

namespace Punkartuchera.Core.Rules;

/// <summary>
/// Contenido del reglamento: reglas básicas, reglas especiales, equipo y facciones.
/// Se carga de los JSON de <c>Rules/Data</c> con <see cref="RulebookLoader"/>.
/// </summary>
public sealed class Rulebook
{
    public string Edition { get; set; } = string.Empty;

    public List<SourceDocument> Sources { get; set; } = [];

    public List<AttributeInfo> Attributes { get; set; } = [];

    public List<RuleSection> Sections { get; set; } = [];

    public List<SpecialRule> SpecialRules { get; set; } = [];

    /// <summary>Armas genéricas y de facción (estas con <see cref="Weapon.FactionId"/>).</summary>
    public List<Weapon> Weapons { get; set; } = [];

    public List<ArmorItem> Armors { get; set; } = [];

    public List<GearItem> Gear { get; set; } = [];

    [JsonIgnore]
    public List<FactionRules> Factions { get; } = [];

    public FactionRules? FindFaction(string? id) =>
        string.IsNullOrEmpty(id) ? null : Factions.FirstOrDefault(f => f.Id == id);

    public SourceDocument? FindSource(string? id) =>
        string.IsNullOrEmpty(id) ? null : Sources.FirstOrDefault(s => s.Id == id);

    /// <summary>
    /// Busca una regla especial por nombre. "Retraso (2)" encuentra "Retraso (X)".
    /// </summary>
    public SpecialRule? FindSpecialRule(string name)
    {
        var key = RulesText.Fold(RulesText.StripParameter(name));
        return SpecialRules.FirstOrDefault(r => RulesText.Fold(RulesText.StripParameter(r.Name)) == key);
    }

    /// <summary>Armas que puede usar una facción: las genéricas más las suyas.</summary>
    public IEnumerable<Weapon> WeaponsFor(string? factionId) =>
        Weapons.Where(w => w.FactionId is null || w.FactionId == factionId);
}

public sealed class SourceDocument
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}

public sealed class AttributeInfo
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;

    public int Min { get; set; }

    public int Max { get; set; } = 10;

    public int Default { get; set; }

    public string Description { get; set; } = string.Empty;
}

public sealed class RuleSection
{
    public string Id { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Source { get; set; }

    public int? Page { get; set; }

    public List<string> Keywords { get; set; } = [];

    /// <summary>Párrafos. Los que empiezan por "• " se muestran como lista.</summary>
    public List<string> Body { get; set; } = [];

    public RuleTable? Table { get; set; }
}

public sealed class RuleTable
{
    public List<string> Columns { get; set; } = [];

    public List<List<string>> Rows { get; set; } = [];
}

public sealed class SpecialRule
{
    public string Name { get; set; } = string.Empty;

    public string? Source { get; set; }

    public int? Page { get; set; }

    public string Text { get; set; } = string.Empty;

    [JsonIgnore]
    public string Slug => RulesText.Slug(Name);
}

public enum WeaponKind
{
    Melee,
    Ranged,
}

public sealed class Weapon
{
    public string Name { get; set; } = string.Empty;

    public WeaponKind Kind { get; set; }

    public int? Cost { get; set; }

    /// <summary>Modificador al Combate (solo cuerpo a cuerpo).</summary>
    public string? Combat { get; set; }

    /// <summary>Rangos corto/medio/largo en cm (solo a distancia).</summary>
    public string? Range { get; set; }

    public string Strength { get; set; } = string.Empty;

    public string Penetration { get; set; } = string.Empty;

    public List<string> Rules { get; set; } = [];

    public string? Source { get; set; }

    public int? Page { get; set; }

    public string? Notes { get; set; }

    /// <summary>Nulo si es equipo genérico.</summary>
    [JsonIgnore]
    public string? FactionId { get; set; }

    [JsonIgnore]
    public string Slug => RulesText.Slug(Name);
}

public sealed class ArmorItem
{
    public string Name { get; set; } = string.Empty;

    public string Armor { get; set; } = string.Empty;

    public int? Cost { get; set; }

    public List<string> Rules { get; set; } = [];

    public string? Source { get; set; }

    public int? Page { get; set; }

    public string? Notes { get; set; }

    [JsonIgnore]
    public string? FactionId { get; set; }

    [JsonIgnore]
    public string Slug => RulesText.Slug(Name);
}

public sealed class GearItem
{
    public string Name { get; set; } = string.Empty;

    public int? Cost { get; set; }

    public string Text { get; set; } = string.Empty;

    public string? Source { get; set; }

    public int? Page { get; set; }

    [JsonIgnore]
    public string? FactionId { get; set; }

    [JsonIgnore]
    public string Slug => RulesText.Slug(Name);
}

public sealed class FactionRules
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Source { get; set; }

    public int? Page { get; set; }

    public string Summary { get; set; } = string.Empty;

    /// <summary>Reglas publicadas como beta.</summary>
    public bool Beta { get; set; }

    /// <summary>Mercenarios: no es una facción jugable, se unen a otras.</summary>
    public bool IsMercenaries { get; set; }

    public List<string> Restrictions { get; set; } = [];

    public List<PriceOption> GenericGear { get; set; } = [];

    public List<NamedText> Rules { get; set; } = [];

    public List<Troop> Troops { get; set; } = [];

    public List<Weapon> Weapons { get; set; } = [];

    public List<ArmorItem> Armors { get; set; } = [];

    public List<GearItem> Gear { get; set; } = [];

    /// <summary>Listas propias: mutaciones, poderes psíquicos, artículos de coña…</summary>
    public List<ExtraGroup> Extras { get; set; } = [];
}

public sealed class PriceOption
{
    public string Name { get; set; } = string.Empty;

    public int? Cost { get; set; }

    /// <summary>El coste es por unidad (bombas, granadas…).</summary>
    public bool Each { get; set; }

    public string? Notes { get; set; }
}

public sealed class NamedText
{
    public string Name { get; set; } = string.Empty;

    public int? Cost { get; set; }

    /// <summary>Texto; los saltos de línea separan párrafos y las líneas con "• " son lista.</summary>
    public string Text { get; set; } = string.Empty;

    public RuleTable? Table { get; set; }

    [JsonIgnore]
    public string Slug => RulesText.Slug(Name);
}

public sealed class Troop
{
    public string Name { get; set; } = string.Empty;

    public int Cost { get; set; }

    public int? Page { get; set; }

    public bool Personality { get; set; }

    /// <summary>Atributos como texto ("4", "3 (5)", "-", "X"), por clave de <see cref="AttributeInfo"/>.</summary>
    public Dictionary<string, string> Stats { get; set; } = [];

    public string? WeaponRule { get; set; }

    /// <summary>Armas fijas (personalidades).</summary>
    public string? Weapons { get; set; }

    public List<PriceOption> WeaponOptions { get; set; } = [];

    public string? Armor { get; set; }

    public List<PriceOption> ArmorOptions { get; set; } = [];

    public string? Equipment { get; set; }

    public List<PriceOption> EquipmentOptions { get; set; } = [];

    public string? Mutations { get; set; }

    public List<string> SpecialRules { get; set; } = [];

    public List<NamedText> Rules { get; set; } = [];

    /// <summary>Perfiles que la acompañan (mascota, fieras…).</summary>
    public List<SubProfile> Profiles { get; set; } = [];

    [JsonIgnore]
    public string Slug => RulesText.Slug(Name);

    public string Stat(string key) => Stats.TryGetValue(key, out var value) ? value : "-";
}

public sealed class SubProfile
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, string> Stats { get; set; } = [];

    public string Stat(string key) => Stats.TryGetValue(key, out var value) ? value : "-";
}

public sealed class ExtraGroup
{
    public string Title { get; set; } = string.Empty;

    public string? Intro { get; set; }

    public int? Page { get; set; }

    public List<NamedText> Items { get; set; } = [];

    [JsonIgnore]
    public string Slug => RulesText.Slug(Title);
}
