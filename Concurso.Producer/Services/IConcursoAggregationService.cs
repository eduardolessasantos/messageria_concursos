using Concurso.Producer.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Concurso.Producer.Services;

/// <summary>
/// Orquestra múltiplas IConcursoSource e agrega resultados de oportunidades de TI.
/// </summary>
public interface IConcursoAggregationService
{
    /// <summary>
    /// Executa todas as fontes registradas.
    /// </summary>
    Task<IReadOnlyList<ConcursoDto>> AggregateAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa as fontes filtrando pelo nome especificado (ex: "todas", "pci", "gran", "mock").
    /// </summary>
    Task<IReadOnlyList<ConcursoDto>> AggregateAsync(string? fonte = null, CancellationToken cancellationToken = default);
}