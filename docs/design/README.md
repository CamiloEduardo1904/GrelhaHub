# Propostas de design

Documentos visuais navegáveis. Abra direto no navegador — são páginas HTML
autocontidas, sem dependência externa nem build.

| Arquivo | O que é | Situação |
|---------|---------|----------|
| [`proposta-chapa.html`](proposta-chapa.html) | Direção visual **Chapa** — impresso e carimbado. Telas reais (comanda, painel, cardápio público, tabela densa), tokens, tipografia e as 8 regras `CHAPA-*` para revisão de código | Em avaliação |

## Como abrir

```bash
# Windows
start docs/design/proposta-chapa.html

# macOS
open docs/design/proposta-chapa.html
```

A página respeita o tema do sistema: **claro** vira papel (sensação de
administrativo), **escuro** vira carvão (sensação de cozinha). Vale olhar nos dois —
são os dois ambientes reais do produto.

## Por que HTML e não imagem

Direção de design se julga interagindo. A comanda da página avança pelos seis
estados de `RN-VEN-018` no clique, e é aí que a régua de listras — o mecanismo
central da direção — se explica sozinha.

## Quando a direção for aprovada

Vira `ADR-0012` (decisão e alternativas descartadas) e
`docs/08-design-system.md` (tokens e regras de uso). A proposta fica aqui como
registro do que foi considerado — o mesmo tratamento que qualquer outra decisão
recebe neste projeto.
