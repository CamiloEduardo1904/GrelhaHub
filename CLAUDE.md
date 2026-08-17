# GrelhaHub — instruções para agentes

ERP para restaurantes e deliveries (nicho hamburgueria). Trabalho acadêmico de 9 sprints,
que precisa ser **defensável em banca**: decisão sem justificativa registrada é decisão
errada, mesmo quando o código funciona.

> **Existe um agente dedicado a este projeto:** `grelhahub-engineer`
> (`.claude/agents/grelhahub-engineer.md`). Use-o para implementar, escrever specs, modelar
> o banco ou responder dúvidas de contexto — ele carrega o catálogo de regras e os ADRs.

---

## A regra inegociável

> **Nenhuma regra de negócio nasce no código.**

O projeto é conduzido em **Document First** e **Spec-Driven Development**:

```
catálogo de regras  →  spec  →  código + teste que cita a regra
```

Se aparecer um comportamento necessário que não está no catálogo:
**pare o código**, escreva a regra em `docs/03-catalogo-de-regras-de-negocio.md` com estado
`HIP` e origem `IMP`, acrescente-a ao cabeçalho da spec, e só então continue.
Não implemente "provisoriamente".

---

## Leia antes de agir

| Arquivo | Para quê |
|---------|----------|
| `docs/README.md` | Mapa e estado atual |
| `docs/03-catalogo-de-regras-de-negocio.md` | **Fonte de verdade** — 138 regras |
| `docs/06-guia-de-specs.md` | Formato da spec, ondas de implementação, definição de pronto |
| `docs/adr/README.md` | 11 decisões e 4 em aberto |
| `docs/02-glossario.md` | Vocabulário controlado — use os termos exatos |
| `docs/04-parametros-do-sistema.md` | Valores configuráveis |

**Nunca responda de memória sobre uma regra** — `grep` pelo ID. As regras mudam de estado a
cada entrevista.

---

## As sete regras estruturais

Errar aqui custa retrabalho de projeto, não de código.

| Regra | O que exige |
|-------|-------------|
| `RN-GER-006` ⚠⚠ | Dia operacional das 04h00 às 03h59. **Nunca agrupe por data de criação** — use a coluna gerada `data_operacional` |
| `RN-PRO-004` ⚠⚠ | Ficha técnica recursiva: o molho da casa é produto **e** insumo. Exige proteção contra ciclo |
| `RN-EST-001` | Saldo **nunca** é coluna editável — é soma dos movimentos, materializada em view |
| `RN-EST-004` + `RN-EST-011` | Todo movimento referencia um **lote**; custo é o do lote consumido, consumo **FEFO**. Nunca custo médio |
| `RN-VEN-008` + `RN-VEN-010` | Preço praticado é copiado na venda; preço de tabela tem vigência |
| `RN-VEN-018` | `Aberto → Confirmado → Em produção → Pronto → Entregue → Finalizado`. Baixa de estoque e ordem de produção disparam na **Confirmação** |
| `RN-GER-009` | `unidade_id` em toda tabela transacional, **primeira coluna** de toda chave única e índice |

### Armadilhas

- Cancelamento **não** devolve estoque se o item já entrou em produção — gera **perda**
  (`RN-EST-018`). Antes da produção, não gera movimento (`RN-EST-019`)
- Nenhum literal de negócio no código nem em `CHECK`. `10%` é `PAR-VEN-002`.
  `CHECK` valida domínio (`entre 0 e 100`), nunca política (`<= 10`)
- Quantidade de insumo: **4** casas decimais. Moeda: 2
- Custo, CMV e margem **não são retornados pela API** sem permissão (`RN-GER-012`)
- Nenhum registro transacional é excluído fisicamente (`RN-GER-002`)
- O termo é `unidade`, não `estabelecimento`; `estacao`, não `praça`

---

## Convenções

### Backend

```
backend/src/
├── GrelhaHub.Domain/          sem dependências — nem EF Core, nem MediatR
├── GrelhaHub.Application/     Features/<Modulo>/<Caso>/ com Command|Query + Handler + Validator
├── GrelhaHub.Infrastructure/  EF Core, migrations, PostgreSQL
└── GrelhaHub.Api/             controllers, auth, DI
```

- Domínio em **português** (`Pedido`, `FichaTecnica`, `MovimentoEstoque`); termos de
  framework em inglês (`Handler`, `Command`)
- Banco em `snake_case`, PK `uuid`, `timestamptz` em UTC, `numeric` para dinheiro —
  nunca `float`
- Versões de pacote **só** em `backend/Directory.Packages.props` (CPM)
- Release ativa `TreatWarningsAsErrors` — build de Release limpo é obrigatório

### Testes

O ID da regra vai no `DisplayName`. É isso que torna a rastreabilidade real:

```csharp
[Fact(DisplayName = "RN-EST-018 — item cancelado após produção gera perda, não devolução")]
```

Teste de integração usa **PostgreSQL real** (Testcontainers). O provider in-memory do
EF Core não executa constraint, trigger nem coluna gerada — daria falsa confiança
justamente nas regras que mais importam (ADR-0009).

### Onde cada regra é imposta — ADR-0009

O projeto usa **as duas camadas**, deliberadamente:

> O banco garante que o dado nunca fica inválido. A aplicação garante que o usuário nunca
> chega até lá.

Ordem de preferência: `MOD → constraints → GEN → TRG → VW → APP`. A aplicação é o último
recurso — o que a disciplina avalia é o modelo. Mas a **mensagem de erro ao usuário vem
sempre da aplicação**: erro de constraint que vaza para o usuário é defeito da aplicação.

---

## Comandos

```bash
# Backend
cd backend
dotnet build -c Release      # Release trata warning como erro
dotnet test -c Release

# Frontend
cd frontend
npm run lint && npm run build
```

---

## Estado atual

Sprint 1 & 2 — documentação entregue. **138 regras catalogadas: 134 em `HIP`, 4 em `DEC`;
nenhuma validada com cliente.** Onda 0 (fundação) tem **5 specs `PRONTA`**; nenhuma
implementada além do cálculo de dia operacional.

**Próximo trabalho de código:** implementar a onda 0, na ordem
`SPEC-GER-001 → 002 → 003 → 004 → 005` (índice em
[`docs/06-guia-de-specs.md`](docs/06-guia-de-specs.md#8-índice-de-specs)).

Enquanto `RN-GER-006` e `RN-PRO-004` estiverem em `HIP`, qualquer modelagem da ficha técnica
ou da dimensão tempo é provisória. Diga isso ao pedir ou receber trabalho nessas áreas — não
recuse, mas registre que a decisão pode virar retrabalho.

### Decisões do time — 17/08/2026

- **FIS é o último módulo** (onda 11). Nada depende dele, e o valor exige homologação
  externa. As regras seguem catalogadas e a modelagem reserva o espaço
- **RH permanece no MVP**, em nível Modelo — `RN-LOG-005` depende dele
- **`RN-VEN-035`** — falta de insumo **sempre** gera alerta na tela, nos três modos de
  `PAR-VEN-003`. O parâmetro decide a fricção (recusar, exigir confirmação, informar),
  **nunca se o operador é avisado**

---

## Idioma

Documentação, domínio e mensagens em **português do Brasil**.
