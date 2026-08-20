using DnsClient;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

public class DirectMxEmailSender : IEmailSender
{
    private readonly ILogger<DirectMxEmailSender> _logger;

    public DirectMxEmailSender(ILogger<DirectMxEmailSender> logger)
    {
        _logger = logger;
    }

    public async Task SendEmailAsync(MimeMessage message, string recipient, CancellationToken ct)
    {
        var domain = recipient.Split('@')[1];
        var lookup = new LookupClient();
        var result = await lookup.QueryAsync(domain, QueryType.MX, cancellationToken: ct);
        var mxRecord = result.Answers.MxRecords().OrderBy(r => r.Preference).FirstOrDefault();

        if (mxRecord == null)
        {
            _logger.LogError("[Direct-MX] Nenhum servidor MX encontrado para o domínio {Domain}", domain);
            return;
        }

        string mxHost = mxRecord.Exchange.Value.TrimEnd('.');
        _logger.LogInformation("[Direct-MX] Servidor MX {MxHost} resolvido para {Domain}", mxHost, domain);

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(mxHost, 25, SecureSocketOptions.None, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            _logger.LogInformation("[Direct-MX] Notificação entregue diretamente em {MxHost}", mxHost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Direct-MX] Falha ao entregar em {MxHost}. Verifique se a porta 25 não está bloqueada pelo seu ISP.", mxHost);
        }
    }
}
