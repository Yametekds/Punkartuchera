using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Punkartuchera.UI.Services;

/// <summary>
/// Lleva la vista al ancla (#id) de la URL actual. Las páginas de reglas lo llaman
/// tras pintarse, porque el contenido aparece después de la navegación.
/// </summary>
public sealed class FragmentScroller(IJSRuntime js, NavigationManager navigation) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;

    public async Task ScrollToCurrentAsync()
    {
        var hash = new Uri(navigation.Uri).Fragment.TrimStart('#');
        if (hash.Length == 0)
        {
            return;
        }

        _module ??= js.InvokeAsync<IJSObjectReference>("import", "./_content/Punkartuchera.UI/js/rules.js").AsTask();
        var module = await _module;
        await module.InvokeVoidAsync("scrollToFragment", hash);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await (await _module).DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
