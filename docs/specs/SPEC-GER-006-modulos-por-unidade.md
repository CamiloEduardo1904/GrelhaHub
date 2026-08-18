# SPEC-GER-006 — Módulos habilitados por unidade

| Campo | Valor |
|-------|-------|
| ID | `SPEC-GER-006-modulos-por-unidade` |
| Módulo | GER |
| Situação | `ENTREGUE` — 17/08/2026 |
| Onda | 0 — fundação |
| Regras implementadas | `RN-GER-017` ⚠, `RN-GER-018`, `RN-GER-019`, `RN-GER-020`, `RN-GER-021`, `RN-GER-014` *(quem habilita)* |
| ADRs aplicáveis | [ADR-0014](../adr/0014-modularidade-comercial.md), [ADR-0013](../adr/0013-acesso-de-plataforma.md), [ADR-0007](../adr/0007-core-generico-com-nicho-por-configuracao.md) |
| Depende de | `SPEC-GER-001`, `SPEC-GER-003` *(padrão de vigência)*, `SPEC-GER-004` *(perfil de plataforma)* |
| Data | 17/08/2026 |

---

## 1. Objetivo

Tornar o **contrato comercial executável**: o restaurante contrata os módulos que usa, e o que
não contratou **não responde** — nem por tela, nem por rota, nem por job.

> Esconder o menu e deixar a rota respondendo produz um sistema em que o cliente usa o que não
> pagou. É o mesmo modo de falha que `RN-GER-012` rejeita para custo, aplicado a módulo inteiro.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-GER-017` ⚠ | Operação de módulo não habilitado é recusada, mesmo com permissão | Aplicação | Behavior do MediatR + `[ExigeModulo]` |
| `RN-GER-018` | Desabilitar não apaga dado | Modelo | Ausência de `DELETE`; a vigência fecha, a tabela permanece |
| `RN-GER-019` | GER é núcleo, sempre habilitado | Modelo + Aplicação | `CHECK` de núcleo + recusa na desabilitação |
| `RN-GER-020` | Dependência entre módulos | Modelo + Aplicação | `modulo_dependencia` + validação nas duas direções |
| `RN-GER-021` | Habilitação tem vigência | Modelo | `[inicio, fim)` + `EXCLUDE` — o padrão de `parametro_valor` |

## 3. Contrato

### Área de plataforma — `/api/v1/plataforma` *(perfil `Plataforma`)*

| Verbo | Rota | Descrição |
|-------|------|-----------|
| `GET` | `/modulos` | Catálogo dos dez módulos, com dependências |
| `GET` | `/unidades/{id}/modulos` | O que a unidade tem habilitado, e desde quando |
| `POST` | `/unidades/{id}/modulos/{codigo}/habilitar` | Habilita, abrindo vigência |
| `POST` | `/unidades/{id}/modulos/{codigo}/desabilitar` | Fecha a vigência corrente |

**Quem habilita é a plataforma, não o restaurante** (`RN-GER-014`): é decisão comercial. O
administrador do restaurante **consulta** o que tem contratado, e não altera.

### Área do restaurante

| Verbo | Rota | Descrição |
|-------|------|-----------|
| `GET` | `/api/v1/modulos` | Módulos habilitados na própria unidade — para a interface montar o menu |

### Erros

| Situação | Código | Mensagem | Regra |
|----------|--------|----------|-------|
| Operação de módulo não habilitado | 403 | "O módulo {nome} não está habilitado para este restaurante." | `RN-GER-017` |
| Habilitar sem dependência | 422 | "O módulo {nome} depende de {dependências}, que não estão habilitados." | `RN-GER-020` |
| Desabilitar com dependente ativo | 422 | "{dependentes} dependem de {nome} e estão habilitados." | `RN-GER-020` |
| Desabilitar o núcleo | 422 | "O módulo GER é núcleo e não pode ser desabilitado." | `RN-GER-019` |

## 4. Comportamento

### A declaração é obrigatória, e a aplicação recusa subir sem ela

```csharp
[ExigeModulo(Modulo.Vendas)]
public sealed record CriarPedidoPublicoCommand(...) : IRequest<PedidoCriadoResponse>;

[SemModulo("autenticação antecede qualquer módulo")]
public sealed record AutenticarCommand(...) : IRequest<AutenticarResponse>;
```

Na partida, a aplicação enumera **todo** `IRequest` do assembly de Application e lança se algum
não declarar nada:

```
InvalidOperationException: RN-GER-017 — operação sem declaração de módulo:
CriarPedidoPublicoCommand. Marque com [ExigeModulo] ou justifique com [SemModulo].
```

**Falha fechada por construção**, mesmo desenho de `GarantirAusenciaDeDecimalCru`: operação nova
sem declaração não passa despercebida, porque nada sobe.

### Por que no pipeline e não no controller

O gate vale para **qualquer origem**. A onda 4 tem o caso que decide isso: a confirmação de um
pedido dispara baixa de estoque e ordem de produção, por handler interno. Um gate no controller
protegeria a rota de confirmação e deixaria a baixa passar — exatamente no módulo que o cliente
pode não ter contratado.

### Rota pública e módulo

O cardápio público (`RN-VEN-037`) pertence a VEN. Unidade sem VEN habilitado devolve **404** no
cardápio, não 403: para quem está do lado de fora, o restaurante simplesmente não tem cardápio
online — e 403 anunciaria que existe algo ali que não foi contratado.

### Vigência, não booleano

Habilitação usa `[inicio, fim)` com `EXCLUDE`, o padrão de `parametro_valor` (`SPEC-GER-003`).
Um booleano responderia "tem VEN hoje?"; a vigência responde "o que esta unidade tinha
contratado em março", que é a pergunta da cobrança.

## 5. Critérios de aceite

### CA-1 — Operação de módulo não habilitado é recusada (`RN-GER-017`)

> **Dado** uma unidade sem o módulo EST habilitado,
> **Quando** um usuário **com permissão** de estoque emitir uma operação de estoque,
> **Então** a resposta é 403 citando o módulo,
> **e** o handler não é executado.
>
> "Com permissão" é o ponto: módulo não contratado não é questão de perfil.

### CA-2 — Toda operação declara o seu módulo (`RN-GER-017`)

> **Dado** o assembly de Application,
> **Quando** a aplicação subir,
> **Então** todo `IRequest` tem `[ExigeModulo]` ou `[SemModulo]`,
> **e** acrescentar uma operação sem declaração **impede a aplicação de subir**.

### CA-3 — Desabilitar não apaga dado (`RN-GER-018`)

> **Dado** uma unidade com pedidos e o módulo VEN habilitado,
> **Quando** VEN for desabilitado,
> **Então** os pedidos continuam na base,
> **e** ao reabilitar, voltam a ser consultáveis, todos.

### CA-4 — GER não é desabilitável (`RN-GER-019`)

> **Quando** for tentada a desabilitação de GER,
> **Então** a operação é recusada com 422,
> **e** nenhuma unidade existe sem GER habilitado.

### CA-5 — Dependência nas duas direções (`RN-GER-020`)

> **Dado** que EST depende de PRO,
> **Quando** EST for habilitado sem PRO, **então** é recusado com 422 citando PRO,
> **e quando** PRO for desabilitado com EST habilitado, **então** é recusado citando EST.

### CA-6 — Histórico de contratação (`RN-GER-021`)

> **Dado** um módulo habilitado em 01/03 e desabilitado em 01/06,
> **Quando** for consultado o que a unidade tinha em 15/04,
> **Então** o módulo aparece como habilitado,
> **e** a consulta em 15/07 não o inclui,
> **e** duas vigências sobrepostas são recusadas pelo banco.

### CA-7 — Cardápio de unidade sem VEN devolve 404 (`RN-GER-017`, `RN-VEN-037`)

> **Dado** uma unidade sem VEN habilitado,
> **Quando** o cardápio público do seu slug for acessado,
> **Então** a resposta é 404, não 403.

## 6. Modelo de dados tocado

```sql
CREATE TABLE modulo (
    id       uuid PRIMARY KEY,
    codigo   varchar(3)   NOT NULL,   -- GER, VEN, PRO, EST, COM, FIN, CRM, LOG, RH, FIS
    nome     varchar(60)  NOT NULL,
    nucleo   boolean      NOT NULL DEFAULT false,
    ordem    smallint     NOT NULL,

    CONSTRAINT uq_modulo_codigo UNIQUE (codigo),
    CONSTRAINT ck_modulo_codigo CHECK (codigo ~ '^[A-Z]{2,3}$')  -- RH tem duas letras
);

CREATE TABLE modulo_dependencia (
    modulo_id     uuid NOT NULL REFERENCES modulo(id) ON DELETE RESTRICT,
    depende_de_id uuid NOT NULL REFERENCES modulo(id) ON DELETE RESTRICT,

    PRIMARY KEY (modulo_id, depende_de_id),
    CONSTRAINT ck_modulo_dependencia_ciclo CHECK (modulo_id <> depende_de_id)
);

CREATE TABLE unidade_modulo (
    id              uuid        PRIMARY KEY,
    unidade_id      uuid        NOT NULL REFERENCES unidade(id) ON DELETE RESTRICT,
    modulo_id       uuid        NOT NULL REFERENCES modulo(id)  ON DELETE RESTRICT,
    vigencia_inicio timestamptz NOT NULL,
    vigencia_fim    timestamptz,
    justificativa   varchar(500),
    dh_criacao      timestamptz NOT NULL,

    CONSTRAINT ck_unidade_modulo_vigencia CHECK (vigencia_fim IS NULL OR vigencia_fim > vigencia_inicio),

    -- O padrão de SPEC-GER-003, terceira aplicação.
    CONSTRAINT ex_unidade_modulo_vigencia EXCLUDE USING gist (
        unidade_id WITH =,
        modulo_id  WITH =,
        tstzrange(vigencia_inicio, vigencia_fim) WITH &&
    )
);
```

`ck_modulo_dependencia_ciclo` cobre só o ciclo de tamanho 1. Ciclo maior é impedido na
aplicação: com dez módulos e um grafo escrito à mão na carga inicial, uma trigger recursiva
custaria mais do que resolve.

**Migration:** `Modulos`.

**Carga inicial:** os dez módulos, com `GER` marcado núcleo, e o grafo de dependências. Toda
unidade nova nasce com **GER + VEN** habilitados — o mínimo para vender e operar.

## 7. Parâmetros consumidos

Nenhum. Módulo habilitado é contrato, não configuração operacional — não cabe em `PAR-*`.

## 8. Fora de escopo

- **Granularidade menor que módulo.** Vender "vendas sem delivery" exigiria gate por
  funcionalidade, e isso não está decidido. Se vier, é ADR novo
- **Cobrança.** A vigência dá o histórico; emitir fatura a partir dele é outro produto
- **Interface de administração da plataforma.** Esta spec entrega as rotas; a tela entra depois
- **Menu do restaurante reagindo ao módulo.** `GET /api/v1/modulos` existe para isso; consumir
  no frontend é trabalho da onda de interface

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Domínio | `src/GrelhaHub.Domain/Gerais/Modulo.cs`, `UnidadeModulo.cs` |
| Atributos | `src/GrelhaHub.Application/Common/Modulos/` |
| Behavior | `src/GrelhaHub.Application/Common/Behaviors/ModuloBehavior.cs` |
| Validação na partida | `GrelhaHub.Application/DependencyInjection.cs` |
| Migration | `.../Migrations/*_Modulos.cs` |
| Testes | `tests/GrelhaHub.Integration.Tests/Gerais/ModulosTests.cs` |

## 10. Definição de pronto

- [x] `modulo`, `modulo_dependencia` e `unidade_modulo` com `EXCLUDE` de vigência
- [x] Todo `IRequest` declarando módulo; **a aplicação recusa subir sem isso**
- [x] Behavior recusando antes do handler
- [x] CA-1 a CA-7 como teste de integração — 7 casos
- [x] Carga inicial com os dez módulos e o grafo de dependências; unidade nova nasce com GER + VEN
- [x] Rastreabilidade do catálogo §16 atualizada
- [x] `dotnet build -c Release` e `dotnet test` verdes — **109 testes**

> ### O gate no pipeline não alcança a rota pública
>
> Descoberto ao implementar: o behavior roda **antes** do handler, e nas rotas do cardápio a
> unidade só é conhecida **depois** de resolver o `slug`. Sete testes ficaram vermelhos com 403
> em rota anônima.
>
> As três operações públicas passaram a declarar `[SemModulo]` com justificativa e a verificar o
> módulo **dentro do handler**, logo após resolver a unidade — o que também é o que o CA-7 pedia:
> a resposta ali é **404**, não 403, porque 403 anunciaria a quem está fora que existe algo não
> contratado.
>
> É o limite honesto do desenho escolhido no ADR-0014: o gate no pipeline cobre tudo o que já tem
> unidade no escopo, e a rota que resolve a própria unidade cobre-se sozinha, declaradamente.
