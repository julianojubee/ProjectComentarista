<#
.SYNOPSIS
    Publica a versao Release do ControleFutebolWeb e sobe para o servidor de producao.

.DESCRIPTION
    1. Gera o build de publicacao (dotnet publish -c Release).
    2. Copia os arquivos publicados para o servidor via scp.
    3. Reinicia o servico systemd "analisedecraque" via ssh.

.PARAMETER ServerHost
    Usuario@host do servidor (default: root@76.13.160.202).

.PARAMETER RemotePath
    Caminho remoto onde a aplicacao roda (default: /var/www/analisedecraque/).

.PARAMETER ServiceName
    Nome do servico systemd a reiniciar (default: analisedecraque).

.PARAMETER SkipPublish
    Pula a etapa de "dotnet publish" e reaproveita o conteudo atual de ./publish.

.EXAMPLE
    .\scripts\deploy.ps1
    Roda o fluxo completo com os valores default.

.EXAMPLE
    .\scripts\deploy.ps1 -SkipPublish
    Sobe o que ja estiver em ./publish sem gerar build de novo.
#>

[CmdletBinding()]
param(
    [string]$ServerHost = "root@76.13.160.202",
    [string]$RemotePath = "/var/www/analisedecraque/",
    [string]$ServiceName = "analisedecraque",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $repoRoot "ControleFutebolWeb"
$publishDir = Join-Path $projectDir "publish"

function Write-Step($msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

try {
    if (-not $SkipPublish) {
        Write-Step "Gerando build de publicacao (dotnet publish -c Release)"
        Push-Location $projectDir
        try {
            dotnet publish -c Release -o ./publish
            if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou (exit code $LASTEXITCODE)" }
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-Step "Pulando 'dotnet publish' (-SkipPublish). Usando conteudo atual de $publishDir"
        if (-not (Test-Path $publishDir)) {
            throw "Pasta de publish nao encontrada: $publishDir. Rode sem -SkipPublish primeiro."
        }
    }

    Write-Step "Enviando arquivos para $ServerHost`:$RemotePath (scp)"
    # Barra final em \* garante que o CONTEUDO da pasta publish seja copiado, nao a pasta em si.
    $sourceGlob = Join-Path $publishDir "*"
    scp -r $sourceGlob "${ServerHost}:${RemotePath}"
    if ($LASTEXITCODE -ne 0) { throw "scp falhou (exit code $LASTEXITCODE)" }

    Write-Step "Reiniciando servico '$ServiceName' no servidor (ssh)"
    ssh $ServerHost "systemctl restart $ServiceName"
    if ($LASTEXITCODE -ne 0) { throw "ssh/systemctl restart falhou (exit code $LASTEXITCODE)" }

    Write-Host ""
    Write-Host "Deploy concluido com sucesso." -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "ERRO no deploy: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
