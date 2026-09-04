using Concurso.Consumer.Consumers;
using Concurso.Consumer.Data;
using Concurso.Consumer.Repositories;
using Concurso.Shared.Health;
using Concurso.Shared.Metrics;
using Concurso.Shared.Options;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Security.Authentication;

var builder = Host.CreateApplicationBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

// Bind options
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMQ"));

// Database (MySQL 8.0 via Pomelo)
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

// Repository
builder.Services.AddScoped<IConcursoRepository, ConcursoRepository>();

// MassTransit consumer registration
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ConcursoPublicadoConsumer>();

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

        cfg.ReceiveEndpoint("concurso-published-queue", e =>
        {
            e.ConfigureConsumer<ConcursoPublicadoConsumer>(context);
            e.PrefetchCount = 16;
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        });
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ConcursoDbContext>("db-context")
    .AddCheck<RabbitMqHealthCheck>("rabbitmq");

var app = builder.Build();

// Garante criação do banco de dados MySQL e tabelas na inicialização se acessível
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ConcursoDbContext>();
    db.Database.EnsureCreated();
    Log.Information("Banco de dados verificado com sucesso no Consumer.");
}
catch (Exception ex)
{
    Log.Warning("Banco de dados indisponível no startup do Consumer ({Erro}). As tentativas de reconexão ocorrerão durante o consumo.", ex.Message);
}

await app.RunAsync();
