using Microsoft.Extensions.DependencyInjection;
using Punkapocalyptic.Core.Matches;
using Punkapocalyptic.Core.Rules;
using Punkapocalyptic.Core.Services;
using Punkapocalyptic.Core.Storage;
using Punkapocalyptic.UI.Services;
using Punkapocalyptic.UI.Storage;

namespace Punkapocalyptic.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra los servicios de la interfaz con almacenamiento local en IndexedDB.
    /// Compartido por la PWA y la futura app MAUI.
    /// </summary>
    public static IServiceCollection AddPunkapocalypticUi(this IServiceCollection services)
    {
        services.AddSingleton<IRulesCatalog, PlaceholderRulesCatalog>();
        services.AddScoped<KeyValueDb>();
        services.AddScoped<IBandRepository, IndexedDbBandRepository>();
        services.AddScoped<IPhotoStore, IndexedDbPhotoStore>();
        services.AddScoped<IMatchService, LocalMatchService>();
        services.AddScoped<BandService>();
        services.AddScoped<PhotoUrlProvider>();
        return services;
    }
}
