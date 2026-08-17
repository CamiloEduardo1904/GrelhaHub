# GrelhaHub — Documento de Levantamento e Regras de Negócio

| Campo | Valor |
|-------|-------|
| Projeto | GrelhaHub — ERP para restaurantes e deliveries |
| Sprint | 1 & 2 — Levantamento & Regras de Negócio |
| Versão | 1.1 — revisado na consolidação com o Catálogo de Regras |
| Data | Agosto/2026 |
| Documentos relacionados | [**Catálogo de Regras**](03-catalogo-de-regras-de-negocio.md) · [Glossário](02-glossario.md) · [ADRs](adr/) · [Cruzamento](07-cruzamento-catalogo-x-levantamento.md) · [Contexto](../GRELHAHUB-context.md) |

> **Papel deste documento após a consolidação.** As regras de negócio **saíram daqui** e
> vivem no [Catálogo Consolidado](03-catalogo-de-regras-de-negocio.md) — regra de negócio não
> mora em dois lugares. Este documento permanece como a camada que o catálogo não cobre:
> visão de produto, escopo, atores, processos, requisitos funcionais, requisitos
> não-funcionais e riscos. O que mudou está registrado no
> [cruzamento](07-cruzamento-catalogo-x-levantamento.md).

---

## Sumário

1. [Objetivo do documento](#1-objetivo-do-documento)
2. [Visão do produto](#2-visão-do-produto)
3. [Escopo](#3-escopo)
4. [Atores e perfis de acesso](#4-atores-e-perfis-de-acesso)
5. [Processos de negócio](#5-processos-de-negócio)
6. [Requisitos funcionais](#6-requisitos-funcionais)
7. [Regras de negócio](#7-regras-de-negócio)
8. [Requisitos não-funcionais](#8-requisitos-não-funcionais)
9. [Critérios de aceite dos fluxos críticos](#9-critérios-de-aceite-dos-fluxos-críticos)
10. [Matriz de rastreabilidade — dor do nicho → requisito](#10-matriz-de-rastreabilidade--dor-do-nicho--requisito)
11. [Premissas, restrições e riscos](#11-premissas-restrições-e-riscos)
12. [Entidades candidatas (ponte para a Sprint 3 & 4)](#12-entidades-candidatas-ponte-para-a-sprint-3--4)

---

## 1. Objetivo do documento

Formalizar **o que** o GrelhaHub precisa fazer e **sob quais regras**, antes de qualquer
modelagem de dados ou linha de código. Este documento é a fonte de verdade para:

- a modelagem conceitual e lógica (Sprint 3 & 4);
- o dicionário de dados e o modelo físico (Sprint 5 & 6);
- a definição da carga inicial e dos indicadores de BI (Sprint 7 & 8);
- a defesa das decisões na apresentação executiva (Sprint 9).

**Convenções de identificação**

| Prefixo | Significado |
|---------|-------------|
| `RF-<MOD>-nnn` | Requisito Funcional, agrupado por módulo |
| `RN-nnn` | Regra de Negócio |
| `RNF-nnn` | Requisito Não-Funcional |
| `ADR-nnnn` | Architecture Decision Record |

**Módulos**: `CAD` cadastros · `PED` pedidos · `SAL` salão/mesas · `DEL` delivery ·
`EST` estoque · `FIN` financeiro · `REL` relatórios · `SEG` segurança e acesso.

**Prioridade (MoSCoW)**: `M` must (MVP) · `S` should (MVP se couber) ·
`C` could (fase 2) · `W` won't (fora do escopo atual).

---

## 2. Visão do produto

### 2.1 Contexto

Pequenos e médios restaurantes — especialmente hamburguerias — operam hoje com um
conjunto desconexo de ferramentas: caderno ou planilha para o estoque, WhatsApp para
pedidos, aplicativos de terceiros (iFood) com painel próprio, e uma calculadora para o
fechamento do caixa. O resultado é retrabalho de digitação, ruptura de insumo em pleno
pico e ausência total de visibilidade sobre margem por produto.

### 2.2 Problema

> O dono da hamburgueria não sabe, ao final do mês, **qual dos seus lanches dá lucro** —
> porque o custo do lanche muda a cada compra de insumo e ninguém recalcula.
> E não sabe, durante o serviço, **o que vai faltar** — porque a baixa de estoque
> nunca acompanha a venda em tempo real.

### 2.3 Proposta de valor

Sistema completo **do pedido à cozinha, do estoque ao financeiro**, com especialização
progressiva em hamburgueria. Um único cadastro de produto alimenta simultaneamente o
cardápio, a ficha técnica, a baixa de estoque e o cálculo de CMV.

### 2.4 Público-alvo

| Segmento | Perfil |
|----------|--------|
| Primário | Hamburguerias de 1 a 3 unidades, com salão e delivery, 2 a 15 funcionários |
| Secundário | Pizzarias, lanchonetes, restaurantes à la carte de pequeno porte |
| Fora do alvo (por ora) | Redes de franquia, self-service por peso, food trucks itinerantes |

### 2.5 Estratégia de produto

Modelar um **core genérico de restaurante** (produtos, pedidos, mesas, delivery,
financeiro) e tratar o "sabor hamburgueria" como **camada de configuração**
(modificadores, combos, ficha técnica) — não como código especializado. Nichar depois,
sobre uma base que já suporta outros tipos de operação. Ver [ADR-0007](adr/0007-core-generico-com-nicho-por-configuracao.md).

---

## 3. Escopo

> **Escopo revisado na consolidação.** A versão 1.0 deste documento excluía RH e Fiscal e
> tratava produção, compras e CRM como fase 2. O Catálogo de Regras trata os **dez módulos**
> como exigência do trabalho. Prevaleceu o catálogo — requisito da disciplina não é
> negociável por decisão de escopo do time. Ver
> [cruzamento §7](07-cruzamento-catalogo-x-levantamento.md#7-divergência-de-escopo--a-decisão-que-precisa-da-sua-confirmação).

### 3.1 Os dez módulos e a profundidade de cada um

A classificação por módulo está na coluna **MVP** de cada regra do
[catálogo](03-catalogo-de-regras-de-negocio.md).

| Nível | Significado |
|-------|-------------|
| **N — Núcleo** | Modelado *e* implementado até a Sprint 9 |
| **M — Modelo** | Modelado no banco; software apenas cadastro e consulta, sem automação |
| **2 — Fase 2** | Modelado; implementação após o MVP |

| Módulo | Nível | Entrega no MVP |
|--------|:-----:|----------------|
| **GER** — Gerais e transversais | N | Unidade, parâmetros com vigência, usuários e perfis, auditoria, **dia operacional**, LGPD |
| **VEN** — Vendas e Comercial | N | Pedido nos quatro canais, comanda e mesa, modificadores, preço com vigência, ciclo de vida, pagamento múltiplo |
| **PRO** — Produção | N | Ficha técnica versionada com sub-receita e perda de preparo, ordem de produção, estação, etapas |
| **EST** — Estoque | N | Lote e validade, movimento como livro-razão, FEFO, custo por lote, perda, inventário |
| **FIN** — Financeiro | N | Caixa, sangria e suprimento, contas a pagar e receber, plano de contas, centro de custo, DRE derivado |
| **LOG** — Logística | N | Zona, taxa, entrega e estados. Rota e frota em nível de modelo |
| **CRM** — Relacionamento | M + RFM em N | Cliente, endereços, cupom em modelo; **segmentação RFM implementada** (alimenta o BI da Sprint 7 & 8) |
| **COM** — Compras | M | Fornecedor, pedido de compra, recebimento. Recebimento gerando estoque + conta a pagar é **N** |
| **RH** — Recursos Humanos | M | Colaborador, cargo, escala, apontamento. Sem folha de pagamento. **Mantido no MVP** — `RN-LOG-005` depende dele: o entregador é um colaborador ativo |
| **FIS** — Fiscal | **2** | Estrutura do documento fiscal, NCM, CFOP, Simples Nacional. **Último da fila** |

> **Ordem de ataque (decisão de 17/08/2026).** **FIS é o último módulo.** É o único cujo
> valor depende de homologação externa — SEFAZ, certificado digital, contingência — e
> **nenhum dos outros nove depende dele**: `RN-FIS-001` é disparada pelo pedido finalizado,
> não o contrário. Adiá-lo não bloqueia nenhuma spec.
>
> **RH permanece no MVP.** Além de ser um dos módulos exigidos, `RN-LOG-005` o torna
> necessário para o delivery funcionar, e `RN-RH-001` (custo por hora do cargo) alimenta a
> apuração gerencial da Sprint 7 & 8.

### 3.2 Explicitamente fora do escopo

- **Emissão fiscal efetiva** — comunicação com a SEFAZ, certificado digital A1, contingência.
  O modelo tem a estrutura (módulo FIS); a transmissão não é implementada
- **Folha de pagamento** — o módulo RH modela colaborador, escala e apontamento, mas não
  calcula holerite nem encargos
- **Integração com maquininha (TEF)** — o pagamento é **registrado**, não capturado
- **App mobile** — a API é preparada para ele (ADR-0003), mas o app é posterior ao MVP
- **Multi-idioma e multi-moeda**

> **Sobre a emissão fiscal.** É uma limitação consciente: em produção, um ERP de restaurante
> brasileiro exige NFC-e ou SAT. As seis regras do módulo FIS existem justamente para que a
> emissão seja acoplável sem redesenho do modelo. A pergunta 20 da
> [fila de validação](05-fila-de-validacao-com-cliente.md) confirma essa leitura com o
> professor.

---

## 4. Atores e perfis de acesso

| Ator | Descrição | Principais permissões |
|------|-----------|-----------------------|
| **Administrador** | Dono ou sócio do estabelecimento | Acesso total, incluindo custos, margem, relatórios financeiros e cadastro de usuários |
| **Gerente** | Responsável pelo turno | Tudo do operador + cancelamento de pedido, desconto, fechamento de caixa, ajuste de estoque |
| **Atendente / Caixa** | Balcão e salão | Abrir/lançar/fechar pedido e comanda, receber pagamento, abrir e fechar o próprio caixa |
| **Cozinha** | Equipe de preparo | Visualizar fila de pedidos e alterar status de preparo. **Não** vê preço nem custo |
| **Entregador** | Motoboy | Visualizar entregas atribuídas e marcar como entregue |
| **Cliente** | Consumidor final | *Ator externo* — não acessa o sistema no MVP; é cadastrado pelo atendente |

**Princípio de acesso:** custo, margem e CMV são visíveis apenas a Administrador e
Gerente. Ver `RN-041`.

---

## 5. Processos de negócio

### 5.1 Pedido de balcão (venda direta)

```
Cliente pede → Atendente abre pedido (tipo BALCAO)
             → lança itens (+ modificadores)
             → sistema calcula total
             → envia para a cozinha (status EM_PREPARO)
             → recebe pagamento → status PAGO
             → baixa de estoque pela ficha técnica
             → cozinha finaliza → status PRONTO → ENTREGUE
```

### 5.2 Pedido de mesa (comanda)

```
Cliente senta → Atendente abre comanda vinculada à mesa (mesa → OCUPADA)
              → lançamentos sucessivos de itens ao longo da permanência
              → cliente pede a conta → comanda FECHADA (não aceita mais itens)
              → aplica taxa de serviço (opcional) e divisão de conta
              → recebe pagamento (pode ser múltiplas formas)
              → comanda PAGA → mesa volta a LIVRE
```

### 5.3 Pedido de delivery

```
Cliente liga/manda mensagem → Atendente identifica ou cadastra o cliente
                            → seleciona endereço → sistema resolve a ZONA e a TAXA
                            → lança itens → total = itens + taxa de entrega
                            → confirma → EM_PREPARO
                            → PRONTO → atribui entregador → SAIU_PARA_ENTREGA
                            → ENTREGUE (pagamento na entrega ou antecipado)
```

### 5.4 Entrada de estoque (compra de insumo)

```
Compra chega → Gerente registra entrada (fornecedor, insumo, quantidade, custo unitário)
             → sistema atualiza saldo do insumo
             → recalcula custo médio ponderado do insumo
             → recalcula o custo de todos os produtos cuja ficha técnica usa esse insumo
             → CMV e margem dos produtos são atualizados
```

Esse encadeamento é o coração da proposta de valor: **uma compra mais cara reflete
automaticamente na margem exibida no relatório**, sem recálculo manual.

### 5.5 Fechamento de caixa

```
Início do turno → Operador abre caixa com valor inicial (fundo de troco)
                → movimentações: vendas, sangrias, suprimentos
Fim do turno    → Operador informa o valor contado em espécie
                → sistema calcula o esperado e a DIFERENÇA (quebra de caixa)
                → caixa FECHADO (imutável a partir daí)
```

---

## 6. Requisitos funcionais

### 6.1 Cadastros (CAD)

| ID | Requisito | Prior. |
|----|-----------|--------|
| RF-CAD-001 | Cadastrar, editar e inativar **categorias** de produto (ex.: Lanches, Bebidas, Porções) | M |
| RF-CAD-002 | Cadastrar, editar e inativar **produtos** com nome, descrição, categoria, preço de venda, foto e disponibilidade | M |
| RF-CAD-003 | Cadastrar **grupos de modificadores** (ex.: "Ponto da carne", "Adicionais") com regra de seleção mínima e máxima | M |
| RF-CAD-004 | Vincular grupos de modificadores a produtos, definindo se cada opção altera o preço | M |
| RF-CAD-005 | Cadastrar **insumos** com unidade de medida, saldo mínimo e custo unitário | M |
| RF-CAD-006 | Montar a **ficha técnica** de um produto: lista de insumos e quantidade consumida por unidade vendida | M |
| RF-CAD-007 | Cadastrar **clientes** com nome, telefone e múltiplos endereços | M |
| RF-CAD-008 | Cadastrar **mesas** com identificação e capacidade | M |
| RF-CAD-009 | Cadastrar **zonas de entrega** com taxa e tempo estimado | M |
| RF-CAD-010 | Cadastrar **formas de pagamento** e sinalizar quais movimentam o caixa em espécie | M |
| RF-CAD-011 | Cadastrar **fornecedores** de insumo | S |
| RF-CAD-012 | Cadastrar **combos** com preço próprio e composição de produtos | C |
| RF-CAD-013 | Definir **praças de preparo** e vincular categorias a praças | C |

### 6.2 Pedidos (PED)

| ID | Requisito | Prior. |
|----|-----------|--------|
| RF-PED-001 | Abrir pedido informando o tipo: `BALCAO`, `MESA` ou `DELIVERY` | M |
| RF-PED-002 | Adicionar item ao pedido com quantidade, modificadores selecionados e observação livre | M |
| RF-PED-003 | Remover ou alterar a quantidade de item enquanto o pedido estiver aberto | M |
| RF-PED-004 | Calcular automaticamente o total do pedido (itens + modificadores + taxas − descontos) | M |
| RF-PED-005 | Aplicar desconto em valor ou percentual, com registro de quem autorizou | M |
| RF-PED-006 | Acompanhar o pedido por status ao longo do ciclo de vida | M |
| RF-PED-007 | Cancelar pedido informando o motivo, com estorno da baixa de estoque | M |
| RF-PED-008 | Registrar o pagamento do pedido, aceitando **múltiplas formas** no mesmo pedido | M |
| RF-PED-009 | Listar e filtrar pedidos por período, status, tipo, canal e operador | M |
| RF-PED-010 | Imprimir ou visualizar o comprovante do pedido | S |
| RF-PED-011 | Importar pedidos de canais externos (iFood) mantendo a origem identificada | C |
| RF-PED-012 | **Exibir alerta na tela de lançamento quando faltar saldo de qualquer insumo da ficha técnica**, identificando o insumo, a quantidade faltante e quantas unidades do produto ainda são possíveis (`RN-VEN-035`) | M |

**Ciclo de vida do pedido**

```
ABERTO ──► EM_PREPARO ──► PRONTO ──► ENTREGUE ──► PAGO
   │            │            │           │
   └────────────┴────────────┴───────────┴──► CANCELADO
```

> No delivery, `PRONTO` passa por `SAIU_PARA_ENTREGA` antes de `ENTREGUE`.
> Em pedido de mesa, o pagamento ocorre no fechamento da comanda, após `ENTREGUE`.

### 6.3 Salão e comandas (SAL)

| ID | Requisito | Prior. |
|----|-----------|--------|
| RF-SAL-001 | Visualizar o mapa de mesas com o status de cada uma (`LIVRE`, `OCUPADA`, `RESERVADA`) | M |
| RF-SAL-002 | Abrir comanda vinculada a uma mesa, registrando a quantidade de pessoas | M |
| RF-SAL-003 | Lançar itens em uma comanda aberta em múltiplos momentos | M |
| RF-SAL-004 | Fechar a comanda, gerando a conta com o consumo consolidado | M |
| RF-SAL-005 | Aplicar taxa de serviço configurável (padrão 10%), com opção de remover | M |
| RF-SAL-006 | Dividir a conta por número de pessoas ou por item | S |
| RF-SAL-007 | Transferir comanda de uma mesa para outra | S |
| RF-SAL-008 | Unir duas comandas em uma só (junção de mesas) | C |

### 6.4 Delivery (DEL)

| ID | Requisito | Prior. |
|----|-----------|--------|
| RF-DEL-001 | Selecionar o endereço de entrega dentre os endereços do cliente | M |
| RF-DEL-002 | Determinar automaticamente a zona de entrega a partir do bairro do endereço | M |
| RF-DEL-003 | Aplicar a taxa de entrega da zona ao total do pedido | M |
| RF-DEL-004 | Cadastrar entregadores e atribuir um entregador ao pedido | M |
| RF-DEL-005 | Registrar o horário de saída e o de entrega, calculando o tempo real | S |
| RF-DEL-006 | Registrar a necessidade de troco quando o pagamento for em espécie na entrega | M |
| RF-DEL-007 | Listar as entregas em andamento agrupadas por entregador | S |

### 6.5 Estoque (EST)

| ID | Requisito | Prior. |
|----|-----------|--------|
| RF-EST-001 | Registrar entrada de insumo por compra (fornecedor, quantidade, custo unitário, data) | M |
| RF-EST-002 | Dar baixa automática nos insumos conforme a ficha técnica ao confirmar a venda | M |
| RF-EST-003 | Registrar ajuste manual de saldo (perda, quebra, uso interno) com motivo obrigatório | M |
| RF-EST-004 | Consultar o saldo atual e o histórico de movimentações de cada insumo | M |
| RF-EST-005 | Alertar quando o saldo de um insumo ficar abaixo do mínimo configurado | M |
| RF-EST-006 | Recalcular o **custo médio ponderado** do insumo a cada entrada | M |
| RF-EST-007 | Recalcular o custo do produto quando a ficha técnica ou o custo de um insumo mudar | M |
| RF-EST-008 | Bloquear a venda de produto cujo insumo esteja zerado (configurável por produto) | S |
| RF-EST-009 | Registrar inventário periódico com contagem física e apuração da divergência | C |

### 6.6 Financeiro (FIN)

| ID | Requisito | Prior. |
|----|-----------|--------|
| RF-FIN-001 | Abrir caixa informando o valor inicial em espécie | M |
| RF-FIN-002 | Registrar sangria (retirada) e suprimento (aporte) com motivo | M |
| RF-FIN-003 | Fechar caixa informando o valor contado, apurando a diferença | M |
| RF-FIN-004 | Consultar o extrato do caixa com todas as movimentações do turno | M |
| RF-FIN-005 | Registrar contas a pagar com vencimento, fornecedor e categoria de despesa | S |
| RF-FIN-006 | Registrar a baixa (pagamento) de uma conta a pagar | S |
| RF-FIN-007 | Consultar o fluxo de caixa consolidado por período | S |
| RF-FIN-008 | Conciliar os recebimentos por forma de pagamento no fechamento | C |

### 6.7 Relatórios e BI (REL)

| ID | Requisito | Prior. |
|----|-----------|--------|
| RF-REL-001 | Relatório de vendas por período, com quebra por tipo de pedido e canal | M |
| RF-REL-002 | Ranking de produtos mais vendidos por quantidade e por faturamento | M |
| RF-REL-003 | **CMV e margem por produto** — custo da ficha técnica × preço de venda | M |
| RF-REL-004 | Ticket médio por tipo de pedido e por período | M |
| RF-REL-005 | Curva de vendas por dia da semana e por faixa de horário | S |
| RF-REL-006 | Relatório de consumo de insumos por período | S |
| RF-REL-007 | Dashboard operacional consolidado (faturamento do dia, pedidos em aberto, alertas de estoque) | S |
| RF-REL-008 | Exportar relatórios em CSV | S |

### 6.8 Segurança e acesso (SEG)

| ID | Requisito | Prior. |
|----|-----------|--------|
| RF-SEG-001 | Autenticar usuário por e-mail e senha, emitindo token JWT | M |
| RF-SEG-002 | Autorizar as operações conforme o perfil do usuário | M |
| RF-SEG-003 | Registrar trilha de auditoria de operações sensíveis (cancelamento, desconto, ajuste de estoque, fechamento de caixa) | M |
| RF-SEG-004 | Permitir ao administrador criar, editar e desativar usuários | M |
| RF-SEG-005 | Forçar a troca da senha no primeiro acesso | S |

---

## 7. Regras de negócio

> **As regras de negócio saíram deste documento.**
>
> A versão 1.0 mantinha 40 regras aqui. Na consolidação com o *Catálogo de Regras v0.1*,
> todas foram cruzadas regra a regra: 14 convergiram, 9 conflitaram e foram resolvidas com
> decisão registrada, 17 foram incorporadas como complemento e 4 foram **revogadas** por
> estarem incorretas.
>
> **Fonte de verdade:** [`03-catalogo-de-regras-de-negocio.md`](03-catalogo-de-regras-de-negocio.md)
> — 138 regras, com tipo, entidades, mecanismo de implementação, estado de validação e
> escopo no MVP.
>
> **Auditoria do cruzamento:** [`07-cruzamento-catalogo-x-levantamento.md`](07-cruzamento-catalogo-x-levantamento.md)

Regra de negócio não mora em dois lugares. Manter as duas listas garantiria que elas
divergiriam — e a divergência apareceria como bug meses depois, quando ninguém lembrasse
qual documento estava certo.

### 7.1 Regras deste documento que foram revogadas

Registradas aqui para que quem leu a versão 1.0 saiba o que mudou e por quê.

| Regra v1.0 | O que dizia | Substituída por | Motivo |
|------------|-------------|-----------------|--------|
| RN-042 | Custo do insumo por **custo médio ponderado** | RN-EST-011 — custo específico por lote | O domínio é de perecíveis: sem lote não há validade, FEFO nem perda por vencimento. [ADR-0010](adr/0010-custeio-por-lote-com-fefo.md) |
| RN-015 | Cancelar pedido **devolve** o estoque por estorno | RN-EST-018 e RN-EST-019 | O lanche pronto não volta à geladeira. Devolver o saldo esconderia a perda — o número que o dono precisa ver |
| RN-046 | O saldo de insumo **pode** ficar negativo | RN-EST-009 + RN-VEN-021 parametrizada | Saldo negativo torna o custo da venda indeterminado. Substituído por bloqueio configurável (`PAR-VEN-003`) |
| RN-048 | Produto **sem** ficha técnica, com custo zero | RN-PRO-001 | Custo zero contamina o CMV consolidado em silêncio. Bebida revendida usa ficha de item único |

### 7.2 Onde encontrar o que estava aqui

| Assunto | Onde está agora |
|---------|-----------------|
| Produtos, preço, modificadores | [Catálogo — VEN](03-catalogo-de-regras-de-negocio.md#ven--vendas-e-comercial) |
| Pedidos, desconto, pagamento | [Catálogo — VEN](03-catalogo-de-regras-de-negocio.md#ven--vendas-e-comercial) |
| Salão e comandas | [Catálogo — VEN](03-catalogo-de-regras-de-negocio.md#ven--vendas-e-comercial), estrutura do pedido |
| Delivery | [Catálogo — LOG](03-catalogo-de-regras-de-negocio.md#log--logística-e-frota) |
| Estoque e custos | [Catálogo — EST](03-catalogo-de-regras-de-negocio.md#est--estoque) e [PRO](03-catalogo-de-regras-de-negocio.md#pro--produção) |
| Financeiro | [Catálogo — FIN](03-catalogo-de-regras-de-negocio.md#fin--financeiro) |
| Gerais e transversais | [Catálogo — GER](03-catalogo-de-regras-de-negocio.md#ger--gerais-e-transversais) |
| Valores configuráveis (10%, limites) | [Parâmetros do sistema](04-parametros-do-sistema.md) |

## 8. Requisitos não-funcionais

| ID | Categoria | Requisito |
|----|-----------|-----------|
| RNF-001 | Desempenho | O lançamento de um item no pedido deve responder em até **500 ms** no percentil 95 — é a operação mais repetida no pico de serviço. |
| RNF-002 | Desempenho | Consultas de listagem devem ser paginadas, com no máximo 50 registros por página. |
| RNF-003 | Desempenho | Relatórios de período devem responder em até 5 s para uma janela de 12 meses. |
| RNF-004 | Disponibilidade | O sistema deve operar durante todo o horário de funcionamento do restaurante; janelas de manutenção apenas fora dele. |
| RNF-005 | Segurança | Senhas armazenadas com hash + salt (nunca em texto claro). |
| RNF-006 | Segurança | Autenticação por JWT com expiração; toda rota de negócio exige token válido. |
| RNF-007 | Segurança | O banco de dados nunca é exposto publicamente — acesso apenas pela rede interna do Docker. |
| RNF-008 | Segurança | Segredos (senha do banco, chave JWT, chave SSH) nunca versionados no repositório. |
| RNF-009 | Auditoria | Operações sensíveis registram usuário, data/hora, operação e valores anterior e posterior. |
| RNF-010 | Usabilidade | A tela de lançamento de pedido deve ser operável por toque, com alvos de no mínimo 44 px — o atendente usa tablet, não mouse. |
| RNF-011 | Usabilidade | Fluxo de venda de balcão concluído em no máximo 5 interações para um pedido de item único. |
| RNF-012 | Compatibilidade | Front responsivo, funcional em tablet (1024×768) e desktop. |
| RNF-013 | Manutenibilidade | Backend em Clean Architecture com CQRS; o domínio não depende de infraestrutura. Ver [ADR-0002](adr/0002-clean-architecture-com-cqrs.md). |
| RNF-014 | Manutenibilidade | Toda mudança de schema por migration versionada do EF Core — nunca DDL manual em produção. |
| RNF-015 | Portabilidade | Aplicação containerizada (Docker), reproduzível em qualquer host com Docker instalado. |
| RNF-016 | Integridade | Operações que envolvem múltiplas tabelas (pedido + itens + estoque + pagamento) executam em transação única. |
| RNF-017 | Backup | Volume do PostgreSQL com backup diário e antes de qualquer apresentação. |
| RNF-018 | Extensibilidade | A API é *mobile-first by design* — nenhuma regra de negócio reside no frontend. |
| RNF-019 | Observabilidade | Log estruturado de requisições e erros, com identificador de correlação por requisição. |
| RNF-020 | Localização | Interface e dados em português do Brasil, moeda BRL, fuso America/Sao_Paulo. |

---

## 9. Critérios de aceite dos fluxos críticos

Formato **Given-When-Then**, para os requisitos de maior risco. Cada critério cita as regras
do [catálogo consolidado](03-catalogo-de-regras-de-negocio.md) que prova.

> **Revisados na consolidação.** CA-02 e CA-03 foram reescritos: assumiam regras revogadas
> (devolução de estoque no cancelamento e custo médio ponderado). CA-01 e CA-05 ganharam
> lote e dia operacional.

### CA-01 — Baixa automática de estoque por FEFO (`RN-EST-007`, `RN-EST-011`, `RN-PRO-006`)

> **Dado** que o produto "X-Burger" tem ficha técnica vigente com 1 pão, 150 g de carne e
> 2 fatias de queijo, todos com perda de preparo 0%,
> **e** que há dois lotes de carne — lote A com 200 g validade 20/08 a R$ 0,03/g, e lote B
> com 5.000 g validade 25/08 a R$ 0,035/g,
> **Quando** um pedido com 2 unidades de X-Burger for **confirmado**,
> **Então** são gerados movimentos de saída consumindo **primeiro o lote A** (200 g) e o
> restante do lote B (100 g),
> **e** o custo da carne no item é `200 × 0,03 + 100 × 0,035 = R$ 9,50`,
> **e** cada movimento registra lote, custo unitário, usuário e data operacional.

### CA-02 — Cancelamento após produção gera perda (`RN-EST-018`, `RN-EST-019`, `RN-VEN-019`)

> **Dado** um item de pedido que já entrou em produção e consumiu insumo,
> **Quando** um Gerente cancelar o item informando o motivo,
> **Então** **nenhum** insumo é devolvido ao estoque,
> **e** é gerado movimento de **perda** com motivo `cancelamento`, vinculado ao item,
> **e** os movimentos de saída originais permanecem inalterados,
> **e** o pedido passa a `Cancelado` com motivo e autor registrados.
>
> **E, no caso simétrico:** dado um item cancelado **antes** de entrar em produção,
> **então** nenhum movimento de estoque é gerado — nem saída, nem perda.

### CA-03 — Custo específico por lote (`RN-EST-010`, `RN-EST-011`, `ADR-0010`)

> **Dado** um insumo com lote A de 10 kg a R$ 20,00/kg,
> **Quando** entrar uma compra de 10 kg a R$ 30,00/kg,
> **Então** é criado o **lote B**, com custo próprio de R$ 30,00/kg,
> **e** o lote A **permanece** a R$ 20,00/kg — nenhum custo é recalculado,
> **e** o saldo total do insumo, derivado dos movimentos, é 20 kg,
> **e** a próxima saída custa conforme o lote que o FEFO selecionar.

### CA-04 — Congelamento da taxa de entrega (`RN-LOG-009`)

> **Dado** um pedido de delivery criado quando a zona "Centro" tinha taxa de R$ 5,00,
> **Quando** a taxa da zona "Centro" for alterada para R$ 8,00,
> **Então** o pedido já criado continua com taxa de R$ 5,00,
> **e** pedidos novos para a mesma zona passam a usar R$ 8,00.

### CA-05 — Fechamento de caixa com diferença (`RN-FIN-007`, `RN-FIN-015`, `RN-FIN-016`)

> **Dado** um caixa aberto com R$ 100,00 de fundo, R$ 500,00 recebidos em formas que
> movimentam espécie, R$ 300,00 recebidos em cartão, R$ 50,00 de suprimento e R$ 200,00 de
> sangria,
> **Quando** o operador informar R$ 445,00 como valor contado,
> **Então** o esperado é R$ 450,00 — os R$ 300,00 em cartão **não** entram,
> **e** a diferença registrada é de −R$ 5,00 como quebra de caixa,
> **e** o caixa é marcado como fechado e não pode ser reaberto.

### CA-06 — Uma comanda por mesa (`RN-VEN-006`)

> **Dado** que a mesa 5 já tem uma comanda em aberto,
> **Quando** um atendente tentar abrir outra comanda na mesa 5,
> **Então** a operação é rejeitada com mensagem indicando a comanda existente,
> **e** nenhuma comanda nova é criada,
> **e** a rejeição ocorre na aplicação — a `EXCLUDE` constraint do banco nunca é atingida
> por operação normal, apenas como última defesa.

### CA-07 — Pagamento com múltiplas formas e troco (`RN-VEN-023`, `RN-VEN-024`, `RN-VEN-030`)

> **Dado** um pedido no valor de R$ 100,00,
> **Quando** for registrado pagamento de R$ 60,00 em cartão e R$ 50,00 em espécie,
> **Então** o pedido é finalizado,
> **e** o troco registrado é de R$ 10,00 e não integra a receita,
> **e** apenas os R$ 50,00 em espécie entram no saldo do caixa.

### CA-08 — Restrição de visibilidade de custo (`RN-GER-012`)

> **Dado** um usuário com perfil Atendente,
> **Quando** ele acessar a listagem de produtos ou qualquer relatório,
> **Então** os campos de custo, CMV e margem **não são retornados pela API**,
> **e** não apenas ocultados na tela.

### CA-09 — Dia operacional (`RN-GER-006`, `PAR-GER-001`)

> **Dado** um pedido registrado às 01h30 de domingo,
> **Quando** o faturamento for apurado por dia operacional,
> **Então** o pedido pertence ao dia operacional de **sábado**,
> **e** integra o fechamento do caixa aberto no sábado,
> **e** o relatório de melhor dia da semana o contabiliza em sábado.

### CA-10 — Sub-receita recursiva (`RN-PRO-004`, `RN-PRO-012`)

> **Dado** que o "Molho da Casa" é um produto intermediário com ficha técnica própria e é
> estocado,
> **e** que o X-Burger tem 30 ml de Molho da Casa na sua ficha técnica,
> **Quando** um X-Burger for confirmado,
> **Então** a saída é de 30 ml **do estoque do Molho da Casa**, e não dos insumos que o
> compõem,
> **e** a produção antecipada de molho gera entrada de estoque do intermediário, consumindo
> os insumos da ficha dele.

---

## 10. Matriz de rastreabilidade — dor do nicho → requisito

Atualizada com as regras do [catálogo consolidado](03-catalogo-de-regras-de-negocio.md).

| Dor mapeada | Requisitos que a atendem | Regras do catálogo |
|-------------|--------------------------|--------------------|
| Pedidos por múltiplos canais | RF-PED-001, RF-PED-009, RF-PED-011 | `RN-VEN-001`, `RN-VEN-026` |
| Insumos perecíveis / ruptura no pico | RF-EST-002, RF-EST-004, RF-EST-005 | `RN-EST-004` a `RN-EST-008`, `RN-EST-016` |
| Personalização do lanche | RF-CAD-003, RF-CAD-004, RF-PED-002 | `RN-VEN-015`, `RN-VEN-016`, `RN-VEN-027`, `RN-VEN-028` |
| Fila da cozinha no pico | RF-PED-006, RF-CAD-013 | `RN-PRO-008` a `RN-PRO-011`, `RN-PRO-013` |
| Taxa de entrega por zona | RF-CAD-009, RF-DEL-002, RF-DEL-003 | `RN-LOG-003`, `RN-LOG-004`, `RN-LOG-009` |
| Margem apertada / CMV desconhecido | RF-CAD-006, RF-EST-006, RF-EST-007, RF-REL-003 | `RN-PRO-005`, `RN-PRO-006`, `RN-EST-011`, `RN-GER-012` |
| Fidelização | *(nível Modelo no MVP)* | `RN-CRM-004` a `RN-CRM-008` |
| Caixa sem controle | RF-FIN-001 a RF-FIN-004 | `RN-FIN-007` a `RN-FIN-009`, `RN-FIN-013` a `RN-FIN-016` |
| Faturamento por dia não bate | *(descoberta na consolidação)* | `RN-GER-006` ⚠⚠ |
| Margem real do delivery desconhecida | *(descoberta na consolidação)* | `RN-EST-017`, `RN-LOG-007`, `RN-FIN-003` |

---

## 11. Premissas, restrições e riscos

### 11.1 Premissas

| # | Premissa |
|---|----------|
| P1 | O estabelecimento tem conexão de internet estável durante o serviço — o MVP **não** opera offline. |
| P2 | O atendente opera em tablet ou desktop; não há terminal dedicado de PDV. |
| P3 | O cardápio e as fichas técnicas são cadastrados antes da operação começar. |
| P4 | Um único estabelecimento será usado na demonstração, ainda que o modelo suporte vários. |

### 11.2 Restrições

| # | Restrição | Origem |
|---|-----------|--------|
| R1 | Toda a stack roda em uma **única VPS** (front, back e banco) | Orçamento do projeto |
| R2 | Repositório e CI/CD em Azure DevOps | Definição do curso/time |
| R3 | Prazo fechado em 9 sprints, com entregas fixas por sprint | Cronograma acadêmico |
| R4 | Sem integração com TEF, maquininha ou emissor fiscal | Escopo e custo de homologação |
| R5 | Domínio ainda não adquirido — HTTPS depende dessa compra | Pendência aberta |

### 11.3 Riscos

| # | Risco | Prob. | Impacto | Mitigação |
|---|-------|-------|---------|-----------|
| RS1 | Escopo do MVP grande demais para 9 sprints | Alta | Alto | Priorização MoSCoW rígida; entregar `M` completo antes de qualquer `S` |
| RS2 | Recálculo de custo em cascata (insumo → produtos) degradar a performance | Média | Médio | Recalcular de forma assíncrona e persistir o custo no produto, em vez de calcular sob demanda |
| RS3 | VPS com pouca RAM derrubar containers | Média | Alto | Swap de 2 GB, limites de memória por container, monitoramento |
| RS4 | Falta de dados reais empobrecer o BI da Sprint 7 & 8 | Alta | Médio | Gerar carga sintética realista (sazonalidade por dia e horário) já na Sprint 5 & 6 |
| RS5 | Ausência de entrevista com estabelecimento real enfraquecer a defesa | Média | Médio | Realizar entrevista com dono de hamburgueria e anexar o roteiro e as respostas a este documento |
| RS6 | Domínio/HTTPS não resolvido até a apresentação | Média | Alto | DuckDNS + Let's Encrypt como plano B, validado com antecedência |

---

## 12. Entidades candidatas (ponte para a Sprint 3 & 4)

Levantamento preliminar derivado do [catálogo consolidado](03-catalogo-de-regras-de-negocio.md).
A modelagem formal — atributos, cardinalidades, normalização — é entrega da Sprint 3 & 4.

| Agregado | Entidades candidatas |
|----------|----------------------|
| Unidade e parâmetros | `unidade`, `parametro`, `parametro_valor`, `dim_tempo` |
| Acesso e auditoria | `usuario`, `perfil`, `permissao`, `log_auditoria`, `autorizacao` |
| Cardápio | `categoria`, `produto`, `preco_produto`, `produto_composicao`, `disponibilidade_produto` |
| Modificadores | `grupo_modificador`, `opcao_modificador`, `produto_grupo_modificador` |
| Ficha técnica | `ficha_tecnica`, `ficha_tecnica_item`, `insumo`, `unidade_medida`, `conversao_unidade` |
| Estoque | `lote`, `movimento_estoque`, `tipo_movimento`, `motivo_perda`, `local_estoque`, `inventario` |
| Produção | `ordem_producao`, `estacao`, `etapa_preparo` |
| Compras | `fornecedor`, `insumo_fornecedor`, `pedido_compra`, `pedido_compra_item`, `cotacao`, `recebimento`, `ocorrencia_recebimento` |
| Clientes | `cliente`, `endereco_cliente`, `consentimento`, `avaliacao` |
| Campanhas | `cupom`, `uso_cupom`, `campanha` |
| Salão | `mesa`, `comanda` |
| Pedidos | `pedido`, `canal_venda`, `status_pedido`, `item_pedido`, `item_pedido_ajuste`, `pagamento` |
| Logística | `zona_entrega`, `entrega`, `evento_entrega`, `rota`, `entregador`, `veiculo`, `status_entrega` |
| Financeiro | `caixa_diario`, `movimento_caixa`, `forma_pagamento`, `conta_pagar`, `conta_receber`, `lancamento_financeiro`, `plano_de_contas`, `centro_de_custo`, `conta_bancaria`, `rateio` |
| RH | `colaborador`, `cargo`, `escala`, `turno`, `apontamento_ponto`, `evento_folha` |
| Fiscal | `documento_fiscal`, `ncm`, `cfop`, `regime_tributario`, `aliquota` |

### Sete pontos de atenção para a modelagem

Cada um deriva de uma das [regras estruturais](03-catalogo-de-regras-de-negocio.md#15-as-sete-regras-estruturais).
Resolvê-los é pré-requisito do modelo lógico.

1. **`data_operacional` como coluna gerada** (`RN-GER-006`) em toda tabela com data de evento
   comercial, indexada. Nenhuma agregação de negócio usa `dh_criacao`.
   Ver [ADR-0011](adr/0011-dia-operacional.md).

2. **Item estocável polimórfico** (`RN-PRO-004`) — `ficha_tecnica_item` precisa referenciar
   algo que pode ser `insumo` **ou** `produto` (a sub-receita). Exige decisão entre
   especialização, tabela unificadora ou FK dupla com `CHECK` de exclusividade — **e**
   proteção contra ciclo: uma ficha não pode conter a si mesma, direta ou indiretamente.
   É a decisão mais difícil da Sprint 3 & 4.

3. **Estoque como livro-razão com lote** (`RN-EST-001`, `RN-EST-004`) — nunca coluna de
   saldo editável. Saldo materializa em view por `(unidade, insumo, lote)`.
   Ver [ADR-0008](adr/0008-estoque-como-livro-razao.md) e [ADR-0010](adr/0010-custeio-por-lote-com-fefo.md).

4. **Congelamento de valores históricos** — `item_pedido` guarda cópia do preço praticado
   (`RN-VEN-008`) e do custo apurado; `pedido` guarda a taxa de entrega vigente
   (`RN-LOG-009`); `documento_fiscal` guarda o retrato do emitente e do destinatário
   (`RN-FIS-006`). Referenciar o cadastro atual não basta.

5. **Vigência como padrão, não exceção** — `preco_produto` (`RN-VEN-010`),
   `ficha_tecnica` (`RN-PRO-002`) e `parametro_valor` seguem o mesmo desenho:
   `vigencia_inicio`, `vigencia_fim`, `EXCLUDE` contra sobreposição. Vale a pena tratar como
   um padrão único de modelagem.

6. **`unidade_id` em toda tabela transacional** (`RN-GER-009`), como **primeira coluna** de
   toda chave única de negócio e de todo índice composto.

7. **Comanda × Pedido** — decidir se a comanda é um pedido de canal salão com lançamentos
   incrementais, ou uma entidade que agrega vários pedidos. `RN-VEN-004` e `RN-VEN-007`
   (transferência e unificação) apontam para a segunda; a primeira é mais simples. **Decisão
   em aberto**, registrada no [índice de ADRs](adr/README.md#decisões-ainda-em-aberto).

---

## Anexos previstos

- [ ] **Entrevista com dono de hamburgueria** — roteiro pronto em
      [`05-fila-de-validacao-com-cliente.md`](05-fila-de-validacao-com-cliente.md).
      É o risco RS5 e o item de maior valor pendente
- [ ] Diagrama de casos de uso por ator
- [ ] Protótipos de baixa fidelidade: lançamento de pedido e mapa de mesas
- [ ] Modelo conceitual (Sprint 3 & 4), resolvendo os sete pontos de atenção acima
