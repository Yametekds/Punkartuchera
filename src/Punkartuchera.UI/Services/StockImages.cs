using Punkartuchera.Core.Models;

namespace Punkartuchera.UI.Services;

/// <summary>Direcciones de los retratos genéricos de <see cref="StockPortraits"/>.</summary>
public static class StockImages
{
    public static IReadOnlyList<StockPortraits.StockPortrait> All => StockPortraits.All;

    /// <summary>Solo da dirección a las claves conocidas: una clave rara no puede apuntar a otro archivo.</summary>
    public static string? Url(string key) =>
        StockPortraits.Exists(key) ? $"_content/Punkartuchera.UI/img/stock/{key}.svg" : null;
}
