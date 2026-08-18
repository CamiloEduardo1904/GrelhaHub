# ADR-0014 — Modularidade comercial: módulo habilitado por unidade

| Campo | Valor |
|-------|-------|
| Status | **Aceito** — 17/08/2026 |
| Regras | `RN-GER-017` ⚠, `RN-GER-018`, `RN-GER-019`, `RN-GER-020`, `RN-GER-021` |
| Relacionado | [ADR-0007](0007-core-generico-com-nicho-por-configuracao.md) — nicho por configuração |
| Spec | [SPEC-GER-006](../specs/SPEC-GER-006-modulos-por-unidade.md) |

---

## Contexto

Os dez módulos do GrelhaHub são **vendidos separadamente**. Uma hamburgueria de bairro compra
vendas e estoque; não compra logística de frota nem RH. O produto precisa ligar e desligar
módulo por restaurante, e o que não foi contratado não pode aparecer nem responder.

Isso não é preferência de interface. Esconder o menu e deixar a rota respondendo produz um
sistema em que o cliente usa o que não pagou — e em que o vendedor não consegue demonstrar o
que está vendendo.

## Decisão

**Cada operação declara a qual módulo pertence, por atributo, e um behavior do MediatR recusa a
operação se o módulo não estiver habilitado na unidade.**

```csharp
[ExigeModulo(Modulo.Vendas)]
public sealed record CriarPedidoPublicoCommand(...) : IRequest<PedidoCriadoResponse>;

[SemModulo("autenticação antecede qualquer módulo")]
public sealed record AutenticarCommand(...) : IRequest<AutenticarResponse>;
```

E — a parte que faz a decisão valer — **a aplicação recusa subir se alguma operação não
declarar nada**:

```
InvalidOperationException: RN-GER-017 — operação sem declaração de módulo:
CriarPedidoPublicoCommand. Marque com [ExigeModulo] ou justifique com [SemModulo].
```

Habilitação por unidade usa o **padrão de vigência** de `parametro_valor`: `[inicio, fim)` com
`EXCLUDE` contra sobreposição (`RN-GER-021`). É o que permite responder "o que esta unidade
tinha contratado em março" — pergunta de cobrança, não de operação.

## Alternativas consideradas

### Derivar o módulo do namespace

`Features/Vendas/…` → `VEN`, por um mapa. Zero anotação, nada a esquecer. Recusada porque o
vínculo fica implícito: mover uma pasta mudaria o módulo de uma operação sem ninguém notar, e
o mapa de namespaces viraria um segundo lugar onde a verdade mora.

### Gate por rota, no controller

`[ExigeModulo]` no controller. Simples e visível junto do mapa de rotas. Recusada porque só
protege o que entra por HTTP: comando disparado por outro handler, por job ou por consumidor de
fila escaparia do gate — e a onda 4 tem exatamente isso, quando a confirmação de pedido dispara
baixa de estoque e ordem de produção.

### Sem gate, apenas esconder na interface

Recusada de imediato: a rota continuaria respondendo. É o modo de falha que `RN-GER-012` já
rejeita para custo, aplicado a módulo inteiro.

## Consequências

**Positivas**

- O contrato comercial é executável: o que não foi vendido não responde
- **Falha fechada por construção.** Operação nova sem declaração não passa despercebida — a
  aplicação não sobe. É o mesmo desenho de `GarantirAusenciaDeDecimalCru`
- O gate vale para qualquer origem: HTTP, job, handler interno
- Vigência dá histórico de contratação sem tabela nova de cobrança

**Negativas / riscos assumidos**

- Toda operação passa a exigir uma linha de declaração. Custo real, pago uma vez por feature
- `[SemModulo]` é uma porta de saída: exige justificativa em texto, mas nada impede alguém de
  escrever qualquer coisa nela. É revisão de código, não mecanismo
- O gate é por módulo, não por funcionalidade. Vender "vendas sem delivery" exigiria granularidade
  menor, e isso **não** está decidido — se vier, é ADR novo, não extensão silenciosa deste
- Uma consulta de módulo desabilitado responde erro, não vazio. É deliberado: vazio se confunde
  com "não há dados", e o cliente ligaria para o suporte perguntando onde estão os pedidos
