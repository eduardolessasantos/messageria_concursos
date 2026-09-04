using Concurso.Producer.DTOs;
using Concurso.Producer.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Concurso.Producer.Sources;

/// <summary>
/// Fonte de coleta do Gran Cursos Online focada em editais de TI.
/// </summary>
public sealed class GranCursosSource : IConcursoSource
{
    public const string HttpClientName = "GranCursos";
    public string Name => "Gran Cursos Online";

    private const string UrlListagem = "https://www.grancursosonline.com.br/concursos/abertos?area=ti";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConcursoHtmlParser _parser;
    private readonly ILogger<GranCursosSource> _logger;

    public GranCursosSource(
        IHttpClientFactory httpClientFactory,
        IConcursoHtmlParser parser,
        ILogger<GranCursosSource> logger)
    {
        _httpClientFactory = httpClientFactory;
        _parser = parser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ConcursoDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executando fonte {Fonte} | URL: {Url}", Name, UrlListagem);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetAsync(UrlListagem, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Fonte {Fonte} retornou status HTTP {Status}", Name, response.StatusCode);
                return Array.Empty<ConcursoDto>();
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var concursos = _parser.Parse(html, Name);

            _logger.LogInformation("Fonte {Fonte} retornou {Total} oportunidade(s) de TI", Name, concursos.Count);
            return concursos;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha não impeditiva ao executar fonte {Fonte}", Name);
            return Array.Empty<ConcursoDto>();
        }
    }
}
