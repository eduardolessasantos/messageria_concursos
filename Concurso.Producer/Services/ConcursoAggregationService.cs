using Concurso.Producer.DTOs;
using Concurso.Producer.Sources;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Concurso.Producer.Services;

/// <summary>
/// Implementação que consulta fontes registradas e agrega os resultados de forma resiliente.
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

    public Task<IReadOnlyList<ConcursoDto>> AggregateAllAsync(CancellationToken cancellationToken = default)
    {
        return AggregateAsync(null, cancellationToken);
    }

    public async Task<IReadOnlyList<ConcursoDto>> AggregateAsync(string? fonte = null, CancellationToken cancellationToken = default)
    {
        var fontesSelecionadas = FiltrarFontes(fonte).ToList();
        _logger.LogInformation("Iniciando agregação com {Total} fonte(s) selecionada(s) (Filtro: '{Filtro}')",
            fontesSelecionadas.Count, fonte ?? "todas");

        var all = new List<ConcursoDto>();

        foreach (var source in fontesSelecionadas)
        {
            try
            {
                _logger.LogInformation("Executando fonte: {Fonte}", source.Name);
                var itens = await source.GetAsync(cancellationToken);
                if (itens is not null && itens.Count > 0)
                {
                    all.AddRange(itens);
                    _logger.LogInformation("Fonte {Fonte} retornou {Count} oportunidade(s)", source.Name, itens.Count);
                }
                else
                {
                    _logger.LogInformation("Fonte {Fonte} não retornou itens", source.Name);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Execução da fonte {Fonte} cancelada", source.Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao obter dados da fonte {Fonte}. Continuando com as demais.", source.Name);
            }
        }

        // Desduplicação em memória e ordenação por RelevanciaScore
        var unicos = all
            .DistinctBy(c => c.DeduplicationKey)
            .OrderByDescending(c => c.RelevanciaScore)
            .ToList();

        _logger.LogInformation("Agregação finalizada. Total bruto: {Bruto} | Únicos: {Unicos}", all.Count, unicos.Count);
        return unicos.AsReadOnly();
    }

    private IEnumerable<IConcursoSource> FiltrarFontes(string? filtro)
    {
        if (string.IsNullOrWhiteSpace(filtro) || filtro.Equals("todas", StringComparison.OrdinalIgnoreCase))
        {
            return _sources;
        }

        var f = filtro.Trim().ToLowerInvariant();
        var correspondentes = _sources.Where(s =>
            s.Name.Contains(f, StringComparison.OrdinalIgnoreCase) ||
            (f == "pci" && s.Name.Contains("pci", StringComparison.OrdinalIgnoreCase)) ||
            (f == "gran" && s.Name.Contains("gran", StringComparison.OrdinalIgnoreCase)) ||
            (f == "mock" && s.Name.Contains("mock", StringComparison.OrdinalIgnoreCase))
        ).ToList();

        return correspondentes.Count > 0 ? correspondentes : _sources;
    }
}