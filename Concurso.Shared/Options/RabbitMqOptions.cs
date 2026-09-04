using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concurso.Shared.Options;

public sealed class RabbitMqOptions
{
    private string? _virtualHost;

    public string Host { get; init; } = "localhost";
    public ushort Port { get; init; } = 5672;
    public string VirtualHost 
    { 
        get => !string.IsNullOrWhiteSpace(_virtualHost) && _virtualHost != "/" 
            ? _virtualHost 
            : (Username != "guest" && !string.IsNullOrWhiteSpace(Username) ? Username : "/");
        init => _virtualHost = value;
    }
    public string Username { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public bool UseSsl { get; init; } = false;
}
