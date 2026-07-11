#!/usr/bin/env bash
# Backup diário do PostgreSQL de produção (banco: projectcomentarista).
#
# INSTALAÇÃO NO SERVIDOR (uma vez):
#   1. scp scripts/backup-postgres.sh root@76.13.160.202:/usr/local/bin/backup-postgres.sh
#   2. ssh root@76.13.160.202 "chmod +x /usr/local/bin/backup-postgres.sh"
#   3. ssh root@76.13.160.202 "echo '30 3 * * * root /usr/local/bin/backup-postgres.sh >> /var/log/backup-postgres.log 2>&1' > /etc/cron.d/backup-postgres"
#   (roda todo dia às 03:30; o pg_dump roda como usuário postgres via sudo,
#    então não precisa de senha — usa autenticação peer local.)
#
# RESTAURAÇÃO (exemplo):
#   sudo -u postgres pg_restore -d projectcomentarista --clean --if-exists \
#       /var/backups/postgres/projectcomentarista-AAAA-MM-DD.dump
#
# IMPORTANTE: os backups ficam no MESMO servidor. Copie periodicamente para
# fora (ex.: scp para sua máquina, rclone para um storage) — se o disco do
# servidor morrer, backup local não salva.

set -euo pipefail

BANCO="projectcomentarista"
DESTINO="/var/backups/postgres"
RETENCAO_DIAS=14
ARQUIVO="$DESTINO/$BANCO-$(date +%F).dump"

mkdir -p "$DESTINO"

# Formato custom (-Fc): comprimido e restaurável seletivamente com pg_restore.
sudo -u postgres pg_dump -Fc "$BANCO" > "$ARQUIVO.tmp"
mv "$ARQUIVO.tmp" "$ARQUIVO"

# Sanidade: um dump vazio/minúsculo indica falha silenciosa.
TAMANHO=$(stat -c%s "$ARQUIVO")
if [ "$TAMANHO" -lt 10240 ]; then
    echo "[ERRO] $(date -Is) dump suspeito (apenas ${TAMANHO} bytes): $ARQUIVO" >&2
    exit 1
fi

# Remove dumps além da retenção.
find "$DESTINO" -name "$BANCO-*.dump" -mtime +"$RETENCAO_DIAS" -delete

echo "[OK] $(date -Is) backup gerado: $ARQUIVO (${TAMANHO} bytes)"
