<div align="center">

# GrelhaHub

**DO PEDIDO À BRASA**

ERP para pequenos e médios restaurantes e deliveries, com especialização progressiva em
hamburgueria.

</div>

---

## O problema

> O dono da hamburgueria não sabe, ao final do mês, **qual dos seus lanches dá lucro** —
> porque o custo do lanche muda a cada compra de insumo e ninguém recalcula.
> E não sabe, durante o serviço, **o que vai faltar** — porque a baixa de estoque nunca
> acompanha a venda em tempo real.

Um único cadastro de produto alimenta simultaneamente o cardápio, a ficha técnica, a baixa
de estoque e o cálculo de CMV.

---

## Como este projeto é construído

**Document First + Spec-Driven Development.** Nenhum comportamento existe no sistema sem
estar documentado antes:

```
catálogo de regras  →  spec  →  código + teste que cita a regra
```

Se durante a implementação aparecer um comportamento necessário que não está no catálogo,
**a implementação para**, a regra é escrita, e só então o código continua.

📖 **Comece por [`docs/README.md`](docs/README.md).**

| Documento | O que é |
|-----------|---------|
| [Catálogo de Regras](docs/03-catalogo-de-regras-de-negocio.md) ★ | **Fonte de verdade** — 138 regras de negócio |
| [Guia de Specs](docs/06-guia-de-specs.md) | Como a regra vira spec e a spec vira código |
| [ADRs](docs/adr/) | As 12 decisões de arquitetura, com alternativas e custos |
| [Design System Chapa](docs/08-design-system.md) | Identidade da interface — tokens, componentes e as 8 regras `CHAPA-*` |
| [Fila de Validação](docs/05-fila-de-validacao-com-cliente.md) | Roteiro da entrevista com o cliente |

---

## Stack

| Camada | Tecnologia | Decisão |
|--------|------------|---------|
| Backend | .NET 10 · ASP.NET Core · Clean Architecture + CQRS (MediatR) · FluentValidation | [ADR-0001](docs/adr/0001-stack-dotnet-react-postgresql.md), [ADR-0002](docs/adr/0002-clean-architecture-com-cqrs.md) |
| Banco | PostgreSQL · EF Core 10 · `snake_case` · `uuid` · `timestamptz` | [ADR-0004](docs/adr/0004-postgresql-como-sgbd.md) |
| Frontend | React 19 · TypeScript · Vite — desacoplado, REST + JWT | [ADR-0003](docs/adr/0003-frontend-e-backend-desacoplados.md) |
| Infra | Docker · Nginx · VPS única · Azure DevOps | [ADR-0006](docs/adr/0006-deploy-em-vps-unica-com-docker.md) |

---

## Estrutura

```
GrelhaHub/
├── docs/                        📖 documentação — comece aqui
│   ├── 03-catalogo-de-regras-de-negocio.md    ★ fonte de verdade
│   ├── adr/                     decisões de arquitetura
│   └── specs/                   6 specs (onda 0 + fatia vertical)
├── backend/
│   ├── src/
│   │   ├── GrelhaHub.Domain/            entidades e regras — sem dependências
│   │   ├── GrelhaHub.Application/       Features/<Módulo>/ — CQRS
│   │   ├── GrelhaHub.Infrastructure/    EF Core, migrations, PostgreSQL
│   │   └── GrelhaHub.Api/               controllers, auth, DI
│   ├── tests/                   unit (Domain, Application) + integração (PostgreSQL real)
│   └── Dockerfile
├── frontend/                    React + TypeScript + Vite
├── assets/brand/                kit de marca (SVG, PNG, favicon)
├── .claude/agents/              agente de implementação e contexto
├── docker-compose.yml           orquestração na VPS
├── nginx.conf                   proxy reverso e TLS
├── azure-pipelines.yml          CI/CD
└── .env.example                 template de variáveis (sem segredos)
```

---

## Rodar localmente

**Pré-requisitos:** .NET 10 SDK, Node 22+, PostgreSQL 16+ (ou Docker).

```bash
# 1. Banco — uma vez só
psql -h localhost -U postgres -f scripts/preparar-banco.sql

# 2. Backend  →  http://localhost:5000  (as migrations aplicam sozinhas em Development)
cd backend
dotnet run --project src/GrelhaHub.Api

# 3. Frontend →  http://localhost:5173
cd frontend
npm install
npm run dev
```

### Ver funcionando em 2 minutos

1. Abra `http://localhost:5173/cadastrar` e cadastre um restaurante — já vem com um
   cardápio de hamburgueria de exemplo
2. Copie o link público na aba **Link público** do painel
3. Abra o link em outra aba (ou no celular), monte um pedido e envie
4. Volte ao painel: o pedido aparece em **Aberto**. Confirme e acompanhe o ciclo
   `Confirmado → Em produção → Pronto → Entregue → Finalizado`

### Testes

```bash
cd backend && dotnet test -c Release
cd frontend && npm run lint && npm run build
```

---

## Estado atual

| Item | Situação |
|------|----------|
| Sprint | 1 & 2 — documentação entregue |
| Regras catalogadas | **138** — 134 em `HIP`, 4 em `DEC` |
| Regras validadas com cliente | **0** — a entrevista é o próximo marco |
| Specs | **6** — 5 da onda 0 (`PRONTA`) + `SPEC-VEN-001` (`EM IMPLEMENTAÇÃO`) |
| Código | **Fatia vertical funcionando** na direção Chapa: cadastro → cardápio público → pedido → painel. 26 testes de domínio |

### O que já funciona

`SPEC-VEN-001` entrega o caminho completo, com **dívida declarada** na §8 da spec:

| Fluxo | Estado |
|-------|--------|
| Cadastro de restaurante + admin, com JWT | ✔ |
| Link público do cardápio (`/c/:slug`) | ✔ |
| Pedido pelo cardápio, sem login | ✔ |
| Painel do restaurante com ciclo de vida do pedido | ✔ |
| Ficha técnica, estoque, lote e FEFO | ✘ — ondas 2 e 3 |
| Alerta de falta de insumo (`RN-VEN-035`) | ✘ — depende de estoque |
| Pagamento, caixa, comanda, modificadores | ✘ — ondas 5 e 6 |

> **Um pedido que não baixa estoque hoje não significa que a regra mudou** — significa que o
> estoque ainda não existe. A [§8 da spec](docs/specs/SPEC-VEN-001-cardapio-publico-e-pedido.md)
> lista cada simplificação e a onda que a resolve.

### O próximo passo continua sendo uma conversa

As [sete regras estruturais](docs/03-catalogo-de-regras-de-negocio.md#15-as-sete-regras-estruturais)
precisam sair de `HIP` antes da modelagem lógica. Duas delas decidem sozinhas o formato do
modelo:

- **`RN-GER-006`** — o dia operacional vai das 04h00 às 03h59. Se errar, o faturamento de
  sábado à noite cai em domingo e o BI inteiro fica errado em definitivo
- **`RN-PRO-004`** — o molho da casa é produto **e** insumo ao mesmo tempo, o que obriga a
  ficha técnica a ser recursiva. Descobrir isso na Sprint 7 custa uma sprint; descobrir
  agora custa uma conversa

O roteiro da entrevista está pronto em
[`docs/05-fila-de-validacao-com-cliente.md`](docs/05-fila-de-validacao-com-cliente.md).

---

## Cronograma

| Sprint | Entrega | Situação |
|--------|---------|----------|
| 1 & 2 | Levantamento e Regras de Negócio | Docs entregues — falta entrevista e anexos |
| 3 & 4 | Modelo Conceitual e Lógico | Bloqueado pelas regras estruturais |
| 5 & 6 | Dicionário de Dados e Modelo Físico | Pendente |
| 7 & 8 | Carga Inicial, BI e IA | Pendente |
| 9 | Apresentação Executiva | Pendente |
