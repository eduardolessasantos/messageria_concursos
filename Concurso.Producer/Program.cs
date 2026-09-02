using Concurso.Producer;
using Concurso.Producer.Interfaces;
using Concurso.Producer.Parsers;
using Concurso.Producer.Services;
using Concurso.Producer.Sources;
using Concurso.Shared.Health;
using Concurso.Shared.Metrics;
using Concurso.Shared.Options;
using MassTransit;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using Serilog;
using Serilog.Events;
using System.Security.Authentication;

// ─────────────────────────────────────────────────────────────────────────────
// Host builder — estilo minimal hosting do .NET 8
// ─────────────────────────────────────────────────────────────────────────────
var builder = Host.CreateApplicationBuilder(args);

// -----------------------------------------------------------------------------
// Serilog (logs estruturados)
// -----------------------------------------------------------------------------
// Ajuste a configuração conforme necessário (sink, level, enrichers)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Services.AddSerilog();

// Bind options
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.Configure<CollectorOptions>(builder.Configuration.GetSection("Collector"));

// Register shared metrics
builder.Services.AddSingleton<IAppMetrics, InMemoryAppMetrics>();

// -----------------------------------------------------------------------------
// Resiliência HTTP (IHttpClientFactory + Polly)
// -----------------------------------------------------------------------------
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .Or<TimeoutRejectedException>()
    .WaitAndRetryAsync(new[]
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(7)
    }, (outcome, timespan, retryCount, context) =>
    {
        // Pode logar retry aqui via context se necessário
    });

var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10));
// ─────────────────────────────────────────────────────────────────────────────
// HttpClient — configuração centralizada
//
// O HttpClient nomeado garante:
//   - Reuso de conexões TCP (evita socket exhaustion)
//   - Headers padrão (User-Agent obrigatório na maioria dos sites)
//   - Timeout configurável sem alterar o serviço
// ─────────────────────────────────────────────────────────────────────────────
builder.Services
    .AddHttpClient(ConcursoCollectorService.HttpClientName, client =>
    {
        client.BaseAddress = new Uri("https://www.pciconcursos.com.br");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/5.0 (compatible; ConcursosTI-Bot/1.0)");
        client.DefaultRequestHeaders.Add(
            "Accept",
            "text/html,application/xhtml+xml");
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .AddPolicyHandler(timeoutPolicy)
    .AddPolicyHandler(retryPolicy);

// Registrations relacionados a fontes e agregador
builder.Services.AddTransient<IConcursoSource, Concurso.Producer.Sources.PciConcursosSource>();
builder.Services.AddTransient<IConcursoAggregationService, Concurso.Producer.Services.ConcursoAggregationService>();

// ─────────────────────────────────────────────────────────────────────────────
// Serviços de coleta
//
// Transient: sem estado compartilhado — cada chamada recebe instância limpa.
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddTransient<IConcursoHtmlParser, ConcursoHtmlParser>();
builder.Services.AddTransient<IConcursoCollectorService, ConcursoCollectorService>();

// ─────────────────────────────────────────────────────────────────────────────
// MassTransit + RabbitMQ
//
// UseSsl: obrigatório para CloudAMQP (porta 5671, amqps://).
// Para ambiente local sem SSL (localhost), basta setar "UseSsl": false
// no appsettings.json — sem nenhuma alteração de código.
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var mq = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

        cfg.Host(mq.Host, mq.Port, mq.VirtualHost, h =>
        {
            h.Username(mq.Username);
            h.Password(mq.Password);

            if (mq.UseSsl)
            {
                h.UseSsl(s => s.Protocol = SslProtocols.Tls12);
            }
        });
    });
});

// Health checks (registro; exposição HTTP é opcional e pode ser adicionada depois)
builder.Services.AddHealthChecks()
    .AddCheck<RabbitMqHealthCheck>("rabbitmq")
    .AddCheck("self", () => HealthCheckResult.Healthy("OK"));

// Hosted service que periodicamente avalia health checks e loga (preparação para expor endpoint)
builder.Services.AddHostedService<Concurso.Producer.Observability.HealthCheckPublisher>();

// ─────────────────────────────────────────────────────────────────────────────
// Worker
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();
