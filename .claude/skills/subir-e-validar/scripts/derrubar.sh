#!/usr/bin/env bash
#
# Derruba tudo: API, Vite e o contêiner — com o volume.
#
# ⚠ Dado de demonstração que sobrevive à sessão seguinte é dado que alguém vai confundir com dado
# real. O contêiner sai inteiro, não é parado.
set -uo pipefail

CONTEINER=${CONTEINER:-grelhahub-dev}
PORTA_WEB=${PORTA_WEB:-5173}

echo "── API ──────────────────────────────────────────────────────────────────"
taskkill //F //IM GrelhaHub.Api.exe 2>/dev/null | tail -1 || echo "   já estava parada"

echo "── Vite ─────────────────────────────────────────────────────────────────"
#
# ⚠⚠ MATA PELO PID DA PORTA, nunca por nome de imagem.
#
# A versão anterior fazia `taskkill //F //IM node.exe`, o que derruba **todo processo Node da
# máquina** — inclusive o servidor de desenvolvimento de outro projeto do usuário, que ele não
# pediu para encerrar e que pode estar com trabalho não salvo do outro lado. Numa máquina de
# desenvolvimento é comum haver dois ou três.
#
# O PID sai do `netstat` pela porta que ESTE script subiu, e mais nada é tocado.
pid=$(netstat -ano | grep -E ":$PORTA_WEB[^0-9].*LISTENING" | awk '{print $NF}' | head -1)

if [ -n "$pid" ]; then
  taskkill //F //PID "$pid" 2>/dev/null | tail -1 || echo "   PID $pid já havia saído"
else
  echo "   nada escutando em :$PORTA_WEB"
fi

echo "── PostgreSQL ───────────────────────────────────────────────────────────"
docker rm -f "$CONTEINER" 2>/dev/null || echo "   já estava removido"

sleep 2
curl -s -m 3 http://localhost:5000/api/health >/dev/null 2>&1 && echo "⚠ API ainda responde" || echo "✔ API parada"
curl -s -m 3 -o /dev/null "http://localhost:$PORTA_WEB/" 2>/dev/null && echo "⚠ :$PORTA_WEB ainda responde (outro projeto?)" || echo "✔ Vite parado"

# ⚠ As tarefas de background da API e do Vite terminam com CÓDIGO 1 quando mortas. É esperado, e
# não é falha da validação — não vá caçar defeito nelas.
