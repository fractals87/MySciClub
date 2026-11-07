namespace MySciClub.Infrastructure.Persistence.Entities;

internal sealed class AuditRecord
{
    public long Id { get; set; }
    public long? AdministratorId { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? PreviousValues { get; set; }
    public string? NewValues { get; set; }
}
