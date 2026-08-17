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
├── app/          casca, rotas, estilos globais, providers
├── components/   componentes reutilizáveis de UI
├── features/     uma pasta por módulo do domínio (vendas, estoque, ...)
└── services/     camada de acesso à API (axios)
```

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
