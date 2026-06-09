using Concurso.Producer.DTOs;
using Concurso.Producer.Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concurso.Producer.Services;

/// <summary>
/// Implementação que consulta todas as fontes registradas e agrega os resultados.
/// Falhas em uma fonte não interrompem as demais — são logadas e a execução continua.
/// </summary>
public sealed class ConcursoAggregationService : IConcursoAggregationService
{
    private readonly IEnumerable<IConcursoSource> _sources;
    private readonly ILogger<ConcursoAggregationService> _logger;

    public ConcursoAggregationService(IEnumerable<IConcursoSource> sources, ILogger<ConcursoAggregationService> logger)
    {
        _sources = sources ?? Enumerable.Empty<IConcursoSource>();
        _logger = logger;
    }

    public async Task<IReadOnlyList<ConcursoDto>> AggregateAllAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<ConcursoDto>();

        foreach (var source in _sources)
        {
            try
            {
                _logger.LogInformation("Iniciando fonte {Fonte}", source.Name);
                var itens = await source.GetAsync(cancellationToken);
                if (itens is not null && itens.Count > 0)
                {
                    all.AddRange(itens);
                    _logger.LogInformation("Fonte {Fonte} trouxe {Count} item(s)", source.Name, itens.Count);
                }
                else
                {
                    _logger.LogInformation("Fonte {Fonte} não retornou itens relevantes", source.Name);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Execução da fonte {Fonte} cancelada", source.Name);
                throw;
            }
            catch (Exception ex)
            {
                // Não propagar para evitar interromper outras fontes
                _logger.LogError(ex, "Falha ao obter dados da fonte {Fonte}", source.Name);
            }
        }

        _logger.LogInformation("Agregação concluída. Total agregado: {Total}", all.Count);
        return all.AsReadOnly();
    }
}