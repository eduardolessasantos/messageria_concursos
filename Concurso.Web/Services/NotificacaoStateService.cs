using Concurso.Messaging.Events;
using Concurso.Web.Models;

namespace Concurso.Web.Services;

public class NotificacaoStateService
{
    private readonly object _lock = new();
    private readonly List<NotificacaoLog> _logs = new();
    private DateTime _lastNotify = DateTime.MinValue;

    public event Action? OnChange;

    public IReadOnlyList<NotificacaoLog> Logs
    {
        get
        {
            lock (_lock)
            {
                return _logs.ToList();
            }
        }
    }

    public void AdicionarLog(NotificacaoEnviadaEvent evt)
    {
        lock (_lock)
        {
            _logs.Insert(0, new NotificacaoLog(evt));
            if (_logs.Count > 200)
            {
                _logs.RemoveAt(_logs.Count - 1);
            }
        }

        NotifyDebounced();
    }

    public void AdicionarLogConcurso(ConcursoPublicadoEvent evt)
    {
        lock (_lock)
        {
            _logs.Insert(0, new NotificacaoLog
            {
                EventId = evt.EventId,
                DeduplicationKey = evt.DeduplicationKey,
                Tipo = "Broker",
                Status = "Publicado",
                Detalhe = $"[Broker] Novo concurso detectado: '{evt.Cargo} ({evt.Orgao})' - Remuneração: {evt.Salario} | Fonte: {evt.Fonte}",
                Data = evt.DataCaptura.UtcDateTime
            });

            if (_logs.Count > 200)
            {
                _logs.RemoveAt(_logs.Count - 1);
            }
        }

        NotifyDebounced();
    }

    private void NotifyDebounced()
    {
        // Evita spam de StateHasChanged quando chegam dezenas de mensagens em lote
        lock (_lock)
        {
            if ((DateTime.UtcNow - _lastNotify).TotalMilliseconds < 200)
            {
                return;
            }
            _lastNotify = DateTime.UtcNow;
        }

        OnChange?.Invoke();
    }

    public void LimparLogs()
    {
        lock (_lock)
        {
            _logs.Clear();
            _lastNotify = DateTime.MinValue;
        }

        OnChange?.Invoke();
    }
}
