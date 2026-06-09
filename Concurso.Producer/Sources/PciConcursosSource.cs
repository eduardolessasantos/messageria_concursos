using Concurso.Producer.DTOs;
using Concurso.Producer.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concurso.Producer.Sources;

/// <summary>
/// Adapta o serviço de coleta existente para o contrato IConcursoSource.
/// Reutiliza IConcursoCollectorService para manter compatibilidade.
/// </summary>
public sealed class PciConcursosSource : IConcursoSource
{
    public string Name => "PCI Concursos";

    private readonly IConcursoCollectorService _collector;
    private readonly ILogger<PciConcursosSource> _logger;

    public PciConcursosSource(IConcursoCollectorService collector, ILogger<PciConcursosSource> logger)
    {
        _collector = collector;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ConcursoDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executando fonte {Fonte}", Name);

        try
        {
            var result = await _collector.ColetarAsync(cancellationToken);
            _logger.LogInformation("Fonte {Fonte} retornou {Total} item(s)", Name, result.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Coleta cancelada na fonte {Fonte}", Name);
            throw;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar fonte {Fonte}", Name);
            // Em caso de erro, devolve lista vazia para não interromper o fluxo das demais fontes
            return System.Array.Empty<ConcursoDto>();
        }
    }
}
