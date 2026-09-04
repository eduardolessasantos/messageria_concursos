using Concurso.Producer.DTOs;
using Concurso.Producer.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Concurso.Producer.Services;

/// <summary>
/// Orquestra a coleta de concursos na fonte PCI Concursos com suporte a paginação resiliente.
/// </summary>
public sealed class ConcursoCollectorService : IConcursoCollectorService
{
    public const string HttpClientName = "PciConcursos";
    private const string BaseUrl = "https://www.pciconcursos.com.br/concursos/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConcursoHtmlParser _parser;
    private readonly ILogger<ConcursoCollectorService> _logger;

    public ConcursoCollectorService(
        IHttpClientFactory httpClientFactory,
        IConcursoHtmlParser parser,
        ILogger<ConcursoCollectorService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _parser = parser;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConcursoDto>> ColetarAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando coleta paginada de concursos (páginas 1 a 3). Fonte: {Fonte}", HttpClientName);

        var paginas = new[] { BaseUrl, $"{BaseUrl}2", $"{BaseUrl}3" };
        var dictUnicos = new ConcurrentDictionary<string, ConcursoDto>(StringComparer.OrdinalIgnoreCase);

        var tasks = paginas.Select(async url =>
        {
            try
            {
                var html = await BuscarHtmlAsync(url, cancellationToken);
                if (string.IsNullOrWhiteSpace(html))
                {
                    return;
                }

                var parseados = _parser.Parse(html, HttpClientName);
                foreach (var c in parseados)
                {
                    dictUnicos.TryAdd(c.DeduplicationKey, c);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Falha ao coletar página {Url}. Prosseguindo com demais páginas.", url);
            }
        });

        await Task.WhenAll(tasks);

        var resultado = dictUnicos.Values.OrderByDescending(c => c.RelevanciaScore).ToList();

        _logger.LogInformation(
            "Coleta paginada finalizada. {Total} concurso(s) de TI unificados e classificados. Fonte: {Fonte}",
            resultado.Count, HttpClientName);

        return resultado.AsReadOnly();
    }

    private async Task<string> BuscarHtmlAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        _logger.LogDebug("Requisição GET iniciada. URL: {Url}", url);

        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug(
            "HTML recebido. URL: {Url} | Status: {Status} | Tamanho: {Size} bytes",
            url, (int)response.StatusCode, html.Length);

        return html;
    }
}