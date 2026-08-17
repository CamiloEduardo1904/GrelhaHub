# ADR-0006 — Deploy em VPS única com Docker e Nginx

**Status:** Aceito · **Data:** Agosto/2026 · **Revisado:** 17/08/2026, ao inspecionar a VPS real

> ## Revisão — o Nginx não é a borda
>
> O ADR foi escrito assumindo VPS vazia. A VPS real (`srv1626893.hstgr.cloud`, Ubuntu 24.04,
> 2 vCPU, 8 GB) **já roda um Traefik na borda há três meses**, servindo um `evolution-api`,
> com Let's Encrypt e redirecionamento HTTP→HTTPS automáticos.
>
> Colocar nosso Nginx em 80/443 significaria disputar a porta com um serviço em produção.
> **Decisão revisada:** a pilha do GrelhaHub **não publica porta nenhuma**. O Nginx interno
> recebe labels e o Traefik o descobre pelo socket do Docker.
>
> | Antes | Agora |
> |-------|-------|
> | Nginx em 80/443 | Nenhuma porta publicada |
> | Certbot próprio (`ativar-https.sh`) | Traefik emite o certificado |
> | Domínio no `nginx.conf` | Domínio no label; Nginx aceita qualquer Host |
>
> **O Nginx permanece**, e não é redundância: é ele que mantém front e API na mesma origem
> (`/` e `/api/*`), que é o que elimina CORS (ADR-0003). Sem ele seriam dois roteadores
> Traefik e duas origens.
>
> O caminho antigo continua versionado em `infra/borda-propria/` e no override
> `docker-compose.exposto.yml`, para o caso de uma VPS sem proxy na borda.
>
> **Consequência para o firewall:** o ADR previa `ufw` com 22, 80 e 443. Ligar isso na VPS
> real derrubaria o `evolution-api`, que publica em 32769. O script de preparação **não
> mexe mais no firewall** — virou decisão consciente, com a lista de portas em uso na mão.

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
