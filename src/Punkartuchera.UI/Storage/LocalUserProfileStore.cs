using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Punkartuchera.UI.Services;
using Punkartuchera.Core.Users;

namespace Punkartuchera.UI.Storage;

/// <summary>
/// Guarda el perfil en localStorage (lo lee también el index.html al arrancar) y aplica
/// el tamaño de letra y la tipografía a la página cada vez que cambian.
/// </summary>
public sealed class LocalUserProfileStore(IJSRuntime js, ILogger<LocalUserProfileStore> logger) : JsModule(js, "punkPrefs.js"), IUserProfileStore
{
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
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Perfil guardado que no se puede leer: se usan los valores de serie.");
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
}
