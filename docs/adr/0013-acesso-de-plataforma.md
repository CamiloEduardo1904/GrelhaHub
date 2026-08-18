# ADR-0013 — Acesso de plataforma: o suporte enxerga todas as unidades

| Campo | Valor |
|-------|-------|
| Status | **Aceito** — 17/08/2026 |
| Regras | `RN-GER-014` ⚠⚠, `RN-GER-015` ⚠, `RN-GER-016` |
| Revisa | [ADR-0005](0005-multi-estabelecimento-por-coluna-discriminadora.md) — a invariante de isolamento passa a ter uma exceção |
| Spec | [SPEC-GER-004](../specs/SPEC-GER-004-usuarios-perfis-e-permissoes.md) |

---

## Contexto

O GrelhaHub é **vendido** a restaurantes. Quem o vende precisa dar suporte sem pedir a senha do
cliente, e precisa saber o que está acontecendo na base instalada.

Até aqui, o [ADR-0005](0005-multi-estabelecimento-por-coluna-discriminadora.md) sustentava uma
invariante simples e forte: **nenhuma consulta vê duas unidades.** O filtro global do EF Core
compara `unidade_id` com a unidade do token, e quem não tem unidade no token não vê nada —
falha fechada.

Um usuário de suporte não tem unidade. Pela regra atual, ele não veria nada.

## Decisão

**Para o usuário de plataforma, e somente para ele, o filtro global de unidade é desligado.**

```csharp
// GrelhaHubDbContext — filtro global
e.UnidadeId == contexto.UnidadeId || contexto.EhPlataforma
```

Consequência direta: o suporte enxerga todas as unidades em uma única consulta, e relatório
agregado entre restaurantes passa a ser possível.

Isso vem com duas contrapartidas, que são a razão de este ADR existir:

1. **`RN-GER-015` — leitura registrada, escrita com motivo.** Toda operação de plataforma sobre
   dado de unidade é registrada. Escrita exige, além do registro, uma **sessão de suporte
   aberta com motivo**. Ler para diagnosticar é rotina; alterar o dado de um cliente sem dizer
   por quê, não.
2. **`RN-GER-016` — o usuário de plataforma é invisível ao restaurante.** O administrador do
   restaurante não o cria, não o altera e não o vê em listagem alguma. Um usuário que enxerga
   todos os clientes não pode ser gerenciável por um cliente.

## Alternativas consideradas

### Sessão de suporte com token escopado em uma unidade

O suporte abriria uma sessão em **uma** unidade e receberia um token escopado nela, curto. O
filtro global nunca seria tocado: o suporte ganharia o poder de *definir* o escopo, não de
ignorá-lo. A invariante "nenhuma consulta vê duas unidades" continuaria valendo literalmente,
e o motivo do acesso ficaria registrado uma vez, na abertura.

**Recusada pelo time** porque impede o que motivou o pedido: relatório agregado sobre a base
instalada. Um token por unidade responde "o que houve neste restaurante", nunca "quantos
restaurantes estão com pedido travado".

### Header `X-Unidade` por requisição

Mais simples, sem endpoint novo. Recusada por não ter onde registrar o motivo do acesso, e por
transformar a auditoria em uma linha por requisição em vez de uma por sessão.

## Consequências

**Positivas**

- Suporte e diagnóstico sem pedir credencial do cliente
- Relatório sobre a base instalada — pergunta comercial que o produto precisa responder
- Nenhuma tabela nova para o recorte: a mudança é uma condição no filtro

**Negativas / riscos assumidos**

- ⚠⚠ **A invariante de isolamento deixa de ser absoluta.** Antes, um esquecimento de filtro era
  impossível de causar vazamento; agora, um defeito que marque `EhPlataforma` indevidamente
  vaza dado de cliente para cliente. **É o risco central deste ADR.** Mitigação:
  `EhPlataforma` deriva de uma única claim, tem teste de integração provando que usuário comum
  não a obtém, e o padrão de teste do projeto passa a incluir "usuário comum continua isolado"
  em toda spec que toque consulta
- A trilha de auditoria (`SPEC-GER-005`) deixa de ser desejável e passa a ser **necessária**:
  sem ela, `RN-GER-015` não é cumprível
- Relatório agregado precisa decidir explicitamente o recorte. Uma consulta de plataforma sem
  `WHERE` de unidade agora **retorna tudo** em vez de nada — o inverso do comportamento seguro
  anterior

## Pendência vinculada

`RN-GER-015` só fica completa com `SPEC-GER-005`. Até lá, a sessão de suporte é registrada em
`sessao_suporte` e a escrita é bloqueada sem ela; o registro por operação entra com a trilha.
