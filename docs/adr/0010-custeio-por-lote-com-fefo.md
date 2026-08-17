# ADR-0010 — Custeio específico por lote com consumo FEFO

**Status:** Aceito · **Data:** Agosto/2026 · **Resolve:** conflito C1 do
[cruzamento](../07-cruzamento-catalogo-x-levantamento.md#c1--método-de-custeio-do-estoque--estrutural)
· **Supersede:** a regra de custo médio ponderado do Documento de Levantamento (RN-042, revogada)

## Contexto

Há duas formas de responder "quanto custou a carne deste lanche":

1. **Custo médio ponderado** — o insumo tem um custo unitário único, recalculado a cada
   entrada pela média ponderada. Simples: uma coluna no insumo, uma fórmula na entrada.
2. **Custo específico por lote** — cada entrada cria um lote com seu próprio custo, e a
   saída custa o que o lote consumido custou. Exige a entidade `lote` e rastreabilidade em
   cada movimento.

O Documento de Levantamento adotou a primeira; o Catálogo de Regras, a segunda
(RN-EST-004, RN-EST-007, RN-EST-011). Não são variações de detalhe — são modelos de dados
diferentes, e a escolha precisa acontecer antes da Sprint 3 & 4.

**O fator decisivo não é o custo — é a validade.** O domínio é de insumos perecíveis: pão,
carne, queijo. Essa é a dor mapeada desde o início do projeto.

## Decisão

**Custo específico por lote, com consumo FEFO** (*First Expired, First Out* — sai primeiro
o que vence primeiro).

- Todo movimento de estoque refere-se a um lote específico (RN-EST-004)
- Todo lote registra custo unitário de aquisição e, se perecível, data de validade
  (RN-EST-005, RN-EST-010)
- A saída consome prioritariamente o lote de menor data de validade entre os disponíveis
  (RN-EST-007)
- O custo da saída é o custo do lote efetivamente consumido (RN-EST-011)
- O saldo de um lote nunca é negativo (RN-EST-009)

Uma saída pode consumir **vários lotes** quando o primeiro não cobre a quantidade — o que
gera múltiplos movimentos, cada um com seu custo. O custo do item vendido é a soma deles.

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| **Custo médio ponderado** (posição do levantamento) | Não modela validade, logo não implementa FEFO, não registra perda por vencimento (RN-EST-013) nem valida prazo mínimo no recebimento (RN-EST-008). Seria a escolha certa para insumo fungível e não perecível — não é o caso do domínio |
| **PEPS / FIFO por data de entrada** | Quase certo, mas erra no caso que importa: um lote comprado depois pode vencer antes (validade mais curta na origem). FEFO é FIFO corrigido pela realidade do perecível |
| **Custo médio com lote só para validade** | Mantém a rastreabilidade de validade mas descarta o custo do lote, perdendo a informação justamente onde ela já está disponível de graça |

## Consequências

**Positivas**
- Alerta de vencimento, perda por vencimento e rastreabilidade de lote saem do mesmo modelo
- O CMV reflete o custo real do insumo consumido, não uma média que suaviza a variação —
  que é exatamente o que o dono precisa ver quando a carne sobe de preço
- Atende a validade mínima no recebimento (RN-EST-008) sem estrutura adicional
- FEFO reduz perda por vencimento, o que é ganho operacional direto e demonstrável na banca

**Negativas / custos aceitos**
- Mais movimentos por venda: uma saída pode gerar N movimentos quando atravessa lotes
- A seleção FEFO é uma consulta ordenada com bloqueio dentro da transação da venda — ponto
  de contenção no pico. **Mitigação:** índice por `(unidade, insumo, validade)` filtrando
  lotes com saldo, e a seleção acontece na confirmação do pedido, não a cada digitação
- O cadastro fica mais trabalhoso: toda entrada exige informar lote, custo e validade
- Inventário passa a ser contagem **por lote**, não por insumo

## Ressalva registrada

A pergunta 6 da [fila de validação](../05-fila-de-validacao-com-cliente.md) é *"Se comprou
carne a R$ 30 e depois a R$ 35, qual preço vocês consideram no custo do lanche?"*.

Se o operador responder **"faço média"**, há divergência entre a prática real e este ADR.
Ainda assim o lote permanece necessário — por causa da validade. Nesse cenário, o modelo
mantém lote e custo por lote como fonte de verdade e **expõe o custo médio como visão
derivada**, atendendo à prática do operador sem perder informação. A recíproca não vale:
custo médio como fonte de verdade não permite reconstruir o custo por lote.

Este ADR é revisado se a entrevista mudar o estado de RN-EST-011 para `REJ`.
