using Microsoft.Extensions.Logging;
using MySciClub.Application.Abstractions.TechnicalLog;

namespace MySciClub.Infrastructure.Files;

internal sealed class TechnicalLogService(ILogger<TechnicalLogService> logger) : ITechnicalLogService
{
    public Task RecordAsync(TechnicalLogEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogError("{EventName}: {Message}; CorrelationId={CorrelationId}", entry.EventName, entry.Message, entry.CorrelationId);
        return Task.CompletedTask;
    }
}
