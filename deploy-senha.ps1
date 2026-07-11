$ErrorActionPreference = "Stop"

$senha   = "SUA_SENHA_AQUI"
$servidor = "76.13.160.202"
$destino  = "/var/www/analisedecraque/"

Write-Host "==> Compilando..." -ForegroundColor Cyan
Set-Location "C:\Users\Juliano\source\repos\ProjectComentarista\ControleFutebolWeb"

# Limpa o publish anterior: dotnet publish NAO remove arquivos antigos, e uma
# pasta suja ja mandou binarios de janeiro (incl. a DLL falsa NpgsqlVault) pra
# producao em 11/07/2026.
if (Test-Path ./publish) { Remove-Item -Recurse -Force ./publish }

dotnet publish -c Release -o ./publish
# $ErrorActionPreference nao cobre exe nativo: sem esta checagem, um publish
# falho seguia para o pscp com o publish velho.
if ($LASTEXITCODE -ne 0) {
    Write-Host "==> FALHA no dotnet publish - deploy abortado (nada foi enviado)." -ForegroundColor Red
    exit 1
}

Write-Host "==> Enviando arquivos..." -ForegroundColor Cyan
& pscp -pw $senha -r "C:\Users\Juliano\source\repos\ProjectComentarista\ControleFutebolWeb\publish\*" "root@${servidor}:${destino}"

Write-Host "==> Reiniciando servico..." -ForegroundColor Cyan
& plink -pw $senha -batch root@$servidor "systemctl restart analisedecraque"

# Verificação pós-deploy: consulta /health (checa app + conexão com o Postgres)
# até responder 200. Se não subir em ~60s, o deploy FALHOU.
Write-Host "==> Verificando saude da aplicacao..." -ForegroundColor Cyan
$healthUrl = "https://analisedecraque.cloud/health"
$ok = $false
foreach ($tentativa in 1..12) {
    Start-Sleep -Seconds 5
    try {
        $resp = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 5
        if ($resp.StatusCode -eq 200) { $ok = $true; break }
    } catch {
        Write-Host "    tentativa $tentativa/12: aplicacao ainda nao respondeu..." -ForegroundColor DarkGray
    }
}

if ($ok) {
    Write-Host "==> Deploy concluido! ($healthUrl respondeu 200)" -ForegroundColor Green
} else {
    Write-Host "==> DEPLOY COM PROBLEMA: $healthUrl nao respondeu 200 em 60s." -ForegroundColor Red
    Write-Host "    Diagnostico: plink -pw SENHA -batch root@$servidor 'journalctl -u analisedecraque -n 50 --no-pager'" -ForegroundColor Yellow
    exit 1
}
