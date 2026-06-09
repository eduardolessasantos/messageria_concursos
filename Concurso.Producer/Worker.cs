using Concurso.Producer.Interfaces;
using Concurso.Producer.Services;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

    public Worker(
        ILogger<Worker> logger,
        IBus bus,
        IServiceProvider services)
    {
        _logger = logger;
        _bus = bus;
        _services = services;
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

                if (concursos.Count == 0)
                {
                    _logger.LogInformation("Nenhum concurso de TI encontrado neste ciclo.");
                }
                else
                {
                    _logger.LogInformation("{Total} concurso(s) coletado(s):", concursos.Count);

                    foreach (var concurso in concursos)
                    {
                        // Activity por item (enriquecimento para tracing)
                        using (var act = new Activity("ProcessarConcurso"))
                        {
                            act.Start();
                            act.AddTag("dedupKey", concurso.DeduplicationKey);
                            act.AddTag("titulo", concurso.Titulo);

                            // Exemplo de publicação (a ativar quando for publicar):
                            // await _bus.Publish(new ConcursoCriadoEvent { ... }, stoppingToken);

                            _logger.LogInformation(
                                "  → [{Key}] {Titulo} | {Cargo} | {Orgao} | {Salario}",
                                concurso.DeduplicationKey,
                                concurso.Titulo,
                                concurso.Cargo,
                                concurso.Orgao,
                                concurso.Salario);

                            act.Stop();
                        }
                    }
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