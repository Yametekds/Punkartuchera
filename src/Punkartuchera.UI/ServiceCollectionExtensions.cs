using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Punkartuchera.Core.Backup;
using Punkartuchera.Core.Maps;
using Punkartuchera.Core.Matches;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Services;
using Punkartuchera.Core.Sharing;
using Punkartuchera.Core.Storage;
using Punkartuchera.Core.Users;
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
        services.AddSingleton<GearLookup>();
        services.AddSingleton<RuleMentions>();
        services.AddSingleton<UnitCostCalculator>();
        services.AddSingleton<LoadoutAdvisor>();
        services.AddSingleton<UnitRules>();
        services.AddSingleton<CloseCombat>();
        services.AddSingleton<MemberWeapons>();
        services.AddSingleton<LoadoutModifiers>();
        services.AddSingleton(sp => new RosterBuilder(sp.GetRequiredService<Rulebook>(), sp.GetRequiredService<IRulesCatalog>()));
        services.AddScoped<FragmentScroller>();
        services.AddSingleton<Punkartuchera.Core.Matches.MatchAidCatalog>();
        services.AddSingleton<ITerrainCatalog, DefaultTerrainCatalog>();
        services.AddScoped<KeyValueDb>();
        services.AddScoped<IBandRepository, IndexedDbBandRepository>();
        services.AddScoped<IPhotoStore, IndexedDbPhotoStore>();
        services.AddScoped<ISavedMatchStore, IndexedDbSavedMatchStore>();
        services.AddScoped<IMatchRelay>(sp => RelayUrl(sp.GetService<IConfiguration>()) is { } url
            ? new SignalRMatchRelay(new HttpClient { BaseAddress = url, Timeout = TimeSpan.FromSeconds(20) })
            : new DisabledMatchRelay());
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<BandService>();
        services.AddScoped<BandExchangeService>();
        services.AddScoped<PhotoUrlProvider>();
        services.AddScoped<FileSharer>();
        services.AddScoped<PhotoCropper>();
        services.AddScoped<StampNotifier>();
        services.AddScoped<LocalUserProfileStore>();
        services.AddScoped<IUserProfileStore>(sp => sp.GetRequiredService<LocalUserProfileStore>());
        services.AddScoped<BackupService>();
        services.AddScoped<Punkartuchera.Core.Tournaments.ITournamentStore, IndexedDbTournamentStore>();
        services.AddScoped<Punkartuchera.Core.Tournaments.TournamentService>();
        return services;
    }

    /// <summary>
    /// Dirección del relé (<c>Relay:BaseUrl</c> en <c>appsettings.json</c>). Vacía = sin partidas en línea.
    /// Solo se acepta HTTPS, salvo en localhost para probar.
    /// </summary>
    internal static Uri? RelayUrl(IConfiguration? configuration)
    {
        var value = configuration?["Relay:BaseUrl"];
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var url))
        {
            return null;
        }

        return url.Scheme == Uri.UriSchemeHttps || url.IsLoopback ? url : null;
    }
}
