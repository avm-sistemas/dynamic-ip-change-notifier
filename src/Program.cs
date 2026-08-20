using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// 1. Carrega e valida as configurações de ambiente (Fail Fast)
var config = AppConfig.LoadFromEnvironment();
var (isValid, errors) = AppConfigValidator.Validate(config);

if (!isValid)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("[ERRO CRÍTICO] Falha na validação das variáveis de ambiente:");
    foreach (var error in errors)
        Console.WriteLine($" - {error}");
    Console.ResetColor();
    Environment.Exit(1);
}

// 2. Inicialização do Host
var builder = Host.CreateApplicationBuilder(args);

// Injeta a instância validada como Singleton
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<IpRepository>();

// Registra a estratégia de envio condicional
if (config.DeliveryMode == EmailDeliveryMode.DirectMx)
    builder.Services.AddTransient<IEmailSender, DirectMxEmailSender>();
else
    builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

builder.Services.AddHostedService<IpMonitorWorker>();

var host = builder.Build();
host.Run();
