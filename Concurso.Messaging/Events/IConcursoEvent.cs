namespace Concurso.Messaging.Events;

/// <summary>
/// Contrato base para todos os eventos de concurso.
/// Garante que qualquer evento carregue os campos mínimos necessários
/// para rastreabilidade, correlação e idempotência.
/// </summary>
public interface IConcursoEvent
{
    /// <summary>Identificador único do evento (UUID v4). Usado para idempotência.</summary>
    Guid EventId { get; }

    /// <summary>Versão do schema do evento. Permite evolução sem breaking changes.</summary>
    int EventVersion { get; }

    /// <summary>Nome canônico do evento (ex: "concurso.publicado.v1"). Útil para roteamento e logs.</summary>
    string EventType { get; }

    /// <summary>Momento exato em que o evento foi gerado (UTC).</summary>
    DateTimeOffset OcorridoEm { get; }

    /// <summary>Identificador único do concurso ao qual o evento se refere.</summary>
    Guid ConcursoId { get; }
}
