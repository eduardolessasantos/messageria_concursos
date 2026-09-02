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

// Serilog básico (console structured)
Log.Logger = new LoggerConfiguration().MinimumLevel.Information().Enrich.FromLogContext().WriteTo.Console().CreateLogger();

builder.Services.AddSerilog();

// Bind options
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMQ"));

// Db
var sqliteConn = builder.Configuration.GetValue<string>("ConnectionStrings:Sqlite") ?? "Data Source=concurso_consumer.db";

builder.Services.AddDbContext<ConcursoDbContext>(opts =>
{
opts.UseSqlite(sqliteConn);
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
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5))); // 3 tentativas, 5s
        });
    });

});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ConcursoDbContext>("db-context")
    .AddCheck<RabbitMqHealthCheck>("rabbitmq");

var app = builder.Build();

// Ensure database created/migrations applied (safe for dev; for prod use migrations)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ConcursoDbContext>();
    db.Database.EnsureCreated();
}

await app.RunAsync();
