using Concurso.Producer.DTOs;
using Concurso.Producer.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Concurso.Producer.Parsers;

/// <summary>
/// Implementação do parser HTML usando HtmlAgilityPack.
/// Suporta seletores XPath dinâmicos e análise heurística com 80+ palavras-chave de TI.
/// </summary>
public sealed partial class ConcursoHtmlParser : IConcursoHtmlParser
{
    private readonly ILogger<ConcursoHtmlParser> _logger;

    private static readonly string[] CandidateXPaths =
    {
        "//*[@id='pagina']/aside[1]/ul/li",
        "//ul[contains(@class,'ultimas-noticias')]/li",
        "//div[contains(@class,'da')]",
        "//div[contains(@class,'concurso')]",
        "//div[contains(@class,'card-concurso')]",
        "//section[contains(@class,'listagem')]//article",
        "//article"
    };

    // -------------------------------------------------------------------------
    // Filtro de relevância — 80+ palavras-chave de TI (Especificação v6)
    // -------------------------------------------------------------------------
    private static readonly HashSet<string> PalavrasChaveTi = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core TI
        "analista de sistemas", "analista de ti", "analista de tecnologia", "analista de informática",
        "desenvolvedor", "programador", "engenheiro de software", "arquiteto de software", "arquiteto de sistemas",
        "analista de infraestrutura", "analista de redes", "administrador de redes", "analista de suporte",
        "suporte técnico", "técnico de ti", "técnico de informática", "técnico em informática",
        "tecnologia da informação", "tecnologia da informação e comunicação", "tic", "informática",
        "sistemas de informação", "engenharia da computação", "ciência da computação", "sistemas para internet",
        "desenvolvimento web", "desenvolvedor web", "desenvolvedor frontend", "desenvolvedor backend",
        "desenvolvedor fullstack", "desenvolvedor mobile",

        // Dados e IA
        "ciência de dados", "cientista de dados", "engenheiro de dados", "analista de dados", "dba",
        "administrador de banco", "banco de dados", "inteligência artificial", "ia", "machine learning",
        "aprendizado de máquina", "deep learning", "big data", "business intelligence", "bi", "analytics",

        // Cloud e DevOps
        "devops", "devsecops", "sre", "site reliability", "cloud", "computação em nuvem", "aws", "azure", "gcp",
        "kubernetes", "docker", "ci/cd", "automação de infraestrutura", "terraform",

        // Segurança
        "segurança da informação", "segurança cibernética", "cibersegurança", "pentest", "perito digital",
        "análise forense", "soc", "infosec", "segurança de redes", "privacidade de dados", "lgpd",

        // Gestão Ágil e Governança
        "product owner", "po", "scrum master", "agilista", "governança de ti", "auditor de ti",
        "auditor de sistemas", "itil", "cobit", "gestão de ti",

        // Redes e Infraestrutura
        "redes de computadores", "infraestrutura de ti", "telecomunicações", "analista de telecom",
        "administrador de sistemas", "sysadmin", "virtualização", "linux", "servidores"
    };

    [GeneratedRegex(@"R\$\s?[\d.,]+", RegexOptions.IgnoreCase)]
    private static partial Regex RegexSalario();

    [GeneratedRegex(
        @"^(.+?)\s+(abre?|lança|publica|realiza|seleciona|oferece|divulga|encerra|prorroga)\s+",
        RegexOptions.IgnoreCase)]
    private static partial Regex RegexOrgaoNoTitulo();

    public ConcursoHtmlParser(ILogger<ConcursoHtmlParser> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<ConcursoDto> Parse(string html, string fonte)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            _logger.LogWarning("HTML recebido está vazio. Fonte: {Fonte}", fonte);
            return [];
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        HtmlNodeCollection? nodes = null;
        foreach (var xp in CandidateXPaths)
        {
            nodes = doc.DocumentNode.SelectNodes(xp);
            if (nodes is not null && nodes.Count > 0)
            {
                _logger.LogDebug("Seletor XPath ativo: {XPath} retornou {Count} nós", xp, nodes.Count);
                break;
            }
        }

        if (nodes is null || nodes.Count == 0)
        {
            _logger.LogWarning(
                "Nenhum item encontrado no HTML. Verifique o seletor XPath ou o layout da fonte. Fonte: {Fonte}",
                fonte);
            return Array.Empty<ConcursoDto>();
        }

        _logger.LogDebug("Encontrados {Total} nós candidatos. Fonte: {Fonte}", nodes.Count, fonte);

        var resultado = new List<ConcursoDto>();
        var dataCaptura = DateTimeOffset.UtcNow;

        foreach (var no in nodes)
        {
            try
            {
                var dto = ExtrairItem(no, fonte, dataCaptura);

                if (dto is null)
                {
                    continue;
                }

                var textoCompleto = $"{dto.Titulo} {dto.Cargo} {dto.Descricao}";
                var keywordsEncontradas = ObterKeywordsEncontradas(textoCompleto);

                if (keywordsEncontradas.Count == 0)
                {
                    _logger.LogDebug(
                        "Concurso ignorado por não ser área de TI. Cargo: '{Cargo}' | Fonte: {Fonte}",
                        dto.Cargo, fonte);
                    continue;
                }

                var dtoComScore = dto with
                {
                    RelevanciaScore = keywordsEncontradas.Count,
                    KeywordsEncontradas = keywordsEncontradas.ToArray()
                };

                resultado.Add(dtoComScore);
                _logger.LogDebug("Concurso TI extraído: '{Titulo}' | Score: {Score} | Keywords: {Keywords}",
                    dtoComScore.Titulo, dtoComScore.RelevanciaScore, string.Join(", ", dtoComScore.KeywordsEncontradas));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar nó HTML. Fonte: {Fonte}", fonte);
            }
        }

        _logger.LogInformation(
            "Parse concluído. {Encontrados} de {Total} itens são relevantes para TI (Filtro 80+ Keywords). Fonte: {Fonte}",
            resultado.Count, nodes.Count, fonte);

        return resultado.AsReadOnly();
    }

    private ConcursoDto? ExtrairItem(HtmlNode no, string fonte, DateTimeOffset dataCaptura)
    {
        var anchor = no.SelectSingleNode(".//a");
        if (anchor is null) return null;

        var titulo = Limpar(anchor.InnerText);
        var link = anchor.GetAttributeValue("href", string.Empty);
        var descricaoNode = no.SelectSingleNode(".//div[contains(@class,'cd')]")
            ?? no.SelectSingleNode(".//p")
            ?? no.SelectSingleNode(".//span[contains(@class,'descricao')]");

        var descricao = Limpar(descricaoNode?.InnerText);

        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(link))
            return null;

        link = NormalizarLink(link, fonte);

        var orgao = ExtrairOrgaoDoTitulo(titulo) ?? "Não informado";
        var cargo = InferirCargo(titulo, descricao) ?? titulo;
        var salario = ExtrairSalario(descricao) ?? "Não informado";

        return new ConcursoDto
        {
            DeduplicationKey = GerarChaveDeduplicacao(link),
            Titulo = titulo,
            Orgao = orgao,
            Cargo = cargo,
            Salario = salario,
            Link = link,
            DataPublicacao = dataCaptura,
            DataCaptura = dataCaptura,
            Fonte = fonte,
            Descricao = descricao
        };
    }

    public static bool EhRelevanteTi(string texto) =>
        PalavrasChaveTi.Any(p => texto.Contains(p, StringComparison.OrdinalIgnoreCase));

    private static List<string> ObterKeywordsEncontradas(string texto)
    {
        var encontradas = new List<string>();
        foreach (var palavra in PalavrasChaveTi)
        {
            if (texto.Contains(palavra, StringComparison.OrdinalIgnoreCase))
            {
                encontradas.Add(palavra);
            }
        }
        return encontradas;
    }

    private static string? ExtrairOrgaoDoTitulo(string titulo)
    {
        var match = RegexOrgaoNoTitulo().Match(titulo);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? InferirCargo(string titulo, string? descricao)
    {
        var texto = $"{titulo} {descricao}";
        foreach (var palavra in PalavrasChaveTi)
        {
            if (texto.Contains(palavra, StringComparison.OrdinalIgnoreCase))
            {
                return palavra;
            }
        }
        return null;
    }

    private static string? ExtrairSalario(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao)) return null;
        var match = RegexSalario().Match(descricao);
        return match.Success ? match.Value.Trim() : null;
    }

    private static string GerarChaveDeduplicacao(string link)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(link.ToLowerInvariant().Trim()));
        return Convert.ToHexString(bytes[..16]).ToLowerInvariant();
    }

    private static string NormalizarLink(string link, string fonte)
    {
        if (link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return link;
        }

        if (fonte.Contains("Gran", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://www.grancursosonline.com.br{(link.StartsWith('/') ? link : "/" + link)}";
        }

        return $"https://www.pciconcursos.com.br{(link.StartsWith('/') ? link : "/" + link)}";
    }

    private static string? Limpar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        texto = HtmlEntity.DeEntitize(texto);
        texto = Regex.Replace(texto, "<.*?>", string.Empty);
        texto = Regex.Replace(texto, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }
}