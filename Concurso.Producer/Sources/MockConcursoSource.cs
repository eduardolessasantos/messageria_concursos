using Concurso.Producer.DTOs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Concurso.Producer.Sources;

/// <summary>
/// Fonte de testes sintéticos/mock com dados realistas de concursos de TI.
/// Útil para testes automatizados, CI/CD e demonstrações locais offline.
/// </summary>
public sealed class MockConcursoSource : IConcursoSource
{
    public string Name => "Mock";

    private readonly ILogger<MockConcursoSource> _logger;

    public MockConcursoSource(ILogger<MockConcursoSource> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<ConcursoDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Gerando dados sintéticos da fonte Mock...");

        var agora = DateTimeOffset.UtcNow;
        var mockId = Guid.NewGuid().ToString("N")[..8];

        var lista = new List<ConcursoDto>
        {
            new()
            {
                DeduplicationKey = GerarHash($"https://concursos.gov.br/ti/dataprev-{mockId}"),
                Titulo = $"Dataprev {mockId} abre processo seletivo para Engenharia de Software e Cloud",
                Orgao = "Dataprev",
                Cargo = "Engenheiro de Software & Cloud (DevOps / Kubernetes)",
                Salario = "R$ 15.300,00",
                Link = $"https://concursos.gov.br/ti/dataprev-{mockId}",
                DataPublicacao = agora,
                DataCaptura = agora,
                Fonte = "Mock",
                Descricao = "Oportunidade para profissionais de TI atuarem com arquitetura de microsserviços, RabbitMQ e nuvem privada.",
                RelevanciaScore = 4,
                KeywordsEncontradas = new[] { "engenheiro de software", "cloud", "devops", "kubernetes" }
            },
            new()
            {
                DeduplicationKey = GerarHash($"https://concursos.gov.br/ti/serpro-{mockId}"),
                Titulo = $"Serpro {mockId} divulga edital com vagas para Cientista de Dados e IA",
                Orgao = "Serpro",
                Cargo = "Cientista de Dados (Inteligência Artificial & Machine Learning)",
                Salario = "R$ 16.800,00",
                Link = $"https://concursos.gov.br/ti/serpro-{mockId}",
                DataPublicacao = agora,
                DataCaptura = agora,
                Fonte = "Mock",
                Descricao = "Desenvolvimento de modelos preditivos, governança de dados e esteiras analíticas de inteligência artificial.",
                RelevanciaScore = 3,
                KeywordsEncontradas = new[] { "cientista de dados", "inteligência artificial", "machine learning" }
            }
        };

        _logger.LogInformation("Fonte Mock gerou {Count} oportunidades de TI sintéticas", lista.Count);
        return Task.FromResult<IReadOnlyList<ConcursoDto>>(lista);
    }

    private static string GerarHash(string url)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(url.ToLowerInvariant()));
        return Convert.ToHexString(bytes[..16]).ToLowerInvariant();
    }
}
