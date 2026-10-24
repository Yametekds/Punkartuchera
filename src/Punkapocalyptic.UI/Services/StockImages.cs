namespace Punkapocalyptic.UI.Services;

/// <summary>Retratos genéricos incluidos en la app para miniaturas sin foto.</summary>
public static class StockImages
{
    public sealed record StockImage(string Key, string Label);

    public static IReadOnlyList<StockImage> All { get; } =
    [
        new("cresta", "Cresta"),
        new("mascara", "Máscara de gas"),
        new("calavera", "Calavera"),
        new("llave", "Llave inglesa"),
        new("mutante", "Mutante"),
        new("casco", "Casco"),
    ];

    public static string Url(string key) => $"_content/Punkapocalyptic.UI/img/stock/{key}.svg";
}
