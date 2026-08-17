#!/usr/bin/env bash
#
# Ativa HTTPS no GrelhaHub — Let's Encrypt via certbot, desafio HTTP-01.
#
# Rode NA VPS, dentro de /opt/grelhahub, com a pilha já no ar em HTTP e o
# domínio já apontando para o IP do servidor.
#
#   ./infra/ativar-https.sh
#
# Idempotente: rodar de novo com certificado válido não emite outro.

set -euo pipefail

cd "$(dirname "$0")/.."

if [ ! -f .env ]; then
  echo "ERRO: .env não encontrado. Copie de .env.example e preencha." >&2
  exit 1
fi

# shellcheck disable=SC1091
set -a; source .env; set +a

: "${DOMINIO:?defina DOMINIO no .env}"
: "${EMAIL_ACME:?defina EMAIL_ACME no .env — é para onde vai o aviso de expiração}"

echo "==> Conferindo se o domínio chega neste servidor"
if ! curl -fsS --max-time 10 "http://${DOMINIO}/api/health" >/dev/null; then
  cat >&2 <<MSG
ERRO: http://${DOMINIO}/api/health não respondeu.

O certbot usa o desafio HTTP-01: ele pede que a Let's Encrypt busque um arquivo
em http://${DOMINIO}/.well-known/acme-challenge/. Se o domínio ainda não aponta
para este servidor, ou a pilha não está no ar, a emissão falha e a Let's Encrypt
aplica limite de tentativas.

Confira:
  - o A record de ${DOMINIO} aponta para o IP desta VPS
  - docker compose ps mostra nginx, api e web no ar
  - a porta 80 está liberada no ufw
MSG
  exit 1
fi

echo "==> Emitindo certificado para ${DOMINIO}"
docker run --rm \
  -v /etc/letsencrypt:/etc/letsencrypt \
  -v grelhahub_certbot-www:/var/www/certbot \
  certbot/certbot certonly \
    --webroot --webroot-path /var/www/certbot \
    -d "${DOMINIO}" \
    --email "${EMAIL_ACME}" \
    --agree-tos --no-eff-email \
    --keep-until-expiring \
    --non-interactive

echo "==> Escrevendo a configuração de TLS"

cat > infra/nginx/tls-ativo/redirecionar.conf <<CONF
# Gerado por infra/ativar-https.sh. Não edite a mão.
location = /.well-known/acme-challenge/ { root /var/www/certbot; }
return 301 https://\$host\$request_uri;
CONF

cat > infra/nginx/tls-ativo/servidor.conf <<CONF
# Gerado por infra/ativar-https.sh. Não edite a mão.
server {
    listen 443 ssl;
    listen [::]:443 ssl;
    http2 on;
    server_name ${DOMINIO};

    ssl_certificate     /etc/letsencrypt/live/${DOMINIO}/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/${DOMINIO}/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers off;
    ssl_session_cache   shared:SSL:10m;

    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Content-Type-Options    "nosniff" always;
    add_header X-Frame-Options           "SAMEORIGIN" always;
    add_header Referrer-Policy           "strict-origin-when-cross-origin" always;

    server_tokens off;
    client_max_body_size 10M;

    gzip on;
    gzip_types text/plain text/css application/javascript application/json image/svg+xml;
    gzip_min_length 1024;

    location /api/ {
        proxy_pass         http://api:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              \$host;
        proxy_set_header   X-Real-IP         \$remote_addr;
        proxy_set_header   X-Forwarded-For   \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
        proxy_read_timeout 60s;
    }

    location / {
        proxy_pass         http://web:80;
        proxy_http_version 1.1;
        proxy_set_header   Host \$host;
    }

    # Assets do Vite têm hash no nome — cache agressivo é seguro.
    location /assets/ {
        proxy_pass     http://web:80;
        expires        1y;
        add_header     Cache-Control "public, immutable";
        access_log     off;
    }
}
CONF

echo "==> Recarregando o nginx"
docker compose exec nginx nginx -t
docker compose restart nginx

echo
echo "HTTPS ativo: https://${DOMINIO}"
echo
echo "A renovação NÃO é automática ainda. Instale o cron:"
echo "  sudo crontab -e"
echo "  0 3 * * * cd /opt/grelhahub && ./infra/renovar-https.sh >> /var/log/grelhahub-certbot.log 2>&1"
