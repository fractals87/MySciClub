namespace MySciClub.Application.Abstractions.TechnicalLog;

public interface ITechnicalLogService
{
    Task RecordAsync(TechnicalLogEntry entry, CancellationToken cancellationToken = default);
}
