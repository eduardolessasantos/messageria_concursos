using System;

namespace Concurso.Producer.DTOs;

/// <summary>
/// Objeto de transferência de dados entre o parser e o serviço de coleta.
/// Representa um concurso bruto capturado da fonte — ainda sem validação de negócio
/// nem decisão de publicar/ignorar.
///
/// Usa record para imutabilidade: uma vez parseado, o dado não deve ser mutado.
/// </summary>
public sealed record ConcursoDto
{
    /// <summary>
    /// Chave de deduplicação gerada a partir do Link.
    /// Permite verificar, futuramente, se o concurso já foi processado
    /// sem depender de banco de dados — basta comparar o hash.
    /// </summary>
    public required string DeduplicationKey { get; init; }

    /// <summary>Título do concurso conforme extraído da fonte.</summary>
    public required string Titulo { get; init; }

    /// <summary>Órgão ou entidade responsável pelo concurso.</summary>
    public required string Orgao { get; init; }

    /// <summary>Cargo ou vaga ofertada.</summary>
    public required string Cargo { get; init; }

    /// <summary>Salário como texto livre, preservando a formatação da fonte.</summary>
    public required string Salario { get; init; }

    /// <summary>URL do edital ou página do concurso.</summary>
    public required string Link { get; init; }

    /// <summary>Data de publicação conforme informada na fonte (UTC).</summary>
    public required DateTimeOffset DataPublicacao { get; init; }

    /// <summary>Momento em que este dado foi capturado pelo sistema (UTC).</summary>
    public required DateTimeOffset DataCaptura { get; init; }

    /// <summary>
    /// Fonte de origem do concurso (ex: "PCI Concursos", "Gran Cursos", "Mock").
    /// Útil para rastrear de qual crawler o dado veio e calibrar parsers.
    /// </summary>
    public required string Fonte { get; init; }

    public string? Descricao { get; set; }

    public int RelevanciaScore { get; init; } = 1;

    public string[] KeywordsEncontradas { get; init; } = Array.Empty<string>();
}