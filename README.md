<div align="center">

# GrelhaHub

**DO PEDIDO À BRASA**

ERP para pequenos e médios restaurantes e deliveries, com especialização progressiva em
hamburgueria.

</div>

---

## O problema

> O dono da hamburgueria não sabe, ao fim do mês, **qual dos seus lanches dá lucro** — porque o
> custo do lanche muda a cada compra de insumo e ninguém recalcula. E não sabe, durante o serviço,
> **o que vai faltar** — porque a baixa de estoque nunca acompanha a venda em tempo real.

Um único cadastro de produto alimenta ao mesmo tempo o cardápio, a ficha técnica, a baixa de
estoque e o cálculo de CMV.

---

## Por onde começar

Escolha a linha que descreve você. Cada caminho tem três paradas, e nessa ordem.

| Você quer… | Caminho |
|------------|---------|
| **Entender o produto** | [O problema](#o-problema) → [Estado atual](#estado-atual) → [Glossário](docs/02-glossario.md) |
| **Entender uma regra de negócio** | [Catálogo](docs/03-catalogo-de-regras-de-negocio.md) → `grep` pelo ID → a spec na coluna de rastreabilidade |
| **Implementar** | [Guia de specs](docs/06-guia-de-specs.md) → [Catálogo](docs/03-catalogo-de-regras-de-negocio.md) → [ADRs](docs/adr/) |
| **Mexer em tela** | [Design System Chapa](docs/08-design-system.md) → [skill de validação](.claude/skills/subir-e-validar/SKILL.md) |
| **Entender o banco** | [Modelo de dados](docs/arquitetura/01-modelo-de-dados.md) → [ADR-0009](docs/adr/0009-onde-as-regras-de-negocio-vivem.md) → [ADR-0011](docs/adr/0011-dia-operacional.md) |
| **Saber por que algo é assim** | [ADRs](docs/adr/README.md) — 22 decisões, com as alternativas e o que cada uma custou |
| **Rodar** | [Rodar localmente](#rodar-localmente) |

📖 **O mapa completo da documentação está em [`docs/README.md`](docs/README.md).**

---

## O vocabulário — o dicionário dos IDs

Tudo neste repositório é identificado por prefixo, e **o prefixo diz onde a coisa mora**. Este é o
atalho mais útil do projeto: viu um ID, você sabe onde procurar.

| Prefixo | O que é | Mora em | Exemplo |
|---------|---------|---------|---------|
| `RN-<MOD>-nnn` | **Regra de negócio** — a fonte de verdade | [Catálogo §5 em diante](docs/03-catalogo-de-regras-de-negocio.md) | `RN-GER-006` — dia operacional |
| `PAR-<MOD>-nnn` | **Parâmetro** — valor configurável da operação | [Parâmetros](docs/04-parametros-do-sistema.md) | `PAR-VEN-004` — teto de produtos |
| `SPEC-<MOD>-nnn` | **Spec** — como a regra vira código | [`docs/specs/`](docs/specs/) | `SPEC-VEN-004` — painel de senha |
| `ADR-nnnn` | **Decisão de arquitetura**, com alternativas e custo | [`docs/adr/`](docs/adr/) | `ADR-0011` — dia operacional |
| `CHAPA-nn` | **Regra de interface** — o design system | [Design System §4](docs/08-design-system.md) | `CHAPA-02` — uma barra âmbar por grupo |
| `RF-` / `RNF-` | Requisito funcional / não funcional | [Levantamento](docs/01-documento-de-levantamento.md) | `RNF-005` — senha com hash |
| `MOD` `GEN` `TRG` `VW` `APP` | **Mecanismo** — em que camada a regra é imposta | [ADR-0009](docs/adr/0009-onde-as-regras-de-negocio-vivem.md) | `GEN` = coluna gerada |
| `HIP` `DEC` `VAL` | **Estado** da regra — hipótese, decidida pelo time, validada com cliente | [Catálogo §2](docs/03-catalogo-de-regras-de-negocio.md#2-como-ler-este-documento) | — |

Os módulos: `GER` geral · `VEN` vendas · `PRO` produção · `EST` estoque · `COM` compras ·
`FIN` financeiro · `CRM` clientes · `LOG` logística · `RH` pessoas · `FIS` fiscal.

> ⚠ **Nunca responda de memória sobre uma regra — `grep` pelo ID.** As regras mudam de estado a
> cada entrevista, e o modo de falha real deste repositório não é inventar regra: é **citar o ID
> mais parecido**. O código fica plausível, o comentário fica plausível, e a rastreabilidade morre
> em silêncio.

---

## Como este projeto é construído

**Document First + Spec-Driven Development.** Nenhum comportamento existe no sistema sem estar
documentado antes:

```
catálogo de regras  →  spec  →  código + teste que cita a regra no DisplayName
```

Se durante a implementação aparecer um comportamento necessário que **não está no catálogo**, a
implementação **para**, a regra é escrita, e só então o código continua.

```csharp
[Fact(DisplayName = "RN-EST-018 — item cancelado após produção gera perda, não devolução")]
```

É esse `DisplayName` que torna a rastreabilidade real: quando uma regra é rejeitada na entrevista,
um `grep` pelo ID encontra a spec, o handler e o teste.

---

## As sete regras estruturais

Errar aqui custa retrabalho de projeto, não de código. A lista completa está no
[Catálogo §15](docs/03-catalogo-de-regras-de-negocio.md#15-as-sete-regras-estruturais).

| Regra | O que exige |
|-------|-------------|
| [`RN-GER-006`](docs/03-catalogo-de-regras-de-negocio.md) ⚠⚠ | Dia operacional pela **virada cadastrada da unidade**. Nunca agrupe por data de criação |
| `RN-PRO-004` ⚠⚠ | Ficha técnica **recursiva** — o molho da casa é produto **e** insumo |
| `RN-EST-001` | Saldo **nunca** é coluna editável: é soma dos movimentos |
| `RN-EST-004` + `RN-EST-011` | Todo movimento referencia **lote**; custo é o do lote consumido, consumo **FEFO** |
| `RN-VEN-008` + `RN-VEN-010` | Preço praticado é **copiado** na venda; preço de tabela tem vigência |
| `RN-VEN-018` | O ciclo do pedido, e o que a Confirmação dispara |
| `RN-GER-009` | `unidade_id` em toda tabela transacional, **primeira coluna** de toda chave única |

---

## Stack

| Camada | Tecnologia | Decisão |
|--------|------------|---------|
| Backend | .NET 10 · ASP.NET Core · Clean Architecture + CQRS (MediatR) · FluentValidation | [ADR-0001](docs/adr/0001-stack-dotnet-react-postgresql.md), [ADR-0002](docs/adr/0002-clean-architecture-com-cqrs.md), [ADR-0019](docs/adr/0019-cqrs-com-modelo-de-leitura-proprio.md) |
| Acesso | ASP.NET Core Identity — claims, roles, lockout | [ADR-0022](docs/adr/0022-identity-como-modelo-de-acesso.md) |
| Banco | PostgreSQL · EF Core 10 · `snake_case` · `uuid` · `timestamptz` · **três bancos** | [ADR-0004](docs/adr/0004-postgresql-como-sgbd.md), [ADR-0017](docs/adr/0017-tres-bancos-aplicacao-arquivo-e-log.md) |
| Frontend | React 19 · TypeScript · Vite — REST + JWT, sem webfont | [ADR-0003](docs/adr/0003-frontend-e-backend-desacoplados.md), [ADR-0012](docs/adr/0012-direcao-visual-chapa.md) |
| Infra | Docker · Nginx · VPS única · Azure DevOps | [ADR-0006](docs/adr/0006-deploy-em-vps-unica-com-docker.md) |

---

## Onde cada coisa mora

```
docs/                      a fonte de verdade — comece por docs/README.md
├── 03-catalogo…           ★ as 190 regras de negócio
├── 04-parametros…           valores configuráveis (PAR-*)
├── 06-guia-de-specs…        formato da spec, ondas, definição de pronto
├── 08-design-system…      ★ base visual obrigatória (CHAPA-*)
├── adr/                     22 decisões de arquitetura
├── specs/                   14 specs entregues
└── arquitetura/             modelo de dados, backend e frontend — extraídos do modelo real

backend/src/
├── GrelhaHub.Domain/         sem dependências de infraestrutura
├── GrelhaHub.Application/    Features/<Modulo>/<Caso>/ — Command|Query + Handler + Validator
├── GrelhaHub.Infrastructure/ EF Core, migrations, Identity, PostgreSQL
└── GrelhaHub.Api/            controllers, autenticação, DI

frontend/src/
├── components/               peças do design system
├── features/                 site · publico · admin · plataforma
└── services/                 cliente HTTP, tipos, ciclo do pedido, tema

.claude/
├── agents/grelhahub-engineer.md   o agente que carrega o contexto do projeto
└── skills/subir-e-validar/        sobe a pilha real e valida a tela no navegador
```

---

## Rodar localmente

```bash
# 1. Banco — a porta é 5433 de propósito: 5432 costuma ter um Postgres local
docker run -d --name grelhahub-db -e POSTGRES_DB=grelhahub \
  -e POSTGRES_USER=grelhahub -e POSTGRES_PASSWORD=grelhahub -p 5433:5432 postgres:16-alpine

# 2. Backend → http://localhost:5000
cd backend && dotnet run --project src/GrelhaHub.Api -- --migrate --semear
cd backend && dotnet run --project src/GrelhaHub.Api

# 3. Frontend → http://localhost:5173
cd frontend && npm ci && npm run dev
```

> ⚠ **A flag é `--migrate`, em inglês.** Escrita como `--migrar` ela é ignorada **em silêncio** e o
> processo sobe como servidor: a API responde `/api/health`, e os bancos de **log** e **arquivo**
> ficam vazios — a trilha falha em toda operação e o primeiro upload dá 500. O startup de
> Development migra **só** o banco principal.

### O caminho mais rápido — a skill

```bash
bash .claude/skills/subir-e-validar/scripts/subir.sh     # os 3 bancos + API + Vite
bash .claude/skills/subir-e-validar/scripts/semear.sh    # cenário completo pelas ROTAS da API
bash .claude/skills/subir-e-validar/scripts/derrubar.sh
```

Entrar em `/entrar` com `dona@brasa.local` / `senha-de-demo-1234`.
Detalhe em [`SKILL.md`](.claude/skills/subir-e-validar/SKILL.md).

### Os gates

```bash
cd backend
dotnet build -c Release      # Release trata warning como erro
dotnet test -c Release       # exige Docker: a integração sobe PostgreSQL efêmero

cd frontend
npm ci                       # sem isto, `npm run lint` sai 0 sem ter rodado nada
npm run lint && npm run build
```

> ⚠⚠ **Nenhum dos quatro gates olha a tela.** Em 19/08/2026 uma tela passou nos quatro — testes
> verdes, zero avisos — e **subiu vazia no navegador**. Mudança de interface se valida com a
> [skill](.claude/skills/subir-e-validar/SKILL.md), e a dívida do runner de frontend está
> declarada.

---

## Estado atual

| Item | Situação |
|------|----------|
| Regras catalogadas | **190** — GER 41 · VEN 58 · EST 23 · FIN 16 · PRO 13 · LOG 9 · CRM 9 · COM 9 · RH 6 · FIS 6 |
| Regras **validadas com cliente** | **0** — a entrevista é o próximo marco. É o maior risco do projeto |
| Decisões de arquitetura | **22 ADRs**, 6 decisões em aberto |
| Specs | **14**, todas `ENTREGUE` |
| Regras com o ID citado em teste | **66** |
| Testes | **372 verdes** — 110 domínio + 25 aplicação + 237 integração, contra PostgreSQL real |
| Cobertura | **90,8% de linha**, 70,8% de ramo — medida com `cobertura.runsettings` **e** o filtro de classe. Application 95,7% · Domain 90,0% · Infrastructure 86,3% · Api 79,9% |
| Sprint | 1 & 2 entregues — documentação, fatia vertical e onda 0 |

### O que já funciona

Cadastro de restaurante pela plataforma → cardápio público em `/c/:slug` → pedido do cliente →
painel da loja, com **quatro superfícies**: a vitrine, o cardápio, o painel do restaurante
(pedidos, cardápio, equipe, loja, operação, **monitor**) e a administração da plataforma.

Da onda 1, o cadastro do cardápio. Fora da ordem: painel de operação, painel de senha e a lista de
pedidos por grupo.

### O próximo passo continua sendo uma conversa

**Nenhuma das 190 regras foi validada com um dono de restaurante.** Elas vieram do conhecimento de
domínio e da estrutura dos módulos de ERP — não de entrevista. O roteiro está pronto em
[`05-fila-de-validacao-com-cliente.md`](docs/05-fila-de-validacao-com-cliente.md), com as perguntas
em ordem de risco.

**Uma rejeição vale tanto quanto uma confirmação** como evidência de método.

---

<div align="center">

Trabalho acadêmico · 9 sprints · [Cronograma](GRELHAHUB-context.md#6-cronograma-das-sprints)

</div>
