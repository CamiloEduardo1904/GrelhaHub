# SPEC-GER-005 — Trilha de auditoria

| Campo | Valor |
|-------|-------|
| ID | `SPEC-GER-005-trilha-de-auditoria` |
| Módulo | GER |
| Situação | `PRONTA` |
| Onda | 0 — fundação |
| Regras implementadas | `RN-GER-001`, `RN-GER-011`, `RN-GER-002` |
| ADRs aplicáveis | [ADR-0008](../adr/0008-estoque-como-livro-razao.md), [ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md) |
| Depende de | `SPEC-GER-001`, `SPEC-GER-004` |
| Data | 17/08/2026 |

---

## 1. Objetivo

Registrar **quem fez o quê, quando, e o que mudou** nas operações sensíveis — de forma
automática, para que nenhum handler precise lembrar de auditar.

> Auditoria que depende de o desenvolvedor chamar `RegistrarAuditoria()` é auditoria que
> falta exatamente no caso em que alguém quis esconder algo. Por isso esta spec é um
> **interceptor**, não uma chamada.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-GER-001` | Todo registro transacional guarda data/hora de criação e usuário responsável | Modelo + Banco | Colunas `NOT NULL` + `SaveChangesInterceptor` |
| `RN-GER-011` | Operações sensíveis registram usuário, data/hora, operação e valores anterior e posterior | Aplicação | `SaveChangesInterceptor` sobre entidades marcadas |
| `RN-GER-002` | Nenhum registro transacional é excluído fisicamente | Aplicação | O interceptor **recusa** `EntityState.Deleted` |

## 3. Contrato

### `ConsultarTrilhaQuery`

| Entrada | Tipo | Obrigatório | Observação |
|---------|------|:-----------:|------------|
| `entidade` | `string?` | | Nome da tabela |
| `registroId` | `uuid?` | | |
| `usuarioId` | `uuid?` | | |
| `de` / `ate` | `DateTimeOffset?` | | |
| `pagina`, `tamanho` | `int` | | Máximo 50 por página — `RNF-002` |

**Saída:** `entidade`, `registroId`, `operacao`, `usuario`, `ocorridoEm`,
`valoresAnteriores`, `valoresNovos`, `justificativa`.

### Erros

| Situação | Código | Mensagem |
|----------|--------|----------|
| Sem permissão | 403 | "Apenas a gerência pode consultar a trilha de auditoria." |

### Autorização

Apenas **Administrador** e **Gerente** (`auditoria.consultar`). A trilha contém valores
anteriores de campos de custo — expô-la ao atendente contornaria `RN-GER-012`.

## 4. Comportamento

### O que é auditado

Entidades marcadas com `IAuditavelDetalhado`. Na onda 0 a lista é fixa e cresce a cada
spec — **acrescentar a entidade à lista é item da definição de pronto de quem a cria**:

| Operação | Entidade | Regra que exige |
|----------|----------|-----------------|
| Cancelamento de pedido | `pedido` | `RN-VEN-014`, `RN-GER-011` |
| Desconto acima do limite | `pedido`, `autorizacao` | `RN-VEN-013` |
| Ajuste de estoque | `movimento_estoque` | `RN-EST-014` |
| Fechamento de caixa | `caixa_diario` | `RN-FIN-007` |
| Alteração de parâmetro | `parametro_valor` | `SPEC-GER-003` CA-6 |
| Alteração de preço | `preco_produto` | `RN-VEN-010` |
| Alteração de ficha técnica | `ficha_tecnica` | `RN-PRO-002` |
| Usuário e permissão | `usuario`, `perfil_permissao` | `RN-GER-010` |

### Como funciona

Um `SaveChangesInterceptor` roda **dentro da mesma transação** da operação. Se a auditoria
falhar, a operação inteira falha — auditoria que grava depois, fora da transação, pode
perder o registro justamente quando a operação foi anômala.

Antes de salvar, para cada entrada auditável:

1. Lê `EntityEntry.OriginalValues` e `CurrentValues`
2. Grava **apenas as propriedades que mudaram** — auditar 40 colunas para registrar a
   alteração de uma torna a trilha ilegível
3. Serializa em `jsonb`
4. Anexa usuário e instante do `IContextoDeExecucao` e do `IRelogio`

### Campos sensíveis

Senha e hash **nunca** entram na trilha, nem como valor anterior. Propriedades marcadas com
`[NaoAuditar]` são substituídas por `"***"`.

### `RN-GER-002` — exclusão física recusada no interceptor

```csharp
var exclusoes = ChangeTracker.Entries()
    .Where(e => e.State == EntityState.Deleted && e.Entity is IPertenceAUnidade);

if (exclusoes.Any())
    throw new InvalidOperationException(
        "RN-GER-002 — registro transacional não é excluído fisicamente. " +
        "Use cancelamento, inativação ou movimento contrário.");
```

A checagem é da aplicação, complementada por `ON DELETE RESTRICT` no banco (`SPEC-GER-001`).
Falha ruidosa em desenvolvimento vale mais do que descobrir a exclusão pela ausência do dado
em produção.

### `RN-GER-001` — preenchimento automático

O mesmo interceptor preenche `criado_em` e `criado_por` em toda entidade `Added` que
implemente `IAuditavel` — e **bloqueia alteração** desses campos em entidades `Modified`.

### A trilha é imutável

`log_auditoria` não tem `UPDATE` nem `DELETE` em nenhuma rota, e a permissão do usuário de
aplicação no PostgreSQL é apenas `INSERT` e `SELECT` sobre ela. Uma trilha alterável não é
trilha.

## 5. Critérios de aceite

### CA-1 — Criação registra autor e instante (`RN-GER-001`)

> **Dado** um usuário autenticado criando um registro transacional,
> **Quando** o registro for gravado,
> **Então** `criado_em` e `criado_por` estão preenchidos,
> **e** o handler não atribuiu nenhum dos dois.

### CA-2 — Campos de criação são imutáveis (`RN-GER-001`)

> **Dado** um registro existente,
> **Quando** houver tentativa de alterar `criado_por`,
> **Então** o interceptor recusa a alteração,
> **e** o valor original é preservado.

### CA-3 — Alteração sensível registra antes e depois (`RN-GER-011`)

> **Dado** um gerente alterando o preço de um produto de R$ 25,00 para R$ 28,00,
> **Quando** a alteração for concluída,
> **Então** existe registro em `log_auditoria` com operação `Alteracao`, o usuário, o
> instante, `{"preco": 25.00}` em valores anteriores e `{"preco": 28.00}` nos novos,
> **e** apenas a propriedade alterada aparece — não o objeto inteiro.

### CA-4 — Auditoria é atômica com a operação (`RN-GER-011`)

> **Dado** uma operação sensível cuja gravação de auditoria falhe,
> **Quando** a transação for concluída,
> **Então** **nada** é gravado — nem a operação, nem a auditoria,
> **e** o erro é propagado.

### CA-5 — Exclusão física é recusada (`RN-GER-002`)

> **Dado** um registro transacional qualquer,
> **Quando** o código chamar `Remove()` sobre ele,
> **Então** `SaveChanges` lança exceção citando `RN-GER-002`,
> **e** o registro permanece no banco.

### CA-6 — Senha nunca aparece na trilha

> **Dado** a alteração de senha de um usuário,
> **Quando** o registro de auditoria for lido,
> **Então** o hash não aparece em valores anteriores nem novos,
> **e** o campo consta como `"***"`.

### CA-7 — Trilha é imutável

> **Dado** um registro de auditoria existente,
> **Quando** houver tentativa de alterá-lo ou excluí-lo pela aplicação,
> **Então** a operação falha,
> **e** nenhuma rota da API expõe alteração de `log_auditoria`.

### CA-8 — Consulta restrita à gerência (`RN-GER-012`)

> **Dado** um usuário com perfil Atendente,
> **Quando** consultar a trilha,
> **Então** recebe 403.

## 6. Modelo de dados tocado

```sql
CREATE TABLE log_auditoria (
    id                  uuid        PRIMARY KEY,
    unidade_id          uuid        NOT NULL REFERENCES unidade(id) ON DELETE RESTRICT,
    entidade            varchar(100) NOT NULL,
    registro_id         uuid        NOT NULL,
    operacao            varchar(20) NOT NULL,
    valores_anteriores  jsonb,
    valores_novos       jsonb,
    justificativa       varchar(500),
    usuario_id          uuid        NOT NULL REFERENCES usuario(id) ON DELETE RESTRICT,
    ocorrido_em         timestamptz NOT NULL,
    data_operacional    date        GENERATED ALWAYS AS
        (((ocorrido_em AT TIME ZONE 'America/Sao_Paulo') - interval '4 hours')::date) STORED,

    CONSTRAINT ck_log_operacao CHECK (operacao IN ('Criacao','Alteracao','Cancelamento')),
    CONSTRAINT ck_log_valores  CHECK (valores_anteriores IS NOT NULL OR valores_novos IS NOT NULL)
);

CREATE INDEX ix_log_registro ON log_auditoria (unidade_id, entidade, registro_id, ocorrido_em DESC);
CREATE INDEX ix_log_usuario  ON log_auditoria (unidade_id, usuario_id, data_operacional);
```

Sem `Exclusao` no domínio de `operacao`: `RN-GER-002` a torna impossível, e admiti-la no
`CHECK` sugeriria que ela pode acontecer.

**Migration necessária:** sim — `0003_Auditoria`.

## 7. Parâmetros consumidos

Nenhum.

## 8. Fora de escopo

- **Auditoria de leitura** — quem consultou o quê. Volume alto e valor baixo no MVP
- **Auditoria de login** — `SPEC-GER-004` cobre o acesso; esta trilha é de operação de negócio
- Retenção e arquivamento — a tabela cresce; a política entra quando o volume justificar
- Interface de consulta — onda 10; no MVP a consulta é por API

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Entidade | `src/GrelhaHub.Domain/Gerais/LogAuditoria.cs` |
| Marcadores | `IAuditavel` ✔, `IAuditavelDetalhado`, `[NaoAuditar]` |
| Interceptor | `src/GrelhaHub.Infrastructure/Persistence/Interceptors/AuditoriaInterceptor.cs` |
| Feature | `src/GrelhaHub.Application/Features/Gerais/ConsultarTrilha/` |
| Migration | `.../Migrations/0003_Auditoria` |
| Testes | `tests/GrelhaHub.Integration.Tests/Gerais/AuditoriaTests.cs` |

## 10. Definição de pronto

- [ ] Interceptor preenchendo `RN-GER-001` e bloqueando alteração dos campos de criação
- [ ] Auditoria detalhada gravando **apenas as propriedades alteradas**
- [ ] Auditoria **na mesma transação** da operação (CA-4)
- [ ] `RN-GER-002` recusando exclusão física com mensagem citando a regra
- [ ] Campos sensíveis mascarados (CA-6)
- [ ] Permissão de banco da aplicação sobre `log_auditoria` restrita a `INSERT` e `SELECT`
- [ ] CA-1 a CA-8 passando como teste de integração
- [ ] Rastreabilidade do catálogo §16 atualizada
- [ ] `dotnet build -c Release` e `dotnet test` verdes
