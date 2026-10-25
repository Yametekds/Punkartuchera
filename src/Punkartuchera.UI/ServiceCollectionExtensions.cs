using Microsoft.Extensions.DependencyInjection;
using Punkartuchera.Core.Matches;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Services;
using Punkartuchera.Core.Sharing;
using Punkartuchera.Core.Storage;
using Punkartuchera.UI.Services;
using Punkartuchera.UI.Storage;

namespace Punkartuchera.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra los servicios de la interfaz con almacenamiento local en IndexedDB.
    /// Compartido por la PWA y la futura app MAUI.
    /// </summary>
    public static IServiceCollection AddPunkartucheraUi(this IServiceCollection services)
    {
        services.AddSingleton(_ => RulebookLoader.LoadEmbedded());
        services.AddSingleton<IRulesCatalog, RulebookCatalog>();
        services.AddSingleton<RulesSearch>();
        services.AddScoped<FragmentScroller>();
        services.AddScoped<KeyValueDb>();
        services.AddScoped<IBandRepository, IndexedDbBandRepository>();
        services.AddScoped<IPhotoStore, IndexedDbPhotoStore>();
        services.AddScoped<IMatchService, LocalMatchService>();
        services.AddScoped<BandService>();
        services.AddScoped<BandExchangeService>();
        services.AddScoped<PhotoUrlProvider>();
        services.AddScoped<FileSharer>();
        return services;
    }
}
