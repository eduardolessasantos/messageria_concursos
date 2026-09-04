using Microsoft.Extensions.Configuration;
using Resend;
using Log = Serilog.Log;

namespace Concurso.Notification.Services;

public class ResendEmailSender : IEmailSender
{
    private readonly IResend _resend;
    private readonly IConfiguration _configuration;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public ResendEmailSender(IResend resend, IConfiguration configuration)
    {
        _resend = resend;
        _configuration = configuration;
        _fromEmail = configuration["Email:From"] ?? "onboarding@resend.dev";
        _fromName = configuration["Email:FromName"] ?? "Concursos TI - Alertas";
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string bodyHtml, string plainText = "")
    {
        var apiKey = _configuration["Resend:ApiKey"]
            ?? _configuration["Resend:ApiToken"]
            ?? _configuration["ResendApiKey"]
            ?? _configuration["RESEND_API_KEY"];

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("re_123") || apiKey.Contains("SUA_CHAVE"))
        {
            Log.Information("[ResendEmailSender] [SIMULAÇÃO / DEMO] Chave Resend não configurada. E-mail simulado com sucesso! Destinatário: {ToEmail} | Assunto: {Subject}", toEmail, subject);
            return;
        }

        Log.Information("[ResendEmailSender] Enviando e-mail para {ToEmail} via Resend API...", toEmail);

        var fromAddress = !string.IsNullOrWhiteSpace(_fromName)
            ? $"{_fromName} <{_fromEmail}>"
            : _fromEmail;

        var message = new EmailMessage
        {
            From = fromAddress,
            Subject = subject,
            HtmlBody = bodyHtml,
            TextBody = plainText
        };
        message.To.Add(toEmail);

        var response = await _resend.EmailSendAsync(message);

        if (!response.Success)
        {
            var erro = response.Exception?.Message ?? "Falha ao enviar e-mail via Resend.";
            Log.Error("[ResendEmailSender] Falha ao enviar via Resend: {Erro}", erro);
            throw new InvalidOperationException($"Falha no Resend: {erro}", response.Exception);
        }

        Log.Information("[ResendEmailSender] E-mail enviado com sucesso via Resend para {ToEmail} (EmailId: {EmailId})", toEmail, response.Content);
    }
}
