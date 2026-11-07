namespace MySciClub.Application.Abstractions.TechnicalLog;

public sealed record TechnicalLogEntry(
    DateTimeOffset OccurredAtUtc,
    string Level,
    string EventName,
    string Message,
    string? CorrelationId);
