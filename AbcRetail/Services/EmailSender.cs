using System.Net;
using System.Net.Mail;
using AbcRetail.Options;
using Microsoft.Extensions.Options;

namespace AbcRetail.Services;

public interface IEmailSender
{
    bool SmtpConfigured { get; }
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool SmtpConfigured =>
        !string.IsNullOrWhiteSpace(_options.Smtp.Host)
        && !_options.Smtp.Host.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!SmtpConfigured)
        {
            _logger.LogInformation("SMTP not configured; verification message for {Email} is shown in-app.", to);
            return;
        }

        try
        {
            using var client = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
            {
                EnableSsl = _options.Smtp.UseStartTls,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            if (!string.IsNullOrWhiteSpace(_options.Smtp.User))
            {
                client.Credentials = new NetworkCredential(_options.Smtp.User, _options.Smtp.Password);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(ParseFrom(_options.From).Address, ParseFrom(_options.From).Name),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP send failed for {Email}; in-app preview still works.", to);
        }
    }

    private static (string Address, string Name) ParseFrom(string from)
    {
        try
        {
            var parsed = new MailAddress(from);
            return (parsed.Address, string.IsNullOrWhiteSpace(parsed.DisplayName) ? "ABC Retail" : parsed.DisplayName);
        }
        catch
        {
            return ("noreply@abcretail.local", "ABC Retail");
        }
    }
}
