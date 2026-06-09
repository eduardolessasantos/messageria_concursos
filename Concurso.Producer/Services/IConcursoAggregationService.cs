using Concurso.Producer.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concurso.Producer.Services;

/// <summary>
/// Orquestra múltiplas IConcursoSource e agrega resultados.
/// </summary>
public interface IConcursoAggregationService
{
    Task<IReadOnlyList<ConcursoDto>> AggregateAllAsync(CancellationToken cancellationToken = default);
}