# run-all.ps1
# Inicia toda a infraestrutura e os serviços do ConcursosTI em comando único

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  ConcursosTI - Iniciando Ambiente Completo               " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Iniciar Docker (RabbitMQ e Mailpit)
Write-Host "`n[1/5] Verificando e iniciando Docker (RabbitMQ + Mailpit)..." -ForegroundColor Yellow
docker compose up -d

# 2. Iniciar Concurso.Api
Write-Host "`n[2/5] Iniciando Concurso.Api (http://localhost:5000)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle = 'Concurso.Api (Porta 5000)'; dotnet run --project .\Concurso.Api\Concurso.Api.csproj"

# 3. Iniciar Concurso.Notification (Worker Resend de E-mail)
Write-Host "`n[3/5] Iniciando Concurso.Notification (Worker E-mail)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle = 'Concurso.Notification (Worker E-mail)'; dotnet run --project .\Concurso.Notification\Concurso.Notification.csproj"

# 4. Iniciar Concurso.Consumer (Worker Banco de Dados SQLite)
Write-Host "`n[4/5] Iniciando Concurso.Consumer (Worker Banco SQLite)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle = 'Concurso.Consumer (Worker Banco)'; dotnet run --project .\Concurso.Consumer\Concurso.Consumer.csproj"

# 5. Iniciar Concurso.Web (Painel Blazor + MudBlazor)
Write-Host "`n[5/5] Iniciando Concurso.Web (http://localhost:5001)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle = 'Concurso.Web (Painel 5001)'; dotnet run --project .\Concurso.Web\Concurso.Web.csproj"

Write-Host "`nTodos os serviços foram iniciados!" -ForegroundColor Green
Write-Host "Aguardando 6 segundos para abertura do painel no navegador..." -ForegroundColor Cyan
Start-Sleep -Seconds 6
Start-Process "http://localhost:5001"

Write-Host "`nPara encerrar todos os serviços a qualquer momento, execute: .\stop-all.ps1`n" -ForegroundColor Magenta
