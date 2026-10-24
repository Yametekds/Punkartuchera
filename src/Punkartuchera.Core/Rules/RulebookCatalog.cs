namespace Punkartuchera.Core.Rules;

/// <summary>
/// Catálogo de facciones, atributos y tipos de miembro sacado del reglamento oficial.
/// </summary>
public sealed class RulebookCatalog : IRulesCatalog
{
    public RulebookCatalog(Rulebook book)
    {
        Book = book;
        Factions = book.Factions
            .Where(f => !f.IsMercenaries)
            .Select(f => new Faction(f.Id, f.Name, f.Summary))
            .ToList();
        Stats = book.Attributes
            .Select(a => new StatDefinition(a.Key, a.Name, a.Abbreviation, a.Min, a.Max, a.Default))
            .ToList();
        Roles = book.Factions
            .SelectMany(f => f.Troops)
            .Select(t => t.Name)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public Rulebook Book { get; }

    public bool IsPlaceholder => false;

    public IReadOnlyList<Faction> Factions { get; }

    public IReadOnlyList<StatDefinition> Stats { get; }

    public IReadOnlyList<string> Roles { get; }

    public Faction? FindFaction(string? id) =>
        string.IsNullOrEmpty(id) ? null : Factions.FirstOrDefault(f => f.Id == id);
}
