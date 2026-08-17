# Cruzamento — Catálogo de Regras × Documento de Levantamento

| Campo | Valor |
|-------|-------|
| Objetivo | Auditar a convergência entre o *Catálogo de Regras de Negócio v0.1* (117 regras, P3 — Análise de Negócios, 15/08/2026) e o *Documento de Levantamento v1.0* (40 regras, 16/08/2026) |
| Resultado | Catálogo consolidado v1.0 — [`03-catalogo-de-regras-de-negocio.md`](03-catalogo-de-regras-de-negocio.md) |
| Data | Agosto/2026 |

---

## 1. Por que este documento existe

Os dois artefatos foram produzidos de forma independente, a partir do mesmo domínio.
Isso é uma oportunidade rara: onde **convergem**, a regra ganha confiança sem entrevista;
onde **divergem**, a divergência aponta exatamente onde o entendimento do domínio ainda é
frágil — e é essa lista que a entrevista precisa resolver.

Este documento registra o cruzamento regra a regra. Ele é evidência de método para a banca
e é o que justifica cada decisão presente no catálogo consolidado.

### Resumo do cruzamento

| Situação | Qtd. | Significado |
|----------|------|-------------|
| **Convergentes** | 14 | Regra formulada de forma equivalente nos dois documentos — confiança alta |
| **Conflitantes** | 9 | Os documentos afirmam coisas incompatíveis — exigiram decisão explícita |
| **Lacuna do levantamento** | 71 | Presente só no catálogo — o levantamento não enxergou |
| **Complemento do levantamento** | 17 | Presente só no levantamento e válido — incorporado ao catálogo |
| **Divergência de escopo** | 3 módulos | Compras, RH e Fiscal: o levantamento excluiu o que o catálogo trata como obrigatório |

**Veredito geral:** o Catálogo é o artefato tecnicamente superior e passa a ser a fonte de
verdade. O Levantamento contribui 17 regras genuínas e permanece como documento de
contexto (visão, escopo, atores, requisitos funcionais, RNFs) — camadas que o Catálogo,
por ser um catálogo de regras, não cobre.

---

## 2. Por que o Catálogo prevalece

Três razões estruturais, não de preferência:

1. **Taxonomia formal.** O catálogo classifica cada regra por *tipo* (Definição, Fato,
   Restrição, Cálculo, Inferência, Ação), *estado de validação* (HIP/VAL/CNF/ESC/REJ) e
   *mecanismo de implementação previsto* (MOD, NN, UQ, FK, CK, EX, ENU, GEN, TRG, VW, APP,
   PAR). O levantamento tem apenas prosa numerada. A taxonomia é o que torna a regra
   transcritível em spec e verificável no modelo.

2. **Honestidade epistêmica.** O catálogo declara as 117 regras como **hipóteses** e traz
   a fila de validação com as perguntas a fazer. O levantamento afirma suas 40 regras como
   se fossem fato — o que é metodologicamente pior, mesmo quando a regra está certa.

3. **Profundidade de domínio.** Lote e validade, FEFO, sub-receita recursiva, percentual de
   perda de preparo, dia operacional, conversão de unidade — o catálogo enxergou o que
   distingue um modelo de restaurante real de um CRUD de pedidos.

O que o levantamento tem de melhor: **precisão nos casos de borda numéricos** (arredondamento
de divisão de conta, troco, teto do total) e a **estrutura de modificadores com regra de
seleção**, que o catálogo trata de forma mais rasa.

---

## 3. Convergências — regras confirmadas pelos dois documentos

Formuladas independentemente e compatíveis. **Confiança alta mesmo antes da entrevista** —
mas ainda `HIP`, porque concordância entre dois analistas não é validação com o operador.

| # | Regra | Catálogo | Levantamento | Observação |
|---|-------|----------|--------------|------------|
| 1 | Nenhum registro transacional é excluído fisicamente | RN-GER-002 | RN-061 | Idênticas |
| 2 | Valores monetários com 2 casas decimais | RN-GER-003 | RN-062 (parte) | Idênticas |
| 3 | Datas com fuso, operação em America/Sao_Paulo | RN-GER-005 | RN-063 | Idênticas |
| 4 | Vínculo de todo registro a uma unidade, com uma só unidade em operação | RN-GER-009 | RN-060 + ADR-0005 | **Mesma conclusão por caminhos diferentes** — o levantamento chegou nela pelo posicionamento de produto, o catálogo pela expansão futura |
| 5 | Usuário pertence a perfil que determina permissões | RN-GER-010 | RF-SEG-002 | Idênticas |
| 6 | Pedido sem itens não pode ser confirmado | RN-VEN-002 | RN-011 | Idênticas |
| 7 | Uma mesa tem no máximo uma comanda aberta | RN-VEN-006 | RN-020 | O catálogo aponta o mecanismo: `EXCLUDE` constraint |
| 8 | Preço registrado no momento da venda não muda depois | RN-VEN-008 | RN-045 (parte) | Idênticas — ambas identificaram o congelamento histórico |
| 9 | Desconto acima de limite exige gerência com registro | RN-VEN-013 | RN-013 | O catálogo parametriza o limite; o levantamento cravou 10% — **ver conflito C7** |
| 10 | Taxa de serviço sobre consumo de salão, não sobre delivery | RN-VEN-014 | RN-023 | Idênticas |
| 11 | Produto inativo não entra em pedido novo, permanece no histórico | RN-VEN-020 | RN-002 | Idênticas |
| 12 | Comanda fechada quando pagamentos igualam o total | RN-VEN-024 | RN-017 | Idênticas |
| 13 | Nova versão de ficha técnica não altera custo de pedidos passados | RN-PRO-002 | RN-045 | Idênticas |
| 14 | Caixa fechado é imutável / não reabre | RN-FIN-009 | RN-054 | Idênticas |

---

## 4. Conflitos — e como foram resolvidos

Nove pontos em que os documentos afirmam coisas incompatíveis. Cada um exigiu decisão, e
cada decisão está registrada no catálogo consolidado.

---

### C1 — Método de custeio do estoque ⚠ **estrutural**

| | |
|---|---|
| **Catálogo** | RN-EST-011: custo da saída é o **custo do lote efetivamente consumido** (custo específico por lote), com consumo FEFO (RN-EST-007) e lote obrigatório em todo movimento (RN-EST-004) |
| **Levantamento** | RN-042: **custo médio ponderado**, recalculado a cada entrada, sem noção de lote |

**Incompatíveis por construção.** Não é diferença de detalhe: um exige a entidade `lote` e
rastreabilidade por lote em cada movimento; o outro exige um campo `custo_unitario` no
insumo. São modelos de dados diferentes.

**Decisão: prevalece o Catálogo (custo específico por lote + FEFO).**

Razão: o domínio é de **insumos perecíveis** — pão, carne, queijo. A dor mapeada desde o
início do projeto é justamente essa. Um modelo sem lote não consegue responder "qual carne
vence primeiro", não implementa FEFO, não registra perda por vencimento (RN-EST-013) e não
suporta a validade mínima no recebimento (RN-EST-008). Custo médio ponderado seria a
escolha certa para insumo não perecível e fungível — não é o caso.

**Consequência:** [ADR-0008](adr/0008-estoque-como-livro-razao.md) foi revisado e
[ADR-0010](adr/0010-custeio-por-lote-com-fefo.md) foi criado. A regra RN-042 do levantamento
está **revogada**.

> **Atenção:** esta é a pergunta 6 da fila de validação — *"Se comprou carne a R$ 30 e depois
> a R$ 35, qual preço vocês consideram no custo do lanche?"*. Se o operador responder
> "faço média", há um conflito entre a prática real e o modelo. Ainda assim o lote é
> necessário para validade; nesse cenário o modelo mantém lote para rastreabilidade e
> expõe o custo médio como **visão derivada**, sem perder informação.

---

### C2 — Devolução de estoque no cancelamento ⚠ **estrutural**

| | |
|---|---|
| **Catálogo** | RN-EST-018: item cancelado **após** entrar em produção **não** devolve insumo — gera **perda** com motivo de cancelamento. RN-EST-019: cancelado **antes** da produção não gera movimento nenhum |
| **Levantamento** | RN-015: cancelar pedido que já baixou estoque **devolve** o saldo por movimentações de estorno |

**Decisão: prevalece o Catálogo. A regra do levantamento está factualmente errada.**

Razão: o lanche já foi montado e grelhado. A carne não volta para a geladeira — ela vai
para o lixo. Devolver o saldo ao estoque faria o sistema acreditar em insumo que não
existe fisicamente, e — pior — **esconderia a perda**, que é exatamente o número que o dono
precisa enxergar. O catálogo transforma o cancelamento em informação gerencial; o
levantamento o transformava em ficção contábil.

O acerto do levantamento foi o princípio subjacente (não apagar o movimento original).
Isso permanece, via RN-GER-002.

> Pergunta 7 da fila de validação: *"Se o cliente desiste depois que o lanche já foi feito,
> o que acontece com o lanche?"*

---

### C3 — Saldo pode ser negativo?

| | |
|---|---|
| **Catálogo** | RN-EST-009: o saldo de um lote **nunca** é negativo (`TRG`). RN-VEN-021: produto só é lançado se houver saldo de todos os insumos da ficha vigente |
| **Levantamento** | RN-046: o saldo **pode** ficar negativo (venda com estoque desatualizado), com sinalização de inconsistência |

**Decisão: prevalece o Catálogo, com ressalva registrada.**

Razão: saldo negativo é um sintoma sendo tratado como funcionalidade. Se o sistema aceita
vender o que não tem, o custo daquela venda é indeterminado (não há lote de onde tirar o
custo) e o CMV vira estimativa.

**Ressalva importante:** a pergunta 10 da fila de validação é *"Já aconteceu de vender e
descobrir que acabou o ingrediente?"* — e a resposta quase certamente será **sim**. Em
restaurante real, a cozinha improvisa e o sistema fica para trás. Bloquear a venda no pico
é um risco operacional sério: o atendente vai contornar o sistema, e aí o dado se perde de
vez.

**Resolução adotada no catálogo consolidado:** o bloqueio de RN-VEN-021 passa a ser
**parametrizável por produto** (`PAR-VEN-003`), com três modos: bloquear, avisar ou
permitir. O saldo do lote continua não podendo ficar negativo — a venda sem lote disponível
gera movimento contra um **lote de acerto** explicitamente marcado, que aparece no
relatório de divergência. O sistema nunca mente sobre o estoque, mas também nunca impede a
venda quando o dono não quer que impeça.

---

### C4 — Ciclo de vida do pedido

| | |
|---|---|
| **Catálogo** | RN-VEN-018: Aberto → Confirmado → Em produção → Pronto → Entregue → Finalizado (e Cancelado a partir de qualquer estado anterior a Finalizado) |
| **Levantamento** | ABERTO → EM_PREPARO → PRONTO → ENTREGUE → PAGO |

**Decisão: prevalece o Catálogo.**

Razão: o levantamento colapsou dois eventos distintos em um. **Confirmado** e **Em
produção** são momentos diferentes — a confirmação é o instante em que o pedido vira
compromisso (e dispara a ordem de produção, RN-PRO-008), enquanto a produção é o instante
em que a cozinha começa. Entre os dois pode haver fila. Além disso, o levantamento usou
`PAGO` como estado final do pedido, o que confunde **ciclo do pedido** com **ciclo do
pagamento**: pedido de marketplace já entra pago (RN-VEN-026) e comanda de salão paga só no
fim. Pagamento é dimensão própria, não estado do pedido.

---

### C5 — Precisão decimal de quantidades

| | |
|---|---|
| **Catálogo** | RN-GER-004: até **4** casas decimais (para gramas e mililitros) |
| **Levantamento** | RN-062: **3** casas decimais |

**Decisão: prevalece o Catálogo (4 casas).**

Razão: com percentual de perda de preparo (RN-PRO-005/006), a quantidade bruta é o
resultado de uma divisão — `líquida ÷ (1 − perda)`. Truncar em 3 casas propaga erro no
custo. Quatro casas custam nada em `numeric` e sobram para tempero medido em grama.

---

### C6 — Produto sem ficha técnica

| | |
|---|---|
| **Catálogo** | RN-PRO-001: todo produto vendável tem **exatamente uma** ficha técnica vigente em cada data (`EX TRG`) |
| **Levantamento** | RN-048: produto sem ficha técnica é permitido, reportado com custo zero e destacado no relatório |

**Decisão: prevalece o Catálogo.**

Razão: custo zero é pior que erro — é um número que parece válido e contamina o CMV
consolidado silenciosamente. Tornar a ficha obrigatória força o problema a aparecer no
cadastro, onde é barato resolver, em vez de no relatório, onde é caro descobrir.

**Complemento incorporado:** o levantamento estava certo ao prever o caso da bebida
revendida (lata de refrigerante não tem receita). Resolve-se com **ficha técnica de item
único** — a lata é insumo de si mesma, quantidade 1, perda 0. O modelo não precisa de
exceção.

---

### C7 — Limites numéricos cravados na regra

| | |
|---|---|
| **Catálogo** | Valores vivem em **parâmetros** (`PAR-VEN-001` desconto máximo, `PAR-VEN-002` taxa de serviço); a regra referencia o parâmetro |
| **Levantamento** | RN-013 crava "acima de 10%"; RN-023 crava "padrão 10%" |

**Decisão: prevalece o Catálogo.**

Razão: o próprio catálogo enuncia o princípio — *"não devem virar constante no código nem
CHECK com número cravado"*. Taxa de serviço e teto de desconto são decisão comercial do
dono, mudam sem aviso e não podem exigir deploy.

---

### C8 — Endereço fora de zona de entrega

| | |
|---|---|
| **Catálogo** | RN-LOG-004: endereço fora de qualquer zona atendida **não permite** a confirmação do pedido |
| **Levantamento** | RN-032: bloqueia **até** que a zona seja cadastrada **ou** a taxa seja informada manualmente por um gerente |

**Decisão: prevalece o Catálogo, com a exceção do levantamento registrada como pergunta.**

Razão: a exceção do levantamento é operacionalmente sensata (cliente conhecido, uma rua
além da zona), mas cria um caminho de escape que quebra a apuração de rentabilidade por
zona. Vai para a fila de validação como pergunta nova — não para o modelo, por ora.

---

### C9 — Onde as regras são implementadas ⚠ **arquitetural**

| | |
|---|---|
| **Catálogo** | Ordem de preferência explícita: `MOD → NN/UQ/FK/CK/EX/ENU → GEN → TRG → VW → APP`. *"A camada de aplicação é o último recurso, não o primeiro: o que está sendo avaliado é o modelo."* |
| **Levantamento** | ADR-0002: Clean Architecture com o domínio em C#, regras nos handlers e validators, banco como persistência |

**Este é o conflito mais interessante do cruzamento, e não é um mal-entendido — são duas
visões legítimas com finalidades diferentes.**

O catálogo é um artefato de **modelagem de dados**: as Sprints 3 a 6 avaliam o modelo, e
uma regra implementada só em C# é, para efeito de avaliação, uma regra que o modelo não
garante. O ADR-0002 é um artefato de **arquitetura de aplicação**: regras enterradas em
triggers são invisíveis, difíceis de testar e de evoluir.

**Decisão: os dois, deliberadamente, em camadas com papéis distintos.** Registrado em
[ADR-0009](adr/0009-onde-as-regras-de-negocio-vivem.md):

| Camada | Responsabilidade | Exemplo |
|--------|------------------|---------|
| **Modelo** (constraint, ENUM, EXCLUDE, GENERATED) | Invariante que **nunca** pode ser violada, por nenhum caminho — inclusive carga direta e script de manutenção | `EXCLUDE` de uma comanda aberta por mesa (RN-VEN-006) |
| **Banco** (trigger, view) | Derivação e consistência multi-tabela que precisa valer independentemente da aplicação | Saldo derivado de movimentos (RN-EST-001) |
| **Aplicação** (domínio C#, handler, validator) | Fluxo, orquestração, autorização, mensagem ao usuário e regra que muda por decisão comercial | Autorização de desconto acima do parâmetro (RN-VEN-013) |

O princípio: **o banco garante que o dado nunca fica inválido; a aplicação garante que o
usuário nunca chega até lá.** Validação duplicada aqui não é redundância acidental — é
defesa em profundidade, e a duplicação é declarada regra a regra na coluna de mecanismo.

---

## 5. Lacunas do levantamento — o que só o catálogo enxergou

71 regras. As que mudam o modelo de forma relevante:

### 5.1 Estruturais — errar aqui custa retrabalho de projeto

| Regra | O que o levantamento não viu | Impacto |
|-------|------------------------------|---------|
| **RN-GER-006** — dia operacional 04h00→03h59 | Tratou o dia de venda como dia civil | **A regra mais crítica do catálogo.** Venda de sábado 01h30 cai em domingo; faturamento, fechamento de caixa e "melhor dia da semana" ficam errados em definitivo. Exige coluna gerada e `dim_tempo` própria |
| **RN-PRO-004** — sub-receita recursiva | Ficha técnica apontava só para insumo | O molho da casa é **produto e insumo ao mesmo tempo**. Sem recursividade, não se modela. Descobrir na Sprint 7 custa uma sprint |
| **RN-EST-004/005/006/007/008** — lote e validade | Nenhuma noção de lote | Base de FEFO, perda por vencimento, custeio e rastreabilidade. É o núcleo da dor "insumos perecíveis" |
| **RN-PRO-005/006** — percentual de perda de preparo | Custo = quantidade da ficha × custo do insumo | De 10 kg de alface, boa parte vira descarte. Sem o percentual, **todo CMV fica subestimado** |
| **RN-EST-012** — conversão de unidade compra ↔ consumo | Uma unidade por insumo | Compra-se caixa, consome-se grama. Sem fator de conversão, o custo unitário não fecha |
| **RN-VEN-010** — preço com vigência | Preço era campo do produto | Sem histórico de vigência, não há como reconstruir por que uma venda antiga teve aquele valor |

### 5.2 Módulos inteiros ausentes

| Módulo | Regras | Situação no levantamento |
|--------|--------|--------------------------|
| **COM — Compras** | 9 | Só previa "entrada de estoque". Faltava pedido de compra, fornecedor com mínimo e prazo, cotação, recebimento parcial, ocorrência de divergência e sugestão de compra por previsão de demanda |
| **RH — Recursos Humanos** | 6 | **Explicitamente excluído** do escopo — ver §7 |
| **FIS — Fiscal** | 6 | **Explicitamente excluído** do escopo — ver §7 |

### 5.3 Outras lacunas relevantes

- **RN-GER-007/008** — consentimento LGPD e anonimização com preservação do histórico transacional
- **RN-PRO-008 a 012** — ordem de produção, estação de trabalho, etapas com tempo apurado, produção antecipada de sub-receita. O levantamento jogou tudo isso para "KDS, fase 2"
- **RN-EST-015** — transferência entre locais de estoque, com dois movimentos na mesma transação
- **RN-EST-017** — embalagem e descartável **são insumos** e entram na ficha técnica do produto vendido no delivery
- **RN-FIN-003** — a taxa da forma de pagamento é despesa **no momento da venda**, não no do recebimento
- **RN-FIN-005/011/012** — plano de contas, centro de custo, rateio de despesa fixa e DRE sempre derivado
- **RN-VEN-025/026 + RN-FIN-004** — prazo e taxa por forma de pagamento; pedido de marketplace entra pago, líquido de comissão
- **RN-CRM-004/005/006** — cupom com validade, tipo de benefício, mínimo, limite total e por cliente; não acumulável
- **RN-CRM-007/008** — segmentação RFM e definição de cliente em risco. **Matéria-prima direta do BI da Sprint 7 & 8**
- **RN-LOG-007** — taxa cobrada do cliente e valor pago ao entregador são grandezas distintas
- **RN-VEN-022** — produto disponível apenas em faixa de horário

---

## 6. Complementos do levantamento — incorporados ao catálogo

17 regras que o catálogo não cobre e que sobreviveram à revisão. Entram no consolidado com
origem `LEV`.

| Nova ID | Regra | Origem | Por que importa |
|---------|-------|--------|-----------------|
| RN-VEN-027 | Grupo de modificadores com seleção mínima e máxima; obrigatório quando mínimo ≥ 1 | RN-003 | **A lacuna mais relevante do catálogo.** RN-VEN-015 admite adicional e remoção, mas não modela a *regra de seleção* — "escolha exatamente 1 ponto da carne", "até 3 adicionais". Sem isso, não há validação de composição |
| RN-VEN-028 | Preço do item = (preço praticado + soma dos ajustes) × quantidade | RN-004 | Torna explícito como o ajuste entra no cálculo — RN-VEN-016 diz *que* altera, não *como* |
| RN-VEN-029 | O total do pedido nunca é negativo | RN-012 | Desconto + cupom podem, somados, ultrapassar o total |
| RN-VEN-030 | Em espécie, o valor recebido pode exceder o total; a diferença é troco registrado | RN-017 | O catálogo exige pagamentos = total (RN-VEN-024); sem esta regra, pagamento em dinheiro nunca fecharia |
| RN-VEN-031 | Comanda fechada não aceita novos itens; reabertura exige gerência | RN-022 | O catálogo define fechamento por pagamento, não o congelamento do lançamento |
| RN-VEN-032 | Preço de tabela vigente é sempre maior que zero | RN-001 | `CHECK` trivial e ausente |
| RN-VEN-033 | Transferência de comanda exige mesa de destino livre | RN-025 | Refina RN-VEN-007, que não diz o que acontece se a destino estiver ocupada |
| RN-VEN-034 | Divisão por pessoas: a diferença de arredondamento vai para a primeira parcela | RN-024 | Sem regra de arredondamento, a soma das partes não fecha com o total e RN-VEN-024 trava o fechamento |
| RN-GER-011 | Operações sensíveis registram usuário, data/hora, operação e valores anterior e posterior | RF-SEG-003 | RN-GER-001 registra criação; falta a trilha de **alteração** |
| RN-GER-012 | Custo, CMV e margem são visíveis apenas a perfis de gerência — a API não retorna o campo | RN-041 | Restrição de dado, não de tela. O catálogo trata perfis, não a sensibilidade do campo |
| RN-FIN-013 | Sangria não pode exceder o saldo em espécie do caixa | RN-055 | `CHECK`/trigger ausente |
| RN-FIN-014 | Um único caixa aberto por operador por vez | RN-050 | O catálogo trata `caixa_diario` sem restringir a concorrência |
| RN-FIN-015 | Esperado = inicial + recebimentos em espécie + suprimentos − sangrias | RN-052 | RN-FIN-007 cita "diferença entre esperado e contado" sem definir o esperado |
| RN-FIN-016 | Forma de pagamento declara se movimenta espécie; só as que movimentam entram no caixa | RN-051 | Sem isso, venda em cartão infla o caixa e o fechamento nunca bate |
| RN-LOG-009 | Taxa de entrega vigente é congelada no pedido na criação | RN-031 | Mesmo princípio de RN-VEN-008, aplicado à taxa. Ausente no catálogo |
| RN-PRO-013 | Ordem de produção é roteada pela categoria do produto para a estação padrão, com override por produto | RF-CAD-013 | RN-PRO-009 exige o roteamento sem dizer o que o determina |
| RN-EST-020 | Insumo com movimento registrado não é excluído, apenas inativado | RN-049 | Especialização de RN-GER-002 para cadastro |

---

## 7. Divergência de escopo — a decisão que precisa da sua confirmação

O levantamento declarou **explicitamente fora do escopo**:

> *"Folha de pagamento e gestão de RH"* · *"Contabilidade fiscal completa"*

O catálogo trata **RH** e **FIS** como módulos do sistema, e afirma que os módulos foram
derivados *"da estrutura dos nove módulos de ERP exigidos no trabalho"*.

**São afirmações incompatíveis, e a diferença não é técnica — é de requisito acadêmico.**

O catálogo consolidado adota a posição do catálogo (10 módulos), porque um
requisito da disciplina não é negociável por decisão de escopo do time. Mas isso amplia
o MVP de forma significativa, e a resolução real depende de duas coisas que só você sabe:

1. Os nove módulos são exigidos **no modelo de dados** (Sprints 3–6) ou **implementados
   e funcionando** (até a Sprint 9)?
2. FIS-001 (*"todo pedido finalizado gera documento fiscal"*) significa emitir NFC-e de
   verdade, ou registrar a estrutura do documento fiscal no modelo?

**Recomendação:** modelar os dez módulos por completo — é o que a disciplina avalia e o
custo é de modelagem, não de implementação — e implementar em software o núcleo
`GER + VEN + PRO + EST + FIN`, deixando `COM`, `CRM`, `LOG`, `RH` e `FIS` com cadastro e
estrutura, sem automação profunda. Cada regra do consolidado já traz a coluna **Escopo MVP**
com essa classificação, para que a decisão seja revisada regra a regra e não em bloco.

---

## 8. Correções aplicadas aos documentos existentes

| Documento | Alteração |
|-----------|-----------|
| [`01-documento-de-levantamento.md`](01-documento-de-levantamento.md) | §7 (regras de negócio) substituído por remissão ao catálogo consolidado — as regras deixam de viver em dois lugares. §3 (escopo) revisado para os 10 módulos. Requisitos funcionais e não-funcionais preservados |
| [`02-glossario.md`](02-glossario.md) | Verbetes de custo médio ponderado, baixa de estoque e ciclo do pedido corrigidos; acrescentados dia operacional, lote, FEFO, sub-receita, perda de preparo, rendimento, estação, RFM, ponto de pedido |
| [`adr/0008-estoque-como-livro-razao.md`](adr/0008-estoque-como-livro-razao.md) | Revisado: o saldo desnormalizado editável foi substituído por view, conforme RN-EST-001 |
| **Novo** [`adr/0009`](adr/0009-onde-as-regras-de-negocio-vivem.md) | Onde as regras vivem — resolução do conflito C9 |
| **Novo** [`adr/0010`](adr/0010-custeio-por-lote-com-fefo.md) | Custeio específico por lote com FEFO — resolução do conflito C1 |
| **Novo** [`adr/0011`](adr/0011-dia-operacional.md) | Dia operacional como dimensão do modelo — RN-GER-006 |

Nomenclatura unificada: o levantamento usava `estabelecimento`, o catálogo usa `unidade`.
**Prevalece `unidade`** (RN-GER-009).

---

## 9. Perguntas novas para a fila de validação

O cruzamento produziu perguntas que nenhum dos dois documentos previa. Somam-se às 14 da
fila original — ver [`05-fila-de-validacao-com-cliente.md`](05-fila-de-validacao-com-cliente.md).

| # | Origem | Pergunta a fazer |
|---|--------|------------------|
| 15 | C3 | *"Quando falta ingrediente no meio do movimento, o que vocês fazem? O sistema deveria travar a venda ou deixar passar e avisar depois?"* |
| 16 | C8 | *"Já entregaram para um cliente fora da área normal? Como decidiram a taxa?"* |
| 17 | RN-VEN-027 | *"Quando o cliente monta o lanche, tem escolha obrigatória? Tem limite de adicionais?"* |
| 18 | RN-EST-017 | *"Vocês contam embalagem e sacola no custo do lanche do delivery?"* |
| 19 | RN-VEN-030 | *"Como registram o troco hoje? Entra em algum controle?"* |
| 20 | §7 | *(interno, com o professor)* Os nove módulos precisam estar modelados ou implementados? |
