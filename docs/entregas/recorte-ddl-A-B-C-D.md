# Recorte do DDL — trechos para o diário (seção 5)

> **Fonte:** `docs/entregas/ddl-grelhahub-negocio.sql`, gerado das migrations do EF Core
> (`dotnet ef migrations script --idempotent`, 31/10/2026). O script completo cria as 22 tabelas
> do banco de negócio; aqui estão apenas os quatro trechos que o diário pede, **limpos do invólucro
> idempotente** (`DO $EF$ … END IF; END $EF$;`) para leitura — no arquivo `.sql` cada comando vem
> dentro desse bloco, que só executa a migration se ela ainda não foi aplicada.
>
> ⚠ O DDL **não é escrito à mão**: nasce do modelo de domínio e das migrations (Document First →
> Spec → código). Cada constraint abaixo responde a uma regra do
> [catálogo](../03-catalogo-de-regras-de-negocio.md) — o `RN-` está citado ao lado.

---

## A. Tabela independente — `unidade`

O restaurante. Não tem chave estrangeira nenhuma: **não há entidade acima dela** — é a raiz do
multi-tenant (`RN-GER-009`). Tudo o mais pendura aqui pela coluna `unidade_id`.

```sql
CREATE TABLE unidade (
    id uuid NOT NULL,
    razao_social character varying(200) NOT NULL,
    nome_fantasia character varying(200) NOT NULL,
    cnpj character varying(14) NOT NULL,
    slug character varying(60) NOT NULL,
    telefone character varying(11),
    ativa boolean NOT NULL DEFAULT TRUE,
    dh_criacao timestamp with time zone NOT NULL,
    fuso_horario character varying(50) NOT NULL DEFAULT 'America/Sao_Paulo',
    cep character varying(8),
    logradouro character varying(200),
    numero_endereco character varying(20),
    complemento character varying(100),
    bairro character varying(100),
    cidade character varying(100),
    uf character(2),
    hora_virada_operacional interval NOT NULL DEFAULT (interval '4 hours'),
    url_logo character varying(500),
    CONSTRAINT pk_unidade PRIMARY KEY (id),
    CONSTRAINT ck_unidade_cnpj CHECK (cnpj ~ '^[0-9]{14}$'),
    CONSTRAINT ck_unidade_razao_social CHECK (length(trim(razao_social)) > 0),
    CONSTRAINT ck_unidade_slug CHECK (slug ~ '^[a-z0-9-]{3,60}$')
);
```

**Decisão:** PK `uuid` (v7, gerado pela aplicação — a chave nasce antes do `INSERT`); nenhuma chave
natural (CNPJ e *slug* são **únicos**, não **chave**, porque identificador que o negócio escolhe é
identificador que o negócio muda).

---

## B. Tabela dependente com FOREIGN KEY — `produto`

Depende de duas tabelas: `unidade` (o tenant) e `categoria` (o grupo no cardápio). As duas FKs são
`ON DELETE RESTRICT` — o banco **recusa** apagar o pai que tem filho, porque nada transacional é
excluído fisicamente (`RN-GER-002`).

```sql
CREATE TABLE produto (
    id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    categoria_id uuid NOT NULL,
    nome character varying(200) NOT NULL,
    descricao character varying(500),
    preco numeric(12,2) NOT NULL,
    ativo boolean NOT NULL,
    disponivel boolean NOT NULL,
    url_imagem character varying(500),
    CONSTRAINT pk_produto PRIMARY KEY (id),
    CONSTRAINT ck_produto_preco CHECK (preco > 0),
    CONSTRAINT fk_produto_categoria_categoria_id
        FOREIGN KEY (categoria_id) REFERENCES categoria (id) ON DELETE RESTRICT,
    CONSTRAINT fk_produto_unidades_unidade_id
        FOREIGN KEY (unidade_id)  REFERENCES unidade (id)  ON DELETE RESTRICT
);
```

**Decisão:** preço em `numeric(12,2)` — **nunca `float`** (`RN-GER-003`); `ativo` (cadastral) e
`disponivel` (do turno) são campos distintos de propósito (`RN-VEN-044`), ainda que o efeito para o
cliente seja o mesmo.

---

## C. Tabela associativa que resolve o N:N — `modulo_dependencia`

Um módulo pode depender de vários módulos, e ser exigido por vários — um **N:N do `modulo` consigo
mesmo** (auto-relação). A associativa resolve isso com **chave primária composta pelo par**: a mesma
aresta não entra duas vezes, e ela não tem atributo próprio (é aresta, não coisa). `RN-GER-020`.

```sql
CREATE TABLE modulo_dependencia (
    modulo_id uuid NOT NULL,
    depende_de_id uuid NOT NULL,
    CONSTRAINT pk_modulo_dependencia PRIMARY KEY (modulo_id, depende_de_id),
    CONSTRAINT fk_modulo_dependencia_modulo_depende_de_id
        FOREIGN KEY (depende_de_id) REFERENCES modulo (id) ON DELETE RESTRICT,
    CONSTRAINT fk_modulo_dependencia_modulo_modulo_id
        FOREIGN KEY (modulo_id)     REFERENCES modulo (id) ON DELETE RESTRICT
);

-- e o CHECK que impede a aresta de um módulo para si mesmo:
ALTER TABLE modulo_dependencia
    ADD CONSTRAINT ck_modulo_dependencia_ciclo CHECK (modulo_id <> depende_de_id);
```

**Decisão:** a PK é **o par** (`modulo_id, depende_de_id`), não um `id` sintético — é o que impede a
dependência duplicada no próprio banco. O `CHECK` barra o ciclo curto (A→A); o ciclo longo
(A→B→C→A) é verificado na aplicação (`FechoDeDependencia`), porque `CHECK` não enxerga outras linhas.

> Outra N:N do modelo, com o mesmo padrão: `usuario_perfil (usuario_id, perfil_id)`.

---

## D. Restrições relevantes (CHECK, UNIQUE, EXCLUDE e coluna gerada)

O banco de negócio tem **16 CHECK, 16 índices únicos, 2 EXCLUDE, 1 coluna gerada e 2 gatilhos**.
Quatro exemplos que mostram decisões diferentes:

### D.1 — UNIQUE composto: o número do pedido reinicia a cada dia

```sql
CREATE UNIQUE INDEX uq_pedido_numero ON pedido (unidade_id, data_operacional, numero);
```

O `numero` que o cliente lê em voz alta **reinicia a cada virada** e **se repete entre casas** — por
isso o único é sobre o **trio** `(unidade, dia operacional, numero)`, não sobre `numero` sozinho.
Primeira coluna sempre `unidade_id` (`RN-GER-009`).

### D.2 — Coluna GERADA pelo banco: o dia operacional

```sql
data_operacional date
    GENERATED ALWAYS AS
    ((((dh_criacao AT TIME ZONE fuso_operacional) - hora_virada_operacional)::date))
    STORED NOT NULL
```

Ninguém preenche `data_operacional` — o banco a calcula no `INSERT`, a partir do fuso e da virada
**copiados na própria linha** do pedido. Mudar o horário da casa amanhã **não reclassifica** o
faturamento de ontem (`RN-GER-006`). É o mecanismo preferido do ADR-0009 (`GEN` antes de `TRG`).

### D.3 — EXCLUDE: duas vigências não podem se sobrepor

```sql
ALTER TABLE parametro_valor ADD CONSTRAINT ex_parametro_valor_vigencia
EXCLUDE USING gist (
    parametro_id WITH =,
    unidade_id   WITH =,
    tstzrange(vigencia_inicio, vigencia_fim) WITH &&
);
```

Impede, **no banco**, duas vigências simultâneas do mesmo parâmetro na mesma unidade — sem precisar
de um `SELECT` antes do `INSERT`, que teria corrida. Exige a extensão `btree_gist` (criada em
`scripts/preparar-banco.sql`). `SPEC-GER-003`, `RN-VEN-010`.

### D.4 — CHECK de domínio (nunca de política)

```sql
CONSTRAINT ck_produto_preco      CHECK (preco > 0)               -- item_pedido: ck_item_preco CHECK (preco_praticado >= 0)
CONSTRAINT ck_item_quantidade    CHECK (quantidade > 0)          -- quantidade de produto vendido é int, não numeric (RN-VEN-011)
CONSTRAINT ck_unidade_cnpj       CHECK (cnpj ~ '^[0-9]{14}$')    -- RN-GER-013
CONSTRAINT ck_modulo_codigo      CHECK (codigo ~ '^[A-Z]{2,3}$') -- duas OU três letras
```

**Decisão:** o `CHECK` valida **domínio** (`entre 0 e 100`, `14 dígitos`), **nunca política**
(`<= 10`). `10%` não é literal no `CHECK` — é `PAR-VEN-002`, uma linha de `parametro_valor` com
vigência. Política muda por decisão comercial, e mudança de política não pode exigir migration.

---

## Seção 6 — Testes de integridade ("faça o banco errar")

Os testes de integração rodam contra **PostgreSQL real via Testcontainers** (ADR-0009), justamente
porque o provider in-memory do EF Core não executa constraint, trigger nem coluna gerada — daria
falsa confiança nas regras que mais importam. O ID da regra vai no `DisplayName`. Exemplos em
`backend/tests/GrelhaHub.Integration.Tests/`:

| Teste (`DisplayName`) | O que faz o banco/aplicação recusar |
|---|---|
| `RN-VEN-032 — preço zero é recusado e nada é gravado` | viola `ck_produto_preco` |
| `RN-VEN-043 — nome de categoria repetido na unidade é recusado com 422, não 500` | viola `uq_categoria_nome` |
| `RN-VEN-044 — nome de produto repetido na unidade é recusado com 422` | viola `uq_produto_nome` |
| `RN-GER-002 — exclusão física é recusada pelo interceptor` | `ON DELETE RESTRICT` + interceptor |
| `RN-GER-009 — gravar sem unidade é recusado antes de tocar em qualquer banco` | `unidade_id NOT NULL` + validação |

> ⚠ A **mensagem de erro ao usuário vem sempre da aplicação** (ADR-0009): o banco garante que o dado
> nunca fica inválido; a aplicação garante que o usuário nunca chega até lá. Erro de constraint que
> vaza cru para o usuário (500) é defeito da aplicação — daí os testes exigirem **422**, não 500.
