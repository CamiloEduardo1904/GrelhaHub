# ADR-0006 — Deploy em VPS única com Docker e Nginx

**Status:** Aceito · **Data:** Agosto/2026

## Contexto

O projeto dispõe de uma VPS na Hostinger com Docker instalado — sem orçamento para
serviços gerenciados de banco, balanceador ou PaaS (restrição R1). O repositório e o CI/CD
ficam no Azure DevOps (restrição R2). O sistema precisa estar no ar de forma confiável
durante a apresentação executiva da Sprint 9.

## Decisão

Toda a stack roda em containers Docker na mesma VPS, com Nginx à frente:

```
                    VPS (Docker)
Internet ──► Nginx (80/443)
              ├── /          → build estático do React
              └── /api/*     → proxy_pass → container da API (.NET)
            + container PostgreSQL (rede interna, sem porta pública)
```

**Pipeline de deploy:** `git push main` → Azure DevOps builda a imagem →
push para o registry (Docker Hub) → SSH na VPS → `docker compose pull && up -d`.
A VPS **não builda** — apenas puxa imagens prontas, porque compilar .NET com pouca RAM
derruba o serviço em execução.

**Configuração de infraestrutura:**
- Firewall (ufw) libera apenas 22, 80 e 443
- PostgreSQL sem `ports:` público; acesso administrativo só por túnel SSH
- Usuário `deploy` dedicado no grupo docker — nunca root
- Segredos em `.env` presente apenas na VPS e em Variable Groups no Azure; jamais no
  repositório (RNF-008)
- Swap de 2 GB se a VPS tiver 1 GB de RAM
- HTTPS por Let's Encrypt/Certbot, com redirect de HTTP

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| Kubernetes | Complexidade e consumo de recursos absurdos para uma VPS e um punhado de containers |
| PaaS (Azure App Service, Railway, Render) | Custo recorrente fora do orçamento; e o projeto perde o valor didático de configurar a infraestrutura |
| Deploy sem containers (publish direto + systemd) | Reprodutibilidade pior, dependências do host, rollback manual |
| Build na própria VPS | Compilação de .NET consome memória demais e compete com o serviço em produção |

## Consequências

**Positivas**
- Ambiente reproduzível e portável (RNF-015); o mesmo compose sobe em qualquer host
- Front e back no mesmo domínio eliminam CORS
- Rollback é trocar a `IMAGE_TAG` no compose e subir de novo
- Custo de infraestrutura fixo e conhecido

**Negativas / riscos assumidos**
- **Ponto único de falha**: se a VPS cair, tudo cai (risco RS3). Aceito para o porte atual;
  mitigado com backup diário do volume `pgdata` e backup extra antes de apresentações
- Sem escala horizontal — a saída para crescer é aumentar a VPS
- Recursos compartilhados entre API, banco e Nginx exigem limites de memória por container
- Certificado, DNS e renovação ficam por conta do time

## Pendências vinculadas

- Domínio ainda não adquirido (restrição R5). Plano B validado: DuckDNS + Let's Encrypt
- Definir se as migrations do EF Core rodam no startup da API (`Database.Migrate()`) ou
  como step separado do pipeline. **Recomendação:** step do pipeline — migration no
  startup falha silenciosamente ou trava o boot quando dá conflito
