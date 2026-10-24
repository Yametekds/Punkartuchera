namespace Punkartuchera.Core.Rules;

/// <summary>
/// DATOS PROVISIONALES. Los nombres de facción son los publicados en la web oficial,
/// pero los atributos y los tipos de miembro son de relleno hasta cargar el reglamento.
/// </summary>
public sealed class PlaceholderRulesCatalog : IRulesCatalog
{
    public bool IsPlaceholder => true;

    public IReadOnlyList<Faction> Factions { get; } =
    [
        new("pandilleros", "Pandilleros"),
        new("mutardos", "Mutardos"),
        new("chatarreros", "Chatarreros"),
        new("sangre-negra", "Sangre Negra"),
        new("v-reich", "V Reich"),
        new("idos", "Idos"),
        new("irradiados", "Irradiados"),
        new("troupe", "Troupé"),
        new("unami", "Unami"),
        new("banda-del-comic", "La Banda del Cómic"),
    ];

    // PROVISIONAL: sustituir por los atributos reales del reglamento.
    public IReadOnlyList<StatDefinition> Stats { get; } =
    [
        new("mov", "Movimiento (provisional)", "MOV", 0, 10, 4),
        new("com", "Combate (provisional)", "COM", 0, 10, 3),
        new("pun", "Puntería (provisional)", "PUN", 0, 10, 3),
        new("res", "Resistencia (provisional)", "RES", 0, 10, 3),
        new("her", "Heridas (provisional)", "HER", 1, 10, 1),
    ];

    // PROVISIONAL: sustituir por los tipos de miembro reales de cada facción.
    public IReadOnlyList<string> Roles { get; } =
    [
        "Líder",
        "Veterano",
        "Miembro",
        "Novato",
        "Bestia",
    ];

    public Faction? FindFaction(string? id) =>
        string.IsNullOrEmpty(id) ? null : Factions.FirstOrDefault(f => f.Id == id);
}
