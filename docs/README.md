# GrelhaHub — Documentação

> Projeto conduzido em **Document First** e **Spec-Driven Development**: nenhum
> comportamento existe no sistema sem estar documentado antes. A regra vem primeiro, a spec
> depois, o código por último.

---

## Por onde começar

| Se você é… | Leia nesta ordem |
|------------|------------------|
| **Novo no projeto** | [Arquitetura](arquitetura/) → [Contexto](../GRELHAHUB-context.md) → [Glossário](02-glossario.md) |
| **Vai apresentar o sistema** | [Arquitetura](arquitetura/) — modelo de dados com diagrama, backend e frontend, escritos para quem não é da área |
| **Vai implementar** | [Guia de specs](06-guia-de-specs.md) → [Catálogo de regras](03-catalogo-de-regras-de-negocio.md) → [ADRs](adr/) |
| **Vai fazer tela** | [Design System Chapa](08-design-system.md) → [proposta navegável](design/proposta-chapa.html) |
| **Vai mexer no banco** | [Modelo de dados](arquitetura/01-modelo-de-dados.md) — diagrama, chaves e tipos, extraídos do modelo real |
| **Vai modelar coisa nova** | [Catálogo §15 — regras estruturais](03-catalogo-de-regras-de-negocio.md#15-as-sete-regras-estruturais) → [Levantamento §12 — entidades](01-documento-de-levantamento.md#12-entidades-candidatas-ponte-para-a-sprint-3--4) → [ADR-0008](adr/0008-estoque-como-livro-razao.md), [0010](adr/0010-custeio-por-lote-com-fefo.md), [0011](adr/0011-dia-operacional.md) |
| **Vai entrevistar o cliente** | [Fila de validação](05-fila-de-validacao-com-cliente.md) |
| **Quer entender uma decisão** | [Índice de ADRs](adr/README.md) |

---

## Mapa dos documentos

### Fonte de verdade

| Documento | O que contém | Quando muda |
|-----------|--------------|-------------|
| **[03 — Catálogo de Regras](03-catalogo-de-regras-de-negocio.md)** ★ | **182 regras de negócio** com tipo, entidades, mecanismo de implementação, estado de validação e escopo no MVP | A cada entrevista (estado das regras) e a cada implementação que descobre regra nova |
| [04 — Parâmetros do sistema](04-parametros-do-sistema.md) | Valores configuráveis da operação (`PAR-*`) | Quando surge valor comercial configurável |

> **Regra de ouro:** regra de negócio mora **só** no catálogo. Se você encontrar uma regra
> escrita em outro documento, é resíduo — reporte.

### Contexto e requisitos

| Documento | O que contém |
|-----------|--------------|
| [01 — Documento de Levantamento](01-documento-de-levantamento.md) | Visão de produto, escopo dos 10 módulos, atores, processos, **60 requisitos funcionais**, **20 requisitos não-funcionais**, critérios de aceite, riscos, entidades candidatas |
| [02 — Glossário](02-glossario.md) | Vocabulário controlado. Base do dicionário de dados da Sprint 5 & 6 |

### Processo

| Documento | O que contém |
|-----------|--------------|
| [05 — Fila de validação](05-fila-de-validacao-com-cliente.md) | Roteiro de entrevista: 20 perguntas em ordem de prioridade, com o que cada uma resolve |
| [06 — Guia de specs](06-guia-de-specs.md) | Como a regra vira spec e a spec vira código; ordem das ondas de implementação; definição de pronto |
| [**08 — Design System Chapa**](08-design-system.md) ★ | Tokens, componentes e as 8 regras `CHAPA-*`. **Base oficial da interface** |
| [07 — Cruzamento](07-cruzamento-catalogo-x-levantamento.md) | Auditoria do cruzamento entre as duas fontes: 14 convergências, 9 conflitos resolvidos, 71 lacunas, 17 complementos |

### Arquitetura — como o sistema é feito

| Documento | O que contém |
|-----------|--------------|
| [**Arquitetura**](arquitetura/) ★ | Mapa da pasta, o sistema em um parágrafo e o vocabulário |
| [01 — Modelo de dados](arquitetura/01-modelo-de-dados.md) | Diagrama de relacionamentos e o dicionário completo: chaves, tipos, índices, restrições. **Extraído do modelo real** |
| [02 — Backend](arquitetura/02-backend.md) | As quatro camadas, o caminho de uma requisição, onde cada regra é imposta |
| [03 — Frontend](arquitetura/03-frontend.md) | Telas, quem vê o quê, e como a identidade visual chega até elas |

> Escritos em dois níveis: a **Parte 1** de cada capítulo é para apresentar ao time sem jargão;
> a **Parte 2** é o detalhe para quem vai mexer.

### Decisões e specs

| Diretório | O que contém |
|-----------|--------------|
| [`adr/`](adr/) | 14 Architecture Decision Records + decisões em aberto |
| [`design/`](design/) | Propostas de design navegáveis. Abra o HTML direto no navegador |
| [`specs/`](specs/) | Specs de implementação. [Template](specs/_template.md) |

---

## O fluxo de trabalho

```
   ┌──────────────┐    ┌───────────────┐    ┌──────────┐    ┌───────────────┐
   │  entrevista  │───►│   catálogo    │───►│   spec   │───►│ código + teste │
   │  (valida)    │    │   de regras   │    │          │    │ citando a regra│
   └──────────────┘    └───────────────┘    └──────────┘    └───────────────┘
          ▲                    ▲                                     │
          │                    │      regra nova descoberta          │
          │                    └─────────────────────────────────────┘
          │                              (para o código, escreve a regra, volta)
          │
   regra em HIP que a implementação questionou volta para a fila de validação
```

**As quatro regras do processo** — detalhadas no [guia de specs](06-guia-de-specs.md):

1. Nenhuma regra de negócio nasce no código
2. Toda spec cita as regras que implementa, por ID
3. Todo teste cita a regra que prova, no `DisplayName`
4. A spec é escrita antes do código e corrigida depois dele

---

## Estado atual

| Item | Situação |
|------|----------|
| Regras catalogadas | **159** — 140 em `HIP`, 19 em `DEC` (decisão de produto), das quais 6 com origem `IMP` |
| Regras validadas com cliente | **0** — a entrevista é o próximo marco. Risco RS5 |
| ADRs | **20 aceitos** (1 substituída), 4 decisões em aberto. **ADR-0013 revisa o ADR-0005**; **ADR-0018 substitui a ADR-0015** e põe a imagem no banco; ADR-0017 separa os três bancos; ADR-0019 conclui o CQRS; ADR-0020 remove a sessão de suporte |
| Specs | **9 — todas `ENTREGUE`.** `GER-001` a `007` (onda 0), `VEN-001` (fatia vertical) e `VEN-002` (imagem) |
| Cobertura de regra por spec | **44 de 159** — `RN-EST-021` a `023` têm regra escrita e código pendente (onda 3) |
| Código entregue | Fatia vertical (`SPEC-VEN-001`) + **onda 0 completa**: unidade e convenções, dia operacional, parâmetros com vigência, usuários e permissões, trilha de auditoria, módulos por unidade. **40 regras com o ID citado em teste** |
| Testes | **240 verdes** — 65 de domínio + 7 de aplicação + 168 de integração contra PostgreSQL real (Testcontainers, exige Docker) |
| Gates | `dotnet build -c Release`, `dotnet test`, `npm run lint` e `npm run build` — **todos limpos** |
| Vitrine pública | ✔ `/` em `grelhahub.com` — design system §8, regra `CHAPA-09` |
| Sprint | 1 & 2 — documentação entregue; falta entrevista e anexos |

### Bloqueadores para começar a modelar

As [sete regras estruturais](03-catalogo-de-regras-de-negocio.md#15-as-sete-regras-estruturais)
precisam sair de `HIP`. Duas delas decidem sozinhas o formato do modelo:

- **`RN-GER-006`** — dia operacional. Se errar, o BI inteiro fica errado em definitivo
- **`RN-PRO-004`** — sub-receita recursiva. Se errar, a ficha técnica é remodelada do zero

---

## Convenções de identificação

| Prefixo | Significado | Onde vive |
|---------|-------------|-----------|
| `RN-<MOD>-nnn` | Regra de negócio | [Catálogo](03-catalogo-de-regras-de-negocio.md) |
| `PAR-<MOD>-nnn` | Parâmetro configurável | [Parâmetros](04-parametros-do-sistema.md) |
| `RF-<MOD>-nnn` | Requisito funcional | [Levantamento §6](01-documento-de-levantamento.md#6-requisitos-funcionais) |
| `RNF-nnn` | Requisito não-funcional | [Levantamento §8](01-documento-de-levantamento.md) |
| `SPEC-<MOD>-nnn` | Spec de implementação | [`specs/`](specs/) |
| `ADR-nnnn` | Decisão de arquitetura | [`adr/`](adr/) |
| `CA-nn` | Critério de aceite | Levantamento §9 e cada spec |

**Módulos:** `GER` gerais · `VEN` vendas · `PRO` produção · `EST` estoque · `COM` compras ·
`FIN` financeiro · `CRM` relacionamento · `LOG` logística · `RH` recursos humanos ·
`FIS` fiscal
