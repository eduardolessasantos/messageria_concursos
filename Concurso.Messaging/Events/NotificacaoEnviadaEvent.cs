namespace Concurso.Messaging.Events;

public record NotificacaoEnviadaEvent(
    Guid EventId,
    string DeduplicationKey,
    string Tipo, // "Email"
    string Status, // "Processando", "Enviado", "Falha", "Tentando"
    string Detalhe,
    DateTime EnviadoEm
) : IEvent
{
    public DateTime OcorridoEm => EnviadoEm;
}
