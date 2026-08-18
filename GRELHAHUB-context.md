# GrelhaHub — Contexto do Projeto

> Briefing histórico do projeto: como as decisões de produto, marca e infraestrutura foram
> tomadas. Última atualização: agosto/2026.

> ## ⚠ Este documento foi superado como ponto de entrada
>
> Depois da consolidação com o Catálogo de Regras, a documentação viva do projeto passou a
> ser:
>
> | Para… | Vá para |
> |-------|---------|
> | **Visão geral e como rodar** | [`README.md`](README.md) |
> | **Documentação completa** | [`docs/README.md`](docs/README.md) |
> | **Regras de negócio** *(fonte de verdade)* | [`docs/03-catalogo-de-regras-de-negocio.md`](docs/03-catalogo-de-regras-de-negocio.md) |
> | **Decisões de arquitetura** | [`docs/adr/`](docs/adr/) |
> | **Como implementar** | [`docs/06-guia-de-specs.md`](docs/06-guia-de-specs.md) |
>
> O que permanece útil aqui: a **identidade visual** (§3) e o **raciocínio original de
> infraestrutura** (§4), que foi formalizado em
> [ADR-0006](docs/adr/0006-deploy-em-vps-unica-com-docker.md).

---

## 1. Visão do Produto

**GrelhaHub** — Sistema de gestão (ERP) para pequenos e médios restaurantes e deliveries.

- **Tagline:** DO PEDIDO À BRASA
- **Público-alvo:** pequenos e médios restaurantes / deliveries
- **Sub-nicho principal:** hamburgueria
- **Estratégia de produto:** modelar um **core genérico** de restaurante primeiro
  (produtos, pedidos, mesas, delivery, financeiro) e deixar o "sabor hamburgueria"
  como camada de configuração (modificadores, combos, ficha técnica). Nichar depois.

### Proposta de valor
Sistema completo do pedido à cozinha, do estoque ao financeiro, com especialização
progressiva em hamburgueria.

### Dores do nicho mapeadas (base para regras de negócio)
- Pedidos por múltiplos canais (balcão, WhatsApp, iFood, app próprio) → central unificada
- Insumos perecíveis (pão, carne, queijo) → estoque com ficha técnica e baixa automática
- Personalização do lanche ("sem cebola, dobro de bacon") → modificadores/adicionais
- Fila da cozinha no pico → KDS (Kitchen Display System)
- Taxa de entrega por zona → gestão de delivery com raios/bairros
- Margem apertada → CMV por produto (custo da ficha técnica vs preço de venda)
- Fidelização → programa de pontos/cupons

---

## 2. Stack Tecnológica (decidida)

| Camada        | Tecnologia                                             |
|---------------|--------------------------------------------------------|
| Frontend      | React (mais atual) — base preparada para app mobile futuro |
| Backend       | C# / .NET (mais atual) + ASP.NET Core Web API          |
| Banco         | PostgreSQL                                             |
| Arquitetura   | Frontend e backend **desacoplados** (REST + JWT)       |
| Padrões       | CQRS, Clean Architecture                               |

### Justificativa arquitetural (para a banca)
- **CQRS:** separa leitura (relatórios, dashboards, KDS — queries otimizadas) de
  escrita (pedidos, estoque, caixa — commands com validação e regras). Casa com o
  domínio: muitas leituras no salão/cozinha, escritas críticas no caixa.
- **Desacoplamento:** a API é *mobile-first by design* para suportar o app futuro
  (React Native reaproveitaria a mesma API e parte dos componentes).
- **Recomendação de libs:** MediatR (CQRS), FluentValidation, EF Core.

---

## 3. Identidade Visual

### Nome e conceito
- **GrelhaHub** — "Grelha" ancora no nicho gastronômico, "Hub" comunica plataforma/centralização.
- Símbolo: **monograma GH** construído inteiramente com listras de grelha.
- **A ligadura (conceito central):** o G e o H compartilham a mesma barra central
  (em âmbar) — ela é ao mesmo tempo a barra do meio do G, o travessão do H e uma
  listra da grelha. Leitura: *"a barra da grelha é o que conecta Grelha ao Hub."*

### Paleta de cores
| Cor            | Hex       | Uso                                        |
|----------------|-----------|--------------------------------------------|
| Laranja brasa  | #FF6B2C   | Cor primária, barras, "Hub" no wordmark    |
| Âmbar chama    | #FFB020   | Barra da ligadura (fundo escuro)           |
| Creme          | #FFE3D3   | Barra da ligadura (fundo laranja)          |
| Carvão         | #14100C   | Fundos dark                                |
| Texto escuro   | #1D1812   | Texto/barras em fundo claro                |
| Cinza quente   | #9C8E7E   | Tagline, texto secundário                  |
| Borda clara    | #E5E0D8   | Bordas em fundo branco                     |

### Tipografia
- Wordmark: sans-serif bold (700), letter-spacing -1px
- "Grelha" em neutro (branco/carvão) + "Hub" sempre em laranja #FF6B2C
- Tagline: caps, letter-spacing 5px, cinza quente

### Kit de marca
Entregue em `grelhahub-kit-v2.zip` (53 arquivos): logo-principal, logo-horizontal,
icone-app (1024→128), favicon (.ico + apple-touch), wordmark — SVG + PNG nas variações
dark/light/transparente. README de marca incluso.

**Regra fixa:** a barra da ligadura sempre em cor de destaque diferente das demais barras.

---

## 4. Infraestrutura e Deploy

### Ambiente
- **VPS Hostinger** com Docker já instalado (plano só inclui a VPS)
- **Repositório:** Azure DevOps
- Front, back e banco rodam **na mesma VPS** (suficiente para o projeto)

### Arquitetura de deploy escolhida
```
                    VPS (Docker)
Internet ──► Nginx (80/443)
              ├── /          → arquivos estáticos do React (build)
              └── /api/*     → proxy_pass → container do backend (.NET)
            + container PostgreSQL (rede interna, NUNCA exposto)
```
Front e back no mesmo domínio = sem CORS.

### CI/CD (Azure DevOps → VPS)
Fluxo: `git push main` → Azure builda imagem Docker → push pro registry (Docker Hub)
→ SSH na VPS → `docker compose pull && up -d`.

Componentes necessários:
- Registry: Docker Hub (grátis) ou Azure Container Registry
- Service Connections no Azure: Docker Registry + SSH
- Variable Group com segredos: DOCKERHUB_USER/TOKEN, VPS_HOST/USER/SSH_KEY
- `azure-pipelines.yml` na raiz do repo
- `docker-compose.yml` na VPS puxando a imagem (não builda na VPS)
- Front: buildado no pipeline e enviado via SCP, OU imagem Docker própria

### Domínio e HTTPS (pendente)
- Domínio ainda **não comprado**. Opções: Registro.br (.com.br ~R$40/ano),
  Hostinger, Cloudflare. Alternativa grátis para testes: DuckDNS.
- HTTPS: Let's Encrypt via Certbot (grátis). Redirect HTTP→HTTPS no Nginx.

### Cuidados de VPS
- RAM: .NET + PostgreSQL + Nginx rodam ok em 2GB; se 1GB, criar swap de 2GB.
- Firewall (ufw): liberar só 22, 80, 443.
- PostgreSQL nunca exposto publicamente (sem `ports:` público; acesso via túnel SSH).
- Backup do volume `pgdata` antes de apresentações.
- Usuário `deploy` dedicado (não usar root) no grupo docker.
- Migrations EF Core: decidir entre rodar no startup (`Database.Migrate()`) ou step no pipeline.
- Segredos (senha do banco, chave SSH) nunca no repo — `.env` só na VPS, Variable Groups no Azure.

---

## 5. Estrutura de Pastas Sugerida

```
A estrutura prevista aqui foi **implementada**. A versão vigente está em
[`README.md`](README.md#estrutura); o esqueleto abaixo fica como registro do que foi
planejado nesta fase.

```
GrelhaHub/
├── README.md                    # ponto de entrada do projeto
├── CLAUDE.md                    # instruções para agentes
├── GRELHAHUB-context.md         # este documento (briefing histórico)
├── docs/                        # documentação viva — 8 documentos + ADRs + specs
├── docker-compose.yml           # (na VPS) orquestra api + db + web + nginx
├── nginx.conf                   # Nginx da borda (front + proxy /api + TLS)
├── azure-pipelines.yml          # CI/CD
├── .env.example                 # template de variáveis (sem segredos reais)
├── backend/
│   ├── Dockerfile
│   ├── Directory.Packages.props # versões centralizadas
│   ├── src/                     # solução .NET (Clean Architecture + CQRS)
│   └── tests/                   # unit + integração (PostgreSQL real)
├── frontend/                    # React 19 + TypeScript + Vite
└── assets/brand/                # kit de marca (SVGs, ícones, favicon)
```

---

## 6. Cronograma das Sprints

| Sprint    | Entrega                          | Resultado esperado                              | Status |
|-----------|----------------------------------|-------------------------------------------------|--------|
| 1 & 2     | Levantamento & Regras de Negócio | Entender e formalizar o funcionamento           | DOCS ENTREGUES — falta entrevista + anexos |
| 3 & 4     | Modelo Conceitual & Lógico       | Mapear entidades, relacionamentos, estrutura BD | pendente |
| 5 & 6     | Dicionário de Dados & Modelo Físico | Padronizar atributos, scripts DDL em SQL     | pendente |
| 7 & 8     | Carga Inicial, BI e IA           | Validar consistência, dados para análise        | pendente |
| 9         | Apresentação Executiva           | Demonstrar o ERP e responder perguntas          | pendente |

---

## 7. Próximos Passos Imediatos (Sprint 1 & 2)

1. ~~**Documento de Levantamento**~~ → **feito**: [`docs/01-documento-de-levantamento.md`](docs/01-documento-de-levantamento.md)
   — 60 requisitos funcionais (MoSCoW), 40 regras de negócio, 20 requisitos
   não-funcionais, 8 critérios de aceite Given-When-Then, matriz de rastreabilidade
   dor→requisito, riscos e entidades candidatas.
2. ~~**ADRs**~~ → **feito**: [`docs/adr/`](docs/adr/) — 12 decisões documentadas
   (stack, Clean Architecture + CQRS, desacoplamento, PostgreSQL,
   multi-estabelecimento, deploy, core genérico, estoque como livro-razão,
   onde as regras vivem, custeio por lote com FEFO, dia operacional, direção visual Chapa).
3. ~~**Glossário do domínio**~~ → **feito**: [`docs/02-glossario.md`](docs/02-glossario.md).
4. **Pendente — entrevista com dono de hamburgueria real:** rende regras de negócio
   que brainstorm interno não traz e fica excelente citar "levantamento com
   estabelecimento real" na banca. É o risco RS5 do levantamento.
5. **Pendente — anexos do levantamento:** diagrama de casos de uso por ator e
   protótipos de baixa fidelidade (lançamento de pedido, mapa de mesas).

---

## 8. Glossário do Domínio

O glossário completo (vocabulário controlado, base do dicionário de dados da Sprint 5 & 6)
vive em **[`docs/02-glossario.md`](docs/02-glossario.md)** — mantido lá para não divergir.

Termos-chave: comanda, ficha técnica, CMV, custo médio ponderado, KDS, modificador,
praça, combo, zona de entrega, sangria, suprimento, estorno, livro-razão de estoque.
