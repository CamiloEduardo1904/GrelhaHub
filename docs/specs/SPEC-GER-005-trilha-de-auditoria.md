# SPEC-GER-005 — Trilha de auditoria

| Campo | Valor |
|-------|-------|
| ID | `SPEC-GER-005-trilha-de-auditoria` |
| Módulo | GER |
| Situação | `ENTREGUE` |
| Onda | 0 — fundação |
| Regras implementadas | `RN-GER-001`, `RN-GER-011`, `RN-GER-002`, `RN-GER-012` |
| ADRs aplicáveis | [ADR-0008](../adr/0008-estoque-como-livro-razao.md), [ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md) |
| Depende de | `SPEC-GER-001`, `SPEC-GER-004` |
| Data | 17/08/2026 |
| Entregue em | 17/08/2026 |

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
| `RN-GER-001` | Todo registro transacional guarda data/hora de criação e usuário responsável | Modelo + Aplicação | Colunas `dh_criacao NOT NULL` + `criado_por` **nulável** + `InterceptorDeAuditoria` |
| `RN-GER-011` | Operações sensíveis registram usuário, data/hora, operação e valores anterior e posterior | Aplicação | `SaveChangesInterceptor` sobre entidades marcadas |
| `RN-GER-002` | Nenhum registro transacional é excluído fisicamente | Banco + Aplicação | `ON DELETE RESTRICT` + o interceptor **recusa** `EntityState.Deleted` |
| `RN-GER-012` | Custo, CMV e margem exigem permissão | Aplicação | `auditoria.consultar` na consulta da trilha — ela guarda valores anteriores de campo sensível |

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
var exclusoes = db.ChangeTracker.Entries()
    .Where(entrada => entrada.State == EntityState.Deleted)
    .Where(entrada => entrada.Entity is Entidade);   // e não `IPertenceAUnidade`

if (exclusoes.Count > 0)
    throw new InvalidOperationException(
        $"RN-GER-002 — registro transacional não é excluído fisicamente: {nomes}. " +
        "Use cancelamento, inativação, exclusão lógica ou movimento contrário.");
```

**O filtro é `Entidade`, não `IPertenceAUnidade`** — a spec original deixaria de fora
justamente `unidade` e o usuário de plataforma (`RN-GER-014`), que não pertencem a unidade
alguma e são os registros cuja exclusão mais importaria impedir.

A checagem é da aplicação, complementada por `ON DELETE RESTRICT` no banco (`SPEC-GER-001`).
Falha ruidosa em desenvolvimento vale mais do que descobrir a exclusão pela ausência do dado
em produção. O banco impede a exclusão que **tem** dependente; o interceptor impede também a
que não tem.

> **Descoberto no teste do CA-5:** com os itens do pedido carregados, o próprio `ChangeTracker`
> do EF derruba o `Remove()` antes do `SaveChanges` (`HandleConceptualNulls`). O caminho que
> chega ao interceptor é o do agregado **sem dependente carregado** — e é exatamente o que
> `ON DELETE RESTRICT` também não cobre. O teste precisa dispensar o `Include`, ou passa
> medindo a guarda do EF em vez da nossa.

### `RN-GER-001` — preenchimento automático

O mesmo interceptor preenche `criado_em` e `criado_por` em toda entidade `Added` que
implemente `IAuditavel` — e **bloqueia alteração** desses campos em entidades `Modified`.

### A trilha é imutável

`log_auditoria` não tem `UPDATE` nem `DELETE` em nenhuma rota. Uma trilha alterável não é
trilha — e a garantia está em **duas camadas**:

| Camada | Mecanismo | Cobre |
|--------|-----------|-------|
| Banco | `trg_log_auditoria_imutavel` (`BEFORE UPDATE OR DELETE`) e `trg_log_auditoria_sem_truncate` (`BEFORE TRUNCATE`) | Tudo — inclusive `psql` na mão, script de correção, ORM futuro |
| Aplicação | `ProtegerATrilha` recusa qualquer `LogAuditoria` em `Modified` | A tentativa que vem pelo código, com mensagem legível |

**A permissão de banco `INSERT`/`SELECT` que esta spec pedia não foi implementada, e não deve
ser:** a aplicação é **dona** da tabela, e o dono reconcede o que lhe for revogado. `REVOKE`
daria a aparência da garantia sem a garantia. `ADR-0009` prefere `TRG` a `APP` exatamente
neste caso, e o gatilho vale para qualquer conexão, não só para a da aplicação.

`TRUNCATE` recebe gatilho próprio porque **não dispara** `FOR EACH ROW` — e é o único comando
que apagaria a trilha inteira de uma vez.

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
    id                  uuid         PRIMARY KEY,
    unidade_id          uuid         NULL REFERENCES unidade(id) ON DELETE RESTRICT,
    entidade            varchar(100) NOT NULL,
    registro_id         uuid         NOT NULL,
    operacao            varchar(20)  NOT NULL,
    valores_anteriores  jsonb,
    valores_novos       jsonb,
    justificativa       varchar(500),
    usuario_id          uuid         NULL REFERENCES usuario(id) ON DELETE RESTRICT,
    dh_ocorrencia       timestamptz  NOT NULL,
    data_operacional    date         GENERATED ALWAYS AS
        (((dh_ocorrencia AT TIME ZONE 'America/Sao_Paulo') - interval '4 hours')::date) STORED,

    CONSTRAINT ck_log_operacao CHECK (operacao IN ('Criacao','Alteracao','Cancelamento','Exclusao')),
    CONSTRAINT ck_log_valores  CHECK (valores_anteriores IS NOT NULL OR valores_novos IS NOT NULL)
);

CREATE INDEX ix_log_registro ON log_auditoria (unidade_id, entidade, registro_id, dh_ocorrencia DESC);
CREATE INDEX ix_log_usuario  ON log_auditoria (unidade_id, usuario_id, data_operacional);
```

### Três divergências desta spec, e por quê

**1. `unidade_id` e `usuario_id` são NULÁVEIS.** A spec pedia `NOT NULL` nos dois, e isso
tornaria **inauditável** exatamente o que mais precisa de trilha:

| Caso | Falta | Regra |
|------|-------|-------|
| Usuário de plataforma habilita módulo | `unidade_id` do próprio usuário | `RN-GER-014`, [ADR-0013](../adr/0013-acesso-de-plataforma.md) |
| Pedido do cardápio público | `usuario_id` — o cliente não é usuário | `RN-VEN-038` |
| Cadastro do restaurante | `usuario_id` — ninguém está autenticado ainda | `SPEC-GER-004` |

`NOT NULL` aqui não daria mais garantia; daria a escolha entre não auditar a operação ou
inventar um autor. Preenchi o que existe e deixei nulo o que não existe.

**2. `Exclusao` CONSTA no `CHECK`.** A spec a omitia de propósito — "`RN-GER-002` a torna
impossível". O argumento continua valendo para exclusão **física**; a exclusão **lógica**
nasceu depois, em `RN-VEN-041`, e precisa ser distinguível de um cancelamento: cancelar é
fato de negócio e entra na métrica, excluir tira da apuração.

**3. A coluna é `dh_ocorrencia`,** não `ocorrido_em` — convenção `dh_` de `SPEC-GER-001`.

**Migration:** `20260817182824_TrilhaDeAuditoria` (e não `0003_Auditoria` — a numeração é o
timestamp do EF). Além da tabela, ela semeia a permissão `auditoria.consultar` e a concede a
Administrador e Gerente nas unidades **já existentes**.

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
| Entidade | `src/GrelhaHub.Domain/Gerais/LogAuditoria.cs` — com `OperacaoDeAuditoria`, `TipoDeMudanca`, `IAuditavelDetalhado`, `NaoAuditarAttribute` |
| Marcadores | `IAuditavel` (em `Common/Entidade.cs`), `IAuditavelDetalhado`, `[NaoAuditar]` |
| Interceptor | `src/GrelhaHub.Infrastructure/Persistence/Interceptors/InterceptorDeAuditoria.cs` |
| Configuração | `.../Persistence/Configurations/ConfiguracaoDeAuditoria.cs` |
| Feature | `src/GrelhaHub.Application/Features/Gerais/Auditoria/ConsultarTrilha.cs` |
| Controller | `src/GrelhaHub.Api/Controllers/AuditoriaController.cs` — somente leitura |
| Migration | `.../Persistence/Migrations/20260817182824_TrilhaDeAuditoria.cs` |
| Testes | `tests/GrelhaHub.Integration.Tests/Gerais/AuditoriaTests.cs` — 10 testes |

### Ordem dos interceptores é carga estrutural

Em `Infrastructure/DependencyInjection.cs`, `InterceptorDeUnidade` vem **antes** de
`InterceptorDeAuditoria`: o primeiro preenche `unidade_id` na entidade nova, o segundo lê a
entidade para montar a linha de trilha. Invertidos, a trilha registraria `Guid.Empty` como
unidade de todo registro recém-criado. O harness de integração monta os dois na mesma ordem —
se montasse só um, o teste mediria um comportamento que a produção não tem.

### Entidades auditadas hoje

`pedido` (`RN-VEN-014`, `RN-VEN-041`), `parametro_valor` (`SPEC-GER-003` CA-6),
`unidade_modulo` (`RN-GER-017`), `usuario` (`RN-GER-010`). As demais linhas da tabela em §4
entram com as suas ondas.

## 10. Definição de pronto

- [x] Interceptor preenchendo `RN-GER-001` e bloqueando alteração dos campos de criação
- [x] Auditoria detalhada gravando **apenas as propriedades alteradas**
- [x] Auditoria **na mesma transação** da operação (CA-4)
- [x] `RN-GER-002` recusando exclusão física com mensagem citando a regra
- [x] Campos sensíveis mascarados (CA-6)
- [x] ~~Permissão de banco restrita a `INSERT` e `SELECT`~~ — **substituída** por gatilho de
  imutabilidade. Motivo em §4: a aplicação é dona da tabela e o dono reconcede o `REVOKE`.
  A garantia entregue é mais forte, não menor: vale para qualquer conexão
- [x] CA-1 a CA-8 passando como teste de integração — 10 testes, incluindo um para a camada
  de banco do CA-7 e um que fecha o CA-6 da `SPEC-GER-003`
- [x] Rastreabilidade do catálogo §16 atualizada
- [x] `dotnet build -c Release` e `dotnet test` verdes — **42 domínio + 83 integração = 125**

## 11. Verificação por mutação

Teste de auditoria que não falha quando a auditoria é removida não prova nada — ele mediria
o `SaveChanges`, não a trilha. Oito mutações no interceptor e no gatilho, uma a uma:

| Mutação | Testes que caíram |
|---------|-------------------|
| Sem `RecusarExclusaoFisica` | CA-5 e CA-7 (2) |
| Sem `ProtegerATrilha` | CA-7 aplicação (1) |
| Sem restaurar os campos de criação | CA-2 (1) |
| Sem ocultar `[NaoAuditar]` | CA-6 (1) |
| Serializando o objeto inteiro em vez do que mudou | CA-3 (1) |
| Sem preencher o autor na criação | CA-1 (1) |
| Sem inserir a linha de trilha (auditoria fora da transação) | 5 testes |
| Gatilho de banco removido | CA-7 banco (1) — e o CA-7 de aplicação **continuou verde**, o que mostra que as duas camadas estão medidas em separado |

Nenhuma mutação passou.
