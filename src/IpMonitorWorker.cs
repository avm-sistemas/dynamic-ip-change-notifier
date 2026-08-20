using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;

public class IpMonitorWorker : BackgroundService
{
    private readonly ILogger<IpMonitorWorker> _logger;
    private readonly IEmailSender _emailSender;
    private readonly IpRepository _repository;
    private readonly AppConfig _config;

    // Endpoints consultados em ordem; passa pro próximo se o atual falhar
    private static readonly string[] IpProviders =
    [
        "https://api.ipify.org",
        "https://icanhazip.com",
        "https://ipinfo.io/ip"
    ];

    public IpMonitorWorker(
        ILogger<IpMonitorWorker> logger,
        IEmailSender emailSender,
        IpRepository repository,
        AppConfig config)
    {
        _logger = logger;
        _emailSender = emailSender;
        _repository = repository;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IP Monitor iniciado. Intervalo: {Interval} min.", _config.CheckIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckIpAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado no ciclo de checagem.");
            }

            await Task.Delay(TimeSpan.FromMinutes(_config.CheckIntervalMinutes), stoppingToken);
        }
    }

    private async Task CheckIpAsync(CancellationToken ct)
    {
        var currentIp = await FetchPublicIpAsync(ct);
        if (currentIp is null)
        {
            _logger.LogWarning("Não foi possível obter o IP público. Todos os providers falharam.");
            return;
        }

        var previousIp = _repository.GetLastIp();

        if (currentIp == previousIp)
        {
            _logger.LogInformation("IP inalterado: {Ip}", currentIp);
            return;
        }

        _logger.LogInformation("IP alterado: {Old} → {New}", previousIp ?? "primeira execução", currentIp);
        _repository.SaveIp(currentIp);
        await DispatchNotificationsAsync(currentIp, previousIp, ct);
    }

    private async Task<string?> FetchPublicIpAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        foreach (var url in IpProviders)
        {
            try
            {
                var ip = (await http.GetStringAsync(url, ct)).Trim();
                _logger.LogDebug("IP obtido via {Url}: {Ip}", url, ip);
                return ip;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Url} falhou. Tentando próximo.", url);
            }
        }

        return null;
    }

    private async Task DispatchNotificationsAsync(string newIp, string? oldIp, CancellationToken ct)
    {
        string previousIpText = string.IsNullOrEmpty(oldIp) ? "Primeira execução" : oldIp;

        foreach (var recipient in _config.EmailTo)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("IP Monitor", _config.EmailFrom));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = $"[Alerta] IP público alterado: {newIp}";
            message.Body = new TextPart("plain")
            {
                Text = $"O endereço IP público foi alterado.\n\n" +
                       $"IP anterior : {previousIpText}\n" +
                       $"Novo IP     : {newIp}\n" +
                       $"Data/Hora (UTC): {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}"
            };

            await _emailSender.SendEmailAsync(message, recipient, ct);
        }
    }
}
