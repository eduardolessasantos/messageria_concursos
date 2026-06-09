using Concurso.Producer.DTOs;
using System.Collections.Generic;

namespace Concurso.Producer.Interfaces;

/// <summary>
/// Contrato do parser responsável por extrair concursos de um documento HTML.
///
/// Separar o parser em sua própria interface permite:
/// - Testar o parsing de forma independente do HTTP
/// - Trocar a implementação (ex: HtmlAgilityPack → AngleSharp) sem afetar o serviço
/// - Ter múltiplos parsers para fontes diferentes
/// </summary>
public interface IConcursoHtmlParser
{
    /// <summary>
    /// Extrai concursos a partir do conteúdo HTML bruto de uma página.
    /// </summary>
    /// <param name="html">Conteúdo HTML da página da fonte.</param>
    /// <param name="fonte">Nome da fonte de origem (para rastreabilidade no DTO).</param>
    /// <returns>
    /// Lista de <see cref="ConcursoDto"/> extraídos do HTML.
    /// Retorna lista vazia se nenhum item for encontrado — nunca null.
    /// </returns>
    IReadOnlyList<ConcursoDto> Parse(string html, string fonte);
}