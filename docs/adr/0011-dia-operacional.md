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

> ⚠⚠ **Revisado em 18/08/2026 — a virada deixou de ser constante do sistema.** Decisão do dono do
> produto: horário de funcionamento é cadastro da loja, e a virada sai dele (`RN-GER-036`,
> [`SPEC-GER-009`](../specs/SPEC-GER-009-horario-endereco-e-dia-operacional.md)). O que **não**
> mudou é a razão de a coluna existir: agrupar por data de criação continua sendo o defeito que
> este ADR previne. O que mudou é de onde vem o número.
>
> A expressão passou a ler **colunas da própria linha** — `pedido.hora_virada_operacional` e
> `pedido.fuso_operacional`, copiadas da unidade na inserção. É o que preserva o argumento original
> ("alterar a expressão reclassificaria vendas passadas") num mundo em que cada casa tem a sua
> virada: editar o horário muda os pedidos seguintes, não os gravados.

O **dia operacional** é uma propriedade de primeira classe do modelo, não um cálculo de
relatório.

- Vai da **virada cadastrada da unidade** (`RN-GER-036`; `PAR-GER-001`, 04h00, para quem não
  cadastrou) até um minuto antes dela no dia seguinte
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
- ⚠ **Trocar a expressão da coluna gerada derruba os índices que a contêm** (`DROP COLUMN` leva o
  índice junto, sem aviso). A migration de 18/08/2026 recria `uq_pedido_numero` e
  `ix_pedido_dia_status` à mão — quem mexer na expressão outra vez precisa fazer o mesmo
- ⚠ **Duas colunas por linha transacional** (`hora_virada_operacional`, `fuso_operacional`) para
  que a virada seja por unidade sem reclassificar histórico. É redundância deliberada, do mesmo
  tipo do preço praticado de `RN-VEN-008`
- **A trilha de auditoria ficou com a régua da plataforma** (04h00), e a divergência está declarada
  na §4 da `SPEC-GER-009`: a plataforma não tem unidade, logo não tem virada
- Coluna adicional em várias tabelas, com índice — custo de armazenamento pequeno e
  conhecido
- Mudar a hora de virada é operação de migration, não de configuração
- O time precisa ter a disciplina de nunca agrupar por `dh_criacao`. **Mitigação:** revisão
  de código verifica isso explicitamente, e a carga inicial da Sprint 7 & 8 gera vendas de
  madrugada de propósito, para que o erro apareça em teste se alguém escorregar
