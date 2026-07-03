$ErrorActionPreference = "Stop"

$senha   = "SUA_SENHA_AQUI"
$servidor = "76.13.160.202"
$destino  = "/var/www/analisedecraque/"

Write-Host "==> Compilando..." -ForegroundColor Cyan
Set-Location "C:\Users\Juliano\source\repos\ProjectComentarista\ControleFutebolWeb"
dotnet publish -c Release -o ./publish

Write-Host "==> Enviando arquivos..." -ForegroundColor Cyan
& pscp -pw $senha -r "C:\Users\Juliano\source\repos\ProjectComentarista\ControleFutebolWeb\publish\*" "root@${servidor}:${destino}"

Write-Host "==> Reiniciando servico..." -ForegroundColor Cyan
& plink -pw $senha -batch root@$servidor "systemctl restart analisedecraque"

Write-Host "==> Deploy concluido!" -ForegroundColor Green
