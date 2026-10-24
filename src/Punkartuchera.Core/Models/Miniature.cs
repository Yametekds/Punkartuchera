namespace Punkartuchera.Core.Models;

/// <summary>
/// Un miembro de la banda (una miniatura en la mesa).
/// </summary>
public sealed class Miniature
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Tipo o rol dentro de la banda (líder, pandillero...).</summary>
    public string Role { get; set; } = string.Empty;

    public PhotoRef? Photo { get; set; }

    /// <summary>
    /// Valores de atributos indexados por la clave de <see cref="Rules.StatDefinition"/>.
    /// Se guarda como diccionario para no atar el formato a una lista concreta de atributos.
    /// </summary>
    public Dictionary<string, int> Stats { get; set; } = [];

    public List<string> Skills { get; set; } = [];

    public List<Equipment> Gear { get; set; } = [];

    /// <summary>Balas disponibles. No se comparte al exportar la banda a un rival.</summary>
    public int Ammo { get; set; }

    public int? Cost { get; set; }

    public string? Notes { get; set; }

    public int GetStat(string key, int fallback = 0) => Stats.TryGetValue(key, out var value) ? value : fallback;
}

public sealed class Equipment
{
    public string Name { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
