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

# ⚠⚠ A porta é configurável porque 5173 é o padrão do Vite — e portanto a porta de TODO projeto
# Vite da máquina. Ver o aviso do passo 5.
PORTA_WEB=${PORTA_WEB:-5173}
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

echo "── 5/5 · Vite em :$PORTA_WEB ────────────────────────────────────────────"
cd "$RAIZ/frontend"
# ⚠ `npm ci` primeiro se não houver node_modules: sem ele o `npm run` sai 0 sem rodar nada.
[ -d node_modules ] || npm ci

# ⚠⚠ `--strictPort` NÃO É OPCIONAL, e a falta dele custou uma sessão inteira.
#
# 5173 é o padrão do Vite, e portanto a porta de TODO projeto Vite da máquina. Sem
# `--strictPort` o Vite encontra a porta ocupada, **avisa no log e sobe em 5174 em silêncio** —
# e o `curl` deste script recebia 200 de um projeto ALHEIO, declarava sucesso, e o navegador
# abria a aplicação de outra pessoa. O sintoma foi `Timeout waiting for input[type="email"]`
# numa tela que não tinha campo de e-mail porque não era a nossa.
nohup npx vite --port "$PORTA_WEB" --strictPort > "$LOGS/vite.log" 2>&1 &

for _ in $(seq 1 60); do
  api=$(curl -s -m 2 http://localhost:5000/api/health 2>/dev/null || true)
  web=$(curl -s -m 2 "http://localhost:$PORTA_WEB/" 2>/dev/null || true)
  [ -n "$api" ] && [ -n "$web" ] && break
  sleep 1
done

[ -n "${api:-}" ] || { echo "   ✖ API não respondeu — ver $LOGS/api.log"; exit 1; }

if [ -z "${web:-}" ]; then
  echo "   ✖ Vite não respondeu — ver $LOGS/vite.log"
  echo "     Porta ocupada? PORTA_WEB=5174 bash $0"
  exit 1
fi

# ⚠⚠ Confere a IDENTIDADE, e não a vivacidade. "Respondeu 200" prova que ALGUMA coisa está
# naquela porta; o título prova que é a nossa. É a mesma lição do `pg_isready` que respondia
# ao servidor temporário do `initdb`: o teste tem de perguntar o que importa.
if ! printf '%s' "$web" | grep -q 'GrelhaHub'; then
  echo "   ✖ :$PORTA_WEB responde, mas NÃO é o GrelhaHub — outro projeto está na porta."
  echo "     Título encontrado: $(printf '%s' "$web" | grep -o '<title>[^<]*' | head -1)"
  echo "     Suba noutra: PORTA_WEB=5174 bash $0"
  exit 1
fi

echo
echo "✔ API   http://localhost:5000/api/health  → $api"
echo "✔ Web   http://localhost:$PORTA_WEB  (título conferido)"
echo "  logs  $LOGS"
echo
echo "Próximo: bash .claude/skills/subir-e-validar/scripts/semear.sh"
