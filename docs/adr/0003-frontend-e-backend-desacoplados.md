# ADR-0003 — Frontend e backend desacoplados (REST + JWT)

**Status:** Aceito · **Data:** Agosto/2026

## Contexto

A estratégia de produto prevê um app mobile em fase 2 (React Native). Se a lógica de
negócio ou o fluxo de telas residir no servidor de apresentação, o app precisaria de uma
segunda implementação das mesmas regras — a receita conhecida para as duas versões
divergirem.

Além disso, o KDS da cozinha, o painel do entregador e a tela do balcão são clientes
diferentes do mesmo domínio. Todos precisam falar com a mesma fonte de verdade.

## Decisão

Frontend e backend são aplicações independentes:

- Backend expõe uma **API REST** que não conhece HTML nem sessão de navegador
- Autenticação por **JWT** enviado no header `Authorization: Bearer`
- Estado de autenticação é *stateless* no servidor — nenhuma sessão em memória
- **Nenhuma regra de negócio no frontend.** O front valida por usabilidade (feedback
  imediato); o backend valida por autoridade. Toda regra do §7 do levantamento é
  reimposta no servidor.
- Front e back são servidos pelo **mesmo domínio** via Nginx (`/` e `/api/*`),
  eliminando CORS em produção

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| Monolito server-rendered (Razor/MVC) | Impede o reaproveitamento pelo app mobile — o objetivo principal da decisão |
| BFF (Backend for Frontend) por cliente | Camada extra sem ganho no porte atual; a API única atende web e mobile com os mesmos contratos |
| GraphQL | Flexibilidade de consulta que o projeto não precisa, ao custo de complexidade de cache, autorização por campo e N+1 |
| Cookie de sessão em vez de JWT | Funciona na web, mas complica o cliente mobile e exige estado no servidor |

## Consequências

**Positivas**
- O app mobile futuro consome a mesma API, sem duplicar regra (RNF-018)
- Front e back evoluem e são deployados em ritmos independentes
- API testável isoladamente, sem navegador
- Mesmo domínio em produção = sem preflight CORS no caminho crítico

**Negativas / custos aceitos**
- Duplicação deliberada de validação (front por UX, back por segurança)
- Revogação de JWT antes da expiração exige mecanismo próprio (lista de revogação ou
  expiração curta com refresh) — decisão a detalhar na implementação
- Contratos de API precisam ser versionados e documentados (Swagger/OpenAPI) para não
  quebrar clientes
