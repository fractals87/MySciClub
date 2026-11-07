namespace MySciClub.Application.Abstractions.AuthenticationLog;

public sealed record AuthenticationLogEntry(
    long? AdministratorId,
    DateTimeOffset OccurredAtUtc,
    bool Succeeded,
    string? IpAddress);
