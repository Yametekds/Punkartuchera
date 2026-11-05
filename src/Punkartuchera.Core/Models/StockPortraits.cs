namespace Punkartuchera.Core.Models;

/// <summary>
/// Retratos genéricos incluidos en la app para miniaturas sin foto.
/// La lista vive en Core para poder comprobar las claves que llegan de fuera (bandas rivales).
/// </summary>
public static class StockPortraits
{
    public sealed record StockPortrait(string Key, string Label);

    public static IReadOnlyList<StockPortrait> All { get; } =
    [
        new("cresta", "Cresta"),
        new("mascara", "Máscara de gas"),
        new("calavera", "Calavera"),
        new("llave", "Llave inglesa"),
        new("mutante", "Mutante"),
        new("casco", "Casco"),
    ];

    public static bool Exists(string? key) => All.Any(p => p.Key == key);
}
