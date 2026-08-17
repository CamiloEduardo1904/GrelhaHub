# ADR-0011 — Dia operacional como dimensão do modelo

**Status:** Aceito · **Data:** Agosto/2026 · **Implementa:** RN-GER-006 ⚠⚠

## Contexto

O dia de trabalho de uma hamburgueria não termina à meia-noite. Um pedido registrado à
01h30 de domingo pertence, para todo efeito de negócio, ao **sábado**: foi o mesmo turno, o
mesmo caixa, a mesma equipe, o mesmo estoque.

O catálogo classifica RN-GER-006 como a regra mais subestimada, e a descrição do problema é
literal:

> Se o modelo usar `date_trunc('day', dh_criacao)`, o faturamento de sábado à noite cai em
> domingo, o fechamento de caixa não bate e o relatório de melhor dia da semana fica errado
> em definitivo.

O adjetivo importante é **em definitivo**. Um relatório que agrupa errado não dá erro — dá
um número plausível. Ninguém percebe até alguém conferir manualmente, e a essa altura todas
as análises da Sprint 7 & 8 já foram construídas sobre a agregação errada.

## Decisão

O **dia operacional** é uma propriedade de primeira classe do modelo, não um cálculo de
relatório.

- Vai das **04h00** (`PAR-GER-001`) às **03h59** do dia seguinte
- Toda tabela com data de evento comercial — `pedido`, `pagamento`, `movimento_estoque`,
  `movimento_caixa`, `caixa_diario`, `lancamento_financeiro` — tem a coluna
  **`data_operacional`**, do tipo `date`
- A coluna é **gerada** (`GENERATED ALWAYS AS`), derivada de
  `(dh_evento - intervalo_de_virada)::date`, e **indexada**
- Nenhuma consulta de negócio agrupa por `dh_criacao`. Toda agregação por dia, semana ou
  mês usa `data_operacional`
- A dimensão `dim_tempo` do BI é construída sobre `data_operacional`
- O fechamento de caixa (RN-FIN-007) e a apuração de faturamento operam sobre o dia
  operacional, não o civil

### Sobre a hora de virada ser parâmetro

`PAR-GER-001` é configurável (04h00 é apenas o valor inicial) — mas há uma consequência que
precisa estar declarada: **a coluna gerada não pode depender de um valor que muda**, porque
`GENERATED ALWAYS AS` exige expressão imutável, e uma mudança retroativa reclassificaria
vendas passadas.

Resolução: a coluna gerada usa a constante da migration. Alterar `PAR-GER-001` é uma
**operação de migration** — nova coluna gerada com o novo valor e recomputação explícita do
histórico, decidida caso a caso — e não uma edição de parâmetro em tela. O parâmetro existe
para documentar o valor vigente e alimentar a aplicação e os relatórios; a virada em si é
estrutural.

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| Calcular na consulta (`WHERE dh - interval '4 hours'`) | Impede índice, repete a lógica em cada consulta e uma omissão passa despercebida — exatamente o modo de falha que a regra descreve |
| View com a coluna calculada | Melhor que nada, mas nada impede uma consulta de ir à tabela base. A coluna gerada torna o caminho errado indisponível |
| Coluna comum preenchida pela aplicação | Segunda fonte de verdade: uma carga direta ou um script de correção esquece de preencher, e a divergência é silenciosa |
| Ignorar e usar o dia civil | O erro é permanente e contamina todo o BI |

## Consequências

**Positivas**
- Agrupar errado deixa de ser possível por descuido — a coluna certa está ali, indexada
- O fechamento de caixa bate com o faturamento do dia sem reconciliação manual
- A Sprint 7 & 8 recebe uma dimensão tempo correta desde a carga inicial
- É um ponto forte de defesa na banca: mostra domínio de negócio real, não de CRUD

**Negativas / custos aceitos**
- Coluna adicional em várias tabelas, com índice — custo de armazenamento pequeno e
  conhecido
- Mudar a hora de virada é operação de migration, não de configuração
- O time precisa ter a disciplina de nunca agrupar por `dh_criacao`. **Mitigação:** revisão
  de código verifica isso explicitamente, e a carga inicial da Sprint 7 & 8 gera vendas de
  madrugada de propósito, para que o erro apareça em teste se alguém escorregar
