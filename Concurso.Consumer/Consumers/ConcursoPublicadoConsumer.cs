using Concurso.Consumer.Repositories;
using Concurso.Messaging.Events;
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

    public ConcursoPublicadoConsumer(IConcursoRepository repository, ILogger<ConcursoPublicadoConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ConcursoPublicadoEvent> context)
    {
        // CorrelationId: prefira context.CorrelationId, fallback para header
        var correlationId = context.CorrelationId?.ToString()
                            ?? context.Headers.Get<string>("CorrelationId")
                            ?? Guid.NewGuid().ToString("N");

        using (_logger.BeginScope(new System.Collections.Generic.Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            _logger.LogInformation("Recebido evento ConcursoPublicado | DedupKey: {Key} | Fonte: {Fonte}", context.Message.DeduplicationKey, context.Message.Fonte);

            try
            {
                var exists = await _repository.ExistsAsync(context.Message.DeduplicationKey, context.CancellationToken);
                if (exists)
                {
                    _logger.LogInformation("Concurso já persistido — ignorando. Key: {Key}", context.Message.DeduplicationKey);
                    return;
                }

                var entity = new Data.Entities.ConcursoEntity
                {
                    DeduplicationKey = context.Message.DeduplicationKey,
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

                _logger.LogInformation("Concurso persistido com sucesso. Key: {Key} | Id: {Id}", entity.DeduplicationKey, entity.Id);
            }
            catch (Exception ex)
            {
                // Log detalhado e rethrow para MassTransit aplicar retry/dead-letter policy
                _logger.LogError(ex, "Erro ao processar ConcursoPublicadoEvent | Key: {Key}", context.Message.DeduplicationKey);
                throw;
            }
        }
    }
}