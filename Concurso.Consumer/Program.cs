using Concurso.Consumer.Consumers;
using Concurso.Consumer.Data;
using Concurso.Consumer.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Security.Authentication;


var builder = Host.CreateApplicationBuilder(args);

// Serilog básico (console structured)
Log.Logger = new LoggerConfiguration()
.MinimumLevel.Information()
.Enrich.FromLogContext()
.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
.CreateLogger();

builder.Services.AddSerilog();

var configuration = builder.Configuration;

// Configurações: espera RabbitMQ:Host, VirtualHost, Username, Password, Port
var rabbitHost = builder.Configuration["RabbitMQ:Host"];//configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitPort = builder.Configuration.GetValue<ushort>("RabbitMQ:Port");//configuration.GetValue<ushort?>("RabbitMQ:Port") ?? 5672;
var rabbitVHost = builder.Configuration["RabbitMQ:VirtualHost"];//configuration["RabbitMQ:VirtualHost"] ?? "/";
var rabbitUser = builder.Configuration["RabbitMQ:Username"];//configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"];//configuration["RabbitMQ:Password"] ?? "guest";

// Db
var sqliteConn = configuration.GetValue<string>("ConnectionStrings:Sqlite") ?? "Data Source=concurso_consumer.db";

builder.Services.AddDbContext<ConcursoDbContext>(opts =>
{
opts.UseSqlite(sqliteConn);
});

// Repository
builder.Services.AddScoped<IConcursoRepository, ConcursoRepository>();

Console.WriteLine($"RabbitMQ Config - Host: {builder.Configuration["RabbitMQ:Host"]}, Port: {builder.Configuration.GetValue<ushort>("RabbitMQ:Port")}, VHost: {builder.Configuration["RabbitMQ:VirtualHost"]}, User: {builder.Configuration["RabbitMQ:Username"]}");

// MassTransit consumer registration
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ConcursoPublicadoConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(
        builder.Configuration["RabbitMQ:Host"],
        builder.Configuration.GetValue<ushort>("RabbitMQ:Port"),
        builder.Configuration["RabbitMQ:VirtualHost"],
        h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]);
            h.Password(builder.Configuration["RabbitMQ:Password"]);

            h.UseSsl(s =>
            {
                s.Protocol = SslProtocols.Tls12;
            });
        });

        // Configure a endpoint name as needed; MassTransit will bind exchange -> queue
        cfg.ReceiveEndpoint("concurso-published-queue", e =>
        {
            e.ConfigureConsumer<ConcursoPublicadoConsumer>(context);
            // You can set prefetch, retry, etc. here as needed:
            e.PrefetchCount = 16;
            // basic retry policy (MassTransit middleware)
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        });
    });

});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ConcursoDbContext>("db-context");

var app = builder.Build();

// Ensure database created/migrations applied (safe for dev; for prod use migrations)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ConcursoDbContext>();
    db.Database.EnsureCreated();
}

await app.RunAsync();
