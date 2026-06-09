using Concurso.Producer.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Concurso.Producer.Interfaces;

/// <summary>
/// Contrato do serviço responsável por coletar concursos de TI de fontes externas.
///
/// A interface isola o Worker de qualquer detalhe de implementação (HTTP, parsing, filtros),
/// facilitando testes unitários com mocks e troca de implementação sem alterar o chamador.
/// </summary>
public interface IConcursoCollectorService
{
    /// <summary>
    /// Coleta concursos de TI disponíveis na fonte configurada.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento cooperativo.</param>
    /// <returns>
    /// Lista de <see cref="ConcursoDto"/> com os concursos encontrados e filtrados.
    /// Retorna lista vazia se nenhum concurso relevante for encontrado — nunca null.
    /// </returns>
    Task<IReadOnlyList<ConcursoDto>> ColetarAsync(CancellationToken cancellationToken = default);
}