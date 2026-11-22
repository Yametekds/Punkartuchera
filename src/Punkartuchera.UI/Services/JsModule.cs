using Microsoft.JSInterop;

namespace Punkartuchera.UI.Services;

/// <summary>
/// Un módulo JS de <c>wwwroot/js</c> que se importa la primera vez que hace falta y se libera
/// con el servicio. Si el circuito ya se ha cerrado al liberar, no pasa nada.
/// </summary>
public abstract class JsModule(IJSRuntime js, string fileName) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;

    protected Task<IJSObjectReference> Module =>
        _module ??= js.InvokeAsync<IJSObjectReference>("import", $"./_content/Punkartuchera.UI/js/{fileName}").AsTask();

    /// <summary>Último aviso al módulo antes de liberarlo (por ejemplo, para soltar la cámara).</summary>
    protected virtual Task OnDisposingAsync(IJSObjectReference module) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_module is { IsCompletedSuccessfully: true })
        {
            try
            {
                await OnDisposingAsync(_module.Result);
                await _module.Result.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        GC.SuppressFinalize(this);
    }
}
