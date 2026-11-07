namespace MySciClub.Application.Abstractions.Email;

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
