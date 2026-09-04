using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Concurso.Notification.Services;

public class MailpitEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public MailpitEmailSender(IConfiguration configuration)
    {
        _host = configuration["Email:Mailpit:Host"] ?? configuration["Smtp:Host"] ?? "localhost";
        _port = configuration.GetValue<int>("Email:Mailpit:Port", configuration.GetValue<int>("Smtp:Port", 1025));
        _fromEmail = configuration["Email:From"] ?? "alertas@concursos-ti.com";
        _fromName = configuration["Email:FromName"] ?? "Concursos TI - Alertas (Dev)";
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string bodyHtml, string plainText = "")
    {
        Log.Information("[MailpitEmailSender] Enviando e-mail para {ToEmail} via Mailpit ({Host}:{Port})...", toEmail, _host, _port);

        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_fromEmail, _fromName),
            Subject = subject,
            Body = string.IsNullOrWhiteSpace(bodyHtml) ? plainText : bodyHtml,
            IsBodyHtml = !string.IsNullOrWhiteSpace(bodyHtml)
        };

        mailMessage.To.Add(new MailAddress(toEmail, toName));

        if (!string.IsNullOrWhiteSpace(bodyHtml) && !string.IsNullOrWhiteSpace(plainText))
        {
            var plainView = AlternateView.CreateAlternateViewFromString(plainText, null, "text/plain");
            var htmlView = AlternateView.CreateAlternateViewFromString(bodyHtml, null, "text/html");
            mailMessage.AlternateViews.Add(plainView);
            mailMessage.AlternateViews.Add(htmlView);
        }

        await client.SendMailAsync(mailMessage);

        Log.Information("[MailpitEmailSender] E-mail entregue ao Mailpit com sucesso para {ToEmail}", toEmail);
    }
}
