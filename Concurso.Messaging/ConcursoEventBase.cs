namespace Concurso.Messaging.Events;

/// <summary>
/// Base abstrata para todos os eventos de concurso.
/// Centraliza os campos de infraestrutura (rastreabilidade) e os campos
/// de domínio comuns, garantindo consistência entre os eventos.
///
/// Usa <c>record</c> para imutabilidade estrutural e igualdade por valor.
/// O init-only garante que nenhuma propriedade seja alterada após a construção.
/// </summary>
public abstract record ConcursoEventBase : IConcursoEvent
{
    // -------------------------------------------------------------------------
    // Campos de infraestrutura (envelope do evento)
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public abstract int EventVersion { get; }

    /// <inheritdoc />
    public abstract string EventType { get; }

    /// <inheritdoc />
    public DateTimeOffset OcorridoEm { get; init; } = DateTimeOffset.UtcNow;

    // -------------------------------------------------------------------------
    // Campos de domínio — payload do concurso
    // -------------------------------------------------------------------------

    /// <summary>Identificador único do concurso (chave de negócio estável).</summary>
    public required Guid ConcursoId { get; init; }

    /// <summary>Título completo do concurso conforme publicado no edital.</summary>
    public required string Titulo { get; init; }

    /// <summary>Órgão ou entidade responsável pelo concurso (ex: "Receita Federal").</summary>
    public required string Orgao { get; init; }

    /// <summary>Cargo ou vaga à qual o concurso se destina (ex: "Auditor Fiscal").</summary>
    public required string Cargo { get; init; }

    /// <summary>
    /// Salário descrito no edital como texto livre (ex: "R$ 12.455,50").
    /// Texto livre para preservar a informação original sem risco de perda de precisão.
    /// </summary>
    public required string Salario { get; init; }

    /// <summary>URL do edital ou página oficial do concurso.</summary>
    public required string Link { get; init; }

    /// <summary>Data de publicação do edital pelo órgão (UTC).</summary>
    public required DateTimeOffset DataPublicacao { get; init; }

    /// <summary>Momento em que o sistema capturou/detectou o concurso (UTC).</summary>
    public required DateTimeOffset DataCaptura { get; init; }
}