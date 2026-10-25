namespace Punkartuchera.Core.Rules;

/// <summary>
/// Fuente de datos de reglas para las bandas: facciones, atributos y tipos de miembro.
/// El contenido completo del reglamento está en <see cref="Rulebook"/>.
/// </summary>
public interface IRulesCatalog
{
    /// <summary>Indica que los datos no proceden del reglamento oficial.</summary>
    bool IsPlaceholder { get; }

    IReadOnlyList<Faction> Factions { get; }

    IReadOnlyList<StatDefinition> Stats { get; }

    /// <summary>Tipos de tropa de todas las facciones, para sugerirlos al editar un miembro.</summary>
    IReadOnlyList<string> Roles { get; }

    Faction? FindFaction(string? id);
}

public sealed record Faction(string Id, string Name, string? Description = null);

public sealed record StatDefinition(string Key, string Name, string Abbreviation, int Min, int Max, int Default);
