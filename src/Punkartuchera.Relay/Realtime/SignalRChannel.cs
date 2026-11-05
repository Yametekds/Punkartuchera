using Azure.Identity;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Punkartuchera.Relay.Contracts;

namespace Punkartuchera.Relay.Realtime;

/// <summary>
/// Azure SignalR Service en modo sin servidor (Serverless). Los móviles se conectan directamente al servicio
/// con un token que firma esta Function App; para hablarles se usa la API REST del servicio (transporte Transient),
/// así no hay ninguna conexión abierta que mantener entre ejecuciones.
/// </summary>
public sealed class SignalRChannel(IOptions<RelayOptions> options, ILoggerFactory loggerFactory) : IRealtimeChannel, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ServiceManager? _manager;
    private ServiceHubContext? _hub;

    public async Task<RelayConnectionInfo> NegotiateAsync(string playerId, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        var hub = await HubAsync(cancellationToken);
        var response = await hub.NegotiateAsync(new NegotiationOptions { UserId = playerId, TokenLifetime = lifetime }, cancellationToken);
        return new RelayConnectionInfo(response.Url!, response.AccessToken!);
    }

    public async Task SendAsync(string playerId, string message, CancellationToken cancellationToken = default)
    {
        var hub = await HubAsync(cancellationToken);
        await hub.Clients.User(playerId).SendCoreAsync(RelayProtocol.ClientMethod, [message], cancellationToken);
    }

    private async Task<ServiceHubContext> HubAsync(CancellationToken cancellationToken)
    {
        if (_hub is not null)
        {
            return _hub;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_hub is null)
            {
                var settings = options.Value;
                _manager = new ServiceManagerBuilder()
                    .WithOptions(o =>
                    {
                        o.ServiceTransportType = ServiceTransportType.Transient;
                        if (!string.IsNullOrWhiteSpace(settings.SignalRConnectionString))
                        {
                            o.ConnectionString = settings.SignalRConnectionString;
                        }
                        else
                        {
                            var endpoint = settings.SignalREndpoint
                                ?? throw new InvalidOperationException("Falta Relay:SignalREndpoint o Relay:SignalRConnectionString.");
                            o.ServiceEndpoints = [new ServiceEndpoint(new Uri(endpoint), new DefaultAzureCredential())];
                        }
                    })
                    .WithLoggerFactory(loggerFactory)
                    .BuildServiceManager();
                _hub = await _manager.CreateHubContextAsync(RelayProtocol.Hub, cancellationToken);
            }

            return _hub;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
        }

        _manager?.Dispose();
        _gate.Dispose();
    }
}
