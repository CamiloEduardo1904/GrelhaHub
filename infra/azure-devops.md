# Configurar o CI/CD no Azure DevOps

Checklist para o primeiro deploy. Depois disso, `git push` na `main` publica sozinho.

O que **já está pronto**: a VPS tem `/opt/grelhahub` com o compose, o `infra/` e o `.env`
com os segredos gerados no próprio servidor (permissão 600). Falta ligar o pipeline.

---

## 1. Escolher o registry

O pipeline constrói **duas** imagens: `grelhahub-api` e `grelhahub-web`. Isso importa,
porque o plano gratuito do Docker Hub dá **um** repositório privado.

| Opção | Custo | Privacidade | Observação |
|-------|-------|-------------|------------|
| **Docker Hub, repositórios públicos** | Grátis | Imagem pública | O código-fonte segue privado no Azure. A imagem tem o app compilado, **nenhum segredo** — eles vêm do `.env` em tempo de execução |
| **Docker Hub Personal** | ~US$ 5/mês | Privado | Privados ilimitados |
| **Azure Container Registry Basic** | ~US$ 5/mês | Privado | Integra melhor com o Azure DevOps |
| **GitHub Container Registry** | Grátis | Privado | Privados ilimitados, mas exige conta GitHub |

**Para um trabalho acadêmico, Docker Hub público resolve.** A imagem não carrega segredo —
senha do banco e chave JWT entram como variável de ambiente na VPS. O que fica exposto é o
binário compilado.

Se preferir privado sem custo, o `ghcr.io` é a saída; me avise que ajusto o pipeline.

---

## 2. Criar as Service Connections

No Azure DevOps: **Project settings → Service connections → New service connection**.

Os nomes precisam ser **exatamente** estes — o `azure-pipelines.yml` os referencia:

### `grelhahub-registry` — Docker Registry

| Campo | Valor |
|-------|-------|
| Tipo | Docker Registry |
| Registry type | Docker Hub *(ou Others, para ACR/ghcr)* |
| Docker ID | seu usuário |
| Password | **Access Token**, não a senha da conta |
| Service connection name | `grelhahub-registry` |

> Gere o token em `hub.docker.com` → *Account settings → Personal access tokens*, com
> permissão **Read & Write**. Token pode ser revogado sem trocar a senha da conta.

### `grelhahub-vps` — SSH

| Campo | Valor |
|-------|-------|
| Tipo | SSH |
| Host name | `srv1626893.hstgr.cloud` |
| Port | `22` |
| Username | `root` |
| Autenticação | **Private key** |
| Service connection name | `grelhahub-vps` |

> **Gere um par de chaves só para o pipeline** — não reaproveite a sua chave pessoal.
> A do pipeline não pode ter passphrase (build é não-interativo), e uma chave sem
> passphrase guardada num serviço merece escopo próprio, revogável sozinha:
>
> ```bash
> ssh-keygen -t ed25519 -N "" -f ~/.ssh/grelhahub-deploy -C "azure-pipeline"
> ssh-copy-id -i ~/.ssh/grelhahub-deploy.pub root@srv1626893.hstgr.cloud
> ```
>
> Cole o conteúdo de `~/.ssh/grelhahub-deploy` (a **privada**) no campo do Azure.

---

## 3. Criar o Environment

**Pipelines → Environments → New environment**, nome `producao`, sem recursos.

É o que o `azure-pipelines.yml` usa em `environment: producao`. Serve também para
aprovação manual antes do deploy, se um dia você quiser.

---

## 4. Preencher o registry no `.env` da VPS

```bash
ssh root@srv1626893.hstgr.cloud
cd /opt/grelhahub
sed -i 's|^DOCKER_REGISTRY=.*|DOCKER_REGISTRY=docker.io/SEU_USUARIO|' .env
grep DOCKER_REGISTRY .env
```

Sem isso o `docker compose pull` procura `PREENCHER/grelhahub-api` e falha.

---

## 5. Apontar o domínio

`grelhahub.com` ainda está no parking da Hostinger (`2.57.91.91`). No hPanel →
**Domínios → DNS**:

| Tipo | Nome | Valor | TTL |
|------|------|-------|-----|
| A | `@` | `177.7.42.204` | 300 |
| A | `www` | `177.7.42.204` | 300 |
| AAAA | `@` | `2a02:4780:75:b4d9::1` | 300 |

Remova os A que apontam para `2.57.91.91`.

O Traefik emite o certificado por desafio HTTP: **enquanto o DNS não apontar para cá, não
há certificado e o site não abre.** Confira antes de estranhar:

```bash
dig +short A grelhahub.com     # deve responder 177.7.42.204
```

---

## 6. Criar o pipeline

**Pipelines → New pipeline → Azure Repos Git →** seu repositório **→ Existing Azure
Pipelines YAML file →** `/azure-pipelines.yml`.

Rode uma vez. A ordem é: build e testes → imagens → deploy.

---

## O que acontece no primeiro deploy

1. **Build** — `dotnet build -c Release` (warning é erro), `dotnet test`, lint e build do front, auditoria de dependências vulneráveis
2. **Imagens** — constrói e publica `grelhahub-api` e `grelhahub-web` com a tag do build e `latest`
3. **Deploy** — por SSH:
   - `docker compose run --rm api dotnet GrelhaHub.Api.dll --migrate` → cria o schema e **sai**
   - `docker compose up -d` → sobe db, api, web e nginx
   - espera `/api/health` responder, com até 60 s

O Traefik descobre o nginx pelos labels e emite o certificado. Primeiro acesso:
**https://grelhahub.com/cadastrar**.

---

## Se o deploy falhar

```bash
ssh root@srv1626893.hstgr.cloud
cd /opt/grelhahub
docker compose ps
docker compose logs --tail=80 api
docker compose logs --tail=40 nginx
docker logs traefik-traefik-1 --tail=40 | grep -i grelhahub
```

| Sintoma | Causa provável |
|---------|----------------|
| `pull access denied` | `DOCKER_REGISTRY` no `.env` ainda em `PREENCHER`, ou repositório privado sem login na VPS |
| Certificado não emite | DNS ainda não propagou, ou a 80 não chega no Traefik |
| API sobe e morre | Connection string ou `Jwt__Chave` ausente — confira o `.env` |
| 404 do Traefik | Label `Host()` diferente do domínio acessado |

---

## Rollback

As imagens ficam versionadas pela tag do build:

```bash
cd /opt/grelhahub
sed -i 's|^IMAGE_TAG=.*|IMAGE_TAG=1234|' .env    # o build que funcionava
docker compose up -d
```

Migration **não** volta sozinha. Se a versão anterior for incompatível com o schema novo,
restaure o backup antes (`infra/README.md`).
