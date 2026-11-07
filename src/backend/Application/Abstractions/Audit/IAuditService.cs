namespace MySciClub.Application.Abstractions.Audit;

public interface IAuditService
{
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
