using MimeKit;

public interface IEmailSender
{
    Task SendEmailAsync(MimeMessage message, string recipient, CancellationToken ct);
}
