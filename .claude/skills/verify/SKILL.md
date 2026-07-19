---
name: verify
description: Como buildar, subir e dirigir o ControleFutebolWeb para verificar mudanças de ponta a ponta (web autenticada + API JWT) sem navegador.
---

# Verificar ControleFutebolWeb

## Build e launch

```powershell
dotnet build ControleFutebolWeb -v q --nologo          # "Compilação com êxito."
dotnet run --project ControleFutebolWeb --urls https://localhost:5057 --no-build
```

- Rodar `dotnet run` em background; o site está no ar quando o log mostra `Now listening on: https://localhost:5057`.
- **Não** usar `--launch-profile https` (o perfil não existe): sem o perfil padrão o ambiente não vira Development e o app tenta o Postgres de **produção** (5432) e morre com falha de senha. O perfil padrão (`ControleFutebolWeb`) já seta `ASPNETCORE_ENVIRONMENT=Development` (Postgres dev 5433, user-secrets).
- Migrações pendentes são aplicadas no start (`Database.Migrate()` no Program.cs).

## Dirigir a web autenticada (sem navegador)

`Invoke-WebRequest` do PowerShell 5.1 falha no TLS do dev cert — usar `curl.exe -sk` com cookie jar:

```powershell
$jar = "$env:TEMP\cfw_cookies.txt"
$login = curl.exe -sk -c $jar 'https://localhost:5057/Account/Login' | Out-String
$tok = [regex]::Match($login, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
curl.exe -sk -b $jar -c $jar -o NUL -w '%{http_code}' 'https://localhost:5057/Account/Login' `
  --data-urlencode 'UserName=claude.teste' --data-urlencode 'Password=ClaudeTeste@2026' `
  --data-urlencode 'RememberMe=false' --data-urlencode "__RequestVerificationToken=$tok"   # espera 302
```

- Login é `/Account/Login` (campos `UserName`/`Password`), **não** `/Identity/Account/Login` (404).
- POSTs de forms precisam do `__RequestVerificationToken` da página que contém o form (pegar do HTML via regex).
- Usar `-o NUL` (não `-o $null`) e evitar `$home` como variável (read-only no PowerShell).

## API (app Android) — JWT

Body JSON via arquivo (aspas se perdem passando string direta ao curl.exe):

```powershell
'{"username":"claude.teste","password":"ClaudeTeste@2026"}' | Out-File "$env:TEMP\login.json" -Encoding ascii
$resp = curl.exe -sk -H 'Content-Type: application/json' -d "@$env:TEMP\login.json" 'https://localhost:5057/api/v1/auth/login' | Out-String
$token = [regex]::Match($resp, '"token"\s*:\s*"([^"]+)"').Groups[1].Value
curl.exe -sk -H "Authorization: Bearer $token" 'https://localhost:5057/api/v1/competicoes/82/classificacao'
```

## Fluxos úteis e dados do banco dev

- Competições no dev: 82 Bundesliga (pontos corridos, tem playoff de rebaixamento "Final"), 84 Ligue 1, 5 Premier League, 83 Serie A, 4 SulAmericana (grupos + eliminatória).
- Telas verificáveis por regex no HTML: abas `data-tab="..."`, linhas de tabela `class="cd-row"`, grupos `cd-grupo-card`, chaveamento `cd-mm-fase-nome`.
- Deixar o banco dev como encontrou (remover fases/dados de teste criados).
- Encerrar o `dotnet run` de background ao final.
