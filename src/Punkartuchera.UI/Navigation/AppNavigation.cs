namespace Punkartuchera.UI.Navigation;

/// <summary>
/// Secciones de la barra de navegación. Para activar una sección nueva,
/// crea su página con la ruta indicada y marca <see cref="NavItem.Available"/> a true.
/// </summary>
public static class AppNavigation
{
    public sealed record NavItem(string Href, string Label, string Icon, bool Available, bool MatchAll = false);

    public static IReadOnlyList<NavItem> Items { get; } =
    [
        new("", "Bandas", Icons.Band, Available: true, MatchAll: true),
        new("reglas", "Reglas", Icons.Book, Available: true),
        new("mapa", "Mapa", Icons.Map, Available: false),
        new("partida", "Partida", Icons.Dice, Available: false),
    ];
}
