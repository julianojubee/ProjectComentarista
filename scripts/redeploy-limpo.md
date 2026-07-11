# Redeploy limpo — remover a DLL falsa (NpgsqlVault) de produção

> ✅ **EXECUTADO em 11/07/2026 à noite.** Driver oficial confirmado em prod por
> hash (`5406d26e…`), `Anthropic.dll` removido, senha do Postgres rotacionada
> após a troca do driver, /health 200. Mantido como runbook de referência.

> Situação em 11/07/2026: produção roda o `Npgsql.dll` do typosquat NpgsqlVault
> (hash `663319…`, vindo da pasta `publish/` de janeiro) e um `Anthropic.dll`
> órfão. O deploy de 11/07 17:32 copiou o publish velho porque o `dotnet publish`
> falhou sem abortar o script — já corrigido em `deploy.ps1`/`deploy-senha.ps1`.
> A senha do Postgres foi rotacionada em 11/07, mas **enquanto a DLL falsa estiver
> rodando, considere-a potencialmente exposta** — rotacione de novo após o redeploy.

## 1. Redeploy

Com o app local **parado** (senão o build falha por arquivo travado):

```powershell
.\deploy.ps1
```

O script já: limpa `publish/`, aborta se o publish falhar, e valida `/health`
após o restart. O `/health` só passa a existir em produção **após** este deploy.

Opcional, antes do restart — limpar DLLs órfãs no servidor (preserva `wwwroot/`
e as imagens enviadas em runtime):

```bash
ssh root@76.13.160.202 "find /var/www/analisedecraque -maxdepth 1 -type f -name '*.dll' -delete"
# em seguida rode o deploy.ps1 normalmente (ele reenvia tudo)
```

## 2. Conferir que a DLL falsa sumiu

```powershell
# hash oficial (local, do publish recém-gerado):
Get-FileHash .\ControleFutebolWeb\publish\Npgsql.dll -Algorithm SHA256
# hash em produção — devem ser IGUAIS:
ssh root@76.13.160.202 "sha256sum /var/www/analisedecraque/Npgsql.dll"
# e o órfão deve ter sumido:
ssh root@76.13.160.202 "ls /var/www/analisedecraque/Anthropic.dll"   # esperado: No such file
```

O hash da DLL **falsa** (se aparecer, algo deu errado):
`6633195cd1bc74156d73bd7a51db07ab41b3af85817b60841f3e6be3586a6350`

## 3. Rotacionar a senha do Postgres de novo

Gera a senha no servidor e grava só no override do systemd (não passa pelo chat/terminal local):

```bash
ssh root@76.13.160.202 '
set -e
OVR=/etc/systemd/system/analisedecraque.service.d/override.conf
cp -a "$OVR" "$OVR.bak-$(date +%Y%m%d%H%M%S)" && chmod 600 "$OVR".bak-*
USUARIO=$(grep -oP "Username=\K[^;\"]+" "$OVR" | head -1)
NOVA=$(openssl rand -hex 24)
sudo -u postgres psql -v ON_ERROR_STOP=1 -q -c "ALTER USER \"$USUARIO\" WITH PASSWORD '"'"'$NOVA'"'"';"
sed -i "s/Password=[^;\"]*/Password=$NOVA/" "$OVR"
systemctl daemon-reload && systemctl restart analisedecraque
sleep 6 && systemctl is-active analisedecraque
'
curl -s -o /dev/null -w "%{http_code}\n" https://analisedecraque.cloud/health   # esperado: 200
```

## Já concluído em 11/07/2026 (não repetir)

- Backup diário do Postgres: `/usr/local/bin/backup-postgres.sh` + `/etc/cron.d/backup-postgres` (03:30), testado.
- Serviço rodando como usuário `analisedecraque` (não-root), chaves DataProtection preservadas.
- 1ª rotação da senha do Postgres (tratar como provisória até o redeploy).
