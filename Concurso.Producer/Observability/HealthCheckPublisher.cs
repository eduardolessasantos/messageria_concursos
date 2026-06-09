using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Concurso.Producer.Observability;

public sealed class HealthCheckPublisher : BackgroundService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<HealthCheckPublisher> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public HealthCheckPublisher(HealthCheckService healthCheckService, ILogger<HealthCheckPublisher> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HealthCheckPublisher iniciado. Intervalo: {Interval}", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var report = await _healthCheckService.CheckHealthAsync(stoppingToken);
                _logger.LogInformation("HealthChecks status: {Status} | Duration: {Duration} | Entries: {EntriesCount}",
                    report.Status, report.TotalDuration, report.Entries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao executar HealthChecks.");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("HealthCheckPublisher encerrado.");
    }
}
