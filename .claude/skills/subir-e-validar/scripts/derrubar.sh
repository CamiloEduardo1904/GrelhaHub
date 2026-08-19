#!/usr/bin/env bash
#
# Derruba tudo: API, Vite e o contêiner — com o volume.
#
# ⚠ Dado de demonstração que sobrevive à sessão seguinte é dado que alguém vai confundir com dado
# real. O contêiner sai inteiro, não é parado.
set -uo pipefail

CONTEINER=${CONTEINER:-grelhahub-dev}

echo "── API ──────────────────────────────────────────────────────────────────"
taskkill //F //IM GrelhaHub.Api.exe 2>/dev/null | tail -1 || echo "   já estava parada"

echo "── Vite ─────────────────────────────────────────────────────────────────"
# ⚠ Isto mata TODO node.exe da sessão. Em máquina com outro projeto Node de pé, encerre pelo PID.
taskkill //F //IM node.exe 2>/dev/null | tail -1 || echo "   já estava parado"

echo "── PostgreSQL ───────────────────────────────────────────────────────────"
docker rm -f "$CONTEINER" 2>/dev/null || echo "   já estava removido"

sleep 2
curl -s -m 3 http://localhost:5000/api/health >/dev/null 2>&1 && echo "⚠ API ainda responde" || echo "✔ API parada"
curl -s -m 3 -o /dev/null http://localhost:5173/ 2>/dev/null && echo "⚠ Vite ainda responde" || echo "✔ Vite parado"

# ⚠ As tarefas de background da API e do Vite terminam com CÓDIGO 1 quando mortas. É esperado, e
# não é falha da validação — não vá caçar defeito nelas.
