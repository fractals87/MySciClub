namespace MySciClub.Application.Abstractions.Email;

public sealed record EmailMessage(string Recipient, string Subject, string HtmlBody);
