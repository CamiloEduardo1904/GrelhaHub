#!/usr/bin/env bash
#
# Semeia um cenário de demonstração — TUDO pelas rotas da API.
#
# É a mesma doutrina do harness de integração: dado que entra por caminho que o sistema não tem
# escapa de validator, de autorização e do vínculo de unidade. A única exceção é envelhecer pedido,
# porque `Pedido.CriadoEm` não tem setter — e está certo que não tenha.
#
# O que fica de pé:
#   brasa-da-esquina  dona@brasa.local   senha-de-demo-1234   fila cheia, série de 28 dias
#   casa-vazia        vazia@brasa.local  senha-de-demo-1234   nenhum pedido (cadência ociosa)
set -euo pipefail

API=${API:-http://localhost:5000/api}
CONTEINER=${CONTEINER:-grelhahub-dev}
SENHA=senha-de-demo-1234

json() { python3 -c "import sys,json;print(json.load(sys.stdin)$1)"; }

# CPF válido a partir de uma raiz de 9 dígitos — o CPF é único em todo o sistema (RN-GER-023),
# então cada restaurante do cenário precisa do seu.
cpf() {
  python3 -c "
base=[int(c) for c in '$1']
def dig(ns):
    p=len(ns)+1
    s=sum(n*(p-i) for i,n in enumerate(ns))
    r=(s*10)%11
    return 0 if r==10 else r
d1=dig(base); d2=dig(base+[d1])
print(''.join(map(str,base+[d1,d2])))"
}

echo "── login de plataforma (RN-GER-014) ─────────────────────────────────────"
PLAT=$(curl -s -X POST "$API/v1/autenticacao/login" -H 'Content-Type: application/json' \
  -d '{"email":"suporte@grelhahub.local","senha":"suporte-local-1234"}' | json "['token']")

# ⚠ NÃO existe rota anônima de cadastro de restaurante, e a ausência é decisão de produto
# (SPEC-GER-007 §4): ninguém ganha conta sem contrato. Quem cria é o usuário de plataforma.
casa() { # $1 nome  $2 cnpj  $3 raiz de cpf  $4 email
  curl -s -X POST "$API/v1/plataforma/estabelecimentos" \
    -H 'Content-Type: application/json' -H "Authorization: Bearer $PLAT" \
    -d "{\"razaoSocial\":\"$1 LTDA\",\"nomeFantasia\":\"$1\",\"cnpj\":\"$2\",
         \"telefone\":\"1133224455\",\"nomeAdministrador\":\"Dona\",
         \"sobrenomeAdministrador\":\"Da Brasa\",\"cpfAdministrador\":\"$(cpf "$3")\",
         \"email\":\"$4\",\"senha\":\"$SENHA\",\"modulos\":null}"
}

echo "── cadastrando as duas casas ────────────────────────────────────────────"
SLUG=$(casa "Brasa da Esquina" 60600600000191 123456789 dona@brasa.local | json "['slug']")
casa "Casa Vazia" 70700700000191 987654321 vazia@brasa.local >/dev/null
echo "   $SLUG  ·  casa-vazia (sem pedido, para a cadência ociosa)"

TOKEN=$(curl -s -X POST "$API/v1/autenticacao/login" -H 'Content-Type: application/json' \
  -d "{\"email\":\"dona@brasa.local\",\"senha\":\"$SENHA\"}" | json "['token']")

# O cadastro semeia um cardápio de exemplo — sete produtos em três categorias.
PRODUTOS=$(curl -s "$API/publico/$SLUG/cardapio" | python3 -c "
import sys,json
d=json.load(sys.stdin)
print('\n'.join(p['id'] for c in d['categorias'] for p in c['produtos']))")

# ⚠ RN-CRM-001 — o cliente é casado pelo TELEFONE. Repetir o número faz todos os pedidos
# apontarem para a mesma linha de cliente, e o NOME dela vira o do último pedido: seis comandas
# com nomes diferentes viram seis vezes o mesmo nome na tela. Um telefone por pedido.
#
# ⚠⚠ O contador vive num ARQUIVO, e não numa variável — e isto foi um defeito real deste script.
# `pedido()` é sempre chamada dentro de `$( )`, que roda em SUBSHELL: um `TEL=$((TEL+1))` lá dentro
# incrementa a cópia e morre com ela, então todos os pedidos saíam com o MESMO telefone e a tela
# repetia o mesmo nome de cliente seis vezes. O arquivo atravessa o subshell.
CONTADOR=$(mktemp); echo 0 > "$CONTADOR"
trap 'rm -f "$CONTADOR"' EXIT

telefone() {
  local n=$(( $(cat "$CONTADOR") + 1 ))
  echo "$n" > "$CONTADOR"
  printf '119%08d' "$n"
}

# ⚠ Sem acento no corpo: o Git Bash do Windows manda o byte em cp1252 e a API recusa o JSON.
pedido() { # $1 índice do produto  $2 quantidade  $3 nome
  local pid; pid=$(echo "$PRODUTOS" | sed -n "$1p")
  local TEL; TEL=$(telefone)
  curl -s -X POST "$API/publico/$SLUG/pedidos" -H 'Content-Type: application/json' \
    -d "{\"nomeCliente\":\"$3\",\"telefone\":\"$TEL\",
         \"observacao\":\"Sem cebola e capricha na maionese\",
         \"itens\":[{\"produtoId\":\"$pid\",\"quantidade\":$2,\"observacao\":null}]}" | json "['id']"
}

anda() { local id=$1; shift
  for acao in "$@"; do
    curl -s -o /dev/null -X POST "$API/v1/pedidos/$id/$acao" -H "Authorization: Bearer $TOKEN"
  done
}

# RN-VEN-018 — confirmar, e depois avancar até onde se quer parar.
echo "── os quatro estados VIVOS: entram na fila (RN-VEN-054) ─────────────────"
ABERTO_A=$(pedido 1 1 "Marina Alves");                                    echo "   aberto      $ABERTO_A"
ABERTO_B=$(pedido 2 2 "Rogerio Pinto");                                   echo "   aberto      $ABERTO_B"
CONFIRMADO=$(pedido 3 2 "Camila Souza");   anda "$CONFIRMADO" confirmar;   echo "   confirmado  $CONFIRMADO"
PRODUCAO=$(pedido 4 3 "Tiago Nunes");      anda "$PRODUCAO" confirmar avancar
                                                                          echo "   producao    $PRODUCAO"
PRONTO_A=$(pedido 5 1 "Helena Costa");     anda "$PRONTO_A" confirmar avancar avancar
                                                                          echo "   pronto      $PRONTO_A"
PRONTO_B=$(pedido 2 2 "Bruno Martins");    anda "$PRONTO_B" confirmar avancar avancar
                                                                          echo "   pronto      $PRONTO_B"

echo "── os três que SAEM da fila ─────────────────────────────────────────────"
ENTREGUE=$(pedido 1 1 "Ja entregue");   anda "$ENTREGUE" confirmar avancar avancar avancar
                                                                          echo "   entregue    $ENTREGUE"
FINAL=$(pedido 6 1 "Ja finalizado");    anda "$FINAL" confirmar avancar avancar avancar avancar
                                                                          echo "   finalizado  $FINAL"
CANCELADO=$(pedido 7 1 "Desistiu");     anda "$CANCELADO" confirmar
curl -s -o /dev/null -X POST "$API/v1/pedidos/$CANCELADO/cancelar" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"motivo":"Cliente desistiu na porta"}';                            echo "   cancelado   $CANCELADO"

# ⚠ A fila de PICO — o cenário que revelou o defeito de escala de 19/08/2026.
#
# Com 6 pedidos qualquer desenho de fila parece certo. Com 60, a versão que
# desenhava comanda completa dava quinze linhas de cartão — seis telas de rolagem, com a
# coluna "pronto" embaixo de todas elas. Meça com PICO antes de decidir layout de fila.
#
#   PICO=54 bash semear.sh    → 60 pedidos vivos ao todo
PICO=${PICO:-0}

if [ "$PICO" -gt 0 ]; then
  echo "── PICO: mais $PICO pedidos, na proporção de uma NOITE REAL ─────────────"
  #
  # ⚠⚠ A proporção importa mais que o número, e errá-la faz a medida mentir. A primeira
  # versão deste bloco deixava quase tudo VIVO — e com 59 dos 62 na casa, o agrupamento de
  # `RN-VEN-057` não tinha o que economizar: a queixa que originou a regra é a das onze da
  # noite, quando o que falta preparar divide espaço com DEZENAS de comandas já concluídas.
  #
  # Às 23h de uma hamburgueria: a maioria já saiu, um punhado está na chapa, alguns
  # desistiram. É isso que se semeia aqui.
  for i in $(seq 1 "$PICO"); do
    ID=$(pedido $(( (i % 7) + 1 )) $(( (i % 3) + 1 )) "Pico $i")

    case $(( i % 10 )) in
      0)     anda "$ID" confirmar
             curl -s -o /dev/null -X POST "$API/v1/pedidos/$ID/cancelar"                -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json'                -d '{"motivo":"Cliente desistiu na porta"}' ;;   # ~10% cancelados
      1|2)   anda "$ID" confirmar avancar avancar ;;             # ~20% na casa, prontos
      3)     : ;;                                                # ~10% na casa, abertos
      *)     anda "$ID" confirmar avancar avancar avancar avancar ;;  # ~60% concluídos
    esac
  done
  echo "   $PICO acrescentados — ~60% concluídos, ~30% na casa, ~10% cancelados"
fi

echo "── série longa, para o painel de operação ───────────────────────────────"
HISTORICO=()
for i in $(seq 1 14); do
  ID=$(pedido $(( (i % 7) + 1 )) $(( (i % 3) + 1 )) "Historico $i")
  anda "$ID" confirmar avancar avancar avancar avancar
  HISTORICO+=("$ID")
done
echo "   ${#HISTORICO[@]} finalizados"

# ⚠⚠ A ÚNICA escrita direta no banco, e o motivo está no comentário da SKILL: `Pedido.CriadoEm`
# não tem setter. `data_operacional` é recalculada pelo PostgreSQL — a coluna é
# GENERATED ALWAYS AS ... STORED (RN-GER-006, ADR-0011).
envelhece() {
  docker exec "$CONTEINER" psql -U grelhahub -d grelhahub -q \
    -c "UPDATE pedido SET dh_criacao = dh_criacao - interval '$2 days' WHERE id='$1';"
}

echo "── envelhecendo (RN-GER-006) ────────────────────────────────────────────"
n=1
for id in "${HISTORICO[@]}"; do envelhece "$id" $((n*2)); n=$((n+1)); done
echo "   histórico espalhado por 28 dias"

# O caso da fila que só um pedido de OUTRO dia operacional exercita: `numero` reinicia a cada
# virada, então a comanda passa a exibir a data.
envelhece "$CONFIRMADO" 1
echo "   1 pedido VIVO no dia operacional anterior — a comanda mostra a data"

echo
echo "✔ cenário de pé"
curl -s "$API/v1/pedidos/fila" -H "Authorization: Bearer $TOKEN" | python3 -c "
import sys,json
d=json.load(sys.stdin)
print(f'  fila: {len(d)} pedidos')
for p in d[:8]: print(f\"    #{p['numero']:<3} {p['status']:<11} {p['dataOperacional']}\")
if len(d) > 8: print(f'    ... e mais {len(d) - 8}')
# ⚠ RN-VEN-054 — cinco campos, e a lista existe para DENUNCIAR um sexto. Se aparecer
# 'cliente', 'telefone' ou 'itens' aqui, alguém reabriu o contrato do painel de senha.
print('  campos:', ', '.join(d[0].keys()) if d else '(vazia)')"
echo
echo "  entrar em http://localhost:5173/entrar  →  dona@brasa.local / $SENHA"
