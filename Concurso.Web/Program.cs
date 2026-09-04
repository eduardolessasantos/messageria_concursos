using Concurso.Web.Components;
using Concurso.Web.Consumers;
using Concurso.Web.Services;
using MassTransit;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// State Service (Singleton em memória para atualizar UI via SignalR)
builder.Services.AddSingleton<NotificacaoStateService>();

// HttpClient para comunicação com a Concurso.Api
var apiUrl = builder.Configuration.GetValue<string>("Services:ConcursoApi") ?? "http://localhost:5000";
builder.Services.AddHttpClient<ConcursoApiClient>(client =>
{
    client.BaseAddress = new Uri(apiUrl);
});

// MassTransit (Consumidor da fila concurso-web-queue)
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
    x.AddConsumer<NotificacaoEnviadaConsumer>();

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

        cfg.ReceiveEndpoint("concurso-web-queue", e =>
        {
            e.ConfigureConsumer<NotificacaoEnviadaConsumer>(context);
        });
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
