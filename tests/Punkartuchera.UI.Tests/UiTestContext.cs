using Microsoft.Extensions.DependencyInjection;
using Punkartuchera.Core.Matches;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Services;
using Punkartuchera.Core.Storage;
using Punkartuchera.Core.Tournaments;
using Punkartuchera.Core.Users;

namespace Punkartuchera.UI.Tests;

/// <summary>
/// Contexto con los servicios reales de la app y almacenamiento en memoria en lugar de IndexedDB.
/// El JS responde con valores por defecto: aquí se prueba el C# de las pantallas.
/// </summary>
public abstract class UiTestContext : BunitContext, IAsyncLifetime
{
    protected UiTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddPunkartucheraUi();
        Services.AddSingleton<IBandRepository, InMemoryBandRepository>();
        Services.AddSingleton<IPhotoStore, InMemoryPhotoStore>();
        Services.AddSingleton<ISavedMatchStore, InMemorySavedMatchStore>();
        Services.AddSingleton<ITournamentStore, InMemoryTournamentStore>();
        Services.AddSingleton<IUserProfileStore, InMemoryUserProfileStore>();
        Services.AddSingleton<IMatchRelay, DisabledMatchRelay>();
    }

    // Varios servicios de la app solo se liberan de forma asíncrona.
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();

    protected T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>Banda creada desde una lista preconfigurada del reglamento.</summary>
    protected async Task<Band> CreateBandAsync(string name, string listId)
    {
        var list = Get<Rulebook>().FindList(listId) ?? throw new InvalidOperationException($"No existe la lista {listId}.");
        var members = Get<RosterBuilder>().FromList(list);
        return await Get<BandService>().CreateAsync(name, list.FactionId, null, members);
    }
}
