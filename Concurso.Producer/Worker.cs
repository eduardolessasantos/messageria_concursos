using Concurso.Messaging.Events;
using Concurso.Producer.Interfaces;
using Concurso.Producer.Services;
using Concurso.Shared.Metrics;
using Concurso.Shared.Options;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Concurso.Producer;

/// <summary>
/// Worker responsável por orquestrar o ciclo de coleta.
///
/// Neste momento apenas coleta e loga — publicação de eventos virá na próxima etapa.
/// A separação já está preparada: o Worker chama o serviço e receberá os DTOs prontos.
/// </summary>
public sealed class Worker : BackgroundService
{
    // Intervalo entre cada ciclo de coleta — ideal mover para appsettings
    private static readonly TimeSpan IntervaloColeta = TimeSpan.FromMinutes(60);

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
        _intervalo = TimeSpan.FromMinutes(collectorOptions.Value.IntervaloMinutos);
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker iniciado. Ciclo de coleta a cada {Intervalo}.", IntervaloColeta);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Iniciando ciclo de coleta. {Horario}", DateTimeOffset.UtcNow);

            try
            {
                // Resolve agregador por escopo para evitar problemas de lifetime
                using var scope = _services.CreateScope();
                var aggregator = scope.ServiceProvider.GetRequiredService<IConcursoAggregationService>();

                var concursos = await aggregator.AggregateAllAsync(stoppingToken);

                _metrics.IncrementFound(concursos.Count);
                _logger.LogInformation("Coleta finalizada. Encontrados {Total} concurso(s).", concursos.Count);

                // Exemplo: publicar (quando for ativar) e contabilizar published
                foreach (var concurso in concursos)
                {
                    //await _bus.Publish(new ConcursoPublicadoEvent { ... }, stoppingToken);
                    //_metrics.IncrementPublished();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Erro inesperado não derruba o Worker — aguarda o próximo ciclo
                _logger.LogError(ex, "Erro não tratado durante o ciclo de coleta.");
            }

            _logger.LogInformation("Próxima coleta em {Intervalo}.", IntervaloColeta);
            await Task.Delay(IntervaloColeta, stoppingToken);
        }

        _logger.LogInformation("Worker encerrado.");
    }
}