# SPEC-GER-002 — Dia operacional

| Campo | Valor |
|-------|-------|
| ID | `SPEC-GER-002-dia-operacional` |
| Módulo | GER |
| Situação | `PRONTA` |
| Onda | 0 — fundação |
| Regras implementadas | `RN-GER-006` ⚠⚠ |
| ADRs aplicáveis | [ADR-0011](../adr/0011-dia-operacional.md), [ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md) |
| Depende de | `SPEC-GER-001` |
| Data | 17/08/2026 |

---

> ## ⚠⚠ A regra mais subestimada do catálogo
>
> Se o modelo agrupar por `date_trunc('day', dh_criacao)`, o faturamento de sábado à noite
> cai em domingo, o fechamento de caixa não bate e o relatório de melhor dia da semana fica
> errado **em definitivo**.
>
> O modo de falha é traiçoeiro: **não dá erro**. Dá um número plausível. Ninguém percebe até
> alguém conferir manualmente — e a essa altura as análises da Sprint 7 & 8 já foram
> construídas sobre a agregação errada.
>
> `RN-GER-006` está em estado `HIP`: **a pergunta 1 da entrevista é justamente esta.** Esta
> spec assume 04h00 e é implementada agora porque a coluna entra em toda tabela; se o
> operador responder outro horário, muda-se a constante da migration antes da carga inicial.

## 1. Objetivo

Tornar o **dia operacional** — das 04h00 às 03h59 do dia seguinte — uma propriedade de
primeira classe do modelo, de modo que agrupar errado deixe de ser possível por descuido.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-GER-006` ⚠⚠ | O dia operacional vai das 04h00 às 03h59 do dia seguinte | Modelo + Domínio | `GENERATED ALWAYS AS` + índice; `DiaOperacional.De()` em C# para exibição |

**Divergência declarada em relação ao catálogo:** a coluna `Impl.` prevê `GEN VW PAR`.
Esta spec implementa `GEN` (coluna gerada) e `VW` (`dim_tempo`), mas **não** `PAR` no
sentido de valor lido do banco em tempo de execução — ver §4. O catálogo será atualizado.

## 3. Contrato

Não há comando nem consulta próprios: o dia operacional é atributo de outras entidades.
A camada de aplicação expõe o cálculo por `IRelogio.DiaOperacional(instante)`.

## 4. Comportamento

### A coluna

Toda tabela com data de evento comercial recebe:

```sql
data_operacional date
    GENERATED ALWAYS AS (((dh_evento AT TIME ZONE 'America/Sao_Paulo') - interval '4 hours')::date)
    STORED
```

E um índice, porque toda agregação de negócio passa por ela:

```sql
CREATE INDEX ix_<tabela>_unidade_data_op ON <tabela> (unidade_id, data_operacional);
```

**Tabelas alcançadas** (conforme cada spec as criar): `pedido`, `pagamento`,
`movimento_estoque`, `movimento_caixa`, `caixa_diario`, `lancamento_financeiro`.

### Por que a hora de virada não é lida do banco em tempo de execução

`PAR-GER-001` existe e documenta o valor vigente — mas `GENERATED ALWAYS AS` **exige
expressão imutável**, e uma mudança retroativa reclassificaria vendas passadas, o que é
justamente o que a regra existe para impedir.

Portanto:

| Uso | Fonte do valor |
|-----|----------------|
| Coluna gerada no banco | Constante da migration |
| Cálculo na aplicação (exibição, filtros) | `DiaOperacional.HoraDeViradaPadrao` |
| Documentação do valor vigente | `PAR-GER-001` |

**Alterar a hora de virada é operação de migration** — nova coluna gerada e recomputação
explícita do histórico, decidida caso a caso — e não edição de parâmetro em tela. Isso está
registrado em [ADR-0011](../adr/0011-dia-operacional.md) e precisa constar do manual de
operação.

### `dim_tempo`

View materializada com uma linha por dia operacional, para o BI da Sprint 7 & 8:

| Coluna | Conteúdo |
|--------|----------|
| `data_operacional` | PK |
| `dia_da_semana` | 1–7 |
| `nome_dia_semana` | segunda, terça… |
| `eh_fim_de_semana` | boolean |
| `semana_do_ano`, `mes`, `trimestre`, `ano` | |
| `inicio_utc`, `fim_utc` | limites exatos do dia operacional, para filtro por intervalo |

`inicio_utc` e `fim_utc` existem para que consultas por período possam usar índice em
`dh_evento` sem recalcular a virada.

### Regra de uso — verificada em revisão de código

> **Nenhuma consulta de negócio agrupa ou filtra por `dh_criacao` ou `dh_evento` para fins
> de apuração diária.** A coluna certa é `data_operacional`.

`dh_evento` continua válido para ordenação cronológica, auditoria e cálculo de duração —
casos em que o instante importa e o dia não.

## 5. Critérios de aceite

### CA-1 — Venda de madrugada pertence ao dia anterior (`RN-GER-006`)

> **Dado** um pedido registrado às 01h30 de domingo, 16/08/2026 (horário de Brasília),
> **Quando** a coluna `data_operacional` for lida,
> **Então** o valor é `2026-08-15` — sábado,
> **e** o pedido integra o fechamento do caixa aberto no sábado,
> **e** o relatório de melhor dia da semana o contabiliza em sábado.

### CA-2 — A virada às 04h00 é o limite exato (`RN-GER-006`)

> **Dado** dois pedidos no mesmo domingo, um às 03h59 e outro às 04h00,
> **Quando** as datas operacionais forem lidas,
> **Então** o das 03h59 é `2026-08-15` e o das 04h00 é `2026-08-16`.

### CA-3 — A coluna é gerada, não atribuída (`RN-GER-006`)

> **Dado** um registro sendo inserido,
> **Quando** a aplicação tentar atribuir valor a `data_operacional`,
> **Então** o banco rejeita a escrita em coluna gerada,
> **e** o mapeamento EF Core marca a propriedade como `ValueGeneratedOnAddOrUpdate` com
> `Metadata.SetAfterSaveBehavior(Save)` — a aplicação lê, nunca escreve.

### CA-4 — Aplicação e banco concordam (`RN-GER-006`)

> **Dado** uma série de instantes cobrindo as 24 horas em intervalos de 15 minutos,
> **Quando** `DiaOperacional.De()` em C# e a coluna gerada no PostgreSQL forem comparados,
> **Então** os dois resultados coincidem para **todos** os instantes.
>
> Este é o teste que impede a divergência silenciosa entre as duas implementações da mesma
> regra — o custo assumido no [ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md).

### CA-5 — Horário de verão não quebra o cálculo (`RN-GER-006`, `RN-GER-005`)

> **Dado** um instante em data histórica com horário de verão vigente no Brasil,
> **Quando** o dia operacional for calculado,
> **Então** o resultado usa o deslocamento correto daquela data, e não um `-03:00` fixo.
>
> O Brasil não tem horário de verão desde 2019, mas dados históricos e a possibilidade de
> retomada tornam a conversão fixa uma bomba-relógio. `AT TIME ZONE` no PostgreSQL e
> `TimeZoneInfo` no .NET já tratam isso — o teste garante que ninguém os substitua por
> aritmética de fuso.

## 6. Modelo de dados tocado

| Objeto | Operação | Observação |
|--------|:--------:|------------|
| `dim_tempo` | criação | View materializada, `REFRESH` diário e após carga |
| Demais tabelas | alteração | Coluna gerada + índice, acrescentados por cada spec que criar a tabela |

**Migration necessária:** sim — `0001_Fundacao` cria `dim_tempo` e a função auxiliar; cada
spec seguinte acrescenta a coluna à sua tabela.

## 7. Parâmetros consumidos

| Parâmetro | Uso |
|-----------|-----|
| `PAR-GER-001` | Documenta a hora de virada vigente (04:00). **Não é lido em tempo de execução** — ver §4 |

## 8. Fora de escopo

- Relatórios que consomem `dim_tempo` — onda 9
- Fechamento de caixa por dia operacional — `SPEC-FIN-002`, onda 6
- Hora de virada configurável por unidade — não previsto; a operação é de uma unidade só

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Cálculo (domínio) | `src/GrelhaHub.Domain/Gerais/DiaOperacional.cs` ✔ |
| Marcador | `IPossuiDiaOperacional` em `src/GrelhaHub.Domain/Common/Entidade.cs` ✔ |
| Serviço | `src/GrelhaHub.Infrastructure/Services/Relogio.cs` ✔ |
| `dim_tempo` | `src/GrelhaHub.Infrastructure/Persistence/Migrations/0001_Fundacao` |
| Teste unitário | `tests/GrelhaHub.Domain.Tests/Gerais/DiaOperacionalTests.cs` ✔ (5 casos) |
| Teste de paridade | `tests/GrelhaHub.Integration.Tests/Gerais/DiaOperacionalParidadeTests.cs` (CA-4) |

## 10. Definição de pronto

- [x] Cálculo no domínio, testável sem banco
- [x] Testes unitários cobrindo madrugada, virada e limites
- [ ] Coluna gerada e índice na migration de fundação
- [ ] `dim_tempo` criada com `inicio_utc` e `fim_utc`
- [ ] **CA-4 — teste de paridade C# × PostgreSQL passando nas 96 amostras do dia**
- [ ] CA-5 — teste com data de horário de verão
- [ ] Rastreabilidade do catálogo §16 atualizada
- [ ] `dotnet build -c Release` e `dotnet test` verdes
