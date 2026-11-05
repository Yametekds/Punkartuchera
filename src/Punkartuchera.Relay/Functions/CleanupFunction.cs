using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Punkartuchera.Relay.Matches;

namespace Punkartuchera.Relay.Functions;

/// <summary>Cada hora borra las partidas caducadas para que la tabla no crezca.</summary>
public sealed class CleanupFunction(MatchRelay relay, ILogger<CleanupFunction> logger)
{
    [Function("LimpiarPartidas")]
    public async Task Run([TimerTrigger("0 7 * * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var removed = await relay.CleanupAsync(cancellationToken);
        if (removed > 0)
        {
            logger.LogInformation("Borradas {Count} partidas caducadas", removed);
        }
    }
}
