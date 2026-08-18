# SPEC-GER-001 — Unidade e convenções do modelo

| Campo | Valor |
|-------|-------|
| ID | `SPEC-GER-001-unidade-e-convencoes-do-modelo` |
| Módulo | GER |
| Situação | `ENTREGUE` — 17/08/2026 |
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

> **Entregue por `ObterMeuRestauranteQuery`**, de `SPEC-VEN-001`, e não por uma consulta nova.
> Ela já devolve `id`, `razaoSocial`, `nomeFantasia` e `cnpj` a partir do token; `fusoHorario`
> foi acrescentado ao contrato para fechar esta seção. Uma segunda consulta com os mesmos
> campos seria duplicação, e duas leituras da mesma entidade divergem com o tempo.

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

**Não existe convenção para `decimal` cru, e a ausência é deliberada.** Uma regra geral
`Properties<decimal>().HavePrecision(12, 2)` parece prudente e é a armadilha: a primeira
quantidade de insumo declarada como `decimal` herdaria duas casas e `0,1563` seria gravado
como `0,16` **sem erro nenhum**. Quem tentar usar `decimal` cru é barrado por
`GrelhaHubDbContext.GarantirAusenciaDeDecimalCru`, que **recusa o modelo na partida** e diz
qual propriedade corrigir.

### Custo descoberto na implementação — projeção em memória

O EF Core **não traduz acesso a membro de propriedade com conversor de valor**:
`p.Preco.Valor` dentro de uma árvore de expressão não vira SQL, e `Sum(i => i.PrecoPraticado *
i.Quantidade)` não compila para consulta.

Consequência assumida: as quatro consultas de leitura filtram e ordenam no banco, mas montam
o DTO **em memória**. Os contratos de API seguem em `decimal` — value object é conceito de
domínio, não formato de fio.

Isso teve um efeito colateral bom: o total do pedido saía de um `Sum` na consulta, uma
**segunda definição** de `RN-VEN-039` que divergiria de `Pedido.Total` no dia em que taxa de
serviço entrasse na fórmula. Agora as duas consultas carregam o agregado e usam
`Pedido.Total` — a regra tem um lugar só.

Para o volume atual — o cardápio de um restaurante, os pedidos de um dia operacional — a
diferença é irrelevante. A onda 9 (analítico) não vai por aqui: usa view e projeção
dedicadas, que é o mecanismo `VW` já previsto no catálogo.

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
    dh_criacao     timestamptz  NOT NULL,

    CONSTRAINT uq_unidade_cnpj         UNIQUE (cnpj),
    CONSTRAINT uq_unidade_slug         UNIQUE (slug),
    CONSTRAINT ck_unidade_cnpj         CHECK (cnpj ~ '^[0-9]{14}$'),
    CONSTRAINT ck_unidade_slug         CHECK (slug ~ '^[a-z0-9-]{3,60}$'),
    CONSTRAINT ck_unidade_razao_social CHECK (length(trim(razao_social)) > 0)
);
```

**Migrations:** `Fundacao` criou a tabela; `ConvencoesDoModelo` acrescentou
`ck_unidade_razao_social` e os `DEFAULT` de `ativa` e `fuso_horario`. O nome da coluna de
criação é `dh_criacao`, e não `criado_em` — convenção seguida por todas as tabelas.

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
| Entidade | `src/GrelhaHub.Domain/Gerais/Unidade.cs` ✔ |
| Value objects | `src/GrelhaHub.Domain/Common/Dinheiro.cs`, `Quantidade.cs` ✔ |
| Marcadores | `src/GrelhaHub.Domain/Common/Entidade.cs` ✔ |
| Configuração EF | `src/GrelhaHub.Infrastructure/Persistence/Configurations/ConfiguracoesDoModelo.cs` ✔ |
| Conversores | `src/GrelhaHub.Infrastructure/Persistence/Conversores.cs` ✔ |
| Convenções de tipo | `src/GrelhaHub.Infrastructure/Persistence/ConvencoesDeTipo.cs` ✔ — em ponto próprio para poder ser testada sem tabela que a use |
| Convenções e guarda | `src/GrelhaHub.Infrastructure/Persistence/GrelhaHubDbContext.cs` ✔ |
| Interceptor | `src/GrelhaHub.Infrastructure/Persistence/Interceptors/InterceptorDeUnidade.cs` ✔ |
| Migrations | `Migrations/20260817051948_Fundacao`, `Migrations/20260817152502_ConvencoesDoModelo` ✔ |
| Testes de domínio | `tests/GrelhaHub.Domain.Tests/Common/DinheiroTests.cs`, `QuantidadeTests.cs` ✔ (16 casos) |
| Testes de integração | `tests/GrelhaHub.Integration.Tests/Gerais/ConvencoesDoModeloTests.cs` ✔ (8 casos), `PrecisaoDeQuantidadeTests.cs` ✔ (3 casos, CA-4) |

## 10. Definição de pronto

- [x] `unidade` criada com todas as constraints da §6
- [x] Convenções de tipo aplicadas no `DbContext`, não repetidas por entidade
- [x] Filtro global ativo e coberto por teste de integração (CA-2) — inclui contagem e busca
      direta, não só listagem: é onde um filtro aplicado na consulta errada deixaria vazar
- [x] Interceptor preenchendo `unidade_id`, **e um teste que prova que a API o registra** —
      nenhum fluxo atual cria entidade sem unidade, então sem essa asserção a fiação poderia
      ser removida sem nenhum teste ficar vermelho
- [x] Um teste por regra, com o ID no `DisplayName`
- [x] **CA-1 a CA-6** passando como teste de integração contra PostgreSQL real
- [x] **CA-4 fechado sem esperar a onda 2.** Nenhuma tabela tem coluna de quantidade ainda, mas
      a convenção é verificável sem ela: `PrecisaoDeQuantidadeTests` **deriva a DDL do próprio
      modelo** — lê a precisão e a escala que `ConvencoesDeTipo` atribui a uma propriedade
      `Quantidade`, cria a coluna com esses valores e grava `0,15625`. Com quatro casas o valor
      sobrevive como `0,1563`; se alguém trocar a convenção para a precisão do dinheiro, a
      coluna nasce `numeric(12,2)`, o PostgreSQL devolve `0,16` e o teste fica vermelho.
      É a convenção sob teste, não uma constante repetida no teste
- [x] Rastreabilidade do [catálogo §16](../03-catalogo-de-regras-de-negocio.md#16-rastreabilidade-regra--spec) atualizada
- [x] `dotnet build -c Release` e `dotnet test` verdes — **73 testes**: 42 de domínio, 31 de integração

> **Verificado por mutação — 17/08/2026.** Trocar a convenção de quantidade para a precisão do
> dinheiro faz dois testes falharem com a mensagem exata do modo de falha que `RN-GER-004`
> existe para impedir: *"Expected gravado to be 0.1563M ... but found 0.16M"*. E remover o
> registro do interceptor em `AdicionarInfrastructure` derruba
> `CA1_OInterceptorEstaLigadoNaApi`. As duas asserções que mais importam nesta spec têm dentes.
>
> Além do CA-4, o modelo **recusa** na partida qualquer propriedade mapeada como `decimal` cru
> (`GarantirAusenciaDeDecimalCru`): em vez de testar que uma coluna está certa, impede a classe
> de erro.
