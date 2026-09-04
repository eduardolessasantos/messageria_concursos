using System;

namespace Concurso.Messaging.Events;

/// <summary>
/// Contrato de evento publicado pelo Producer/Collector quando um concurso relevante de TI é detectado.
/// Implementa IEvent para padronização com o modelo NotificaFlow.
/// </summary>
public sealed record ConcursoPublicadoEvent : IEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public required string DeduplicationKey { get; init; }
    public required string Titulo { get; init; }
    public required string Orgao { get; init; }
    public required string Cargo { get; init; }
    public required string Salario { get; init; }
    public required string Link { get; init; }
    public required DateTimeOffset DataPublicacao { get; init; }
    public required DateTimeOffset DataCaptura { get; init; }
    public required string Fonte { get; init; }
    public string? Descricao { get; init; }

    /// <summary>
    /// Pontuação de relevância heurística baseada no volume de palavras-chave casadas.
    /// </summary>
    public int RelevanciaScore { get; init; } = 1;

    /// <summary>
    /// Lista de palavras-chave da área de TI identificadas no edital.
    /// </summary>
    public string[] KeywordsEncontradas { get; init; } = Array.Empty<string>();

    public DateTime OcorridoEm => DataCaptura.UtcDateTime;
}
