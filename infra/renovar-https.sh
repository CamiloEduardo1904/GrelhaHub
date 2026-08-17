#!/usr/bin/env bash
#
# Renova o certificado Let's Encrypt. Rode por cron, diariamente.
# O certbot só renova quando falta menos de 30 dias, então rodar todo dia é
# barato e garante margem se um dia falhar.
#
#   0 3 * * * cd /opt/grelhahub && ./infra/renovar-https.sh >> /var/log/grelhahub-certbot.log 2>&1

set -euo pipefail
cd "$(dirname "$0")/.."

echo "== $(date -Is) renovação =="

docker run --rm \
  -v /etc/letsencrypt:/etc/letsencrypt \
  -v grelhahub_certbot-www:/var/www/certbot \
  certbot/certbot renew \
    --webroot --webroot-path /var/www/certbot \
    --non-interactive

# O nginx só relê o certificado ao recarregar. Sem isto, a renovação acontece e
# o navegador continua vendo o certificado velho até o próximo restart.
docker compose exec -T nginx nginx -s reload
echo "nginx recarregado"
