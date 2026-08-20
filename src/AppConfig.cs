
public enum EmailDeliveryMode
{
    Smtp,
    DirectMx
}

public class AppConfig
{
    public int CheckIntervalMinutes { get; set; } = 10;
    public EmailDeliveryMode DeliveryMode { get; set; } = EmailDeliveryMode.Smtp;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPass { get; set; } = string.Empty;
    public string EmailFrom { get; set; } = string.Empty;
    public string[] EmailTo { get; set; } = Array.Empty<string>();

    public static AppConfig LoadFromEnvironment()
    {
        var rawDeliveryMode = Environment.GetEnvironmentVariable("EMAIL_DELIVERY_MODE") ?? "Smtp";
        Enum.TryParse<EmailDeliveryMode>(rawDeliveryMode, ignoreCase: true, out var mode);

        var rawTo = Environment.GetEnvironmentVariable("EMAIL_TO") ?? string.Empty;
        var recipients = rawTo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new AppConfig
        {
            CheckIntervalMinutes = int.TryParse(Environment.GetEnvironmentVariable("CHECK_INTERVAL_MINUTES"), out var interval) ? interval : 10,
            DeliveryMode = mode,
            SmtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.gmail.com",
            SmtpPort = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var port) ? port : 587,
            SmtpUser = Environment.GetEnvironmentVariable("SMTP_USER") ?? string.Empty,
            SmtpPass = Environment.GetEnvironmentVariable("SMTP_PASS") ?? string.Empty,
            EmailFrom = Environment.GetEnvironmentVariable("EMAIL_FROM") ?? string.Empty,
            EmailTo = recipients
        };
    }
}

public static class AppConfigValidator
{
    public static (bool IsValid, List<string> Errors) Validate(AppConfig config)
    {
        var errors = new List<string>();

        if (config.CheckIntervalMinutes <= 0)
        {
            errors.Add("CHECK_INTERVAL_MINUTES deve ser um número inteiro maior que 0.");
        }

        if (string.IsNullOrWhiteSpace(config.EmailFrom) || !IsValidEmail(config.EmailFrom))
        {
            errors.Add("EMAIL_FROM é obrigatório e deve ser um endereço de e-mail válido.");
        }

        if (config.EmailTo.Length == 0)
        {
            errors.Add("EMAIL_TO é obrigatório e deve conter ao menos um e-mail de destino.");
        }
        else
        {
            foreach (var email in config.EmailTo)
            {
                if (!IsValidEmail(email))
                {
                    errors.Add($"EMAIL_TO contém um e-mail inválido: '{email}'.");
                }
            }
        }

        // Validações específicas caso o modo escolhido seja SMTP
        if (config.DeliveryMode == EmailDeliveryMode.Smtp)
        {
            if (string.IsNullOrWhiteSpace(config.SmtpHost))
            {
                errors.Add("SMTP_HOST é obrigatório quando EMAIL_DELIVERY_MODE é 'Smtp'.");
            }

            if (config.SmtpPort <= 0 || config.SmtpPort > 65535)
            {
                errors.Add("SMTP_PORT deve ser uma porta de rede válida (1-65535).");
            }

            if (string.IsNullOrWhiteSpace(config.SmtpUser))
            {
                errors.Add("SMTP_USER é obrigatório quando EMAIL_DELIVERY_MODE é 'Smtp'.");
            }

            if (string.IsNullOrWhiteSpace(config.SmtpPass))
            {
                errors.Add("SMTP_PASS é obrigatório quando EMAIL_DELIVERY_MODE é 'Smtp'.");
            }
        }

        return (errors.Count == 0, errors);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
