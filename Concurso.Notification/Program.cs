using Concurso.Notification.Consumers;
using Concurso.Notification.Services;
using MassTransit;
using Resend;
using Serilog;
using Log = Serilog.Log;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

var rabbitHost = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
var rabbitUser = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
var rabbitPass = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";

// Configuração do Resend
var resendApiToken = builder.Configuration["Resend:ApiKey"]
    ?? builder.Configuration["Resend:ApiToken"]
    ?? builder.Configuration["ResendApiKey"]
    ?? builder.Configuration["RESEND_API_KEY"]
    ?? string.Empty;

builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.AddScoped<IResend, ResendClient>();
builder.Services.Configure<ResendClientOptions>(options =>
{
    options.ApiToken = resendApiToken;
});

// Registro de IEmailSender (Padrão: Resend)
builder.Services.AddScoped<IEmailSender>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var provider = config["Email:Provider"] ?? "Resend";

    Log.Information("[Worker.Email] Provedor de e-mail ativo: {Provider}", provider);

    return provider.ToLowerInvariant() switch
    {
        "resend" => new ResendEmailSender(sp.GetRequiredService<IResend>(), config),
        "mailpit" => new MailpitEmailSender(config),
        _ => new ResendEmailSender(sp.GetRequiredService<IResend>(), config)
    };
});

// MassTransit com RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ConcursoPublicadoConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ReceiveEndpoint("concurso-notification-email-queue", e =>
        {
            e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2)));
            e.UseInMemoryOutbox(context);
            e.ConfigureConsumer<ConcursoPublicadoConsumer>(context);
        });
    });
});

var host = builder.Build();
host.Run();
