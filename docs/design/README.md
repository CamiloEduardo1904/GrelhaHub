# Propostas de design

Documentos visuais navegáveis. Abra direto no navegador — são páginas HTML
autocontidas, sem dependência externa nem build.

| Arquivo | O que é | Situação |
|---------|---------|----------|
| [`proposta-chapa.html`](proposta-chapa.html) | Direção visual **Chapa** — telas reais, tokens, tipografia e as 8 regras `CHAPA-*` | **Aprovada** — ADR-0012 |
| [`GrelhaHub-Direcao-Chapa.pdf`](GrelhaHub-Direcao-Chapa.pdf) | O mesmo conteúdo em PDF A4, 9 páginas, para apresentar à equipe | Aprovada |

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

## Direção aprovada

Virou [ADR-0012](../adr/0012-direcao-visual-chapa.md) (decisão e alternativas descartadas) e
[`docs/08-design-system.md`](../08-design-system.md) (tokens e regras de uso). A proposta fica aqui como
registro do que foi considerado — o mesmo tratamento que qualquer outra decisão
recebe neste projeto.

## Regenerar o PDF

O PDF sai do próprio HTML — há um bloco `@media print` que fixa a paleta clara,
define A4, evita quebra dentro de comanda e regra, e troca a demo interativa por
uma régua estática do ciclo.

```bash
chrome --headless=new --no-pdf-header-footer \
  --print-to-pdf="docs/design/GrelhaHub-Direcao-Chapa.pdf" \
  "file:///caminho/absoluto/docs/design/proposta-chapa.html"
```

A paleta de impressão é fixada no tema claro de propósito: o PDF não pode sair
diferente dependendo do tema do sistema de quem gerou. Os componentes que **são**
escuros por desenho — barra do app, capa do cardápio, bloco de código — continuam
escuros, porque é o caráter da direção.
