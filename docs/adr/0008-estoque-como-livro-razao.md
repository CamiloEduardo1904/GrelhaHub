# ADR-0008 — Estoque como livro-razão de movimentos

**Status:** Aceito · **Data:** Agosto/2026 · **Revisado:** Agosto/2026, na consolidação com
o Catálogo de Regras · **Implementa:** RN-EST-001, RN-EST-002, RN-EST-003 ·
**Complementado por:** [ADR-0010](0010-custeio-por-lote-com-fefo.md)

> **Nota de revisão.** A versão original deste ADR previa uma coluna `saldo_atual` editável
> no insumo como cache, e afirmava que o cancelamento de pedido devolveria estoque por
> estorno. Ambas as posições foram corrigidas no cruzamento com o Catálogo de Regras — ver
> [§ Correções](#correções-aplicadas-na-revisão) ao final.

## Contexto

Há duas formas de representar o saldo de um insumo:

1. **Campo mutável** — a tabela `insumo` tem uma coluna `saldo`, atualizada a cada venda,
   compra ou ajuste.
2. **Livro-razão** — cada evento gera um registro imutável em `movimento_estoque`, e o
   saldo é a soma algébrica dos movimentos.

A opção 1 é mais simples e mais rápida de ler. Mas o domínio impõe exigências que ela não
atende:

- **Perda em vez de devolução no cancelamento** (RN-EST-018): item cancelado após entrar em
  produção gera **perda com motivo**, não devolução ao estoque. Isso só é representável se
  o evento existir como registro próprio, classificado.
- **Auditoria de ajuste** (RN-EST-014): a contagem de inventário gera movimento de ajuste
  com justificativa obrigatória, rastreável a quem contou.
- **Custo por lote** (RN-EST-011): o custo da saída é o do lote consumido. Um campo de saldo
  agregado não tem onde guardar isso.
- **Relatório de consumo por período** (RF-REL-006): impossível de produzir a partir de um
  saldo atual — exige o histórico dos eventos.

Um campo `saldo` sobrescrito destrói exatamente a informação de que esses requisitos
dependem. Quando o saldo diverge da realidade, não há como descobrir onde divergiu.

## Decisão

`movimento_estoque` é um **livro-razão imutável**. Cada linha registra: unidade, insumo,
**lote** (RN-EST-004), tipo do movimento, quantidade, custo unitário do momento, documento
de origem, usuário e data/hora (RN-EST-003).

**Tipos de movimento** (RN-EST-002, domínio fechado): entrada por compra, saída por venda,
saída por produção, entrada por produção, perda, ajuste de inventário, transferência,
devolução.

Movimentos **nunca** são alterados nem excluídos. Correção se faz por movimento contrário
classificado, não por edição (RN-GER-002).

### O saldo é derivado — e a materialização não é uma exceção a isso

RN-EST-001 é categórica: *"O saldo de um insumo nunca é um valor armazenado e editável. É
sempre derivado da soma algébrica dos seus movimentos."*

Agregar todo o histórico a cada tela é inviável, mas a solução **não** é uma coluna
mantida pela aplicação. O saldo materializa em **view materializada** por
`(unidade, insumo, lote)`, atualizada na transação do movimento.

A distinção não é de desempenho — é de **autoridade**:

| | View materializada | Coluna editável |
|---|---|---|
| Fonte de verdade | Os movimentos | Ambíguo — dois lugares |
| Recomputável | Sim, por definição | Não |
| Divergência silenciosa | Impossível: `REFRESH` reconstrói | Possível, e é o modo de falha típico |
| Escrita fora da API | Continua correta após refresh | Corrompe o saldo |

Uma coluna editável é uma segunda fonte de verdade que pode divergir sem que ninguém
perceba. A view é derivada por construção.

O mesmo princípio se aplica ao **caixa**: `movimento_caixa` é livro-razão, e o valor
esperado no fechamento é derivado dele (RN-FIN-015).

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| Só o campo `saldo` mutável | Não atende perda classificada, auditoria, custo por lote nem relatório de consumo — os requisitos que motivam a decisão |
| Livro-razão sem materialização | Correto, mas cada exibição de estoque agregaria todo o histórico; degrada com o tempo |
| Coluna desnormalizada mantida pela aplicação *(posição original deste ADR)* | Cria segunda fonte de verdade. Rejeitada por RN-EST-001 |
| Event sourcing completo no domínio | Resolve o mesmo problema com muito mais maquinaria (event store, projeções, replay) do que o porte do sistema justifica |

## Consequências

**Positivas**
- Perda, auditoria e relatórios de consumo saem naturalmente do modelo
- Divergência de estoque é sempre investigável até o evento que a causou
- O custo unitário em cada movimento viabiliza o custo específico por lote (RN-EST-011) e o
  congelamento do custo histórico (RN-PRO-002)
- A view materializada é reconstruível a qualquer momento, o que torna a recuperação de
  incidente trivial

**Negativas / custos aceitos**
- A tabela de movimentos cresce continuamente — exige índice por
  `(unidade_id, insumo_id, lote_id, data_operacional)` e, no longo prazo, política de
  arquivamento
- `REFRESH MATERIALIZED VIEW CONCURRENTLY` exige índice único na view e tem custo; para o
  volume de uma unidade única é aceitável, e a alternativa (view comum sobre índice bem
  escolhido) permanece disponível se o refresh incomodar
- Mais escritas por venda: um movimento por insumo da ficha técnica — e mais de um por
  insumo quando a saída atravessa lotes (ADR-0010)
- Toda escrita passa por um único ponto no domínio, dentro de transação (RNF-016)

## Correções aplicadas na revisão

| Posição original | Correção | Motivo |
|------------------|----------|--------|
| `saldo_atual` desnormalizado no insumo, atualizado pela aplicação | View materializada por `(unidade, insumo, lote)` | RN-EST-001 proíbe saldo armazenado e editável. Conflito C3 do cruzamento |
| Cancelamento devolve o saldo por movimentos de estorno | Cancelamento **após** produção gera perda com motivo (RN-EST-018); **antes** da produção não gera movimento (RN-EST-019) | O lanche pronto não volta à geladeira. Devolver o saldo esconderia a perda — que é o número que o dono precisa ver. Conflito C2 |
| Movimento referencia apenas o insumo | Movimento referencia **lote** obrigatoriamente | RN-EST-004, base de FEFO e de validade. Ver [ADR-0010](0010-custeio-por-lote-com-fefo.md) |
