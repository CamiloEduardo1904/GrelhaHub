# GrelhaHub — Catálogo Consolidado de Regras de Negócio

| Campo | Valor |
|-------|-------|
| Versão | **1.2** |
| Data | Agosto/2026 |
| Situação | Em validação — **138 regras**: 134 em hipótese, 4 decididas pelo time |
| Fontes consolidadas | *Catálogo de Regras v0.1* (117 regras, P3 — Análise de Negócios) + *Documento de Levantamento v1.0* (17 regras complementares) |
| Auditoria do cruzamento | [`07-cruzamento-catalogo-x-levantamento.md`](07-cruzamento-catalogo-x-levantamento.md) |
| Papel no projeto | **Fonte de verdade.** Toda spec de implementação transcreve regras daqui. Nenhuma regra de negócio nasce no código |

---

> ## Este documento é a base do SDD
>
> O GrelhaHub é desenvolvido em **Spec-Driven Development** com **Document First**: a regra
> é escrita e validada antes da spec, a spec antes do código, e o código não introduz
> comportamento que não esteja aqui.
>
> **Não implemente a partir deste documento diretamente.** O caminho é
> `regra → spec → código`, descrito em [`06-guia-de-specs.md`](06-guia-de-specs.md).
>
> **Este catálogo é vivo.** Cada implementação o realimenta: uma regra descoberta durante o
> desenvolvimento é escrita aqui **antes** de ser codificada, e a spec correspondente é
> registrada na coluna de rastreabilidade. Um catálogo que não muda durante a implementação
> é um catálogo que parou de ser lido.

---

## Sumário

1. [Estado do catálogo](#1-estado-do-catálogo)
2. [Como ler este documento](#2-como-ler-este-documento)
3. [Ciclo de vida de uma regra](#3-ciclo-de-vida-de-uma-regra)
4. [Resumo por módulo](#4-resumo-por-módulo)
5. [GER — Gerais e transversais](#ger--gerais-e-transversais)
6. [VEN — Vendas e Comercial](#ven--vendas-e-comercial)
7. [PRO — Produção](#pro--produção)
8. [EST — Estoque](#est--estoque)
9. [COM — Compras](#com--compras)
10. [FIN — Financeiro](#fin--financeiro)
11. [CRM — Relacionamento com o cliente](#crm--relacionamento-com-o-cliente)
12. [LOG — Logística e Frota](#log--logística-e-frota)
13. [RH — Recursos Humanos](#rh--recursos-humanos)
14. [FIS — Fiscal](#fis--fiscal)
15. [As sete regras estruturais](#15-as-sete-regras-estruturais)
16. [Rastreabilidade regra → spec](#16-rastreabilidade-regra--spec)
17. [Histórico de versões](#17-histórico-de-versões)

---

## 1. Estado do catálogo

As 134 regras herdadas das fontes estão em estado de **hipótese** (as outras 4 são decisões de
produto, estado `DEC`). Foram derivadas do
conhecimento do domínio (operação de hamburgueria) e da estrutura dos dez módulos de ERP
exigidos no trabalho — **e não de entrevista com cliente real**.

Esse é o ponto de partida correto, mas é apenas o ponto de partida. Um catálogo inteiramente
em hipótese ainda não passou pelo teste que importa: a confirmação de quem opera o negócio.

**Uso pretendido:** levar este catálogo à primeira entrevista como roteiro de validação,
percorrendo regra a regra os blocos críticos. Cada hipótese que se converte em regra
validada — ou rejeitada — é levantamento de requisitos efetivo. **Uma rejeição vale tanto
quanto uma confirmação** como evidência de método.

A ordem de prioridade para a entrevista está em
[`05-fila-de-validacao-com-cliente.md`](05-fila-de-validacao-com-cliente.md).

---

## 2. Como ler este documento

### Tipo da regra

| | |
|---|---|
| **D** | **Definição** — estabelece o significado de um termo |
| **F** | **Fato / estrutura** — afirma uma relação que existe |
| **R** | **Restrição** — limita o que pode ser verdade |
| **C** | **Cálculo** — deriva um valor a partir de outros |
| **I** | **Inferência** — conclui uma classificação a partir de dados |
| **A** | **Ação** — dispara um efeito quando algo acontece |

### Estado de validação

| | |
|---|---|
| **HIP** | Hipótese — ainda não confirmada com o cliente |
| **VAL** | Validada — confirmada em entrevista |
| **DEC** | Decidida pelo time — decisão de produto, não hipótese sobre a operação |
| **CNF** | Conflito — respostas divergentes entre fontes |
| **ESC** | Fora de escopo — real, mas não será implementada |
| **REJ** | Rejeitada — não corresponde à operação real |

> **`DEC` não é o mesmo que `VAL`.** `VAL` afirma *como a operação funciona* e só a
> entrevista pode confirmar. `DEC` afirma *como o sistema vai se comportar* — é decisão de
> produto, tomada pelo time, e não depende de entrevista. Uma regra `DEC` pode conviver com
> uma resposta contrária do cliente: se o operador disser que prefere sem alerta, a decisão
> de produto continua valendo até que o time a mude.

Toda mudança de estado registra a data e a fonte da validação no
[histórico de versões](#17-histórico-de-versões).

### Mecanismo de implementação previsto

| | |
|---|---|
| **MOD** | Estrutura do modelo — a existência da entidade, do relacionamento ou da cardinalidade já expressa a regra |
| **NN** | `NOT NULL` |
| **UQ** | `UNIQUE` |
| **FK** | Chave estrangeira, com `ON DELETE` correspondente |
| **CK** | `CHECK` constraint |
| **EX** | `EXCLUDE` constraint — exclusão por sobreposição |
| **ENU** | Domínio fechado: `ENUM` ou tabela de domínio |
| **GEN** | Coluna gerada — `GENERATED ALWAYS AS` |
| **TRG** | Trigger ou função no banco |
| **VW** | View ou view materializada |
| **APP** | Camada de aplicação (.NET) |
| **PAR** | Depende de um parâmetro configurável do sistema |

**Ordem de preferência:** `MOD → NN/UQ/FK/CK/EX/ENU → GEN → TRG → VW → APP`.
A camada de aplicação é o último recurso, não o primeiro: o que está sendo avaliado é o
modelo. A convivência entre mecanismo de banco e de aplicação está decidida em
[ADR-0009](adr/0009-onde-as-regras-de-negocio-vivem.md).

### Escopo no MVP

| | |
|---|---|
| **N** | **Núcleo** — modelado *e* implementado até a Sprint 9 |
| **M** | **Modelo** — modelado no banco; software apenas cadastro e consulta, sem automação |
| **2** | **Fase 2** — modelado; implementação após o MVP |

### Origem

| | |
|---|---|
| *(sem marca)* | Catálogo de Regras v0.1 |
| **LEV** | Contribuição do Documento de Levantamento, incorporada no cruzamento |
| **CRZ** | Regra criada durante o cruzamento, para resolver conflito entre as fontes |
| **DEC** | Decisão de produto tomada pelo time, registrada como regra |
| **IMP** | Regra descoberta durante a implementação |

### Marcador de criticidade

Regras assinaladas com **⚠** são **estruturais**: se estiverem erradas, o modelo de dados
nasce errado e a correção posterior custa retrabalho de projeto, não de código.
**⚠⚠** indica a regra de maior impacto do catálogo.

---

## 3. Ciclo de vida de uma regra

```
        escrita            entrevista          spec              implementação
   ┌──────────────┐   ┌─────────────────┐  ┌──────────┐    ┌────────────────────┐
   │  HIP         │──►│  VAL / REJ /    │─►│ SPEC-xxx │───►│ código + teste que  │
   │  hipótese    │   │  CNF / ESC      │  │ escrita  │    │ prova a regra       │
   └──────────────┘   └─────────────────┘  └──────────┘    └────────────────────┘
          ▲                                                            │
          │              nova regra descoberta na implementação        │
          └────────────────────────────────────────────────────────────┘
```

**Regras do processo — não negociáveis:**

1. Só se implementa regra que está neste catálogo. Se durante a implementação aparecer um
   comportamento necessário que não está aqui, **a implementação para**, a regra é escrita
   aqui, e só então o código continua.
2. Regra em estado `HIP` pode ser implementada, mas o teste correspondente deve citar o ID
   da regra — para que uma futura rejeição em entrevista encontre o código que a assume.
3. Regra que muda de estado para `REJ` gera issue de remoção do código que a implementava.
4. A coluna **Spec** da [rastreabilidade](#16-rastreabilidade-regra--spec) é preenchida
   quando a spec é escrita, não quando o código é entregue.

---

## 4. Resumo por módulo

| Módulo | Regras | Prioridade de validação | Escopo predominante |
|--------|-------:|-------------------------|---------------------|
| **GER** — Gerais e transversais | 12 | **ALTA** — RN-GER-006 é estrutural | Núcleo |
| **VEN** — Vendas e Comercial | 38 | **MÁXIMA** | Núcleo |
| **PRO** — Produção | 13 | **MÁXIMA** | Núcleo |
| **EST** — Estoque | 20 | **MÁXIMA** | Núcleo |
| **COM** — Compras | 9 | ALTA | Modelo |
| **FIN** — Financeiro | 16 | MÉDIA | Núcleo |
| **CRM** — Relacionamento com o cliente | 9 | MÉDIA | Modelo |
| **LOG** — Logística e Frota | 9 | MÉDIA | Núcleo |
| **RH** — Recursos Humanos | 6 | BAIXA | Modelo |
| **FIS** — Fiscal | 6 | BAIXA | **Fase 2 — por último** |
| **Total** | **138** | | |

> **Ordem de ataque decidida (17/08/2026).** O módulo **FIS** fica **por último**: é o único
> cujo valor depende de homologação externa (SEFAZ, certificado digital), e nada nos outros
> nove módulos depende dele. **RH permanece no MVP em nível Modelo** — colaborador, cargo,
> escala e apontamento, sem folha de pagamento. `RN-LOG-005` (entregador é colaborador
> ativo) depende de RH, o que já o torna necessário.

### Onde a entrevista precisa ir fundo

Os três módulos de prioridade máxima — **Vendas, Produção e Estoque** — formam a cadeia
`venda → ficha técnica → baixa de insumo`. É nela que o modelo ganha ou perde.

---

## GER — Gerais e transversais

**12 regras**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-GER-001 | Todo registro transacional guarda a data e hora de criação e o usuário responsável pela operação. | R | todas transacionais | NN MOD | HIP | N |
| RN-GER-002 | Nenhum registro transacional é excluído fisicamente. O desfazimento é registrado como cancelamento, com motivo e responsável. | R | todas transacionais | MOD APP | HIP | N |
| RN-GER-003 | Valores monetários são expressos em reais, com duas casas decimais. | R | todas com valor | CK | HIP | N |
| RN-GER-004 | Quantidades de insumo admitem até quatro casas decimais, para permitir gramas e mililitros. | R | `movimento_estoque`, `ficha_tecnica_item` | CK | HIP | N |
| RN-GER-005 | Datas e horas são armazenadas com fuso horário; a operação ocorre em `America/Sao_Paulo`. | R | todas com data/hora | MOD | HIP | N |
| **RN-GER-006** ⚠⚠ | **O dia operacional não coincide com o dia civil: começa às 04h00 e termina às 03h59 do dia seguinte. Uma venda registrada à 01h30 de domingo pertence ao dia operacional de sábado.** | D C | `pedido`, `caixa_diario`, `dim_tempo` | GEN VW PAR | HIP | N |
| RN-GER-007 | Dados pessoais de cliente só são armazenados mediante consentimento registrado, com data e finalidade. | R | `cliente`, `consentimento` | MOD NN | HIP | N |
| RN-GER-008 | O cliente pode solicitar a anonimização dos seus dados. O histórico transacional é preservado, desvinculado da identificação pessoal. | A | `cliente`, `pedido` | APP TRG | HIP | M |
| RN-GER-009 ⚠ | A operação ocorre em uma única unidade, mas todo registro transacional é vinculado a uma unidade para permitir expansão futura. | F | todas transacionais | FK | HIP | N |
| RN-GER-010 | Todo usuário do sistema pertence a um perfil, e o perfil determina quais operações ele pode realizar. | F | `usuario`, `perfil`, `permissao` | MOD APP | HIP | N |
| RN-GER-011 `LEV` | Operações sensíveis — cancelamento, desconto, ajuste de estoque, fechamento de caixa — registram usuário, data/hora, operação e os valores anterior e posterior. | A | `log_auditoria` | TRG APP | HIP | N |
| RN-GER-012 `LEV` | Custo, CMV e margem são visíveis apenas a perfis de gerência. A restrição é de dado: a API não retorna o campo para perfil sem permissão, em vez de ocultá-lo na tela. | R | `produto`, `ficha_tecnica`, `movimento_estoque` | APP VW | HIP | N |

> ### ⚠⚠ RN-GER-006 — a regra mais subestimada do catálogo
>
> Se o modelo usar `date_trunc('day', dh_criacao)`, o faturamento de sábado à noite cai em
> domingo, o fechamento de caixa não bate e o relatório de melhor dia da semana fica errado
> em definitivo. **Precisa estar decidida antes do modelo lógico.**
>
> Decisão registrada em [ADR-0011](adr/0011-dia-operacional.md): coluna gerada
> `data_operacional` em toda tabela com data de evento comercial, derivada de
> `(dh_evento - PAR-GER-001)::date`, com índice — nunca calculada na consulta.

---

## VEN — Vendas e Comercial

**38 regras** · prioridade de validação **MÁXIMA**

### Estrutura do pedido

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-001 | Todo pedido pertence a exatamente um canal de venda: salão, balcão, delivery próprio ou marketplace. | F | `pedido`, `canal_venda` | FK NN | HIP | N |
| RN-VEN-002 | Todo pedido tem ao menos um item. Um pedido sem itens não pode ser confirmado. | R | `pedido`, `item_pedido` | TRG APP | HIP | N |
| RN-VEN-003 | Todo item de pedido refere-se a exatamente um produto do cardápio. | F | `item_pedido`, `produto` | FK NN | HIP | N |
| RN-VEN-004 | Pedido do canal salão está vinculado a uma comanda; pedido de balcão, delivery e marketplace não usa comanda. | F | `pedido`, `comanda` | MOD CK | HIP | N |
| RN-VEN-005 | Uma comanda está vinculada a zero ou uma mesa. Comanda de balcão não tem mesa. | F | `comanda`, `mesa` | FK | HIP | N |
| RN-VEN-006 ⚠ | Uma mesa tem no máximo uma comanda aberta por vez. | R | `comanda`, `mesa` | EX | HIP | N |
| RN-VEN-007 | Uma comanda pode ser transferida de uma mesa para outra, e duas comandas podem ser unificadas quando os clientes juntam mesas. | A | `comanda`, `mesa` | APP TRG | HIP | N |

### Preço e valores

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-008 ⚠ | O preço praticado em um item de pedido é registrado no momento da venda e não muda se o preço de tabela do produto for alterado depois. | R | `item_pedido`, `preco_produto` | MOD NN | HIP | N |
| RN-VEN-009 | O valor total de um item é a quantidade multiplicada pelo preço praticado. | C | `item_pedido` | GEN | HIP | N |
| RN-VEN-010 ⚠ | Um produto tem um preço de tabela vigente por período; a alteração de preço cria uma nova vigência e preserva a anterior. | F | `produto`, `preco_produto` | MOD CK | HIP | N |
| RN-VEN-011 | A quantidade de um item de pedido é sempre maior que zero. | R | `item_pedido` | CK | HIP | N |
| RN-VEN-012 | O preço praticado pode ser inferior ao preço de tabela mediante desconto, que exige registro de motivo. | R | `item_pedido` | CK APP | HIP | N |
| RN-VEN-013 | Desconto acima do limite parametrizado (`PAR-VEN-001`) exige autorização de um usuário com perfil de gerência, registrada no pedido. | R A | `pedido`, `autorizacao` | APP PAR | HIP | N |
| RN-VEN-014 | Sobre o consumo no salão incide taxa de serviço opcional (`PAR-VEN-002`), que não incide sobre delivery nem sobre a taxa de entrega. | C R | `comanda`, `pedido` | GEN PAR | HIP | N |

### Composição do item

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-015 | Um item de pedido pode ter adicionais e remoções — por exemplo, mais bacon ou sem cebola. | F | `item_pedido`, `item_pedido_ajuste` | MOD | HIP | N |
| RN-VEN-016 ⚠ | Adicional e remoção alteram tanto o preço do item quanto o consumo de insumos daquele item. | C A | `item_pedido_ajuste`, `ficha_tecnica` | TRG APP | HIP | N |
| RN-VEN-017 | Um combo é um produto que agrupa outros produtos e tem preço próprio, distinto da soma dos componentes. | F | `produto`, `produto_composicao` | MOD | HIP | N |

### Ciclo de vida e disponibilidade

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-018 ⚠ | O pedido percorre os estados: **Aberto → Confirmado → Em produção → Pronto → Entregue → Finalizado**. De qualquer estado anterior a Finalizado pode ir para **Cancelado**. | F R | `pedido`, `status_pedido` | ENU TRG | HIP | N |
| RN-VEN-019 | Um pedido Finalizado ou Cancelado não retorna a nenhum estado anterior. | R | `pedido` | TRG | HIP | N |
| RN-VEN-020 | Um produto inativo não pode ser lançado em um novo pedido, mas permanece nos pedidos históricos. | R | `produto`, `item_pedido` | APP FK | HIP | N |
| RN-VEN-021 | Um produto só pode ser lançado se houver saldo de todos os insumos da sua ficha técnica vigente na quantidade requerida. **O comportamento na falta de saldo é parametrizável por produto (`PAR-VEN-003`): bloquear, avisar ou permitir.** | R A | `produto`, `ficha_tecnica`, `lote` | VW APP PAR | HIP | N |
| RN-VEN-035 `DEC` ⚠ | **A falta de saldo de qualquer insumo da ficha técnica sempre gera alerta visível na tela de lançamento**, identificando o insumo e a quantidade faltante. O alerta é exibido nos três modos de `PAR-VEN-003` — o parâmetro decide se o lançamento é recusado, exige confirmação ou apenas informa, **nunca se o operador é avisado**. | R A | `produto`, `ficha_tecnica`, `lote` | APP VW | **DEC** | N |
| RN-VEN-022 | Um produto disponível apenas em determinada faixa de horário não pode ser lançado fora dela. | R | `produto`, `disponibilidade_produto` | APP PAR | HIP | 2 |

### Pagamento

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-023 | A conta de uma comanda pode ser dividida em múltiplos pagamentos, com formas diferentes. | F | `comanda`, `pagamento` | MOD | HIP | N |
| RN-VEN-024 | Uma comanda só é fechada quando a soma dos pagamentos iguala o seu valor total. | R | `comanda`, `pagamento` | TRG | HIP | N |
| RN-VEN-025 | Cada forma de pagamento tem taxa de administração e prazo de recebimento próprios. | F | `forma_pagamento` | MOD | HIP | N |
| RN-VEN-026 | Pedido de marketplace já entra no sistema como pago; o valor a receber é líquido da comissão do canal. | F C | `pedido`, `canal_venda` | MOD TRG | HIP | M |

### Complementos incorporados no cruzamento

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-027 `LEV` ⚠ | Os ajustes de um produto organizam-se em **grupos de modificadores** com seleção mínima e máxima. Quando o mínimo é maior ou igual a 1, a seleção é obrigatória para lançar o item. | R F | `grupo_modificador`, `opcao_modificador`, `produto_grupo_modificador` | MOD CK APP | HIP | N |
| RN-VEN-028 `LEV` | O valor de um item é `(preço praticado + soma dos ajustes selecionados) × quantidade`. Um ajuste pode ter acréscimo positivo, zero ou negativo. | C | `item_pedido`, `item_pedido_ajuste` | GEN | HIP | N |
| RN-VEN-029 `LEV` | O valor total de um pedido nunca é negativo, ainda que desconto e cupom somados excedam o consumo. | R | `pedido` | CK | HIP | N |
| RN-VEN-030 `LEV` | Em pagamento em espécie, o valor recebido pode exceder o total do pedido; a diferença é registrada como troco e não integra a receita. | C R | `pagamento` | GEN CK | HIP | N |
| RN-VEN-031 `LEV` | Comanda fechada não aceita novos itens. A reabertura exige perfil de gerência e fica registrada. | R | `comanda` | TRG APP | HIP | N |
| RN-VEN-032 `LEV` | O preço de tabela vigente de um produto é sempre maior que zero. | R | `preco_produto` | CK | HIP | N |
| RN-VEN-033 `LEV` | A transferência de comanda exige que a mesa de destino esteja livre. | R | `comanda`, `mesa` | TRG | HIP | N |
| RN-VEN-034 `LEV` | Na divisão de conta por número de pessoas, a diferença de arredondamento é somada à primeira parcela, de modo que a soma das partes iguale exatamente o total. | C | `comanda`, `pagamento` | APP | HIP | N |

### Cardápio público

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-036 `DEC` | Cada unidade possui um **identificador público** (`slug`) único em todo o sistema, imutável após a criação, que compõe o endereço do seu cardápio online. | F R | `unidade` | UQ CK | **DEC** | N |
| RN-VEN-037 `DEC` | O cardápio público é acessível **sem autenticação** e expõe apenas produtos **ativos e disponíveis** da unidade do `slug`. Não expõe custo, CMV, margem (`RN-GER-012`) nem qualquer dado de outra unidade. | R | `unidade`, `produto`, `categoria` | APP VW | **DEC** | N |
| RN-VEN-038 `DEC` | Pedido originado do cardápio público entra no canal **delivery próprio**, no estado **Aberto**, e exige nome e telefone do cliente (`RN-CRM-001`, `RN-CRM-003`). Ele **não é confirmado automaticamente** — a confirmação é ato do restaurante, e é ela que dispara baixa de estoque e ordem de produção (`RN-VEN-018`). | R A | `pedido`, `cliente`, `canal_venda` | ENU APP | **DEC** | N |

---

## PRO — Produção

**13 regras** · prioridade de validação **MÁXIMA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-PRO-001 ⚠ | Todo produto vendável tem exatamente uma ficha técnica vigente em cada data. | R F | `produto`, `ficha_tecnica` | EX TRG | HIP | N |
| RN-PRO-002 ⚠ | A ficha técnica é versionada. Criar uma nova versão encerra a vigência da anterior e não altera o custo dos pedidos já realizados. | R | `ficha_tecnica` | MOD CK | HIP | N |
| RN-PRO-003 | A ficha técnica relaciona um produto aos insumos consumidos, com quantidade e unidade de medida. | F | `ficha_tecnica_item`, `insumo` | MOD | HIP | N |
| **RN-PRO-004** ⚠⚠ | **Uma ficha técnica pode conter uma sub-receita — um produto intermediário que possui ficha técnica própria e é estocado, como o molho da casa.** | F | `ficha_tecnica_item`, `produto` | MOD FK | HIP | N |
| RN-PRO-005 ⚠ | Cada insumo na ficha técnica tem percentual de perda de preparo, que representa o descarte inerente ao processamento — casca, aparas. | F C | `ficha_tecnica_item` | CK | HIP | N |
| RN-PRO-006 | A quantidade bruta consumida de um insumo é a quantidade líquida da ficha dividida por `(1 − percentual de perda)`. | C | `ficha_tecnica_item` | GEN VW | HIP | N |
| RN-PRO-007 | A ficha técnica declara um rendimento: quantas porções são produzidas por execução. | F | `ficha_tecnica` | CK | HIP | N |
| RN-PRO-008 | A confirmação de um pedido gera uma ordem de produção para cada item que exija preparo. | A | `pedido`, `ordem_producao` | TRG APP | HIP | N |
| RN-PRO-009 | Cada ordem de produção é roteada para uma estação de trabalho: chapa, fritadeira, montagem ou bebidas. | F | `ordem_producao`, `estacao` | FK | HIP | N |
| RN-PRO-010 | Cada etapa de preparo registra o instante de início e de conclusão, permitindo apurar o tempo efetivo. | F C | `etapa_preparo` | MOD GEN | HIP | N |
| RN-PRO-011 | Um item só é marcado como Pronto quando todas as suas etapas de preparo estiverem concluídas. | R | `ordem_producao`, `etapa_preparo` | TRG | HIP | N |
| RN-PRO-012 | A produção antecipada de sub-receita ocorre sem pedido vinculado e gera entrada de estoque do produto intermediário. | A F | `ordem_producao`, `movimento_estoque` | TRG | HIP | N |
| RN-PRO-013 `LEV` | O roteamento da ordem de produção é determinado pela categoria do produto, que define a estação padrão, admitindo sobreposição por produto. | F | `categoria`, `produto`, `estacao` | FK MOD | HIP | N |

> ### ⚠⚠ RN-PRO-004 — a regra que mais quebra modelos ingênuos
>
> Uma ficha técnica que só aponta para insumo não representa o molho da casa, que é
> **produto e insumo ao mesmo tempo**. A modelagem precisa admitir recursividade.
> Descobrir isso na Sprint 7 custa uma sprint inteira; descobrir agora custa uma conversa.
>
> Implicação para o modelo: `ficha_tecnica_item` referencia um **item estocável**, que pode
> ser `insumo` ou `produto` — o que exige ou herança/especialização, ou uma entidade
> unificadora. A decisão vai para a Sprint 3 & 4, e precisa de proteção contra ciclo
> (uma ficha não pode conter a si mesma, direta ou indiretamente).

---

## EST — Estoque

**20 regras** · prioridade de validação **MÁXIMA**

### Princípio estrutural

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-EST-001 ⚠ | O saldo de um insumo nunca é um valor armazenado e editável. É sempre derivado da soma algébrica dos seus movimentos. | C R | `movimento_estoque` | VW MOD | HIP | N |
| RN-EST-002 | Todo movimento de estoque tem um tipo: entrada por compra, saída por venda, saída por produção, entrada por produção, perda, ajuste de inventário, transferência ou devolução. | F | `movimento_estoque`, `tipo_movimento` | ENU NN | HIP | N |
| RN-EST-003 | Todo movimento de estoque registra quem o realizou e quando. | R | `movimento_estoque` | NN | HIP | N |

### Lote e validade

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-EST-004 ⚠ | Todo movimento de estoque refere-se a um lote específico do insumo. | F | `movimento_estoque`, `lote` | FK NN | HIP | N |
| RN-EST-005 | Insumo perecível exige lote com data de validade informada. Insumo não perecível admite lote sem validade. | R | `lote`, `insumo` | CK | HIP | N |
| RN-EST-006 | A data de validade de um lote é posterior à sua data de entrada. | R | `lote` | CK | HIP | N |
| RN-EST-007 ⚠ | A saída de estoque consome prioritariamente o lote de menor data de validade entre os disponíveis — critério **FEFO**. | A | `lote`, `movimento_estoque` | TRG VW | HIP | N |
| RN-EST-008 | Não se aceita entrada de insumo cujo prazo restante de validade seja inferior ao mínimo parametrizado (`PAR-EST-001`). | R | `lote`, `recebimento` | APP PAR | HIP | M |

### Saldo e custo

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-EST-009 | O saldo de um lote nunca é negativo. | R | `movimento_estoque` | TRG | HIP | N |
| RN-EST-010 | Toda entrada de estoque registra o custo unitário de aquisição do lote. | R | `lote`, `movimento_estoque` | NN CK | HIP | N |
| RN-EST-011 ⚠ | O custo de uma saída é o custo do lote efetivamente consumido — método do **custo específico por lote**. | C | `movimento_estoque`, `lote` | TRG | HIP | N |
| RN-EST-012 ⚠ | Um insumo pode ser comprado em uma unidade de medida e consumido em outra; o fator de conversão é definido por insumo. | F C | `insumo`, `unidade_medida`, `conversao_unidade` | MOD GEN | HIP | N |

### Operações

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-EST-013 | Toda perda registra um motivo obrigatório: vencimento, quebra, queima, degustação, furto ou devolução ao fornecedor. | R | `movimento_estoque`, `motivo_perda` | ENU NN | HIP | N |
| RN-EST-014 | A contagem de inventário gera movimento de ajuste correspondente à diferença apurada, com justificativa obrigatória. | A R | `inventario`, `movimento_estoque` | TRG NN | HIP | N |
| RN-EST-015 | Uma transferência entre locais de estoque gera dois movimentos — saída na origem e entrada no destino — na mesma transação. | A R | `movimento_estoque`, `local_estoque` | TRG | HIP | M |
| RN-EST-016 | Cada insumo tem estoque mínimo e ponto de pedido definidos, usados para disparar a sugestão de compra. | F | `insumo` | MOD CK | HIP | N |
| RN-EST-017 | Embalagens e descartáveis são insumos e integram a ficha técnica dos produtos vendidos no canal delivery. | F | `insumo`, `ficha_tecnica_item` | MOD | HIP | N |
| RN-EST-018 ⚠ | Item cancelado **após** entrar em produção **não** devolve insumo ao estoque; gera perda com motivo de cancelamento. | A R | `item_pedido`, `movimento_estoque` | TRG | HIP | N |
| RN-EST-019 | Item cancelado **antes** de entrar em produção não gera nenhum movimento de estoque. | R | `item_pedido`, `movimento_estoque` | TRG | HIP | N |
| RN-EST-020 `LEV` | Insumo com movimento registrado não pode ser excluído, apenas inativado. | R | `insumo`, `movimento_estoque` | TRG APP | HIP | N |

> ### RN-EST-001 e o desempenho da consulta de saldo
>
> Saldo derivado é o princípio; agregar todo o histórico a cada tela é inviável.
> A conciliação está em [ADR-0008](adr/0008-estoque-como-livro-razao.md): o saldo materializa
> em **view materializada** por `(unidade, insumo, lote)`, atualizada na transação do
> movimento. **Nunca uma coluna editável** — a distinção não é de desempenho, é de
> autoridade: a view é derivada e recomputável; uma coluna editável é uma segunda fonte de
> verdade que pode divergir sem que ninguém perceba.

---

## COM — Compras

**9 regras** · prioridade de validação **ALTA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-COM-001 | Todo pedido de compra é dirigido a exatamente um fornecedor. | F | `pedido_compra`, `fornecedor` | FK NN | HIP | M |
| RN-COM-002 | Cada fornecedor tem valor de pedido mínimo e prazo de entrega em dias. | F | `fornecedor` | MOD | HIP | M |
| RN-COM-003 | Um pedido de compra abaixo do mínimo do fornecedor não pode ser emitido. | R | `pedido_compra`, `fornecedor` | TRG APP | HIP | M |
| RN-COM-004 | Um mesmo insumo pode ser fornecido por vários fornecedores, com preço e código próprios de cada um. | F | `insumo_fornecedor` | MOD UQ | HIP | M |
| RN-COM-005 | Compras acima do valor parametrizado (`PAR-COM-001`) exigem cotação de ao menos dois fornecedores. | R | `cotacao`, `pedido_compra` | APP PAR | HIP | M |
| RN-COM-006 | O recebimento de um pedido de compra pode ser parcial; a quantidade não recebida permanece pendente. | F | `recebimento`, `pedido_compra_item` | MOD CK | HIP | M |
| RN-COM-007 | Divergência de preço ou quantidade entre o pedido e o recebimento gera ocorrência que exige aprovação para prosseguir. | A R | `recebimento`, `ocorrencia_recebimento` | APP | HIP | M |
| RN-COM-008 ⚠ | O recebimento gera simultaneamente a entrada de estoque e a conta a pagar ao fornecedor. | A | `recebimento`, `movimento_estoque`, `conta_pagar` | TRG | HIP | N |
| RN-COM-009 | A sugestão de compra de um insumo é a demanda prevista para o horizonte de reposição, convertida pela ficha técnica, menos o saldo disponível, mais o estoque de segurança (`PAR-EST-002`), arredondada para o múltiplo de compra do fornecedor. | C A | `insumo`, `ficha_tecnica`, `previsao_demanda` | VW PAR | HIP | 2 |

---

## FIN — Financeiro

**16 regras** · prioridade de validação **MÉDIA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-FIN-001 | Todo pedido finalizado gera lançamento de conta a receber, conforme a forma de pagamento utilizada. | A | `pedido`, `conta_receber` | TRG | HIP | N |
| RN-FIN-002 | O prazo de recebimento depende da forma de pagamento: dinheiro e PIX no mesmo dia, débito no dia seguinte, crédito conforme prazo do adquirente. | C F | `forma_pagamento`, `conta_receber` | MOD PAR | HIP | N |
| RN-FIN-003 ⚠ | A taxa da forma de pagamento é reconhecida como despesa no momento da **venda**, não no do recebimento. | A C | `pagamento`, `lancamento_financeiro` | TRG | HIP | N |
| RN-FIN-004 | A comissão do canal marketplace é despesa vinculada ao pedido e deduzida do valor a receber. | C | `pedido`, `canal_venda` | GEN TRG | HIP | M |
| RN-FIN-005 | Todo lançamento financeiro é classificado em uma conta do plano de contas e em um centro de custo. | R | `lancamento_financeiro`, `plano_de_contas`, `centro_de_custo` | FK NN | HIP | N |
| RN-FIN-006 | Todo recebimento e todo pagamento estão vinculados a uma conta bancária ou ao caixa da loja. | R | `lancamento_financeiro`, `conta_bancaria` | FK NN | HIP | N |
| RN-FIN-007 | O caixa é aberto com valor inicial informado e fechado com conferência; a diferença entre o esperado e o contado é registrada como sobra ou quebra de caixa. | A C | `caixa_diario` | GEN TRG | HIP | N |
| RN-FIN-008 | Sangria e suprimento são movimentações registradas no caixa aberto, com responsável e motivo. | F R | `movimento_caixa` | MOD NN | HIP | N |
| RN-FIN-009 | Um caixa não pode ser reaberto após o fechamento. | R | `caixa_diario` | TRG | HIP | N |
| RN-FIN-010 | Toda conta a pagar tem data de vencimento; o pagamento após o vencimento registra os acréscimos de juros e multa separadamente do principal. | F R | `conta_pagar` | MOD CK | HIP | M |
| RN-FIN-011 | Despesas fixas são rateadas entre centros de custo segundo critério definido por despesa. | C | `lancamento_financeiro`, `rateio` | VW | HIP | M |
| RN-FIN-012 ⚠ | O resultado gerencial (DRE) é sempre derivado dos lançamentos; nenhum valor de resultado é digitado diretamente. | C R | `lancamento_financeiro` | VW | HIP | N |
| RN-FIN-013 `LEV` | Uma sangria não pode exceder o saldo em espécie disponível no caixa. | R | `movimento_caixa` | TRG | HIP | N |
| RN-FIN-014 `LEV` | Existe no máximo um caixa aberto por operador por vez. | R | `caixa_diario` | EX | HIP | N |
| RN-FIN-015 `LEV` | O valor esperado no fechamento é `valor inicial + recebimentos em espécie + suprimentos − sangrias`. | C | `caixa_diario`, `movimento_caixa` | GEN VW | HIP | N |
| RN-FIN-016 `LEV` | Cada forma de pagamento declara se movimenta espécie. Apenas as que movimentam integram o saldo do caixa; as demais são registradas sem vínculo de caixa. | F R | `forma_pagamento`, `movimento_caixa` | MOD CK | HIP | N |

---

## CRM — Relacionamento com o cliente

**9 regras** · prioridade de validação **MÉDIA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-CRM-001 | O cliente é identificado unicamente pelo número de telefone. | R D | `cliente` | UQ | HIP | N |
| RN-CRM-002 | Um cliente pode ter vários endereços cadastrados, dos quais exatamente um é o principal. | F R | `cliente`, `endereco_cliente` | EX MOD | HIP | N |
| RN-CRM-003 | Pedido do canal delivery exige cliente identificado; pedido de salão e balcão admite cliente anônimo. | R | `pedido`, `cliente` | CK | HIP | N |
| RN-CRM-004 | Um cupom tem período de validade, tipo de benefício (valor fixo ou percentual), valor mínimo de pedido, limite total de usos e limite de usos por cliente. | F R | `cupom` | MOD CK | HIP | M |
| RN-CRM-005 | Cupons não são acumuláveis: um pedido admite no máximo um cupom. | R | `pedido`, `uso_cupom` | UQ | HIP | M |
| RN-CRM-006 | Todo uso de cupom é registrado com o pedido e o cliente, permitindo medir a efetividade da campanha. | F | `uso_cupom`, `campanha` | MOD | HIP | M |
| RN-CRM-007 | Um cliente é classificado nos segmentos RFM — Campeão, Fiel, Em risco, Hibernando, Perdido — a partir da recência, frequência e valor dos seus pedidos. | I | `cliente`, `pedido` | VW | HIP | N |
| RN-CRM-008 | Um cliente é considerado em risco quando o tempo desde o seu último pedido supera `PAR-CRM-001` vez o seu intervalo médio histórico entre pedidos. | I | `cliente`, `pedido` | VW PAR | HIP | N |
| RN-CRM-009 | A avaliação de um pedido é dada em nota de 1 a 5 e está vinculada a exatamente um pedido entregue. | F R | `avaliacao`, `pedido` | CK FK | HIP | 2 |

> **RN-CRM-007 e RN-CRM-008 são a matéria-prima do BI da Sprint 7 & 8.** São regras de
> *inferência* — derivam classificação de dados existentes, sem entrada manual. Marcadas
> como Núcleo por isso, ainda que o módulo CRM seja predominantemente Modelo.

---

## LOG — Logística e Frota

**9 regras** · prioridade de validação **MÉDIA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-LOG-001 | Todo pedido de delivery gera exatamente uma entrega. | F | `pedido`, `entrega` | FK UQ | HIP | N |
| RN-LOG-002 | Uma rota agrupa uma ou mais entregas atribuídas a um mesmo entregador em um mesmo trajeto. | F | `rota`, `entrega` | MOD | HIP | M |
| RN-LOG-003 | Cada zona de entrega define a taxa cobrada do cliente e o tempo estimado de entrega. | F | `zona_entrega` | MOD | HIP | N |
| RN-LOG-004 | Endereço fora de qualquer zona atendida não permite a confirmação do pedido de delivery. | R | `endereco_cliente`, `zona_entrega` | APP VW | HIP | N |
| RN-LOG-005 | O entregador é um colaborador ativo, associado a um veículo durante o turno. | F R | `entregador`, `colaborador`, `veiculo` | FK CK | HIP | M |
| RN-LOG-006 | A entrega percorre os estados: Aguardando → Em rota → Entregue, ou Não entregue com motivo obrigatório. | F R | `entrega`, `status_entrega` | ENU TRG | HIP | N |
| RN-LOG-007 ⚠ | A taxa de entrega cobrada do cliente e o valor pago ao entregador são grandezas distintas e registradas separadamente. | F | `entrega` | MOD | HIP | N |
| RN-LOG-008 | Todo evento relevante da entrega — saída, chegada, tentativa frustrada — é registrado com data e hora. | F | `evento_entrega` | MOD | HIP | M |
| RN-LOG-009 `LEV` | A taxa de entrega vigente da zona é congelada no pedido no momento da criação; alteração posterior na tabela de zonas não afeta pedidos já emitidos. | R | `pedido`, `zona_entrega` | MOD NN | HIP | N |

---

## RH — Recursos Humanos

**6 regras** · prioridade de validação **BAIXA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-RH-001 | Todo colaborador ocupa um cargo, e o cargo determina o custo por hora usado nas apurações gerenciais. | F | `colaborador`, `cargo` | FK | HIP | M |
| RN-RH-002 | A escala define os turnos previstos de cada colaborador por dia. | F | `escala`, `turno` | MOD | HIP | M |
| RN-RH-003 | Um mesmo colaborador não pode ter dois turnos com horários sobrepostos. | R | `escala` | EX | HIP | M |
| RN-RH-004 | O apontamento registra os horários efetivos de entrada e saída, que podem divergir do turno previsto. | F | `apontamento_ponto` | MOD | HIP | M |
| RN-RH-005 | O tempo trabalhado além da jornada prevista é classificado como hora extra, com adicional conforme o dia. | C I | `apontamento_ponto`, `evento_folha` | VW PAR | HIP | M |
| RN-RH-006 | Colaborador inativo não pode ser escalado nem registrar operação no sistema. | R | `colaborador` | APP CK | HIP | N |

---

## FIS — Fiscal

**6 regras** · prioridade de validação **BAIXA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
> ### ⏸ Módulo adiado — é o último da fila
>
> **Decisão de 17/08/2026:** FIS fica por último. Duas razões:
>
> 1. É o único módulo cujo valor depende de **homologação externa** — SEFAZ, certificado
>    digital A1, contingência. O esforço não é de modelagem, é de integração e burocracia.
> 2. **Nada nos outros nove módulos depende dele.** `RN-FIS-001` é disparada pelo pedido
>    finalizado, não o contrário. Adiar FIS não bloqueia nenhuma outra spec.
>
> As seis regras permanecem catalogadas e a modelagem já reserva o espaço — em especial
> `RN-FIS-006` (retrato congelado do emitente e do destinatário), que é um requisito de
> modelo, não de integração, e seria caro acrescentar depois.

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-FIS-001 | Todo pedido finalizado gera um documento fiscal. | A | `pedido`, `documento_fiscal` | TRG | HIP | 2 |
| RN-FIS-002 | O documento fiscal é imutável. Correção só é possível por cancelamento seguido de emissão de novo documento. | R | `documento_fiscal` | TRG APP | HIP | 2 |
| RN-FIS-003 | O cancelamento de documento fiscal só é admitido dentro do prazo legal a partir da emissão (`PAR-FIS-001`). | R | `documento_fiscal` | CK PAR | HIP | 2 |
| RN-FIS-004 | Todo produto vendido possui classificação NCM, e toda operação possui um CFOP correspondente. | F R | `produto`, `documento_fiscal`, `ncm`, `cfop` | FK NN | HIP | 2 |
| RN-FIS-005 | A empresa é optante pelo Simples Nacional; a alíquota efetiva depende da faixa de faturamento acumulado dos últimos doze meses. | C F | `regime_tributario`, `aliquota` | VW PAR | HIP | 2 |
| RN-FIS-006 | O documento fiscal congela um retrato dos dados do emitente e do destinatário no momento da emissão. | R | `documento_fiscal` | MOD | HIP | 2 |

> **Limite de escopo declarado.** As regras FIS descrevem a **estrutura** do documento
> fiscal no modelo de dados. A **emissão** efetiva de NFC-e ou SAT está fora do MVP. O
> modelo é projetado para acoplar a emissão sem redesenho. Pergunta 20 da fila de validação.

---

## 15. As sete regras estruturais

Se estiverem erradas, o modelo de dados nasce errado. **Nenhuma spec de modelagem começa
antes que estas sete estejam resolvidas.**

| Regra | O que trava se estiver errada |
|-------|-------------------------------|
| **RN-GER-006** ⚠⚠ — dia operacional | Faturamento por dia, fechamento de caixa e toda a dimensão tempo do BI |
| **RN-PRO-004** ⚠⚠ — sub-receita recursiva | A modelagem da ficha técnica inteira. Exige item estocável polimórfico e proteção contra ciclo |
| **RN-EST-001** — saldo derivado | O modelo de estoque inteiro; define se há coluna de saldo ou view |
| **RN-EST-004** + **RN-EST-011** — lote obrigatório e custo por lote | Toda a apuração de custo e CMV; sem lote não há FEFO nem validade |
| **RN-VEN-008** + **RN-VEN-010** — congelamento de preço e vigência | A reconstituição histórica de qualquer venda passada |
| **RN-VEN-018** — ciclo de vida do pedido | O gatilho de baixa de estoque, a ordem de produção e o momento do faturamento |
| **RN-GER-009** — vínculo a unidade | Toda tabela transacional, toda chave única composta e todo índice |

---

## 16. Rastreabilidade regra → spec

Preenchida à medida que as specs são escritas. Uma regra sem spec é uma regra não
implementada; uma spec sem regra é comportamento não autorizado.

| Regra | Spec | Situação | Teste que a prova |
|-------|------|----------|-------------------|
| `RN-GER-001` | [SPEC-GER-005](specs/SPEC-GER-005-trilha-de-auditoria.md) | Spec pronta | CA-1, CA-2 |
| `RN-GER-002` | [SPEC-GER-001](specs/SPEC-GER-001-unidade-e-convencoes-do-modelo.md) · [005](specs/SPEC-GER-005-trilha-de-auditoria.md) | Spec pronta | CA-6 / CA-5 |
| `RN-GER-003` | [SPEC-GER-001](specs/SPEC-GER-001-unidade-e-convencoes-do-modelo.md) | Spec pronta | CA-3 |
| `RN-GER-004` | [SPEC-GER-001](specs/SPEC-GER-001-unidade-e-convencoes-do-modelo.md) | Spec pronta | CA-4 |
| `RN-GER-005` | [SPEC-GER-001](specs/SPEC-GER-001-unidade-e-convencoes-do-modelo.md) | Spec pronta | CA-5 |
| `RN-GER-006` ⚠⚠ | [SPEC-GER-002](specs/SPEC-GER-002-dia-operacional.md) | **Parcial** — cálculo de domínio entregue | `DiaOperacionalTests` ✔ 5 casos; falta CA-4 (paridade C# × PostgreSQL) |
| `RN-GER-009` ⚠ | [SPEC-GER-001](specs/SPEC-GER-001-unidade-e-convencoes-do-modelo.md) | Spec pronta | CA-1, CA-2 |
| `RN-GER-010` | [SPEC-GER-004](specs/SPEC-GER-004-usuarios-perfis-e-permissoes.md) | Spec pronta | CA-1, CA-2 |
| `RN-GER-011` | [SPEC-GER-005](specs/SPEC-GER-005-trilha-de-auditoria.md) | Spec pronta | CA-3, CA-4 |
| `RN-GER-012` | [SPEC-GER-004](specs/SPEC-GER-004-usuarios-perfis-e-permissoes.md) | Spec pronta | CA-3, CA-4 |
| `RN-RH-006` | [SPEC-GER-004](specs/SPEC-GER-004-usuarios-perfis-e-permissoes.md) | Spec pronta | CA-5, CA-6 |
| `RN-VEN-035` `DEC` | *(onda 4 — lançar item)* | A escrever | — |

**Cobertura:** 11 de 138 regras têm spec escrita (onda 0). Uma tem código e teste
entregues — `RN-GER-006`, parcialmente.

**Convenção de ID de spec:** `SPEC-<MOD>-<nnn>-<slug>` — por exemplo
`SPEC-VEN-001-abrir-pedido`. Cada spec declara no cabeçalho as regras que implementa, e
esta tabela é o índice inverso. Ver [`06-guia-de-specs.md`](06-guia-de-specs.md).

---

## 17. Histórico de versões

| Versão | Data | Alteração |
|--------|------|-----------|
| 0.1 | 15/08/2026 | Catálogo inicial — 117 regras, todas em estado de hipótese. *(P3 — Análise de Negócios)* |
| 1.0 | 17/08/2026 | **Consolidação com o Documento de Levantamento.** 17 regras complementares incorporadas (`LEV`); 9 conflitos resolvidos com decisão registrada; escopo ampliado para 10 módulos; coluna **MVP** acrescentada; RN-VEN-021 parametrizada (`PAR-VEN-003`) para resolver o conflito de saldo negativo. Total: **134 regras**. Auditoria em [`07-cruzamento`](07-cruzamento-catalogo-x-levantamento.md) |
| **1.1** | **17/08/2026** | **Decisões de produto do time.** (a) **RN-VEN-035** criada — alerta de falta de insumo é obrigatório na tela, em qualquer modo de `PAR-VEN-003`; primeira regra em estado `DEC`. (b) `PAR-VEN-003` redefinido: os três modos passam a ser *bloquear / avisar / permitir*, todos com alerta, diferindo apenas na fricção. (c) Módulo **FIS** movido para **Fase 2 — por último**; **RH mantido** no MVP em nível Modelo. Estados `DEC` e origens `DEC`/`IMP` acrescentados à legenda. Total: **135 regras** |

### Regras revogadas na consolidação

| Origem | Regra revogada | Substituída por | Motivo |
|--------|----------------|-----------------|--------|
| Levantamento RN-042 | Custo médio ponderado | RN-EST-011 | Domínio de perecíveis exige custo por lote — conflito C1 |
| Levantamento RN-015 | Cancelamento devolve estoque | RN-EST-018, RN-EST-019 | O lanche pronto não volta à geladeira — conflito C2 |
| Levantamento RN-046 | Saldo pode ser negativo | RN-EST-009 + RN-VEN-021 | Substituído por bloqueio parametrizável — conflito C3 |
| Levantamento RN-048 | Produto sem ficha técnica com custo zero | RN-PRO-001 | Custo zero contamina o CMV silenciosamente — conflito C6 |

---

*Documento mantido pelo time do GrelhaHub. Toda mudança de estado de uma regra registra a
data e a fonte da validação. Regra nova descoberta em implementação entra aqui antes de
entrar no código.*
