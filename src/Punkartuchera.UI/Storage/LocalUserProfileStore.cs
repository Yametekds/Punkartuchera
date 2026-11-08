using System.Text.Json;
using Microsoft.JSInterop;
using Punkartuchera.Core.Users;

namespace Punkartuchera.UI.Storage;

/// <summary>
/// Guarda el perfil en localStorage (lo lee también el index.html al arrancar) y aplica
/// el tamaño de letra y la tipografía a la página cada vez que cambian.
/// </summary>
public sealed class LocalUserProfileStore(IJSRuntime js) : IUserProfileStore, IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;

    private Task<IJSObjectReference> Module =>
        _module ??= js.InvokeAsync<IJSObjectReference>("import", "./_content/Punkartuchera.UI/js/punkPrefs.js").AsTask();

    public async Task<UserProfile> GetAsync(CancellationToken cancellationToken = default)
    {
        var module = await Module;
        var json = await module.InvokeAsync<string?>("load", cancellationToken);
        if (string.IsNullOrEmpty(json))
        {
            return new UserProfile();
        }

        try
        {
            return (JsonSerializer.Deserialize(json, UserJsonContext.Default.UserProfile) ?? new UserProfile()).Normalize();
        }
        catch (JsonException)
        {
            return new UserProfile();
        }
    }

    public async Task SaveAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        profile.Normalize();
        var module = await Module;
        await module.InvokeAsync<bool>("save", cancellationToken, JsonSerializer.Serialize(profile, UserJsonContext.Default.UserProfile));
        await ApplyAsync(profile, cancellationToken);
    }

    /// <summary>Pone en la página el tamaño y la tipografía, sin guardarlos (para la vista previa).</summary>
    public async Task ApplyAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        var module = await Module;
        await module.InvokeVoidAsync("apply", cancellationToken, profile.FontScale, FontAttribute(profile.Font));
    }

    /// <summary>Valor de <c>data-font</c> en la etiqueta html; el CSS cambia las letras según él.</summary>
    public static string FontAttribute(ReadingFont font) => font switch
    {
        ReadingFont.OpenDyslexic => "opendyslexic",
        ReadingFont.Hyperlegible => "hyperlegible",
        _ => "punk",
    };

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
