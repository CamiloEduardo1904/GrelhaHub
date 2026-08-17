# SPEC-<MOD>-<nnn> — <Título do caso de uso>

| Campo | Valor |
|-------|-------|
| ID | `SPEC-<MOD>-<nnn>-<slug>` |
| Módulo | <GER \| VEN \| PRO \| EST \| COM \| FIN \| CRM \| LOG \| RH \| FIS> |
| Situação | `RASCUNHO` |
| Onda | <0–10, conforme [guia de specs §5](../06-guia-de-specs.md#5-ordem-de-escrita-das-specs)> |
| Regras implementadas | `RN-XXX-000`, `RN-XXX-000` |
| ADRs aplicáveis | [ADR-0002](../adr/0002-clean-architecture-com-cqrs.md), [ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md) |
| Depende de | `SPEC-XXX-000` |
| Autor / data | |

---

## 1. Objetivo

<Uma frase. Qual capacidade de negócio esta spec entrega — não como ela é implementada.>

## 2. Regras implementadas

Toda regra citada no cabeçalho aparece aqui, com a camada em que é imposta. A escolha segue
[ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md).

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-XXX-000` | | Modelo \| Banco \| Domínio \| Aplicação | `CK` / `TRG` / validator / … |

> Se a camada escolhida divergir da coluna **Impl.** prevista no catálogo, justifique aqui —
> e atualize o catálogo.

## 3. Contrato

### Entrada

```
<Command | Query>: <Nome>
```

| Campo | Tipo | Obrigatório | Validação | Regra |
|-------|------|:-----------:|-----------|-------|
| | | | | |

### Saída

| Campo | Tipo | Observação |
|-------|------|------------|
| | | |

### Erros

| Situação | Código | Mensagem ao usuário | Regra violada |
|----------|--------|---------------------|---------------|
| | | | |

### Autorização

| Perfil | Permitido |
|--------|:---------:|
| Administrador | |
| Gerente | |
| Atendente | |
| Cozinha | |

## 4. Comportamento

### Fluxo principal

1.
2.

### Fluxos alternativos e de exceção

| # | Condição | Comportamento |
|---|----------|---------------|
| A1 | | |

### Efeitos colaterais

<Movimentos de estoque gerados, lançamentos financeiros, ordens de produção, eventos de
auditoria. Declare também a transação: o que precisa ser atômico com o quê (RNF-016).>

## 5. Critérios de aceite

Um por regra implementada, no mínimo. Cada um vira teste de integração.

### CA-1 — <título> (`RN-XXX-000`)

> **Dado** …
> **Quando** …
> **Então** …
> **E** …

## 6. Modelo de dados tocado

| Tabela | Operação | Constraints envolvidas |
|--------|:--------:|------------------------|
| | leitura / escrita | |

**Migration necessária:** <sim/não — se sim, descreva o que muda>

## 7. Parâmetros consumidos

| Parâmetro | Uso |
|-----------|-----|
| `PAR-XXX-000` | |

> Nenhum literal de negócio no código. Se um número tem significado comercial, ele é
> parâmetro — ver [`04-parametros`](../04-parametros-do-sistema.md).

## 8. Fora de escopo

<O que esta spec explicitamente NÃO faz, e qual spec faz.>

## 9. Rastreabilidade

Preenchida na entrega.

| Artefato | Caminho |
|----------|---------|
| Command / Query | `src/GrelhaHub.Application/Features/<Modulo>/<Caso>/…` |
| Handler | |
| Validator | |
| Migration | |
| Teste unitário | |
| Teste de integração | |

## 10. Definição de pronto

- [ ] Todas as regras do cabeçalho implementadas na camada declarada
- [ ] Um teste por regra, com o ID no `DisplayName`
- [ ] Critérios de aceite passando como teste de integração
- [ ] Migration reflete as constraints da §6
- [ ] Nenhum literal de negócio no código
- [ ] Rastreabilidade do [catálogo §16](../03-catalogo-de-regras-de-negocio.md#16-rastreabilidade-regra--spec) atualizada
- [ ] Regra nova descoberta foi para o catálogo, não só para esta spec
- [ ] `dotnet build` e `dotnet test` verdes
