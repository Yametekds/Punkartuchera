using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Punkartuchera.Relay;
using Punkartuchera.Relay.Matches;
using Punkartuchera.Relay.Realtime;
using Punkartuchera.Relay.Security;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

builder.Services.Configure<RelayOptions>(builder.Configuration.GetSection(RelayOptions.Section));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IMatchStore>(sp => new TableMatchStore(sp.GetRequiredService<IOptions<RelayOptions>>().Value));
builder.Services.AddSingleton<IRealtimeChannel, SignalRChannel>();
builder.Services.AddSingleton<MatchRelay>();
builder.Services.AddSingleton<RequestThrottle>();

builder.Build().Run();
