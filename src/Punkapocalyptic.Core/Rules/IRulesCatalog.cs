namespace Punkapocalyptic.Core.Rules;

/// <summary>
/// Fuente de datos de reglas: facciones, atributos y tipos de miembro.
/// La implementación actual es provisional; se sustituirá por los datos del reglamento.
/// </summary>
public interface IRulesCatalog
{
    /// <summary>Indica que los datos no proceden del reglamento oficial.</summary>
    bool IsPlaceholder { get; }

    IReadOnlyList<Faction> Factions { get; }

    IReadOnlyList<StatDefinition> Stats { get; }

    IReadOnlyList<string> Roles { get; }

    Faction? FindFaction(string? id);
}

public sealed record Faction(string Id, string Name, string? Description = null);

public sealed record StatDefinition(string Key, string Name, string Abbreviation, int Min, int Max, int Default);
