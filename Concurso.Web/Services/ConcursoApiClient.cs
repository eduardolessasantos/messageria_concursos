using System.Net.Http.Json;
using Concurso.Web.Models;

namespace Concurso.Web.Services;

public class ConcursoApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ConcursoApiClient> _logger;

    public ConcursoApiClient(HttpClient http, ILogger<ConcursoApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<ConcursoDto>> GetConcursosAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<List<ConcursoDto>>("/api/concursos", ct);
            return res ?? new List<ConcursoDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao obter lista de concursos da API");
            return new List<ConcursoDto>();
        }
    }

    public async Task<bool> DispararTesteAsync(string orgao, string cargo, string salario, CancellationToken ct = default)
    {
        try
        {
            var query = $"/api/concursos/test-email?orgao={Uri.EscapeDataString(orgao)}&cargo={Uri.EscapeDataString(cargo)}&salario={Uri.EscapeDataString(salario)}";
            var response = await _http.PostAsync(query, null, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao disparar teste via API");
            return false;
        }
    }

    public async Task<string> ColetarAgoraAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsync("/api/concursos/coletar", null, ct);
            if (response.IsSuccessStatusCode)
            {
                return "Coleta executada com sucesso!";
            }
            return $"Falha ao executar coleta: HTTP {response.StatusCode}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao acionar coleta imediata");
            return $"Erro: {ex.Message}";
        }
    }
}
