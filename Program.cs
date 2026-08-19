using System.Data;
using System.Net;
using System.Net.Mail;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<IpMonitorWorker>();
var host = builder.Build();
host.Run();

public class IpMonitorWorker : BackgroundService
{
    private readonly ILogger<IpMonitorWorker> _logger;
    private readonly string _dbPath = "/data/ip_history.db";
    private readonly string _connectionString;

    // Configurações via variáveis de ambiente
    private readonly string _smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.gmail.com";
    private readonly int _smtpPort = int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
    private readonly string _smtpUser = Environment.GetEnvironmentVariable("SMTP_USER") ?? "";
    private readonly string _smtpPass = Environment.GetEnvironmentVariable("SMTP_PASS") ?? "";
    private readonly string _emailFrom = Environment.GetEnvironmentVariable("EMAIL_FROM") ?? "";
    private readonly string[] _recipients = (Environment.GetEnvironmentVariable("EMAIL_TO") ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(
        int.Parse(Environment.GetEnvironmentVariable("CHECK_INTERVAL_MINUTES") ?? "10"));

    public IpMonitorWorker(ILogger<IpMonitorWorker> logger)
    {
        _logger = logger;
        _connectionString = $"Data Source={_dbPath}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IP Monitoring Service started.");
        await InitializeDatabaseAsync();

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                string currentIp = await FetchPublicIpAsync(httpClient, stoppingToken);

                if (!string.IsNullOrEmpty(currentIp))
                {
                    string? lastIp = await GetLastKnownIpAsync();

                    // Se não há histórico (primeiro boot absoluto) ou o IP mudou em relação ao banco
                    if (lastIp != currentIp)
                    {
                        _logger.LogInformation("IP Changed! Old: {OldIp} | New: {NewIp}", 
                            lastIp ?? "None (First execution)", currentIp);

                        // Registra o novo IP no histórico
                        await SaveIpLogAsync(currentIp);

                        // Envia o e-mail (agora dispara com segurança na inicialização se o IP mudou enquanto esteve off)
                        await SendEmailNotificationAsync(currentIp, lastIp, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during verification cicle.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task InitializeDatabaseAsync()
    {
        // Garante que o diretório do volume existe
        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS IpHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                IpAddress TEXT NOT NULL,
                DetectedAt DATETIME NOT NULL
            );";
        await command.ExecuteNonQueryAsync();
    }

    private async Task<string?> GetLastKnownIpAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT IpAddress FROM IpHistory ORDER BY Id DESC LIMIT 1;";

        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }

    private async Task SaveIpLogAsync(string ip)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO IpHistory (IpAddress, DetectedAt) VALUES ($ip, $date);";
        command.Parameters.AddWithValue("$ip", ip);
        command.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("o"));

        await command.ExecuteNonQueryAsync();
    }

    private async Task<string> FetchPublicIpAsync(HttpClient client, CancellationToken ct)
    {
        string[] providers = ["https://api.ipify.org", "https://icanhazip.com", "https://ipinfo.io/ip"];
        
        foreach (var url in providers)
        {
            try
            {
                var response = await client.GetStringAsync(url, ct);
                return response.Trim();
            }
            catch
            {
                continue;
            }
        }
        return string.Empty;
    }

    private async Task SendEmailNotificationAsync(string newIp, string? oldIp, CancellationToken ct)
    {
        if (_recipients.Length == 0 || string.IsNullOrEmpty(_smtpHost)) return;

        using var client = new SmtpClient(_smtpHost, _smtpPort)
        {
            Credentials = new NetworkCredential(_smtpUser, _smtpPass),
            EnableSsl = true
        };

        string previousIpText = string.IsNullOrEmpty(oldIp) ? "First execution of service" : oldIp;

        using var message = new MailMessage
        {
            From = new MailAddress(_emailFrom),
            Subject = $"[Alert] IP Changed: {newIp}",
            Body = $"Public IP address changed.\n\n" +
                   $"Old IP: {previousIpText}\n" +
                   $"New IP: {newIp}\n" +
                   $"Date/Time (UTC): {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}"
        };

        foreach (var recipient in _recipients)
        {
            message.To.Add(recipient);
        }

        await client.SendMailAsync(message, ct);
        _logger.LogInformation("Notification sent.");
    }
}
