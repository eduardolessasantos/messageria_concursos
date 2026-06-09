namespace Concurso.Messaging.Events;

/// <summary>
/// Publicado quando o sistema detecta um concurso, mas decide descartá-lo
/// por não atender aos critérios de relevância (área, salário mínimo, região, etc.).
///
/// Importante para auditoria e rastreabilidade do crawler:
/// permite saber o que foi visto mas não processado, e por quê.
///
/// Versão: 1
/// </summary>
public sealed record ConcursoIgnoradoEvent : ConcursoEventBase
{
    /// <inheritdoc />
    public override int EventVersion => 1;

    /// <inheritdoc />
    public override string EventType => "concurso.ignorado.v1";

    /// <summary>Motivo pelo qual o concurso foi descartado.</summary>
    public required MotivoIgnorado Motivo { get; init; }

    /// <summary>
    /// Detalhes complementares sobre o motivo (ex: "Salário R$ 3.000 abaixo do mínimo R$ 5.000").
    /// Opcional — use para depuração e ajuste de filtros.
    /// </summary>
    public string? Detalhe { get; init; }
}

/// <summary>
/// Enumera as razões pelas quais um concurso pode ser ignorado pelo sistema.
/// Evita strings mágicas e facilita filtragem por consumers.
/// </summary>
public enum MotivoIgnorado
{
    /// <summary>Concurso fora da área de TI.</summary>
    AreaNaoRelevante = 1,

    /// <summary>Salário abaixo do limiar configurado.</summary>
    SalarioAbaixoDoMinimo = 2,

    /// <summary>Concurso já foi processado anteriormente (duplicata).</summary>
    Duplicata = 3,

    /// <summary>Edital incompleto ou dados insuficientes para processamento.</summary>
    DadosInsuficientes = 4,

    /// <summary>Concurso de região geográfica fora do escopo configurado.</summary>
    RegiaoNaoAtendida = 5,

    /// <summary>Motivo não categorizado — consulte o campo <c>Detalhe</c>.</summary>
    Outro = 99
}