namespace MySciClub.Application.Abstractions.AuthenticationLog;

public interface IAuthenticationLogService
{
    Task RecordAsync(AuthenticationLogEntry entry, CancellationToken cancellationToken = default);
}
