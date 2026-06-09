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
///
/// Estruturado para o layout do PCI Concursos (pci concursos.com.br),
/// mas o padrão de seletores XPath pode ser adaptado a qualquer fonte
/// com estrutura semelhante de listagem (ul/li ou table).
///
/// Responsabilidades desta classe:
///   1. Navegar o DOM HTML via XPath
///   2. Extrair os campos brutos (texto, atributos)
///   3. Normalizar e limpar os valores
///   4. Filtrar por palavras-chave de TI
///   5. Gerar a chave de deduplicação
///
/// O que esta classe NÃO faz:
///   - Fazer requisições HTTP
///   - Decidir publicar ou ignorar o concurso
///   - Persistir dados
/// </summary>
public sealed partial class ConcursoHtmlParser : IConcursoHtmlParser
{
    private readonly ILogger<ConcursoHtmlParser> _logger;

    // Preferível centralizar/inyectar seletores via configuration no futuro
    private static readonly string[] CandidateXPaths =
    {
        "//*[@id='pagina']/aside[1]/ul/li",
        "//ul[contains(@class,'ultimas-noticias')]/li",
        "//div[contains(@class,'da')]",
        "//article"
    };

    // -------------------------------------------------------------------------
    // Filtro de relevância — palavras-chave que caracterizam cargos de TI
    // -------------------------------------------------------------------------
    private static readonly HashSet<string> PalavrasChaveTi = new(StringComparer.OrdinalIgnoreCase)
    {
        "analista de sistemas",
        "analista de ti",
        "desenvolvedor",
        "programador",
        "engenheiro de software",
        "arquiteto de software",
        "analista de infraestrutura",
        "analista de redes",
        "administrador de redes",
        "segurança da informação",
        "banco de dados",
        "dba",
        "suporte técnico",
        "técnico de ti",
        "técnico de informática",
        "ciência de dados",
        "inteligência artificial",
        "machine learning",
        "devops",
        "cloud",
        "tecnologia da informação",
        "tecnologia da informação e comunicação",
        "tic",
        "informática",
        "sistemas de informação",
        "engenharia da computação",
        "ciência da computação",
    };
    // -------------------------------------------------------------------------
    // Regex para extrair salário do atributo "title"
    // Exemplos: "R$ 9.004,00", "R$9004,00", "soldo de R$ 12.455,50"
    // -------------------------------------------------------------------------
    [GeneratedRegex(@"R\$\s?[\d.,]+", RegexOptions.IgnoreCase)]
    private static partial Regex RegexSalario();

    // -------------------------------------------------------------------------
    // Verbos separadores de órgão no título
    // Exemplo: "Instituto Militar de Engenharia abre concurso..."
    //           ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ = órgão
    // -------------------------------------------------------------------------
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
                break;
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
                    continue;

                if (!EhRelevanteTi($"{dto.Titulo} {dto.Descricao}"))
                {
                    _logger.LogDebug(
                        "Concurso ignorado por não ser área de TI. Cargo: '{Cargo}' | Fonte: {Fonte}",
                        dto.Cargo, fonte);
                    continue;
                }

                resultado.Add(dto);
                _logger.LogDebug("Concurso extraído: '{Titulo}' | Cargo: '{Cargo}'", dto.Titulo, dto.Cargo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar nó HTML. Fonte: {Fonte}", fonte);
                // Continua para o próximo item — falha isolada não interrompe o lote
            }
        }

        _logger.LogInformation(
            "Parse concluído. {Encontrados} de {Total} itens são relevantes para TI. Fonte: {Fonte}",
            resultado.Count, nodes.Count, fonte);

        return resultado.AsReadOnly();
    }

    // -------------------------------------------------------------------------
    // Extração de um item individual
    // -------------------------------------------------------------------------

    private ConcursoDto? ExtrairItem(HtmlNode li, string fonte, DateTimeOffset dataCaptura)
    {
        // Único <a> dentro do <li>
        var anchor = li.SelectSingleNode(".//a");
        if (anchor is null) return null;

        var titulo = Limpar(anchor.InnerText);
        var link = anchor.GetAttributeValue("href", string.Empty);
        var descricaoNode = li.SelectSingleNode(".//div[contains(@class,'cd')]");

        var descricao = Limpar(descricaoNode?.InnerText);

        var dataNode = li.SelectSingleNode(".//div[contains(@class,'ce')]/span");

        var dataTexto = Limpar(dataNode?.InnerText);

        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(link))
            return null;

        link = NormalizarLink(link);

        // Órgão: extraído do início do título via regex de verbos separadores
        var orgao = ExtrairOrgaoDoTitulo(titulo) ?? "Não informado";

        // Cargo: inferido das palavras-chave de TI no texto completo
        var cargo = InferirCargo(titulo, descricao) ?? titulo;

        // Salário: extraído via regex do atributo title (descrição longa)
        var salario = ExtrairSalario(descricao) ?? "Não informado";

        // PCI Concursos não expõe data por item na listagem lateral
        // DataPublicacao = DataCaptura até termos uma fonte com data explícita
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

    // -------------------------------------------------------------------------
    // Filtro de TI
    // -------------------------------------------------------------------------

    private static bool EhRelevanteTi(string texto) =>
        PalavrasChaveTi.Any(p => texto.Contains(p, StringComparison.OrdinalIgnoreCase));

    // -------------------------------------------------------------------------
    // Helpers de extração
    // -------------------------------------------------------------------------

    private static string? ExtrairOrgaoDoTitulo(string titulo)
    {
        var match = RegexOrgaoNoTitulo().Match(titulo);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? InferirCargo(string titulo, string? descricao)
    {
        var texto = $"{titulo} {descricao}";
        foreach (var palavra in PalavrasChaveTi)
            if (texto.Contains(palavra, StringComparison.OrdinalIgnoreCase))
                return palavra;
        return null;
    }

    private static string? ExtrairSalario(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao)) return null;
        var match = RegexSalario().Match(descricao);
        return match.Success ? match.Value.Trim() : null;
    }

    /// <summary>SHA256 dos primeiros 16 bytes do link normalizado — estável e sem colisão para URLs.</summary>
    private static string GerarChaveDeduplicacao(string link)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(link.ToLowerInvariant().Trim()));
        return Convert.ToHexString(bytes[..16]).ToLowerInvariant();
    }

    private static string NormalizarLink(string link) =>
        link.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? link
            : $"https://www.pciconcursos.com.br{link}";

    private static string? Limpar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        texto = HtmlEntity.DeEntitize(texto);
        texto = Regex.Replace(texto, "<.*?>", string.Empty);
        texto = Regex.Replace(texto, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }
}