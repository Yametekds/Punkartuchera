using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Punkartuchera.UI.Services;

/// <summary>
/// Lleva la vista al ancla (#id) de la URL actual. Las páginas de reglas lo llaman
/// tras pintarse, porque el contenido aparece después de la navegación.
/// </summary>
public sealed class FragmentScroller(IJSRuntime js, NavigationManager navigation) : JsModule(js, "rules.js")
{
    public async Task ScrollToCurrentAsync()
    {
        var hash = new Uri(navigation.Uri).Fragment.TrimStart('#');
        if (hash.Length == 0)
        {
            return;
        }

        var module = await Module;
        await module.InvokeVoidAsync("scrollToFragment", hash);
    }
}
