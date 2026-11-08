namespace Punkartuchera.Core.Models;

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

    /// <summary>Subfacción elegida, si la hay.</summary>
    public string? SubfactionId { get; set; }

    public string? Notes { get; set; }

    /// <summary>Propia (editable) o rival importada (solo lectura).</summary>
    public BandOwnership Ownership { get; set; } = BandOwnership.Own;

    /// <summary>
    /// En las bandas rivales, el identificador que tenía la banda en el dispositivo del rival.
    /// Sirve para sustituir la copia anterior si el rival la vuelve a pasar.
    /// </summary>
    public Guid? SourceId { get; set; }

    /// <summary>
    /// Nickname de quien pasó la banda (solo en las rivales). En los torneos se propone como nombre
    /// del jugador, y el organizador lo puede cambiar allí.
    /// </summary>
    public string? PlayerName { get; set; }

    public List<Miniature> Members { get; set; } = [];

    /// <summary>
    /// En las bandas rivales, el coste total de la banda. El de cada miniatura no se comparte:
    /// con él y el equipo a la vista se podrían deducir sus balas.
    /// </summary>
    public int? TotalCost { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Versión del formato guardado, para poder migrar datos antiguos.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public bool IsReadOnly => Ownership == BandOwnership.Rival;

    /// <summary>Coste de la banda: la suma de sus miniaturas, o el total recibido si es rival.</summary>
    public int? Cost => IsReadOnly
        ? TotalCost
        : Members.Any(m => m.Cost is not null) ? Members.Sum(m => m.Cost ?? 0) : null;

    /// <summary>
    /// Deja solo el coste total de la banda y quita el de cada miniatura (y lo que permite rehacerlo),
    /// para que el rival no pueda deducir cuántas balas lleva cada una.
    /// </summary>
    public void KeepOnlyTotalCost()
    {
        if (TotalCost is null && Members.Any(m => m.Cost is not null))
        {
            TotalCost = Members.Sum(m => m.Cost ?? 0);
        }

        foreach (var member in Members)
        {
            member.Cost = null;
            member.BaseCost = null;
            member.BulletPrice = null;
            member.Included = [];
        }
    }

    public Miniature? FindMember(Guid memberId) => Members.FirstOrDefault(m => m.Id == memberId);
}

public enum BandOwnership
{
    Own,
    Rival
}
