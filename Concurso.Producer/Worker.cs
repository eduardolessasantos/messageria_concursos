using System.Collections.Concurrent;
using Concurso.Messaging.Events;
using Concurso.Producer.Interfaces;
using Concurso.Producer.Services;
using Concurso.Shared.Metrics;
using Concurso.Shared.Options;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Concurso.Producer;

/// <summary>
/// Worker responsável por orquestrar o ciclo de coleta e publicação de concursos de TI.
/// Publica ConcursoPublicadoEvent no RabbitMQ para ser consumido pelo Worker de E-mail (Resend) e Consumer de Banco.
/// </summary>
public sealed class Worker : BackgroundService
{
    private static readonly ConcurrentDictionary<string, DateTime> PublishedKeys = new();

    private readonly IServiceProvider _services;
    private readonly IBus _bus;
    private readonly ILogger<Worker> _logger;
    private readonly TimeSpan _intervalo;
    private readonly IAppMetrics _metrics;

    public Worker(
        ILogger<Worker> logger,
        IBus bus,
        IServiceProvider services,
        IOptions<CollectorOptions> collectorOptions,
        IAppMetrics metrics)
    {
        _logger = logger;
        _bus = bus;
        _services = services;
        var minutos = collectorOptions.Value?.IntervaloMinutos ?? 60;
        _intervalo = TimeSpan.FromMinutes(minutos <= 0 ? 60 : minutos);
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de Coleta iniciado. Ciclo de coleta configurado a cada {Intervalo}.", _intervalo);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Iniciando ciclo de coleta de concursos de TI em {Horario}", DateTimeOffset.UtcNow);

            try
            {
                using var scope = _services.CreateScope();
                var aggregator = scope.ServiceProvider.GetRequiredService<IConcursoAggregationService>();

                var concursos = await aggregator.AggregateAllAsync(stoppingToken);

                _metrics.IncrementFound(concursos.Count);
                _logger.LogInformation("Coleta finalizada. Encontrados {Total} concurso(s) relevantes de TI.", concursos.Count);

                int novosPublicados = 0;

                foreach (var concurso in concursos)
                {
                    // Evita reenviar o mesmo concurso no mesmo dia / sessão
                    if (PublishedKeys.TryAdd(concurso.DeduplicationKey, DateTime.UtcNow))
                    {
                        var evento = new ConcursoPublicadoEvent
                        {
                            EventId = Guid.NewGuid(),
                            DeduplicationKey = concurso.DeduplicationKey,
                            Titulo = concurso.Titulo,
                            Orgao = concurso.Orgao,
                            Cargo = concurso.Cargo,
                            Salario = concurso.Salario,
                            Link = concurso.Link,
                            DataPublicacao = concurso.DataPublicacao,
                            DataCaptura = concurso.DataCaptura,
                            Fonte = concurso.Fonte,
                            Descricao = concurso.Descricao,
                            RelevanciaScore = concurso.RelevanciaScore,
                            KeywordsEncontradas = concurso.KeywordsEncontradas
                        };

                        await _bus.Publish(evento, stoppingToken);
                        _metrics.IncrementPublished();
                        novosPublicados++;

                        _logger.LogInformation("Concurso de TI publicado no broker | Key: {Key} | Cargo: {Cargo} | Órgão: {Orgao}",
                            concurso.DeduplicationKey, concurso.Cargo, concurso.Orgao);
                    }
                    else
                    {
                        _logger.LogDebug("Concurso já publicado anteriormente | Key: {Key}", concurso.DeduplicationKey);
                    }
                }

                _logger.LogInformation("Ciclo concluído. Novos concursos publicados neste lote: {Novos}", novosPublicados);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Erro não tratado durante o ciclo de coleta.");
            }

            _logger.LogInformation("Próxima coleta agendada em {Intervalo}.", _intervalo);
            await Task.Delay(_intervalo, stoppingToken);
        }

        _logger.LogInformation("Worker de Coleta encerrado.");
    }
}