using System.Collections.Concurrent;
using Concurso.Messaging.Events;
using Concurso.Notification.Services;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Concurso.Notification.Consumers;

public class ConcursoPublicadoConsumer : IConsumer<ConcursoPublicadoEvent>
{
    private static readonly ConcurrentDictionary<string, bool> ProcessedKeys = new();
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public ConcursoPublicadoConsumer(IEmailSender emailSender, IConfiguration configuration)
    {
        _emailSender = emailSender;
        _configuration = configuration;
    }

    public async Task Consume(ConsumeContext<ConcursoPublicadoEvent> context)
    {
        var concurso = context.Message;
        var key = concurso.DeduplicationKey;

        // 1. Verificação de idempotência por DeduplicationKey
        if (ProcessedKeys.ContainsKey(key))
        {
            Log.Warning("[Email] Concurso duplicado já processado anteriormente. Key: {Key} | Cargo: {Cargo}. Ignorando.",
                key, concurso.Cargo);
            return;
        }

        var toEmail = _configuration["Email:To"]
            ?? _configuration["Email:Destinatario"]
            ?? "seu-email@dominio.com";

        var toName = _configuration["Email:ToName"] ?? "Candidato TI";

        // PASSO 1/3: Recebimento e início
        Log.Information("[Email] [Passo 1/3] Recebendo evento de concurso. Key: {Key} | Cargo: {Cargo} | Órgão: {Orgao} | Destinatário: {ToEmail}",
            key, concurso.Cargo, concurso.Orgao, toEmail);

        await context.Publish(new NotificacaoEnviadaEvent(
            concurso.EventId,
            key,
            "Email",
            "Processando",
            $"[Passo 1/3] Processando notificação para o concurso '{concurso.Cargo} - {concurso.Orgao}'. Preparando e-mail para {toEmail}...",
            DateTime.UtcNow
        ));

        // PASSO 2/3: Disparo via IEmailSender (Resend / Mailpit)
        Log.Information("[Email] [Passo 2/3] Enviando e-mail com template rico para {ToEmail}...", toEmail);

        var assunto = $"🚨 Concurso TI: {concurso.Cargo} - {concurso.Orgao} ({concurso.Salario})";
        var htmlBody = EmailTemplateBuilder.BuildHtml(concurso);
        var plainText = EmailTemplateBuilder.BuildPlainText(concurso);

        try
        {
            await _emailSender.SendAsync(toEmail, toName, assunto, htmlBody, plainText);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Email] [Passo 2/3] Falha no envio de e-mail para {ToEmail}. O MassTransit acionará política de retry...", toEmail);

            await context.Publish(new NotificacaoEnviadaEvent(
                concurso.EventId,
                key,
                "Email",
                "Tentando",
                $"[Passo 2/3] Falha temporária no envio para {toEmail}: {ex.Message}. Nova tentativa agendada.",
                DateTime.UtcNow
            ));

            throw; // Propaga para o MassTransit executar retry exponencial
        }

        // PASSO 3/3: Conclusão com sucesso e marcação de idempotência
        ProcessedKeys.TryAdd(key, true);

        Log.Information("[Email] [Passo 3/3] E-mail entregue com sucesso para {ToEmail} (Key: {Key}, EventId: {EventId})",
            toEmail, key, concurso.EventId);

        await context.Publish(new NotificacaoEnviadaEvent(
            concurso.EventId,
            key,
            "Email",
            "Enviado",
            $"[Passo 3/3] Notificação do concurso '{concurso.Cargo} ({concurso.Orgao})' enviada com sucesso para {toEmail}.",
            DateTime.UtcNow
        ));
    }
}
