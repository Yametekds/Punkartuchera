using Microsoft.JSInterop;

namespace Punkartuchera.UI.Services;

/// <summary>Envía archivos con el menú de compartir del sistema, o los descarga si no hay.</summary>
public sealed class FileSharer(IJSRuntime js) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;

    private Task<IJSObjectReference> Module =>
        _module ??= js.InvokeAsync<IJSObjectReference>("import", "./_content/Punkartuchera.UI/js/punkShare.js").AsTask();

    public async Task<ShareOutcome> ShareAsync(string fileName, byte[] content, string title, string text)
    {
        var module = await Module;
        var result = await module.InvokeAsync<string>("shareFile", fileName, content, title, text);
        return result switch
        {
            "shared" => ShareOutcome.Shared,
            "cancelled" => ShareOutcome.Cancelled,
            _ => ShareOutcome.Downloaded,
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is { IsCompletedSuccessfully: true })
        {
            try
            {
                await _module.Result.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}

public enum ShareOutcome
{
    Shared,
    Downloaded,
    Cancelled
}
