# GrelhaHub — Frontend

React 19 + TypeScript + Vite. Desacoplado do backend (ADR-0003): consome a API por REST
com JWT.

## Rodar localmente

```bash
npm install
npm run dev     # http://localhost:5173
```

O Vite faz proxy de `/api` para `http://localhost:5000`, reproduzindo o arranjo de
produção — em que Nginx serve `/` e faz proxy de `/api/*`, sem CORS.
Use sempre caminhos relativos (`/api/pedidos`), nunca URL absoluta.

## Scripts

| Comando | O que faz |
|---------|-----------|
| `npm run dev` | Servidor de desenvolvimento |
| `npm run build` | Type-check e build de produção em `dist/` |
| `npm run lint` | ESLint |
| `npm run typecheck` | Apenas o type-check |

## Estrutura

```
src/
├── app/          casca, rotas, estilos globais (tokens da direção Chapa)
├── components/   Grelha, Selo, Comanda — os três componentes do design system
├── features/     admin (painel do restaurante) e publico (cardápio e pedido)
└── services/     acesso à API (axios), tipos e helpers do ciclo do pedido
```

## Direção visual — Chapa

Base oficial desde 17/08/2026: [ADR-0012](../docs/adr/0012-direcao-visual-chapa.md) e
[docs/08-design-system.md](../docs/08-design-system.md).

**A regra central** vem do kit de marca — *"a barra da ligadura sempre em cor de
destaque diferente das demais"*. Na interface: **em qualquer grupo, exatamente uma
barra é âmbar, a ativa**. Estado do pedido, aba, etapa, seleção. Use `<Grelha>`;
não invente outro indicador de progresso.

**Cartoon por fora, sem graça por dentro.** Personalidade na moldura — bordas,
cabeçalhos, estados vazios, cardápio. Dentro do dado, nada: a tabela de produtos é
tabulação, contraste e nenhuma surpresa.

Antes de abrir PR, passe o checklist `CHAPA-01` a `CHAPA-08` do design system.
O que **nunca** entra: gradiente decorativo, glassmorphism, sombra borrada,
`border-radius` uniforme, Inter/Geist, emoji como ícone ou estado, chip pastel de
status, webfont de CDN.

## Regras que valem aqui

- **Nenhuma regra de negócio no frontend** (ADR-0003). O front valida por usabilidade;
  a autoridade é sempre do servidor. Toda regra do catálogo é reimposta na API
- **Nenhum literal de negócio** — percentuais e limites vêm da API, que os lê dos
  parâmetros (`docs/04-parametros-do-sistema.md`)
- **RNF-010** — alvo de toque de no mínimo 44px: o atendente opera em tablet, não com mouse
- **RNF-012** — responsivo, funcional em tablet (1024×768) e desktop
- **RN-GER-012** — custo, CMV e margem simplesmente não chegam ao cliente sem permissão;
  não é questão de ocultar na tela

A marca está em `assets/brand/` — paleta, logos e regras de uso no README de lá.
