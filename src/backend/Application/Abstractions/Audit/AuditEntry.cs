namespace MySciClub.Application.Abstractions.Audit;

public sealed record AuditEntry(
    long? AdministratorId,
    DateTimeOffset RecordedAtUtc,
    DateOnly? EffectiveDate,
    string Operation,
    string EntityType,
    string EntityId,
    string? PreviousValues,
    string? NewValues);
