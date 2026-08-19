#!/usr/bin/env bash
#
# Sobe a pilha real do GrelhaHub para validação em tela.
#
# PostgreSQL em contêiner → os TRÊS bancos migrados (ADR-0017, ADR-0018) → API em :5000 →
# Vite em :5173. Para na primeira falha, porque cada passo depende do anterior.
#
# Exige Docker rodando, o mesmo requisito de `dotnet test` (ADR-0009).
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
CONTEINER=grelhahub-dev
LOGS="${TMPDIR:-/tmp}/grelhahub-validacao"
mkdir -p "$LOGS"

cd "$RAIZ"

echo "── 1/5 · PostgreSQL em :5433 ────────────────────────────────────────────"
# Porta e credenciais são as de `appsettings.Development.json`. Mudar aqui sem mudar lá
# dá "connection refused" numa API que subiu — o defeito mais confuso possível.
docker rm -f "$CONTEINER" >/dev/null 2>&1 || true
docker run -d --name "$CONTEINER" \
  -e POSTGRES_DB=grelhahub -e POSTGRES_USER=grelhahub -e POSTGRES_PASSWORD=grelhahub \
  -e TZ=America/Sao_Paulo -e PGTZ=UTC \
  -p 5433:5432 postgres:16-alpine >/dev/null

# ⚠ O `sleep` é obrigatório, e a falta dele foi o primeiro defeito deste script: sem espera as 30
# tentativas se esgotam em menos de um segundo — antes de o Postgres terminar de subir — e o
# `pg_isready` final derruba o script por `set -e`, com a única mensagem sendo o título do passo.
#
# ⚠⚠ E o `-h 127.0.0.1` é o SEGUNDO defeito, mais traiçoeiro: durante o `initdb` a imagem do
# PostgreSQL sobe um servidor TEMPORÁRIO para criar o banco, e ele escuta APENAS no socket unix.
# Um `pg_isready` sem host fala com esse socket, responde "pronto", e o passo seguinte esbarra em
# `FATAL: the database system is shutting down` — porque o servidor temporário estava justamente
# encerrando para o definitivo assumir. O servidor de verdade é o único que escuta em TCP; por isso
# a espera é por TCP.
for _ in $(seq 1 40); do
  if docker exec "$CONTEINER" pg_isready -h 127.0.0.1 -p 5432 -U grelhahub -d grelhahub >/dev/null 2>&1; then
    pronto=1
    break
  fi
  sleep 1
done

[ "${pronto:-}" = 1 ] || { echo "   ✖ Postgres não respondeu em 40 s"; exit 1; }

echo "── 2/5 · os outros dois bancos (ADR-0017, ADR-0018) ─────────────────────"
docker exec "$CONTEINER" psql -U grelhahub -d grelhahub -q \
  -c "CREATE DATABASE grelhahub_log OWNER grelhahub;" \
  -c "CREATE DATABASE grelhahub_arquivo OWNER grelhahub;"

echo "── 3/5 · migrations dos três + usuário de plataforma ────────────────────"
# ⚠ A flag é `--migrate`, em INGLÊS. Escrita como `--migrar` ela é ignorada em silêncio e o
# processo sobe como SERVIDOR: a API responde /api/health, os bancos de log e de arquivo ficam
# vazios, a trilha falha em toda operação e o primeiro upload dá 500.
#
# ⚠ O startup de Development migra só o banco principal. Os outros dois só vêm por aqui.
cd "$RAIZ/backend"
ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/GrelhaHub.Api -c Debug -- --migrate --semear \
  > "$LOGS/migrate.log" 2>&1

for banco in grelhahub grelhahub_log grelhahub_arquivo; do
  n=$(docker exec "$CONTEINER" psql -U grelhahub -d "$banco" -tAc \
    "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';")
  echo "   $banco: $n tabelas"
  [ "$n" -gt 0 ] || { echo "   ✖ $banco não migrou — ver $LOGS/migrate.log"; exit 1; }
done

echo "── 4/5 · API em :5000 ───────────────────────────────────────────────────"
# Serilog em Warning: em Debug o log de SQL do EF Core enterra qualquer coisa útil.
ASPNETCORE_ENVIRONMENT=Development Serilog__MinimumLevel__Default=Warning \
  nohup dotnet run --project src/GrelhaHub.Api -c Debug --no-build \
  > "$LOGS/api.log" 2>&1 &

echo "── 5/5 · Vite em :5173 ──────────────────────────────────────────────────"
cd "$RAIZ/frontend"
# ⚠ `npm ci` primeiro se não houver node_modules: sem ele o `npm run` sai 0 sem rodar nada.
[ -d node_modules ] || npm ci
nohup npx vite --port 5173 > "$LOGS/vite.log" 2>&1 &

for _ in $(seq 1 60); do
  api=$(curl -s -m 2 http://localhost:5000/api/health 2>/dev/null || true)
  web=$(curl -s -m 2 -o /dev/null -w '%{http_code}' http://localhost:5173/ 2>/dev/null || true)
  [ -n "$api" ] && [ "$web" = "200" ] && break
  sleep 1
done

[ -n "${api:-}" ] || { echo "✖ API não respondeu — ver $LOGS/api.log"; exit 1; }
[ "${web:-}" = "200" ] || { echo "✖ Vite não respondeu — ver $LOGS/vite.log"; exit 1; }

echo
echo "✔ API   http://localhost:5000/api/health  → $api"
echo "✔ Web   http://localhost:5173"
echo "  logs  $LOGS"
echo
echo "Próximo: bash .claude/skills/subir-e-validar/scripts/semear.sh"
