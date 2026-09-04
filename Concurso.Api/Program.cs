using Concurso.Consumer.Data;
using Concurso.Messaging.Events;
using Concurso.Producer.Interfaces;
using Concurso.Producer.Parsers;
using Concurso.Producer.Services;
using Concurso.Producer.Sources;
using Concurso.Shared.Metrics;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using Serilog;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Serilog com Console e Arquivo Rotativo
Directory.CreateDirectory("logs");
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/concurso-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Database (MySQL via Pomelo)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("MySql")
    ?? "Server=localhost;Port=3306;Database=concursos_ti;User=root;Password=270523;CharSet=utf8mb4;";
builder.Services.AddDbContext<ConcursoDbContext>(opts =>
{
    opts.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)), mySqlOptions =>
    {
        mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    });
});

// Metrics
builder.Services.AddSingleton<IAppMetrics, InMemoryAppMetrics>();

// Coleta HTTP + Resiliência Polly
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .Or<TimeoutRejectedException>()
    .WaitAndRetryAsync(new[]
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3)
    });

var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(15));

builder.Services
    .AddHttpClient(ConcursoCollectorService.HttpClientName, client =>
    {
        client.BaseAddress = new Uri("https://www.pciconcursos.com.br");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; ConcursosTI-Bot/1.0)");
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .AddPolicyHandler(timeoutPolicy)
    .AddPolicyHandler(retryPolicy);

builder.Services
    .AddHttpClient(GranCursosSource.HttpClientName, client =>
    {
        client.BaseAddress = new Uri("https://www.grancursosonline.com.br");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; ConcursosTI-Bot/1.0)");
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .AddPolicyHandler(timeoutPolicy)
    .AddPolicyHandler(retryPolicy);

builder.Services.AddTransient<IConcursoSource, PciConcursosSource>();
builder.Services.AddTransient<IConcursoSource, GranCursosSource>();
builder.Services.AddTransient<IConcursoSource, MockConcursoSource>();

builder.Services.AddTransient<IConcursoAggregationService, ConcursoAggregationService>();
builder.Services.AddTransient<IConcursoHtmlParser, ConcursoHtmlParser>();
builder.Services.AddTransient<IConcursoCollectorService, ConcursoCollectorService>();

// MassTransit (Publisher RabbitMQ)
var rabbitHost = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
var rabbitPort = builder.Configuration.GetValue<ushort?>("RabbitMQ:Port") ?? 5672;
var rabbitUser = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
var rabbitPass = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";
var rabbitVHost = builder.Configuration.GetValue<string>("RabbitMQ:VirtualHost")
               ?? builder.Configuration.GetValue<string>("RabbitMQ:VHost")
               ?? (rabbitUser != "guest" ? rabbitUser : "/");
var rabbitUseSsl = builder.Configuration.GetValue<bool>("RabbitMQ:UseSsl", false) || rabbitPort == 5671;

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, rabbitPort, rabbitVHost, h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);

            if (rabbitUseSsl)
            {
                h.UseSsl(s => s.Protocol = System.Security.Authentication.SslProtocols.Tls12);
            }
        });

        cfg.ConfigureEndpoints(context);
    });
});

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Concursos TI - API & Mensageria",
        Version = "v1",
        Description = "API de gestão, coleta e publicação de eventos de concursos de TI (Padrão NotificaFlow)"
    });
});

// CORS para permitir conexões do GitHub Pages e ambientes externos
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Concursos TI API v1");
    c.RoutePrefix = string.Empty; // Swagger na raiz (http://localhost:5000/)
});

// Tenta inicializar o banco de dados sem travar a inicialização do container
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ConcursoDbContext>();
    db.Database.EnsureCreated();
    Log.Information("Banco de dados verificado com sucesso.");
}
catch (Exception ex)
{
    Log.Warning("Banco de dados MySQL indisponível na inicialização ({Erro}). A API continuará ativa e tentará reconectar sob demanda.", ex.Message);
}

// -----------------------------------------------------------------------------
// Endpoints Minimal API
// -----------------------------------------------------------------------------

// Health Check com verificação de dependências
app.MapGet("/health", async (ConcursoDbContext db) =>
{
    bool dbOk = false;
    try
    {
        dbOk = await db.Database.CanConnectAsync();
    }
    catch
    {
        dbOk = false;
    }

    var status = dbOk ? "Healthy" : "Degraded";
    return Results.Ok(new
    {
        status = status,
        timestamp = DateTime.UtcNow,
        service = "Concurso.Api",
        version = "1.0.0",
        dependencies = new
        {
            api = "Healthy",
            database = dbOk ? "Connected" : "Unavailable",
            broker = "Configured"
        }
    });
})
.WithName("HealthCheck")
.WithTags("Observabilidade");

// Listar concursos persistidos no banco (resiliente a banco indisponível)
app.MapGet("/api/concursos", async (ConcursoDbContext db) =>
{
    try
    {
        var list = await db.Concursos
            .OrderByDescending(c => c.DataCaptura)
            .ToListAsync();
        return Results.Ok(list);
    }
    catch (Exception ex)
    {
        Log.Warning("Não foi possível consultar concursos no banco ({Erro}). Retornando lista vazia.", ex.Message);
        return Results.Ok(Array.Empty<object>());
    }
})
.WithName("ListarConcursos")
.WithTags("Concursos");

// Forçar ciclo de coleta manual e publicar no RabbitMQ (com suporte a filtro por fonte)
app.MapPost("/api/concursos/coletar", async (
    [FromServices] IConcursoAggregationService aggregator,
    [FromServices] IBus bus,
    [FromQuery] string? fonte,
    CancellationToken ct) =>
{
    var encontrados = await aggregator.AggregateAsync(fonte, ct);
    var publicados = new List<ConcursoPublicadoEvent>();

    foreach (var c in encontrados)
    {
        var evt = new ConcursoPublicadoEvent
        {
            EventId = Guid.NewGuid(),
            DeduplicationKey = c.DeduplicationKey,
            Titulo = c.Titulo,
            Orgao = c.Orgao,
            Cargo = c.Cargo,
            Salario = c.Salario,
            Link = c.Link,
            DataPublicacao = c.DataPublicacao,
            DataCaptura = c.DataCaptura,
            Fonte = c.Fonte,
            Descricao = c.Descricao,
            RelevanciaScore = c.RelevanciaScore,
            KeywordsEncontradas = c.KeywordsEncontradas
        };

        await bus.Publish(evt, ct);
        publicados.Add(evt);
    }

    return Results.Ok(new
    {
        Mensagem = $"Coleta executada com sucesso. {publicados.Count} concurso(s) de TI publicados no broker.",
        FonteSolicitada = fonte ?? "todas",
        TotalPublicados = publicados.Count,
        Concursos = publicados
    });
})
.WithName("ColetarEPublicar")
.WithTags("Concursos");

// Disparar concurso teste para acionar envio imediato de e-mail via Resend/Mailpit
app.MapPost("/api/concursos/test-email", async (
    [FromServices] IBus bus,
    [FromQuery] string? orgao,
    [FromQuery] string? cargo,
    [FromQuery] string? salario,
    CancellationToken ct) =>
{
    var testEvent = new ConcursoPublicadoEvent
    {
        EventId = Guid.NewGuid(),
        DeduplicationKey = $"test-{Guid.NewGuid():N}",
        Titulo = $"Concurso {orgao ?? "Dataprev"} - {cargo ?? "Analista de Tecnologia da Informação"}",
        Orgao = orgao ?? "Dataprev - Empresa de Tecnologia e Informações da Previdência",
        Cargo = cargo ?? "Analista de TI (Engenharia de Software & Cloud)",
        Salario = salario ?? "R$ 14.850,00",
        Link = "https://www.pciconcursos.com.br/concursos/",
        DataPublicacao = DateTimeOffset.UtcNow,
        DataCaptura = DateTimeOffset.UtcNow,
        Fonte = "Teste NotificaFlow",
        Descricao = "Oportunidade para profissionais de TI com atuação em microsserviços, mensageria e cloud computing.",
        RelevanciaScore = 4,
        KeywordsEncontradas = new[] { "analista de ti", "engenharia de software", "cloud" }
    };

    await bus.Publish(testEvent, ct);

    return Results.Ok(new
    {
        Mensagem = "Evento de teste de concurso publicado no RabbitMQ! O Worker.Email deve consumir e disparar notificação.",
        Evento = testEvent
    });
})
.WithName("DispararEmailTeste")
.WithTags("Testes");

// Consultar últimos erros registrados nos logs
app.MapGet("/api/logs/erros", () =>
{
    try
    {
        var logFiles = Directory.GetFiles("logs", "concurso-*.log")
            .OrderByDescending(File.GetLastWriteTime)
            .ToList();

        if (logFiles.Count == 0)
        {
            return Results.Ok(new { total = 0, erros = Array.Empty<string>() });
        }

        var erros = new List<string>();
        foreach (var file in logFiles.Take(2))
        {
            var linhas = File.ReadAllLines(file);
            erros.AddRange(linhas.Where(l => l.Contains("[ERR]") || l.Contains("[FTL]") || l.Contains("Exception")).TakeLast(50));
        }

        return Results.Ok(new
        {
            total = erros.Count,
            erros = erros.TakeLast(50)
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { total = 0, mensagem = ex.Message, erros = Array.Empty<string>() });
    }
})
.WithName("ConsultarLogsErros")
.WithTags("Observabilidade");

app.Run();
