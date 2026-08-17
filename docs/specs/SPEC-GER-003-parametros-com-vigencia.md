# SPEC-GER-003 — Parâmetros do sistema com vigência

| Campo | Valor |
|-------|-------|
| ID | `SPEC-GER-003-parametros-com-vigencia` |
| Módulo | GER |
| Situação | `PRONTA` |
| Onda | 0 — fundação |
| Regras implementadas | `RN-GER-009`, `RN-GER-011` *(auditoria da alteração)* — e habilita todas as regras marcadas `PAR` |
| ADRs aplicáveis | [ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md) |
| Depende de | `SPEC-GER-001` |
| Data | 17/08/2026 |

---

## 1. Objetivo

Dar ao sistema um lugar para os **valores comerciais configuráveis** (`PAR-*`), com
histórico de vigência — de modo que nenhum número de negócio precise viver no código, e que
um pedido antigo continue explicável pelos valores que vigoravam quando foi feito.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-GER-009` | Vínculo à unidade | Modelo | FK `NOT NULL` |
| `RN-GER-011` | Alteração de valor sensível fica na trilha de auditoria | Aplicação | Interceptor de auditoria (`SPEC-GER-005`) |
| *(habilitador)* | Nove regras marcadas `PAR` dependem desta spec | — | `RN-GER-006`, `RN-VEN-013`, `RN-VEN-014`, `RN-VEN-021`, `RN-EST-008`, `RN-COM-005`, `RN-COM-009`, `RN-CRM-008`, `RN-FIS-003` |

## 3. Contrato

### `ObterParametroQuery`

| Entrada | Tipo | Obrigatório | Validação |
|---------|------|:-----------:|-----------|
| `codigo` | `string` | ✔ | Deve existir em `parametro` |
| `vigenteEm` | `DateTimeOffset?` | | Ausente = agora |

**Saída:** `codigo`, `valor` (tipado), `vigenciaInicio`, `vigenciaFim`.

### `AlterarValorDeParametroCommand`

| Entrada | Tipo | Obrigatório | Validação | Regra |
|---------|------|:-----------:|-----------|-------|
| `codigo` | `string` | ✔ | Parâmetro existente | |
| `novoValor` | `string` | ✔ | Compatível com `tipo_valor`; dentro do domínio declarado | |
| `vigenciaInicio` | `DateTimeOffset` | ✔ | Não pode ser anterior ao início da vigência corrente | |
| `justificativa` | `string` | ✔ | Mínimo 10 caracteres | `RN-GER-011` |

### Erros

| Situação | Código | Mensagem | Regra |
|----------|--------|----------|-------|
| Código inexistente | 404 | "Parâmetro não encontrado." | |
| Valor fora do domínio | 422 | "O valor informado não é válido para este parâmetro: {domínio}." | |
| Vigência retroativa | 422 | "Não é possível alterar um parâmetro com efeito retroativo." | |
| Sem permissão | 403 | "Apenas a gerência pode alterar parâmetros." | `RN-GER-010` |

### Autorização

| Perfil | Consultar | Alterar |
|--------|:---------:|:-------:|
| Administrador | ✔ | ✔ |
| Gerente | ✔ | ✔ |
| Atendente | ✔ | ✘ |
| Cozinha | ✔ | ✘ |

Consulta liberada porque a taxa de serviço e o teto de desconto precisam ser conhecidos pelo
atendente durante a operação.

## 4. Comportamento

### Leitura sempre na data do evento

```csharp
var taxa = await parametros.ObterAsync<decimal>("PAR-VEN-002", pedido.AbertoEm);
```

**Nunca** "o valor de agora". Um pedido de três meses atrás foi calculado com a taxa de
serviço daquela época, e recalculá-lo com a taxa atual reescreveria o passado — o mesmo erro
que `RN-VEN-008` e `RN-VEN-010` evitam para preço.

### Alteração fecha a vigência anterior

Alterar um valor **não faz `UPDATE`** na linha vigente. Faz duas coisas, na mesma transação:

1. `vigencia_fim` da linha corrente recebe `vigenciaInicio` da nova
2. Nova linha é inserida com `vigencia_fim` nula

O histórico permanece íntegro, e a `EXCLUDE` constraint garante que não haja sobreposição.

### Cache

A leitura passa por um único ponto (`IParametros`), com cache em memória invalidado na
escrita. Nenhum handler acessa `parametro_valor` diretamente — se acessasse, o cache
divergiria do banco sem que ninguém percebesse.

O cache é **por `(codigo, vigenciaInicio, vigenciaFim)`**, não por código apenas: consultas
em datas passadas não podem ser servidas pela entrada do valor corrente.

### Domínio do valor

Cada parâmetro declara `tipo_valor` e, opcionalmente, `dominio` — texto que descreve a
restrição e é usado tanto na validação quanto na mensagem de erro:

| `tipo_valor` | Exemplo de domínio |
|--------------|--------------------|
| `percentual` | `0..100` |
| `dinheiro` | `> 0` |
| `dias` | `>= 0` |
| `tempo` | `00:00..23:59` |
| `intervalo` | duração positiva |
| `enum` | `bloquear\|avisar\|permitir` |

> **`CHECK` valida domínio, nunca política.** `percentual entre 0 e 100` é constraint;
> `percentual <= 10` seria política e não pode virar constraint — é justamente o valor que
> o parâmetro existe para deixar configurável.

## 5. Critérios de aceite

### CA-1 — Leitura na data do evento

> **Dado** que `PAR-VEN-002` valia 10% até 01/07/2026 e passou a valer 12% a partir dessa data,
> **Quando** for consultado o valor vigente em 15/06/2026,
> **Então** o retorno é 10%,
> **e** a consulta sem data retorna 12%.

### CA-2 — Alteração preserva o histórico

> **Dado** um parâmetro com uma vigência aberta,
> **Quando** o valor for alterado com início em 01/09/2026,
> **Então** a vigência anterior é fechada em 01/09/2026,
> **e** a linha anterior **não é modificada em nenhum outro campo**,
> **e** consultar uma data anterior continua devolvendo o valor antigo.

### CA-3 — Vigências não se sobrepõem

> **Dado** um parâmetro com vigência de 01/07 em diante,
> **Quando** for inserida diretamente no banco uma vigência começando em 15/07,
> **Então** o banco rejeita por violação da `EXCLUDE` constraint,
> **e** a rejeição ocorre mesmo por escrita fora da aplicação.

### CA-4 — Alteração retroativa é recusada

> **Dado** um parâmetro cuja vigência corrente começou em 01/08,
> **Quando** for solicitada alteração com início em 15/07,
> **Então** a operação é recusada com 422,
> **e** nada é gravado.

### CA-5 — Valor fora do domínio é recusado

> **Dado** o parâmetro `PAR-VEN-002`, de domínio `0..100`,
> **Quando** for informado o valor `150`,
> **Então** a aplicação recusa com mensagem citando o domínio,
> **e** se a escrita for tentada direto no banco, o `CHECK` a rejeita.

### CA-6 — Alteração registrada na auditoria (`RN-GER-011`)

> **Dado** um gerente alterando a taxa de serviço de 10% para 12%,
> **Quando** a alteração for concluída,
> **Então** existe registro de auditoria com usuário, data/hora, valor anterior, valor novo
> e a justificativa informada.

### CA-7 — Carga inicial completa

> **Dado** um banco recém-criado,
> **Quando** a carga inicial rodar,
> **Então** os nove parâmetros de
> [`04-parametros-do-sistema.md`](../04-parametros-do-sistema.md) existem com os valores
> iniciais documentados,
> **e** um teste falha se o documento listar parâmetro que a carga não cria.

> CA-7 impede o modo de falha mais provável desta spec: parâmetro documentado que ninguém
> semeou, e que só aparece como `NullReferenceException` no meio do serviço.

## 6. Modelo de dados tocado

```sql
CREATE TABLE parametro (
    id          uuid PRIMARY KEY,
    codigo      varchar(20)  NOT NULL,
    descricao   varchar(200) NOT NULL,
    tipo_valor  varchar(20)  NOT NULL,
    dominio     varchar(100),

    CONSTRAINT uq_parametro_codigo UNIQUE (codigo),
    CONSTRAINT ck_parametro_tipo CHECK (
        tipo_valor IN ('percentual','dinheiro','dias','tempo','intervalo','enum','inteiro')
    )
);

CREATE TABLE parametro_valor (
    id               uuid        PRIMARY KEY,
    parametro_id     uuid        NOT NULL REFERENCES parametro(id) ON DELETE RESTRICT,
    unidade_id       uuid        NOT NULL REFERENCES unidade(id)   ON DELETE RESTRICT,
    valor            varchar(50) NOT NULL,
    vigencia_inicio  timestamptz NOT NULL,
    vigencia_fim     timestamptz,
    criado_em        timestamptz NOT NULL,
    criado_por       uuid        NOT NULL,

    CONSTRAINT ck_parametro_valor_vigencia CHECK (vigencia_fim IS NULL OR vigencia_fim > vigencia_inicio),

    -- Impede vigências sobrepostas para o mesmo parâmetro na mesma unidade.
    CONSTRAINT ex_parametro_valor_vigencia EXCLUDE USING gist (
        parametro_id WITH =,
        unidade_id   WITH =,
        tstzrange(vigencia_inicio, vigencia_fim) WITH &&
    )
);

CREATE INDEX ix_parametro_valor_busca
    ON parametro_valor (unidade_id, parametro_id, vigencia_inicio DESC);
```

**Migration necessária:** sim — `0001_Fundacao`. Requer a extensão `btree_gist`, porque a
`EXCLUDE` combina igualdade (`=`) com sobreposição de intervalo (`&&`).

> Este é o **padrão de vigência** do projeto. `preco_produto` (`RN-VEN-010`) e
> `ficha_tecnica` (`RN-PRO-002`) usam exatamente o mesmo desenho — vale extrair a
> `EXCLUDE` para um helper de migration em vez de reescrevê-la três vezes.

## 7. Parâmetros consumidos

Todos — esta spec é o mecanismo.

## 8. Fora de escopo

- Tela de administração de parâmetros — onda 10; no MVP a alteração é por API
- Parâmetro por produto (`PAR-VEN-003` tem sobreposição por produto) — a coluna vive em
  `produto`, e é da spec de cadastro de produto, onda 1
- Notificação de alteração para sessões ativas — o cache é invalidado na escrita, e a
  próxima leitura já vê o valor novo

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Entidades | `src/GrelhaHub.Domain/Gerais/Parametro.cs`, `ParametroValor.cs` |
| Interface | `IParametros` em `src/GrelhaHub.Application/Common/Interfaces/IContextoDeExecucao.cs` ✔ |
| Implementação | `src/GrelhaHub.Infrastructure/Services/ServicoDeParametros.cs` |
| Features | `src/GrelhaHub.Application/Features/Gerais/ObterParametro/`, `AlterarValorDeParametro/` |
| Migration | `.../Migrations/0001_Fundacao` |
| Carga inicial | `src/GrelhaHub.Infrastructure/Persistence/CargaInicial.cs` |
| Testes | `tests/GrelhaHub.Integration.Tests/Gerais/ParametroTests.cs` |

## 10. Definição de pronto

- [ ] `parametro` e `parametro_valor` com a `EXCLUDE` de vigência
- [ ] `btree_gist` habilitada na migration
- [ ] `IParametros` com cache por vigência, ponto único de leitura
- [ ] CA-1 a CA-7 passando como teste de integração
- [ ] CA-7 comparando a carga inicial com o documento de parâmetros
- [ ] Nenhum literal de negócio introduzido no código por esta spec
- [ ] Rastreabilidade do catálogo §16 atualizada
- [ ] `dotnet build -c Release` e `dotnet test` verdes
