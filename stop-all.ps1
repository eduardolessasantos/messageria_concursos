# stop-all.ps1
# Encerra todos os serviços do ConcursosTI de forma limpa

Write-Host "Encerrando serviços do ConcursosTI..." -ForegroundColor Yellow

$nomes = @("Concurso.Api", "Concurso.Notification", "Concurso.Consumer", "Concurso.Web", "Concurso.Producer")

foreach ($nome in $nomes) {
    $procs = Get-Process -Name $nome -ErrorAction SilentlyContinue
    if ($procs) {
        $procs | Stop-Process -Force
        Write-Host "Processo $nome encerrado." -ForegroundColor Green
    }
}

# Também busca processos dotnet rodando no diretório da solução se necessário
Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like "*Concurso.*.csproj*" } | ForEach-Object {
    Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    Write-Host "Processo dotnet (PID: $($_.ProcessId)) encerrado." -ForegroundColor Green
}

Write-Host "Todos os serviços foram finalizados com sucesso!" -ForegroundColor Cyan
