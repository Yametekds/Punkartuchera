namespace Punkapocalyptic.Core.Models;

/// <summary>
/// Una banda: el conjunto de miniaturas con las que juega un jugador.
/// </summary>
public sealed class Band
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Identificador de la facción en el catálogo de reglas.</summary>
    public string FactionId { get; set; } = string.Empty;

    public string? Notes { get; set; }

    /// <summary>Propia (editable) o rival importada (solo lectura).</summary>
    public BandOwnership Ownership { get; set; } = BandOwnership.Own;

    public List<Miniature> Members { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Versión del formato guardado, para poder migrar datos antiguos.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public bool IsReadOnly => Ownership == BandOwnership.Rival;

    public Miniature? FindMember(Guid memberId) => Members.FirstOrDefault(m => m.Id == memberId);
}

public enum BandOwnership
{
    Own,
    Rival
}
