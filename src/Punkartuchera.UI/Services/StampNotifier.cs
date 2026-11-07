namespace Punkartuchera.UI.Services;

/// <summary>
/// Avisa de que algo se ha guardado con un sello que se estampa en pantalla
/// (lo pinta <c>StampToast</c> en el layout, así sobrevive a la navegación).
/// </summary>
public sealed class StampNotifier
{
    public event Action<string>? Stamped;

    public void Stamp(string text) => Stamped?.Invoke(text);
}
