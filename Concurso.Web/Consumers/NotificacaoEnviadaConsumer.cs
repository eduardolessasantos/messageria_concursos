using Concurso.Messaging.Events;
using Concurso.Web.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Concurso.Web.Consumers;

public class NotificacaoEnviadaConsumer :
    IConsumer<NotificacaoEnviadaEvent>,
    IConsumer<ConcursoPublicadoEvent>
{
    private readonly NotificacaoStateService _state;
    private readonly ILogger<NotificacaoEnviadaConsumer> _logger;

    public NotificacaoEnviadaConsumer(NotificacaoStateService state, ILogger<NotificacaoEnviadaConsumer> logger)
    {
        _state = state;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<NotificacaoEnviadaEvent> context)
    {
        try
        {
            var msg = context.Message;
            _logger.LogInformation("[Web] Recebido NotificacaoEnviadaEvent: {Tipo} - {Status} para Key {Key}",
                msg.Tipo, msg.Status, msg.DeduplicationKey);

            _state.AdicionarLog(msg);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Web] Falha ao adicionar log de notificação no StateService");
        }

        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<ConcursoPublicadoEvent> context)
    {
        try
        {
            var msg = context.Message;
            _logger.LogInformation("[Web] Recebido ConcursoPublicadoEvent: {Cargo} ({Orgao}) - Key {Key}",
                msg.Cargo, msg.Orgao, msg.DeduplicationKey);

            _state.AdicionarLogConcurso(msg);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Web] Falha ao adicionar log de concurso no StateService");
        }

        return Task.CompletedTask;
    }
}
