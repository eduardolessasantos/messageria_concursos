# ==============================================================================
# test-pipeline.ps1
# Suíte de Testes Automatizados e Validação de Saúde (E2E / Integração)
# ConcursosTI - Pipeline de Mensageria, Coleta e Notificação
# ==============================================================================

[CmdletBinding()]
param (
    [string]$ApiBaseUrl = "http://localhost:5000",
    [string]$WebBaseUrl = "http://localhost:5001",
    [string]$RabbitMqHttpUrl = "http://localhost:15672",
    [string]$MailpitHttpUrl = "http://localhost:8025",
    [int]$TimeoutSeconds = 10,
    [switch]$SkipCollectorScrape = $false
)

$ErrorActionPreference = "Continue"
$testResults = [System.Collections.Generic.List[PSCustomObject]]::new()
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

function Log-Section([string]$title) {
    Write-Host "`n======================================================================" -ForegroundColor Cyan
    Write-Host "  $title" -ForegroundColor Cyan
    Write-Host "======================================================================" -ForegroundColor Cyan
}

function Assert-Test {
    param (
        [string]$TestName,
        [scriptblock]$TestBlock
    )

    Write-Host "[TESTANDO] $TestName..." -NoNewline -ForegroundColor Gray
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $result = & $TestBlock
        $sw.Stop()
        Write-Host " [PASSOU] ($($sw.ElapsedMilliseconds)ms)" -ForegroundColor Green
        $testResults.Add([PSCustomObject]@{
            Nome = $TestName
            Status = "PASSOU"
            DuracaoMs = $sw.ElapsedMilliseconds
            Erro = $null
        })
        return $result
    }
    catch {
        $sw.Stop()
        Write-Host " [FALHOU] ($($sw.ElapsedMilliseconds)ms)" -ForegroundColor Red
        Write-Host "   Detalhe: $_" -ForegroundColor DarkRed
        $testResults.Add([PSCustomObject]@{
            Nome = $TestName
            Status = "FALHOU"
            DuracaoMs = $sw.ElapsedMilliseconds
            Erro = $_.Exception.Message
        })
        return $null
    }
}

Log-Section "1. VERIFICAÇÃO DE INFRAESTRUTURA DOCKER & PORTAS"

# 1.1 RabbitMQ HTTP Management
Assert-Test -TestName "RabbitMQ Management API acessível (Porta 15672)" -TestBlock {
    $pair = "guest:guest"
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($pair)
    $base64 = [Convert]::ToBase64String($bytes)
    $headers = @{ Authorization = "Basic $base64" }

    $res = Invoke-RestMethod -Uri "$RabbitMqHttpUrl/api/overview" -Headers $headers -TimeoutSec $TimeoutSeconds -Method Get
    if (-not $res.rabbitmq_version) { throw "Não foi possível obter a versão do RabbitMQ." }
    Write-Host " (RabbitMQ v$($res.rabbitmq_version))" -NoNewline -ForegroundColor DarkGray
}

# 1.2 Mailpit API
Assert-Test -TestName "Mailpit API de Webmail ativa (Porta 8025)" -TestBlock {
    $res = Invoke-RestMethod -Uri "$MailpitHttpUrl/api/v1/info" -TimeoutSec $TimeoutSeconds -Method Get
    if (-not $res.version) { throw "Mailpit não respondeu com a versão da API." }
    Write-Host " (Mailpit v$($res.version))" -NoNewline -ForegroundColor DarkGray
}

Log-Section "2. TESTES DE SERVIÇO CONCURSO.API (MINIMAL API)"

# 2.1 Health Check da API
Assert-Test -TestName "GET /health - Verificação de integridade da API" -TestBlock {
    $res = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -TimeoutSec $TimeoutSeconds -Method Get
    if ($res.status -ne "Healthy") {
        throw "Status de saúde não retornou 'Healthy': $($res.status)"
    }
}

# 2.2 Swagger OpenAPI Spec
Assert-Test -TestName "Concurso.Api - Swagger OpenAPI JSON disponível" -TestBlock {
    $res = Invoke-RestMethod -Uri "$ApiBaseUrl/swagger/v1/swagger.json" -TimeoutSec $TimeoutSeconds -Method Get
    if ($res.info.title -notlike "*Concursos TI*") {
        throw "Título Swagger incorreto ou API inacessível."
    }
}

# 2.3 Listagem de Concursos
Assert-Test -TestName "GET /api/concursos - Retorna lista de concursos cadastrados" -TestBlock {
    $res = Invoke-RestMethod -Uri "$ApiBaseUrl/api/concursos" -TimeoutSec $TimeoutSeconds -Method Get
    if ($null -eq $res -or -not ($res -is [System.Array])) {
        throw "A resposta não foi uma coleção JSON válida."
    }
    Write-Host " ($($res.Count) registro(s) no banco)" -NoNewline -ForegroundColor DarkGray
}

# 2.4 Consulta de Logs de Erros
Assert-Test -TestName "GET /api/logs/erros - Consulta estruturada de erros recentes" -TestBlock {
    $res = Invoke-RestMethod -Uri "$ApiBaseUrl/api/logs/erros" -TimeoutSec $TimeoutSeconds -Method Get
    if ($null -eq $res.total) {
        throw "Resposta de logs de erros inválida."
    }
    Write-Host " ($($res.total) erro(s) recente(s))" -NoNewline -ForegroundColor DarkGray
}

Log-Section "3. TESTE DE DISPARO DE EVENTO & MENSAGERIA"

# 3.1 Disparo de evento simulado para teste de e-mail e fila
$testOrgao = "Dataprev-Test-$(Get-Random)"
$testCargo = "Analista de Seguranca da Informacao"
$testSalario = "R$ 16.450,00"

Assert-Test -TestName "POST /api/concursos/test-email - Publicação de ConcursoPublicadoEvent" -TestBlock {
    $uri = "$ApiBaseUrl/api/concursos/test-email?orgao=$([Uri]::EscapeDataString($testOrgao))&cargo=$([Uri]::EscapeDataString($testCargo))&salario=$([Uri]::EscapeDataString($testSalario))"
    $res = Invoke-RestMethod -Uri $uri -Method Post -TimeoutSec $TimeoutSeconds
    
    if (-not $res.Evento -or -not $res.Evento.EventId) {
        throw "Resposta não retornou o evento gerado com EventId."
    }
    Write-Host " (EventId: $($res.Evento.EventId))" -NoNewline -ForegroundColor DarkGray
}

# 3.2 Aguardar propagação e verificar se o e-mail chegou no Mailpit (se Mailpit estiver configurado)
Assert-Test -TestName "Mailpit - Verificação de entrega de mensagem no Webmail local" -TestBlock {
    Start-Sleep -Seconds 2
    $msgs = Invoke-RestMethod -Uri "$MailpitHttpUrl/api/v1/messages" -Method Get -TimeoutSec $TimeoutSeconds
    if ($null -eq $msgs.messages) {
        throw "Não foi possível obter a lista de mensagens do Mailpit."
    }
    Write-Host " ($($msgs.total) mensagem(ns) no Mailpit)" -NoNewline -ForegroundColor DarkGray
}

Log-Section "4. TESTE DO FLUXO DE COLETA DE CONCURSOS DE TI"

# 4.1 Coleta via fonte sintética (Mock)
Assert-Test -TestName "POST /api/concursos/coletar?fonte=mock - Coleta sintética multi-fonte" -TestBlock {
    $res = Invoke-RestMethod -Uri "$ApiBaseUrl/api/concursos/coletar?fonte=mock" -Method Post -TimeoutSec $TimeoutSeconds
    if ($null -eq $res.TotalPublicados -or $res.TotalPublicados -lt 1) {
        throw "A fonte mock não publicou eventos."
    }
    Write-Host " ($($res.TotalPublicados) concurso(s) mock publicados)" -NoNewline -ForegroundColor DarkGray
}

if (-not $SkipCollectorScrape) {
    Assert-Test -TestName "POST /api/concursos/coletar - Acionar crawler e publicar editais de TI" -TestBlock {
        $res = Invoke-RestMethod -Uri "$ApiBaseUrl/api/concursos/coletar" -Method Post -TimeoutSec 30
        if ($null -eq $res.TotalPublicados) {
            throw "Resposta da coleta não conteve o campo TotalPublicados."
        }
        Write-Host " ($($res.TotalPublicados) concurso(s) de TI identificados e publicados)" -NoNewline -ForegroundColor DarkGray
    }
} else {
    Write-Host "[IGNORADO] Teste de coleta externa ignorado pelo parâmetro -SkipCollectorScrape." -ForegroundColor Yellow
}

Log-Section "5. TESTE DE ACESSO AO PAINEL WEB (BLAZOR SERVER)"

Assert-Test -TestName "Concurso.Web - Acesso HTTP ao Painel Blazor (Porta 5001)" -TestBlock {
    $res = Invoke-WebRequest -Uri $WebBaseUrl -UseBasicParsing -TimeoutSec $TimeoutSeconds
    if ($res.StatusCode -ne 200) {
        throw "Painel Web retornou status HTTP $($res.StatusCode)"
    }
    if ($res.Content -notlike "*ConcursosTI*") {
        throw "HTML do Concurso.Web não contém o título esperado."
    }
}

# ==============================================================================
# RELATÓRIO FINAL CONSOLIDADO
# ==============================================================================
$stopwatch.Stop()
$totalTests = $testResults.Count
$passCount = ($testResults | Where-Object { $_.Status -eq "PASSOU" }).Count
$failCount = ($testResults | Where-Object { $_.Status -eq "FALHOU" }).Count

Log-Section "RESUMO DA EXECUÇÃO DOS TESTES"
Write-Host "Total de Testes  : $totalTests" -ForegroundColor White
Write-Host "Testes Aprovados : $passCount" -ForegroundColor Green
Write-Host "Testes com Falha : $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host "Tempo Total      : $($stopwatch.Elapsed.TotalSeconds.ToString("0.00")) segundos`n" -ForegroundColor Cyan

if ($failCount -gt 0) {
    Write-Host "Alguns testes falharam. Verifique os logs e se todos os serviços foram iniciados com .\run-all.ps1" -ForegroundColor Red
    exit 1
} else {
    Write-Host "Todos os testes passaram com sucesso! A solução está saudável e operacional." -ForegroundColor Green
    exit 0
}
