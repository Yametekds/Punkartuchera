using Microsoft.JSInterop;
using Punkartuchera.UI.Services;

namespace Punkartuchera.UI.Storage;

/// <summary>
/// Acceso a IndexedDB a través de <c>punkDb.js</c>. Solo intercambia strings
/// para no depender de la serialización por reflexión del interop.
/// </summary>
public sealed class KeyValueDb(IJSRuntime js) : JsModule(js, "punkDb.js")
{
    public const string BandsStore = "bands";
    public const string PhotosStore = "photos";
    public const string MatchesStore = "matches";
    public const string TournamentsStore = "tournaments";



    public async Task<string[]> GetAllAsync(string store, CancellationToken cancellationToken = default)
    {
        var module = await Module;
        return await module.InvokeAsync<string[]>("getAll", cancellationToken, store);
    }

    public async Task<string?> GetAsync(string store, string key, CancellationToken cancellationToken = default)
    {
        var module = await Module;
        return await module.InvokeAsync<string?>("get", cancellationToken, store, key);
    }

    public async Task PutAsync(string store, string key, string value, CancellationToken cancellationToken = default)
    {
        var module = await Module;
        await module.InvokeVoidAsync("put", cancellationToken, store, key, value);
    }

    public async Task RemoveAsync(string store, string key, CancellationToken cancellationToken = default)
    {
        var module = await Module;
        await module.InvokeVoidAsync("remove", cancellationToken, store, key);
    }

    public async Task<bool> RequestPersistenceAsync()
    {
        var module = await Module;
        return await module.InvokeAsync<bool>("requestPersistence");
    }
}
