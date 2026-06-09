using Concurso.Producer.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concurso.Producer.Sources;

/// <summary>
/// Contrato para uma fonte de concursos (crawler/parser).
/// Implementações devem ser stateless e retornar lista de DTOs.
/// </summary>
public interface IConcursoSource
{
    /// <summary>
    /// Nome identificador da fonte (usado em logs).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Obtém concursos da fonte.
    /// </summary>
    Task<IReadOnlyList<ConcursoDto>> GetAsync(CancellationToken cancellationToken = default);
}
