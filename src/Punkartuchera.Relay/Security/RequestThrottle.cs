using System.Threading.RateLimiting;

namespace Punkartuchera.Relay.Security;

/// <summary>
/// Límite de peticiones por clave (IP o jugador) con ventanas fijas de un minuto.
/// Vive en la memoria de cada instancia de la Function App: si Azure arranca varias,
/// cada una cuenta por su lado. Es una protección de mejor esfuerzo contra quien pruebe
/// códigos a lo bruto o inunde a un rival; el tope duro lo pone el plan Free de SignalR.
/// </summary>
public sealed class RequestThrottle : IDisposable
{
    public enum Bucket
    {
        Create,
        Join,
        Message,
    }

    private static readonly Dictionary<Bucket, int> PerMinute = new()
    {
        [Bucket.Create] = 6,
        [Bucket.Join] = 12,
        [Bucket.Message] = 90,
    };

    private readonly PartitionedRateLimiter<(Bucket, string)> _limiter;

    public RequestThrottle()
    {
        _limiter = PartitionedRateLimiter.Create<(Bucket Bucket, string Key), (Bucket, string)>(
            request => RateLimitPartition.GetFixedWindowLimiter(request, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PerMinute[request.Bucket],
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    }

    public bool TryAcquire(Bucket bucket, string key)
    {
        using var lease = _limiter.AttemptAcquire((bucket, key));
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
