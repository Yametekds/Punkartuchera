using Punkartuchera.Relay.Contracts;

namespace Punkartuchera.Relay.Realtime;

/// <summary>Canal en tiempo real hacia los móviles. En Azure es SignalR Service en modo sin servidor.</summary>
public interface IRealtimeChannel
{
    /// <summary>URL y token firmado para que el jugador abra su conexión como <paramref name="playerId"/>.</summary>
    Task<RelayConnectionInfo> NegotiateAsync(string playerId, TimeSpan lifetime, CancellationToken cancellationToken = default);

    Task SendAsync(string playerId, string message, CancellationToken cancellationToken = default);
}
