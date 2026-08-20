using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

public class SmtpEmailSender : IEmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly AppConfig _config;

    public SmtpEmailSender(ILogger<SmtpEmailSender> logger, AppConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task SendEmailAsync(MimeMessage message, string recipient, CancellationToken ct)
    {
        using var client = new SmtpClient();
        await client.ConnectAsync(_config.SmtpHost, _config.SmtpPort, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(_config.SmtpUser, _config.SmtpPass, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("[SMTP] Notificação enviada para {Recipient}", recipient);
    }
}
