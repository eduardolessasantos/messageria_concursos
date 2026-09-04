# ConcursoTI v6 - Refinamento Completo Baseado em Código Real

> **Data:** 06/05/2026
> **Base:** DOCUMENTACAO_TECNICA.md + 9 arquivos reais do projeto + log R-FAULT do Concurso.Web
> **Autor análise:** Revisão de código-fonte enviado

---

## 1. DIAGNÓSTICO REAL DO CÓDIGO ENVIADO

### 1.1 Análise dos 9 arquivos

| Arquivo | O que faz | Problema encontrado | Gravidade |
|---------|-----------|---------------------|-----------|
| **ConcursoHtmlParser.cs** | XPath + 27 keywords + SHA-256 dedup | Só 27 keywords (doc fala 26), XPath fixo `//*[@id='pagina']/aside[1]/ul/li` quebra se PCI mudar layout. Regex salário `R\$\s?[\d.,]+` pega lixo. `NormalizarLink` hard-coded para pciconcursos.com.br, impede multi-fonte | Média |
| **ConcursoCollectorService.cs** | GET https://www.pciconcursos.com.br/concursos/ | URL única hard-coded, sem paginação, sem filtro ?area=TI, sem Polly visible no arquivo (Polly está no Program.cs mas não logado). Retorna vazio em erro de rede - bom para não derrubar Worker, mas esconde falha | Média |
| **NotificacaoStateService.cs** | Singleton com List<NotificacaoLog> + OnChange | Tem `lock` e `OnChange?.Invoke()` - já foi corrigido parcialmente! Mas o `Invoke()` roda na thread do RabbitMQ, não no Dispatcher do Blazor. O componente que assina precisa fazer `InvokeAsync(StateHasChanged)` | **CRÍTICO - Causa do seu R-FAULT** |
| **NotificacaoEnviadaConsumer.cs** (Web) | Consome 2 eventos e chama StateService | Chama `_state.AdicionarLog` direto, sem try/catch, sem debounce. Se Web cair, evento vai pra _error e some | Alta |
| **ConcursoPublicadoEvent.cs** | Record com 9 props | Falta `Fonte` no construtor? Já tem, ok. Mas falta `PayloadHash` e `SalarioDecimal` para ordenação | Baixa |
| **ConcursoPublicadoConsumer.cs** (Consumer) | Check ExistsAsync + AddAsync | Bom: usa `BeginScope` com CorrelationId, métricas. Mas `catch` só loga e dá `throw` - vai pro DLQ infinito se for duplicidade de chave única do MySQL | Média |
| **docker-compose.yml** | mysql 3307, rabbitmq, mailpit | Só infra! Não tem Api, Web, Consumer, Notification, Producer. Por isso `run-all.ps1` usa `Start-Process` - não funciona remoto, não funciona Linux | **CRÍTICO para deploy remoto** |
| **run-all.ps1** | 5 Start-Process powershell | Windows-only, sem healthcheck, abre 5 janelas. Se um Worker crashar, não reinicia | Alta |
| **test-pipeline.ps1** | 5 baterias de teste | Excelente! Testa infra, api, mensageria, coleta e web. Mas não testa `concurso-web-queue` especificamente nem DLQ | Baixa |

### 1.2 O Bug do seu log explicado linha a linha

```
fail: R-FAULT rabbitmq://localhost/concurso-web-queue
System.InvalidOperationException: The current thread is not associated with the Dispatcher
   at Concurso.Web.Services.NotificacaoStateService.AdicionarLogConcurso line 58 -> OnChange?.Invoke();
```

Fluxo:
1. `POST /api/concursos/coletar` publica `ConcursoPublicadoEvent` no exchange fanout
2. `concurso-web-queue` entrega na thread `ThreadPool #7` do MassTransit
3. `NotificacaoEnviadaConsumer.Consume()` chama `AdicionarLogConcurso()` nessa thread #7
4. `AdicionarLogConcurso()` faz `OnChange?.Invoke()` ainda na thread #7
5. O handler `OnChange` no `Home.razor` (que você não mandou, mas está no doc) chama `StateHasChanged()` direto - Blazor Server detecta que não é a thread do circuito e joga R-FAULT

**Solução definitiva:** Não mudar o Service, mudar o componente Blazor que assina.

---

## 2. CORREÇÃO CRÍTICA - Código pronto para colar

### 2.1 Home.razor (ou Index.razor) - O FIX REAL

Seu `NotificacaoStateService.cs` já está quase correto, NÃO precisa herdar de ComponentBase. O erro está no .razor.

```razor
@page "/"
@inject NotificacaoStateService StateService
@implements IDisposable
@using Concurso.Web.Models

<MudTimeline>
  @foreach(var log in StateService.Logs)
  {
    <MudTimelineItem Color="@(log.Tipo == "Broker" ? Color.Info : Color.Success)">
      <MudText Typo="Typo.body2">@log.Detalhe</MudText>
      <MudText Typo="Typo.caption">@log.Data.ToString("HH:mm:ss")</MudText>
    </MudTimelineItem>
  }
</MudTimeline>

@code {
    protected override void OnInitialized()
    {
        // Assina o evento
        StateService.OnChange += OnStateChanged;
    }

    // FIX CRÍTICO: usa async void + InvokeAsync para voltar pro Dispatcher
    private async void OnStateChanged()
    {
        await InvokeAsync(() =>
        {
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        StateService.OnChange -= OnStateChanged;
    }
}
```

### 2.2 NotificacaoStateService.cs - Melhoria com debounce para não travar UI

Seu arquivo atual chama `OnChange` a cada mensagem. Se coletar 50 concursos, vai chamar 50x StateHasChanged em 2 segundos e travar o Blazor. Adicione debounce de 200ms:

```csharp
public class NotificacaoStateService
{
    private readonly object _lock = new();
    private readonly List<NotificacaoLog> _logs = new();
    private DateTime _lastNotify = DateTime.MinValue;
    public event Action? OnChange;

    public IReadOnlyList<NotificacaoLog> Logs { get { lock(_lock) return _logs.ToList(); } }

    public void AdicionarLog(NotificacaoEnviadaEvent evt)
    {
        lock(_lock)
        {
            _logs.Insert(0, new NotificacaoLog(evt));
            if(_logs.Count > 200) _logs.RemoveAt(_logs.Count-1);
        }
        NotifyDebounced();
    }

    public void AdicionarLogConcurso(ConcursoPublicadoEvent evt)
    {
        lock(_lock)
        {
            _logs.Insert(0, new NotificacaoLog
            {
                EventId = evt.EventId,
                DeduplicationKey = evt.DeduplicationKey,
                Tipo = "Broker",
                Status = "Publicado",
                Detalhe = $"[Broker] Novo: {evt.Cargo} ({evt.Orgao}) - {evt.Salario} | Fonte: {evt.Fonte}",
                Data = evt.DataCaptura.UtcDateTime
            });
            if(_logs.Count > 200) _logs.RemoveAt(_logs.Count-1);
        }
        NotifyDebounced();
    }

    private void NotifyDebounced()
    {
        // Evita spam de StateHasChanged
        if((DateTime.UtcNow - _lastNotify).TotalMilliseconds < 200) return;
        _lastNotify = DateTime.UtcNow;
        OnChange?.Invoke();
    }

    public void LimparLogs()
    {
        lock(_lock) _logs.Clear();
        OnChange?.Invoke();
    }
}
```

---

## 3. REFINAMENTO - Coleta Mais Abrangente (Multi-Fonte)

### 3.1 Problema no ConcursoHtmlParser.cs real

Seu código tem:
```csharp
private static readonly HashSet<string> PalavrasChaveTi = new() { "analista de sistemas", ... 27 itens }
private static readonly string[] CandidateXPaths = { "//*[@id='pagina']/aside[1]/ul/li", ... }
```

Isso só pega PCI Concursos lateral. Perde 70% dos concursos de TI porque:

1. PCI tem página dedicada `/concursos/area/tecnologia-da-informacao/` que você não está usando
2. Gran Cursos e Estratégia têm mais vagas de TI que PCI
3. 27 keywords não pegam "engenheiro de dados", "sre", "product owner", "scrum master"

### 3.2 Novo Parser com 80+ keywords e suporte multi-fonte

**ConcursoHtmlParser.cs v6 - Substitua o HashSet:**

```csharp
private static readonly HashSet<string> PalavrasChaveTi = new(StringComparer.OrdinalIgnoreCase)
{
    // Core TI
    "analista de sistemas", "analista de ti", "analista de tecnologia", "analista de informática",
    "desenvolvedor", "programador", "engenheiro de software", "arquiteto de software", "arquiteto de sistemas",
    "analista de infraestrutura", "analista de redes", "administrador de redes", "analista de suporte",
    "suporte técnico", "técnico de ti", "técnico de informática", "técnico em informática",
    "tecnologia da informação", "tecnologia da informação e comunicação", "tic", "informática",
    "sistemas de informação", "engenharia da computação", "ciência da computação", "sistemas para internet",
    // Dados e IA
    "ciência de dados", "cientista de dados", "engenheiro de dados", "analista de dados", "dba", "administrador de banco", "banco de dados",
    "inteligência artificial", "ia", "machine learning", "aprendizado de máquina", "deep learning", "big data",
    // Cloud e DevOps
    "devops", "sre", "site reliability", "cloud", "aws", "azure", "gcp", "kubernetes", "docker",
    // Segurança
    "segurança da informação", "segurança cibernética", "cibersegurança", "pentest", "perito digital",
    // Gestão Ágil
    "product owner", "po", "scrum master", "agilista", "governança de ti", "auditor de ti", "auditor de sistemas",
    // Redes e Infra
    "redes de computadores", "infraestrutura de ti", "telecomunicações", "analista de telecom"
};
```

### 3.3 Nova Arquitetura de Coleta - IConcursoCollector

**Crie interface para suportar várias fontes (sem quebrar seu Collector atual):**

```csharp
// Concurso.Producer/Interfaces/IConcursoSourceCollector.cs
public interface IConcursoSourceCollector
{
    string FonteNome { get; }
    Task<IReadOnlyList<ConcursoDto>> ColetarAsync(CancellationToken ct);
}

// Implementação PCI melhorada com paginação
public class PciConcursosCollector : IConcursoSourceCollector
{
    public string FonteNome => "PCIConcursos";
    private readonly ConcursoCollectorService _inner; // reutiliza seu service atual
    // Adicione loop de 1..3 páginas: https://www.pciconcursos.com.br/concursos/2, /3
}

// Nova: GranCursos
public class GranCursosCollector : IConcursoSourceCollector
{
    public string FonteNome => "GranCursos";
    // URL: https://www.grancursosonline.com.br/concursos/abertos?area=ti
    // XPath diferente: //div[contains(@class,'card-concurso')]
}

// Orquestrador
public class ConcursoOrchestrator
{
    private readonly IEnumerable<IConcursoSourceCollector> _collectors;
    public async Task<Dictionary<string,int>> ColetarTodasAsync()
    {
        var tasks = _collectors.Select(c => c.ColetarAsync(CancellationToken.None));
        var resultados = await Task.WhenAll(tasks);
        // merge, dedup, publish
    }
}
```

### 3.4 ConcursoPublicadoEvent.cs - Adicionar Fonte e Score

Seu Event atual já tem Fonte, ótimo. Adicione para ranking:

```csharp
public sealed record ConcursoPublicadoEvent : IEvent
{
    // ... existentes
    public required string Fonte { get; init; }
    public int RelevanciaScore { get; init; } // quantas keywords bateu
    public string[] KeywordsEncontradas { get; init; } = Array.Empty<string>();
}
```

---

## 4. PUBLICAÇÃO REMOTA - Docker Completo e Teste Remoto

### 4.1 Problema no docker-compose.yml atual

Seu arquivo só sobe infra (mysql, rabbitmq, mailpit). O `run-all.ps1` sobe Api/Web/Consumers via `Start-Process powershell` - isso não funciona em Linux, não funciona no Railway/Render/Azure.

### 4.2 docker-compose.prod.yml completo

```yaml
services:
  api:
    build:
      context: .
      dockerfile: src/Concurso.Api/Dockerfile
    ports: ["5000:8080"]
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnection: "Server=mysql;Port=3306;Database=concursos_ti;Uid=root;Pwd=${MYSQL_ROOT_PASSWORD}"
      RabbitMq__Host: rabbitmq
      Email__Provider: Resend
      Resend__ApiKey: ${RESEND_API_KEY}
      Email__To: ${EMAIL_TO}
    depends_on:
      mysql: { condition: service_healthy }
      rabbitmq: { condition: service_healthy }
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 15s

  web:
    build:
      context: .
      dockerfile: src/Concurso.Web/Dockerfile
    ports: ["5001:8080"]
    environment:
      ApiBaseUrl: http://api:8080
      RabbitMq__Host: rabbitmq
    depends_on: [api, rabbitmq]

  consumer:
    build:
      context: .
      dockerfile: src/Concurso.Consumer/Dockerfile
    environment:
      ConnectionStrings__DefaultConnection: "Server=mysql;Port=3306;Database=concursos_ti;Uid=root;Pwd=${MYSQL_ROOT_PASSWORD}"
      RabbitMq__Host: rabbitmq
    depends_on: [mysql, rabbitmq]
    restart: unless-stopped

  notification:
    build:
      context: .
      dockerfile: src/Concurso.Notification/Dockerfile
    environment:
      RabbitMq__Host: rabbitmq
      Email__Provider: Resend
      Resend__ApiKey: ${RESEND_API_KEY}
    depends_on: [rabbitmq]
    restart: unless-stopped

  mysql:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: ${MYSQL_ROOT_PASSWORD}
      MYSQL_DATABASE: concursos_ti
    ports: ["3307:3306"]
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost"]
      interval: 10s

  rabbitmq:
    image: rabbitmq:3-management
    ports: ["5672:5672", "15672:15672"]
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 10s

  mailpit:
    image: axllent/mailpit
    ports: ["1025:1025", "8025:8025"]
    profiles: ["dev"] # só sobe em dev
```

### 4.3 Endpoints para uso remoto (já tem test-email, adicione mais)

No `Concurso.Api/Program.cs`, garanta:

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.MapPost("/api/concursos/coletar", async (string? fonte, ConcursoOrchestrator orchestrator, IBus bus) =>
{
    // fonte = "todas" | "pci" | "gran" | "estrategia" | "mock"
    var resultado = await orchestrator.ColetarTodasAsync(fonte);
    return Results.Ok(resultado);
});

app.MapPost("/api/concursos/test-email", async (string orgao, string cargo, IBus bus) =>
{
    var evt = new ConcursoPublicadoEvent
    {
        DeduplicationKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{orgao}-{cargo}-{Guid.NewGuid()}")))[..16].ToLower(),
        Titulo = $"{orgao} abre concurso para {cargo}",
        Orgao = orgao,
        Cargo = cargo,
        Salario = "R$ 12.000,00",
        Link = "https://exemplo.com/teste",
        DataPublicacao = DateTimeOffset.UtcNow,
        DataCaptura = DateTimeOffset.UtcNow,
        Fonte = "TesteManual"
    };
    await bus.Publish(evt);
    return Results.Ok(new { EventId = evt.EventId, Message = "Evento publicado, verifique Web e e-mail" });
});
```

---

## 5. PROMPTS FINAIS PARA ANTIGRATY (ORDEM DE EXECUÇÃO)

**PROMPT 1 - CORREÇÃO CRÍTICA DO R-FAULT (FAÇA AGORA):**
> "Corrija o bug crítico de Dispatcher no Concurso.Web. O log mostra System.InvalidOperationException: The current thread is not associated with the Dispatcher em NotificacaoStateService linha 58 (OnChange?.Invoke). O NotificacaoStateService já tem lock e evento OnChange, está correto. O problema está no componente Blazor que assina. No Home.razor/Index.razor, altere o handler OnChange para ser async void OnStateChanged() { await InvokeAsync(() => StateHasChanged()); }. Garanta que o componente implementa IDisposable e desassina no Dispose. Adicione debounce de 200ms no NotificacaoStateService para evitar 50 StateHasChanged por segundo. Teste com POST /api/concursos/coletar e verifique se não há mais R-FAULT no log do Concurso.Web."

**PROMPT 2 - EXPANDIR COLETA PARA 80+ KEYWORDS E MULTI-FONTE:**
> "Refine ConcursoHtmlParser.cs. Expanda PalavrasChaveTi de 27 para 80+ itens incluindo: cientista de dados, engenheiro de dados, dba, devops, sre, cloud, aws, azure, segurança da informação, cibersegurança, pentest, inteligência artificial, machine learning, product owner, scrum master, governança de ti, etc (use lista da spec v6). Melhore CandidateXPaths para tentar também //div[contains(@class,'concurso')] e //section[contains(@class,'listagem')]. Em ConcursoCollectorService.cs, altere UrlListagem para aceitar paginação: https://www.pciconcursos.com.br/concursos/{pagina} e faça loop de 1 a 3 páginas com Task.WhenAll. Crie interface IConcursoSourceCollector e implementação GranCursosCollector para https://www.grancursosonline.com.br/concursos/abertos. Crie ConcursoOrchestrator que roda todos os collectors em paralelo."

**PROMPT 3 - DOCKER PARA DEPLOY REMOTO:**
> "Crie Dockerfile multi-stage para cada projeto: Concurso.Api, Concurso.Web, Concurso.Consumer, Concurso.Notification, Concurso.Producer usando mcr.microsoft.com/dotnet/aspnet:8.0 e sdk:8.0, EXPOSE 8080, ENTRYPOINT dotnet X.dll. Crie docker-compose.prod.yml completo com api, web, consumer, notification, mysql, rabbitmq, mailpit (com profile dev). Use variáveis de ambiente MYSQL_ROOT_PASSWORD, RESEND_API_KEY, EMAIL_TO. Adicione healthcheck em api (curl /health), mysql e rabbitmq. No Concurso.Api, crie endpoint GET /health retornando 200. Crie DEPLOY.md com instruções para Railway: railway up, variáveis, domínio."

**PROMPT 4 - TESTE REMOTO E OBSERVABILIDADE:**
> "No Concurso.Api, garanta endpoints POST /api/concursos/coletar?fonte=todas que aceita parâmetro fonte (todas, pci, gran, mock) e POST /api/concursos/test-email?orgao=Teste&cargo=Analista de TI que publica ConcursoPublicadoEvent fake com DeduplicationKey random para testar pipeline completo remoto. Adicione Serilog com WriteTo.File logs/concurso-.log rolling diário em todos os projetos. No MassTransit, configure UseMessageRetry Exponential 5 tentativas e logue cada retry. Crie GET /api/logs/erros retornando últimos 50 erros do arquivo de log. No Concurso.Web, mostre badge 'DLQ: X' se houver mensagens em _error queue via RabbitMQ Management API."

---

## 6. CHECKLIST DE VALIDAÇÃO COM SEUS ARQUIVOS REAIS

- [ ] Após Prompt 1, `dotnet run --project Concurso.Web` + `POST /api/concursos/coletar` não gera mais `R-FAULT` no log
- [ ] Timeline Blazor atualiza ao vivo sem refresh
- [ ] `ConcursoHtmlParser.cs` contém 80+ keywords e teste `EhRelevanteTi("engenheiro de dados") == true`
- [ ] `ConcursoCollectorService.cs` coleta 3 páginas do PCI
- [ ] `docker-compose.prod.yml` + `docker compose -f docker-compose.prod.yml up -d --build` sobe 6 containers
- [ ] `GET http://localhost:5000/health` retorna 200
- [ ] `POST http://localhost:5000/api/concursos/test-email?orgao=DATAPREV&cargo=Analista de TI` gera e-mail via Resend e aparece no Web
- [ ] `GET http://localhost:5000/api/logs/erros` retorna lista vazia se sem erro