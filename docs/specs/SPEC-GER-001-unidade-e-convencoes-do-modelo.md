# SPEC-GER-001 — Unidade e convenções do modelo

| Campo | Valor |
|-------|-------|
| ID | `SPEC-GER-001-unidade-e-convencoes-do-modelo` |
| Módulo | GER |
| Situação | `PRONTA` |
| Onda | 0 — fundação |
| Regras implementadas | `RN-GER-009` ⚠, `RN-GER-002`, `RN-GER-003`, `RN-GER-004`, `RN-GER-005` |
| ADRs aplicáveis | [ADR-0004](../adr/0004-postgresql-como-sgbd.md), [ADR-0005](../adr/0005-multi-estabelecimento-por-coluna-discriminadora.md), [ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md) |
| Depende de | — (é a primeira) |
| Data | 17/08/2026 |

---

## 1. Objetivo

Estabelecer a **entidade `unidade`** e as **convenções de tipo e integridade** que toda
tabela transacional do sistema vai herdar — de modo que nenhuma spec posterior precise
decidir de novo como se representa dinheiro, quantidade, data ou vínculo de unidade.

> Esta spec quase não entrega funcionalidade visível. Entrega **o formato de todas as
> tabelas que virão depois**. É a spec cujo erro sai mais caro: uma casa decimal errada aqui
> se propaga para 40 tabelas.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-GER-009` ⚠ | Todo registro transacional é vinculado a uma unidade | Modelo + Aplicação | FK `NOT NULL` + filtro global no `DbContext` |
| `RN-GER-002` | Nenhum registro transacional é excluído fisicamente | Modelo + Aplicação | Ausência de `DELETE` na API; `ON DELETE RESTRICT` nas FKs |
| `RN-GER-003` | Valores monetários em reais, duas casas decimais | Modelo | `numeric(12,2)` por convenção do `DbContext` |
| `RN-GER-004` | Quantidades de insumo admitem quatro casas decimais | Modelo | `numeric(14,4)` por convenção do `DbContext` |
| `RN-GER-005` | Datas e horas com fuso; operação em `America/Sao_Paulo` | Modelo | `timestamptz`, sempre UTC no armazenamento |

## 3. Contrato

A unidade não tem CRUD exposto no MVP: existe uma única unidade, criada pela carga inicial
(premissa P4). A API expõe apenas a consulta.

### `ObterUnidadeAtualQuery`

**Entrada:** nenhuma — a unidade vem do token (`IContextoDeExecucao.UnidadeId`).

| Campo de saída | Tipo | Observação |
|----------------|------|------------|
| `id` | `uuid` | |
| `razaoSocial` | `string` | |
| `nomeFantasia` | `string` | |
| `cnpj` | `string` | Somente dígitos, 14 posições |
| `fusoHorario` | `string` | `America/Sao_Paulo` |

### Erros

| Situação | Código | Mensagem | Regra |
|----------|--------|----------|-------|
| Token sem `unidade_id` | 401 | "Sessão inválida." | `RN-GER-009` |

### Autorização

Qualquer perfil autenticado.

## 4. Comportamento

### Convenções aplicadas automaticamente

Registradas uma vez em `GrelhaHubDbContext.ConfigureConventions`, para que nenhuma
configuração de entidade precise repeti-las:

| Tipo C# | Tipo no banco | Regra |
|---------|---------------|-------|
| `decimal` **monetário** (`Dinheiro`) | `numeric(12,2)` | `RN-GER-003` |
| `decimal` **de quantidade** (`Quantidade`) | `numeric(14,4)` | `RN-GER-004` |
| `DateTimeOffset` | `timestamptz` | `RN-GER-005` |
| `DateOnly` | `date` | |
| `string` sem tamanho declarado | `varchar(200)` | evita `text` por descuido |

> **Dinheiro e quantidade são value objects, não `decimal` cru.** Um `decimal` solto não
> distingue R$ 12,00 de 12 gramas, e o compilador não impede somar os dois. Com tipos
> distintos, a precisão certa é aplicada por convenção e a confusão vira erro de compilação.

### Filtro global de unidade

Toda entidade que implementa `IPertenceAUnidade` recebe filtro global no `DbContext`
(já implementado em `GrelhaHubDbContext.AplicarFiltroDeUnidade`).

**Por que global e não por consulta:** uma consulta que escapasse do recorte exporia dados
de outra unidade. O isolamento não pode depender de o desenvolvedor lembrar de filtrar — é
o risco declarado no [ADR-0005](../adr/0005-multi-estabelecimento-por-coluna-discriminadora.md).

### Preenchimento automático de `unidade_id`

Um interceptor do EF Core (`SaveChangesInterceptor`) preenche `UnidadeId` em toda entidade
`Added` que implemente `IPertenceAUnidade`, a partir do `IContextoDeExecucao`. O handler
nunca atribui a unidade a mão — se atribuísse, um esquecimento gravaria `Guid.Empty` e o
registro sumiria do filtro global, dando a impressão de que a gravação falhou.

## 5. Critérios de aceite

### CA-1 — Vínculo obrigatório à unidade (`RN-GER-009`)

> **Dado** uma entidade transacional qualquer,
> **Quando** um registro for inserido sem `unidade_id`,
> **Então** o banco rejeita por violação de `NOT NULL`,
> **e** a inserção pela aplicação nunca chega a esse ponto: o interceptor preenche o campo.

### CA-2 — Isolamento entre unidades (`RN-GER-009`)

> **Dado** dois registros da mesma tabela, um na unidade A e outro na unidade B,
> **Quando** um usuário da unidade A consultar a tabela **sem filtro explícito**,
> **Então** apenas o registro da unidade A é retornado,
> **e** o registro da unidade B não aparece em nenhuma consulta, contagem ou agregação.

### CA-3 — Precisão monetária (`RN-GER-003`)

> **Dado** um campo monetário,
> **Quando** for gravado o valor `10,999`,
> **Então** o valor persistido é `11,00` — arredondado, não truncado,
> **e** a coluna é `numeric(12,2)`, nunca `float` ou `double precision`.

### CA-4 — Precisão de quantidade (`RN-GER-004`)

> **Dado** um campo de quantidade de insumo,
> **Quando** for gravado `0,15625` (resultado de `líquida ÷ (1 − perda)`),
> **Então** o valor persistido é `0,1563`, com quatro casas,
> **e** a coluna é `numeric(14,4)`.

### CA-5 — Data com fuso (`RN-GER-005`)

> **Dado** um evento registrado às 22h00 de 16/08 no horário de Brasília,
> **Quando** o registro for gravado,
> **Então** o valor persistido é `2026-08-17T01:00:00Z` — UTC,
> **e** a coluna é `timestamptz`,
> **e** a leitura convertida para `America/Sao_Paulo` devolve 22h00 de 16/08.

### CA-6 — Ausência de exclusão física (`RN-GER-002`)

> **Dado** uma unidade com registros transacionais vinculados,
> **Quando** for tentada a exclusão da unidade,
> **Então** o banco rejeita por `ON DELETE RESTRICT`,
> **e** nenhuma rota da API expõe `DELETE` sobre entidade transacional.

## 6. Modelo de dados tocado

| Tabela | Operação | Constraints |
|--------|:--------:|-------------|
| `unidade` | criação | PK `uuid`; `cnpj` `UNIQUE` e `CHECK (cnpj ~ '^[0-9]{14}$')`; `fuso_horario NOT NULL DEFAULT 'America/Sao_Paulo'`; `ativa boolean NOT NULL DEFAULT true` |

```sql
CREATE TABLE unidade (
    id             uuid        PRIMARY KEY,
    razao_social   varchar(200) NOT NULL,
    nome_fantasia  varchar(200) NOT NULL,
    cnpj           varchar(14)  NOT NULL,
    fuso_horario   varchar(50)  NOT NULL DEFAULT 'America/Sao_Paulo',
    ativa          boolean      NOT NULL DEFAULT true,
    criado_em      timestamptz  NOT NULL,

    CONSTRAINT uq_unidade_cnpj    UNIQUE (cnpj),
    CONSTRAINT ck_unidade_cnpj    CHECK (cnpj ~ '^[0-9]{14}$'),
    CONSTRAINT ck_unidade_r_social CHECK (length(trim(razao_social)) > 0)
);
```

**Migration necessária:** sim — `0001_Fundacao`.

**Padrão para as tabelas seguintes:**

```sql
unidade_id uuid NOT NULL REFERENCES unidade(id) ON DELETE RESTRICT
```

E `unidade_id` é a **primeira coluna** de toda chave única de negócio e de todo índice
composto — sob pena de o índice degradar quando houver mais de uma unidade.

## 7. Parâmetros consumidos

Nenhum.

## 8. Fora de escopo

- **Cadastro e edição de unidade pela interface** — o MVP opera com uma unidade, criada na
  carga inicial. Se vier a existir, é spec própria
- **Troca de unidade em tempo de execução** — nada na UI expõe isso (ADR-0005)
- **Auditoria de criação** (`RN-GER-001`) — fica em `SPEC-GER-005`
- **Coluna `data_operacional`** — fica em `SPEC-GER-002`, que roda logo em seguida e é
  aplicada na mesma migration de fundação

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Entidade | `src/GrelhaHub.Domain/Gerais/Unidade.cs` |
| Value objects | `src/GrelhaHub.Domain/Common/Dinheiro.cs`, `Quantidade.cs` |
| Marcadores | `src/GrelhaHub.Domain/Common/Entidade.cs` |
| Configuração EF | `src/GrelhaHub.Infrastructure/Persistence/Configurations/UnidadeConfiguration.cs` |
| Convenções | `src/GrelhaHub.Infrastructure/Persistence/GrelhaHubDbContext.cs` |
| Interceptor | `src/GrelhaHub.Infrastructure/Persistence/Interceptors/UnidadeInterceptor.cs` |
| Migration | `src/GrelhaHub.Infrastructure/Persistence/Migrations/0001_Fundacao` |
| Testes | `tests/GrelhaHub.Domain.Tests/Common/`, `tests/GrelhaHub.Integration.Tests/Gerais/` |

## 10. Definição de pronto

- [ ] `unidade` criada com todas as constraints da §6
- [ ] Convenções de tipo aplicadas no `DbContext`, não repetidas por entidade
- [ ] Filtro global ativo e coberto por teste de integração (CA-2)
- [ ] Interceptor preenchendo `unidade_id`
- [ ] Um teste por regra, com o ID no `DisplayName`
- [ ] CA-1 a CA-6 passando como teste de integração contra PostgreSQL real
- [ ] Rastreabilidade do [catálogo §16](../03-catalogo-de-regras-de-negocio.md#16-rastreabilidade-regra--spec) atualizada
- [ ] `dotnet build -c Release` e `dotnet test` verdes
