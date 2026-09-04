using Concurso.Messaging.Events;

namespace Concurso.Web.Models;

public class NotificacaoLog
{
    public Guid EventId { get; set; }
    public string DeduplicationKey { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Email";
    public string Status { get; set; } = string.Empty; // "Processando", "Enviado", "Falha", "Tentando"
    public string Detalhe { get; set; } = string.Empty;
    public DateTime Data { get; set; } = DateTime.UtcNow;

    public NotificacaoLog() { }

    public NotificacaoLog(NotificacaoEnviadaEvent evt)
    {
        EventId = evt.EventId;
        DeduplicationKey = evt.DeduplicationKey;
        Tipo = evt.Tipo;
        Status = evt.Status;
        Detalhe = evt.Detalhe;
        Data = evt.EnviadoEm;
    }
}
