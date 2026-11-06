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

    /// <summary>Fotos oficiales de las miniaturas, por tropa.</summary>
    [JsonIgnore]
    public List<OfficialMini> Minis { get; } = [];

    /// <summary>Listas preconfiguradas de las fichas de control oficiales.</summary>
    [JsonIgnore]
    public List<PresetList> Lists { get; } = [];

    public FactionRules? FindFaction(string? id) =>
        string.IsNullOrEmpty(id) ? null : Factions.FirstOrDefault(f => f.Id == id);

    /// <summary>Busca una subfacción por su id dentro de cualquier facción.</summary>
    public FactionRules? FindSubfaction(string? id) =>
        string.IsNullOrEmpty(id) ? null : Factions.SelectMany(f => f.Subfactions).FirstOrDefault(s => s.Id == id);

    public PresetList? FindList(string? id) =>
        string.IsNullOrEmpty(id) ? null : Lists.FirstOrDefault(l => l.Id == id);

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
    public IEnumerable<Weapon> WeaponsFor(string? factionId)
    {
        var shared = FindFaction(factionId)?.SharedWeapons ?? [];
        return Weapons.Where(w => w.FactionId is null || w.FactionId == factionId || shared.Contains(w.Name));
    }

    /// <summary>
    /// Armas de una facción o subfacción: las genéricas, las de la facción madre sin las
    /// exclusivas de otras subfacciones, y las prestadas de otras facciones.
    /// </summary>
    public IEnumerable<Weapon> WeaponsFor(FactionRules faction)
    {
        var parentId = faction.ParentId ?? faction.Id;
        var shared = faction.SharedWeapons.Concat(FindFaction(parentId)?.SharedWeapons ?? []).ToHashSet();
        return Weapons.Where(w => w.FactionId is null
            || (w.FactionId == parentId && (w.SubfactionId is null || w.SubfactionId == faction.Id))
            || shared.Contains(w.Name));
    }
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

    /// <summary>Solo para esta subfacción.</summary>
    [JsonIgnore]
    public string? SubfactionId { get; set; }

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

    /// <summary>Solo para esta subfacción.</summary>
    [JsonIgnore]
    public string? SubfactionId { get; set; }

    [JsonIgnore]
    public string Slug => RulesText.Slug(Name);
}

public sealed class GearItem
{
    public string Name { get; set; } = string.Empty;

    public int? Cost { get; set; }

    /// <summary>Cuántos puede haber como máximo en toda la banda (Gato parameño, Tótem…).</summary>
    public int? PerBand { get; set; }

    /// <summary>Solo se puede comprar si ya lleva una de estas armas (Arma incrustada: hoz, filo pequeño o filo medio).</summary>
    public List<string> Requires { get; set; } = [];

    /// <summary>Palabras que, en el nombre de lo que lleva, cuentan como este objeto ("incrustado").</summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>Con qué no se combina en la misma miniatura (un solo tipo de munición…), como en <see cref="NamedText.Bans"/>.</summary>
    public List<string> Bans { get; set; } = [];

    public string Text { get; set; } = string.Empty;

    public string? Source { get; set; }

    public int? Page { get; set; }

    [JsonIgnore]
    public string? FactionId { get; set; }

    /// <summary>Solo para esta subfacción.</summary>
    [JsonIgnore]
    public string? SubfactionId { get; set; }

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

    /// <summary>Cómo se juega: estilo y puntos fuertes, resumidos de sus reglas.</summary>
    public string? Playstyle { get; set; }

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

    /// <summary>Armas de otra facción que esta también puede usar (por nombre).</summary>
    public List<string> SharedWeapons { get; set; } = [];

    /// <summary>Subfacciones: equipo de la facción madre que no se puede comprar (todas las tropas o solo algunas).</summary>
    public List<GearBan> BannedGear { get; set; } = [];

    /// <summary>Subfacciones: listas de la facción madre que no se pueden comprar ("Artículos de coña").</summary>
    public List<string> BannedExtras { get; set; } = [];

    /// <summary>Tropas que no pueden comprar el equipo genérico de la facción.</summary>
    public List<string> NoGenericGear { get; set; } = [];

    /// <summary>
    /// Subfacciones: tropas de la facción madre que puede reclutar. Sus personalidades solo si se nombran
    /// (cuando la subfacción admite «una Personalidad» sin decir de quién).
    /// </summary>
    public List<string> BaseTroops { get; set; } = [];

    /// <summary>Subfacciones: nombre que reciben aquí las tropas de la facción madre ("Piltrafa" → "Ninja negro").</summary>
    public Dictionary<string, string> TroopNames { get; set; } = [];

    /// <summary>No admite Mercenarios.</summary>
    public bool NoMercenaries { get; set; }

    /// <summary>Si es una subfacción, la facción a la que pertenece.</summary>
    [JsonIgnore]
    public string? ParentId { get; set; }

    [JsonIgnore]
    public bool IsSubfaction => ParentId is not null;

    [JsonIgnore]
    public List<FactionRules> Subfactions { get; } = [];
}

public sealed class PriceOption
{
    public string Name { get; set; } = string.Empty;

    public int? Cost { get; set; }

    /// <summary>El coste es por unidad (bombas, granadas…).</summary>
    public bool Each { get; set; }

    /// <summary>Tropas que no pueden comprarlo ("Todos menos los Subhumanos").</summary>
    public List<string> ExcludeTroops { get; set; } = [];

    public string? Notes { get; set; }
}

public sealed class NamedText
{
    public string Name { get; set; } = string.Empty;

    public int? Cost { get; set; }

    /// <summary>Texto; los saltos de línea separan párrafos y las líneas con "• " son lista.</summary>
    public string Text { get; set; } = string.Empty;

    public RuleTable? Table { get; set; }

    /// <summary>
    /// Lo que la regla no deja llevar: nombres de objetos ("Arma incrustada"), reglas especiales
    /// que tenga el objeto ("Arma de fuego") o "Armas" y "Blindaje" para toda esa clase de equipo.
    /// </summary>
    public List<string> Bans { get; set; } = [];

    /// <summary>A quién se aplica una regla de facción o subfacción. Vacío: a todas las tropas.</summary>
    public List<string> Troops { get; set; } = [];

    /// <summary>Tropas a las que no se aplica ("Nadie salvo los Tenaceros…").</summary>
    public List<string> ExceptTroops { get; set; } = [];

    /// <summary>El veto es para el resto de la banda, no para quien tiene la regla (Imperdonable).</summary>
    public bool BansOthers { get; set; }

    /// <summary>Quien tiene la regla no está obligado a llevar arma.</summary>
    public bool WeaponOptional { get; set; }

    [JsonIgnore]
    public string Slug => RulesText.Slug(Name);
}

public sealed class Troop
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Tropa de la facción madre a la que sustituye esta versión de la subfacción.</summary>
    public string? Replaces { get; set; }

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

    /// <summary>Cuántas miniaturas de la banda pueden llevar la misma (Reliquias: 1, Mutaciones: 3).</summary>
    public int? PerBand { get; set; }

    /// <summary>Las que no cuentan para <see cref="PerBand"/>.</summary>
    public List<string> PerBandExcept { get; set; } = [];

    /// <summary>Quién puede comprar de esta lista y cuánto. Vacío: cualquier tropa que no tenga el equipo cerrado.</summary>
    public List<ExtraBuyer> Buyers { get; set; } = [];

    public int? Page { get; set; }

    public List<NamedText> Items { get; set; } = [];

    [JsonIgnore]
    public string Slug => RulesText.Slug(Title);
}

/// <summary>Equipo vetado en una subfacción, para todas las tropas o solo para algunas.</summary>
public sealed class GearBan
{
    public List<string> Names { get; set; } = [];

    /// <summary>Vacío: ninguna tropa.</summary>
    public List<string> Troops { get; set; } = [];
}

/// <summary>Una tropa que puede comprar de una lista propia, con sus límites.</summary>
public sealed class ExtraBuyer
{
    /// <summary>Nombre de la tropa, o "*" para cualquiera.</summary>
    public string Troop { get; set; } = string.Empty;

    /// <summary>Cuántas puede llevar como mínimo (aviso si no llega).</summary>
    public int Min { get; set; }

    /// <summary>Cuántas puede llevar como máximo.</summary>
    public int? Max { get; set; }

    /// <summary>Puntos máximos que puede gastar en la lista.</summary>
    public int? MaxPoints { get; set; }

    /// <summary>Puntos mínimos que tiene que gastar (aviso si no llega).</summary>
    public int MinPoints { get; set; }

    /// <summary>Si solo puede elegir algunas de la lista.</summary>
    public List<string> Only { get; set; } = [];
}

/// <summary>Una banda lista para jugar tal como viene en una ficha de control oficial.</summary>
public sealed class PresetList
{
    public string Id { get; set; } = string.Empty;

    public string FactionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Url { get; set; }

    public List<PresetMember> Members { get; set; } = [];

    [JsonIgnore]
    public int Cost => Members.Sum(m => m.Cost);
}

public sealed class PresetMember
{
    /// <summary>Tipo de tropa de la facción (se usa para su perfil).</summary>
    public string Troop { get; set; } = string.Empty;

    /// <summary>Nombre impreso en la ficha si no coincide con la tropa ("Jefa", "Curtida"…).</summary>
    public string? Name { get; set; }

    public int Cost { get; set; }

    /// <summary>Atributos si la ficha los da modificados; si no, los de la tropa.</summary>
    public Dictionary<string, string>? Stats { get; set; }

    public List<string> Weapons { get; set; } = [];

    public string? Armor { get; set; }

    public List<string> Gear { get; set; } = [];

    /// <summary>Mutaciones, reglas o mejoras compradas.</summary>
    public List<string> Skills { get; set; } = [];
}

public sealed class PresetListFile
{
    public List<PresetList> Lists { get; set; } = [];
}

/// <summary>Una miniatura oficial pintada, con la tropa a la que representa.</summary>
public sealed class OfficialMini
{
    /// <summary>Nombre de la tropa, personalidad o mercenario en el reglamento.</summary>
    public string Troop { get; set; } = string.Empty;

    public string FactionId { get; set; } = string.Empty;

    /// <summary>Si la tropa es propia de una subfacción.</summary>
    public string? SubfactionId { get; set; }

    /// <summary>Nombre de la miniatura en la tienda ("Ascua con escopeta").</summary>
    public string Name { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    /// <summary>Página de la miniatura en la tienda oficial.</summary>
    public string? Url { get; set; }
}

public sealed class OfficialMiniFile
{
    public string Source { get; set; } = string.Empty;

    public string? SourceUrl { get; set; }

    public List<OfficialMini> Minis { get; set; } = [];
}
