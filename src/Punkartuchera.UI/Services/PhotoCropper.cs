using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Punkartuchera.UI.Services;

/// <summary>
/// Encuadra fotos en cuadrado dentro del navegador y las devuelve como JPEG de tamaño fijo,
/// pese lo que pese la original.
/// </summary>
public sealed class PhotoCropper(IJSRuntime js) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;

    private Task<IJSObjectReference> Module =>
        _module ??= js.InvokeAsync<IJSObjectReference>("import", "./_content/Punkartuchera.UI/js/photoCrop.js").AsTask();

    /// <summary>Lee la foto elegida en un &lt;input type="file"&gt; y la deja lista para encuadrar.</summary>
    public async Task<PhotoLoadResult> LoadAsync(ElementReference input)
    {
        var module = await Module;
        return await module.InvokeAsync<PhotoLoadResult>("load", input);
    }

    /// <summary>Conecta el lienzo y el deslizador de zoom del editor a la foto cargada.</summary>
    public async Task AttachAsync(ElementReference canvas, ElementReference zoom)
    {
        var module = await Module;
        await module.InvokeVoidAsync("attach", canvas, zoom);
    }

    /// <summary>Devuelve el encuadre actual como JPEG.</summary>
    public async Task<byte[]> ExportJpegAsync()
    {
        var module = await Module;
        return await module.InvokeAsync<byte[]>("exportJpeg");
    }

    public async Task CancelAsync()
    {
        var module = await Module;
        await module.InvokeVoidAsync("cancel");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is { IsCompletedSuccessfully: true })
        {
            try
            {
                await _module.Result.InvokeVoidAsync("cancel");
                await _module.Result.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}

/// <summary>Resultado de leer una foto: Status es "ok", "empty" o "error".</summary>
public sealed record PhotoLoadResult(string Status, string? Type = null, string? Name = null, long Size = 0)
{
    public bool IsOk => Status == "ok";

    public bool IsEmpty => Status == "empty";

    public bool IsHeic =>
        Type is "image/heic" or "image/heif"
        || (Name?.EndsWith(".heic", StringComparison.OrdinalIgnoreCase) ?? false)
        || (Name?.EndsWith(".heif", StringComparison.OrdinalIgnoreCase) ?? false);
}
