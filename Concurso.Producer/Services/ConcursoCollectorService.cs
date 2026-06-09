using Concurso.Producer.DTOs;
using Concurso.Producer.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Concurso.Producer.Services;

/// <summary>
/// Orquestra a coleta de concursos: faz a requisição HTTP, delega o parsing
/// ao <see cref="IConcursoHtmlParser"/> e retorna os DTOs prontos para uso.
///
/// Responsabilidades:
///   1. Buscar o HTML da fonte via HttpClient (injetado e nomeado)
///   2. Repassar o HTML ao parser
///   3. Logar métricas e erros da coleta
///   4. Preparar o campo DeduplicationKey para checagem futura
///
/// O que este serviço NÃO faz:
///   - Persistir dados
///   - Publicar eventos
///   - Decidir se o concurso é novo ou duplicado (responsabilidade futura)
/// </summary>
public sealed class ConcursoCollectorService : IConcursoCollectorService
{
    // Nome registrado no DI para o HttpClient tipado desta fonte
    public const string HttpClientName = "PciConcursos";

    // URL da listagem de concursos de TI — pode ser movida para appsettings
    private const string UrlListagem = "https://www.pciconcursos.com.br/concursos/";

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
        _logger.LogInformation("Iniciando coleta de concursos. Fonte: {Fonte} | URL: {Url}",
            HttpClientName, UrlListagem);

        string html;

        try
        {
            html = await BuscarHtmlAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Falha de rede não deve derrubar o Worker — loga e retorna lista vazia
            // O Worker tentará novamente no próximo ciclo
            _logger.LogError(ex,
                "Falha ao buscar HTML da fonte. Fonte: {Fonte} | URL: {Url}",
                HttpClientName, UrlListagem);
            return Array.Empty<ConcursoDto>();
        }

        var concursos = _parser.Parse(html, HttpClientName);

        _logger.LogInformation(
            "Coleta finalizada. {Total} concurso(s) de TI encontrado(s). Fonte: {Fonte}",
            concursos.Count, HttpClientName);

        // Log individual apenas em Debug para não poluir o log em produção
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var c in concursos)
            {
                _logger.LogDebug(
                    "Concurso coletado | Key: {Key} | Titulo: {Titulo} | Cargo: {Cargo} | Orgao: {Orgao}",
                    c.DeduplicationKey, c.Titulo, c.Cargo, c.Orgao);
            }
        }

        return concursos;
    }

    // -------------------------------------------------------------------------
    // Requisição HTTP
    // -------------------------------------------------------------------------

    private async Task<string> BuscarHtmlAsync(CancellationToken cancellationToken)
    {
        // Usa IHttpClientFactory para respeitar o ciclo de vida de conexões TCP
        // e aplicar a política de retry configurada no Program.cs
        var client = _httpClientFactory.CreateClient(HttpClientName);

        _logger.LogDebug("Requisição GET iniciada. URL: {Url}", UrlListagem);

        var response = await client.GetAsync(UrlListagem, cancellationToken);

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogDebug(
            "HTML recebido. Status: {Status} | Tamanho: {Size} bytes",
            (int)response.StatusCode, html.Length);

        return html;
    }
}