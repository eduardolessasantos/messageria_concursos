using Concurso.Consumer.Repositories;
using Concurso.Messaging.Events;
using Concurso.Shared.Metrics;
using MassTransit;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Concurso.Consumer.Consumers;

public sealed class ConcursoPublicadoConsumer : IConsumer<ConcursoPublicadoEvent>
{
    private readonly IConcursoRepository _repository;
    private readonly ILogger<ConcursoPublicadoConsumer> _logger;
    private readonly IAppMetrics _metrics;

    public ConcursoPublicadoConsumer(IConcursoRepository repository, ILogger<ConcursoPublicadoConsumer> logger, IAppMetrics metrics)
    {
        _repository = repository;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task Consume(ConsumeContext<ConcursoPublicadoEvent> context)
    {
        var key = context.Message.DeduplicationKey;
        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = context.CorrelationId?.ToString() ?? Guid.NewGuid().ToString("N") }))
        {
            _logger.LogInformation("Consumo evento ConcursoPublicado | Key: {Key} | Fonte: {Fonte}", key, context.Message.Fonte);
            _metrics.IncrementConsumed();

            try
            {
                var exists = await _repository.ExistsAsync(key, context.CancellationToken);
                if (exists)
                {
                    _logger.LogInformation("Duplicidade detectada | Key: {Key}", key);
                    _metrics.IncrementIgnored();
                    return;
                }

                var entity = new Data.Entities.ConcursoEntity
                {
                    DeduplicationKey = key,
                    Titulo = context.Message.Titulo,
                    Orgao = context.Message.Orgao,
                    Cargo = context.Message.Cargo,
                    Salario = context.Message.Salario,
                    Link = context.Message.Link,
                    DataPublicacao = context.Message.DataPublicacao,
                    DataCaptura = context.Message.DataCaptura,
                    Fonte = context.Message.Fonte,
                    Descricao = context.Message.Descricao
                };

                await _repository.AddAsync(entity, context.CancellationToken);
                await _repository.SaveChangesAsync(context.CancellationToken);

                _logger.LogInformation("Persistência realizada | Key: {Key} | Id: {Id}", entity.DeduplicationKey, entity.Id);
                _metrics.IncrementPersisted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar evento | Key: {Key}", key);
                throw; // permitir retry/Dead-letter pelo MassTransit
            }
        }
    }
}