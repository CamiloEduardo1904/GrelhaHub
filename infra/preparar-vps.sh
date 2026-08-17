#!/usr/bin/env bash
#
# Prepara uma VPS Ubuntu limpa para receber o GrelhaHub — ADR-0006.
# Rode UMA VEZ, como root ou com sudo.
#
#   curl -fsSL .../preparar-vps.sh | sudo bash
#   ou: sudo ./infra/preparar-vps.sh
#
# Idempotente: pode rodar de novo sem quebrar nada.

set -euo pipefail

USUARIO_DEPLOY="${USUARIO_DEPLOY:-deploy}"
DESTINO="/opt/grelhahub"

echo "==> Pacotes"
apt-get update -qq
apt-get install -y -qq ca-certificates curl ufw

echo "==> Docker"
if ! command -v docker >/dev/null; then
  curl -fsSL https://get.docker.com | sh
else
  echo "    já instalado: $(docker --version)"
fi

echo "==> Usuário ${USUARIO_DEPLOY}"
# ADR-0006 — nunca operar como root.
if ! id "${USUARIO_DEPLOY}" >/dev/null 2>&1; then
  adduser --disabled-password --gecos "" "${USUARIO_DEPLOY}"
fi
usermod -aG docker "${USUARIO_DEPLOY}"

echo "==> Diretório ${DESTINO}"
mkdir -p "${DESTINO}"
chown -R "${USUARIO_DEPLOY}:${USUARIO_DEPLOY}" "${DESTINO}"

echo "==> Firewall"
# RNF-007 — só 22, 80 e 443. O PostgreSQL nunca é exposto.
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable
ufw status verbose

echo "==> Swap"
# Risco RS3 — .NET + PostgreSQL + Nginx numa VPS pequena. Sem swap, o kernel
# mata o processo que estiver usando mais memória, e costuma ser o Postgres.
MEM_MB=$(free -m | awk '/^Mem:/{print $2}')
if [ "${MEM_MB}" -lt 2048 ] && [ ! -f /swapfile ]; then
  echo "    ${MEM_MB} MB de RAM — criando swap de 2 GB"
  fallocate -l 2G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  grep -q '^/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
else
  echo "    ${MEM_MB} MB de RAM — swap dispensável ou já existe"
fi

cat <<FIM

==> Pronto.

Próximos passos, como ${USUARIO_DEPLOY}:

  1. Copie para ${DESTINO}:
       docker-compose.yml
       infra/            (nginx, scripts)
       .env              (a partir de .env.example — NUNCA versionado)

  2. Gere os segredos:
       openssl rand -base64 48   # JWT_CHAVE
       openssl rand -base64 32   # POSTGRES_PASSWORD

  3. Suba:
       cd ${DESTINO} && docker compose up -d

  4. Confira:
       curl -fsS http://localhost/api/health

  5. Com o domínio já apontando para este IP:
       ./infra/ativar-https.sh

FIM
