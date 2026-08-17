# Configurar o CI/CD no Azure DevOps

Checklist para o primeiro deploy. Depois disso, `git push` na `main` publica sozinho.

### Estado

| Passo | Situação |
|-------|----------|
| VPS preparada (`/opt/grelhahub`, compose, `infra/`) | ✔ feito |
| `.env` com segredos gerados na própria VPS, 600 | ✔ feito |
| `DOCKER_REGISTRY=docker.io/dextter` | ✔ feito |
| Chave dedicada do pipeline instalada e testada | ✔ feito |
| Service Connection `grelhahub-registry` | ✔ feito |
| Service Connection `grelhahub-vps` | ✔ feito |
| Environment `producao` | ✔ feito |
| DNS de `grelhahub.com` → VPS | ✔ feito e propagado |
| Imagens validadas localmente (build + execução) | ✔ feito |
| Agente self-hosted na VPS, pool `grelhahub` | ✔ feito |
| Pool `grelhahub` criado e autorizado no projeto | ✔ feito |
| Pipeline executado | ✖ pendente |

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
| Docker ID | `dextter` |
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

## 4. Registry no `.env` da VPS — FEITO

```bash
ssh root@srv1626893.hstgr.cloud
cd /opt/grelhahub
grep DOCKER_REGISTRY .env    # já preenchido: docker.io/dextter
```

Já preenchido com `docker.io/dextter`. As imagens serão:

```
docker.io/dextter/grelhahub-api:<build>
docker.io/dextter/grelhahub-web:<build>
```

Os repositórios nascem no primeiro push — não precisa criá-los antes.

---

## 5. Apontar o domínio

**Feito.** Registros em produção:

| Tipo | Nome | Valor | TTL |
|------|------|-------|-----|
| A | `@` | `177.7.42.204` | 60 |
| AAAA | `@` | `2a02:4780:75:b4d9::1` | 14400 |
| CNAME | `www` | `grelhahub.com` | 300 |

O `CNAME` no `www` é melhor que um segundo `A`: acompanha o apex sozinho se o IP mudar.

Verificado: `http://grelhahub.com` devolve **301** (o Traefik redirecionando para HTTPS) e
`https://grelhahub.com` devolve **404** — que é o certo enquanto nenhum serviço reivindica
esse Host. O domínio já chega no Traefik; falta a pilha subir.

O Traefik emite o certificado por desafio HTTP: **enquanto o DNS não apontar para cá, não
há certificado e o site não abre.** Confira antes de estranhar:

```bash
dig +short A grelhahub.com     # deve responder 177.7.42.204
```

---

## 5.1 Agente self-hosted — por que

A organização **não tem paralelismo hospedado concedido**. Desde 2021 a Microsoft não
libera os agentes gratuitos automaticamente para organizações novas; é preciso pedir por
formulário e leva dias. O sintoma é exatamente:

```
Could not find a pool with name Azure Pipelines.
The pool does not exist or has not been authorized for use.
```

**Vale pedir mesmo assim** — https://aka.ms/azpipelines-parallelism-request — mas o agente
próprio é gratuito, sem fila e sem limite de minutos.

### Pool dedicado, não o `Default`

O `Default` existe no nível da **organização**, e um pipeline só o usa se o **projeto**
tiver o pool vinculado e autorizado. Registrar o agente lá não basta — o sintoma é:

```
Could not find a pool with name Default.
The pool does not exist or has not been authorized for use.
```

Um pool criado pelo próprio projeto (**Project settings → Agent pools → Add pool → New →
Self-hosted**) nasce autorizado, e ainda isola este agente de qualquer outro que a
organização venha a ter. Por isso o pool é `grelhahub`.

O agente vive em `/opt/azagent`, roda como o usuário `azagent` (o `config.sh` recusa
root, e com razão: um agente de CI executa código do repositório) e está no grupo `docker`
para o passo de build de imagem.

**Custo assumido:** o build passa a rodar no mesmo servidor que serve produção, o que
contraria a premissa do ADR-0006. A VPS tem 8 GB e 2 vCPU, e o pico dura poucos minutos por
deploy — aceitável aqui, não seria numa VPS de 2 GB.

### Registrar

O PAT precisa de **um escopo só**: *Agent Pools → Read & manage*. Não use "Full access":
o token fica guardado na VPS.

```
User settings → Personal access tokens → New Token
  Name: agente-vps-grelhahub
  Scopes: Custom defined → Agent Pools (Read & manage)
```

O registro é interativo, com o token digitado dentro da sessão SSH — nunca em linha de
comando, nunca no histórico do shell.

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
