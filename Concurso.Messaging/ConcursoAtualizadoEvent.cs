namespace Concurso.Messaging.Events;

/// <summary>
/// Publicado quando um concurso já conhecido sofre alteração relevante
/// (reabertura de inscrições, correção de salário, novo link de edital, etc.).
///
/// Carrega o estado completo atualizado do concurso (snapshot), não apenas o delta.
/// Isso simplifica o consumo: o consumer não precisa buscar o estado anterior.
///
/// Versão: 1
/// </summary>
public sealed record ConcursoAtualizadoEvent : ConcursoEventBase
{
    /// <inheritdoc />
    public override int EventVersion => 1;

    /// <inheritdoc />
    public override string EventType => "concurso.atualizado.v1";

    /// <summary>
    /// Descrição textual do que foi alterado (ex: "Salário corrigido de R$ 8.000 para R$ 9.500").
    /// Campo livre para rastreabilidade e auditoria — não deve ser usado como lógica de negócio.
    /// </summary>
    public string? MotivoAtualizacao { get; init; }
}