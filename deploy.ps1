$ErrorActionPreference = "Stop"

Write-Host "==> Compilando..." -ForegroundColor Cyan
Set-Location "C:\Users\Juliano\source\repos\ProjectComentarista\ControleFutebolWeb"
dotnet publish -c Release -o ./publish

Write-Host "==> Enviando arquivos para o servidor..." -ForegroundColor Cyan
scp -r "C:/Users/Juliano/source/repos/ProjectComentarista/ControleFutebolWeb/publish/*" root@76.13.160.202:/var/www/analisedecraque/

Write-Host "==> Reiniciando servico..." -ForegroundColor Cyan
ssh root@76.13.160.202 "systemctl restart analisedecraque"

Write-Host "==> Deploy concluido!" -ForegroundColor Green
