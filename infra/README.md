# Infraestrutura

Tudo que a VPS precisa. Nada aqui contém segredo — os segredos vivem no `.env`,
que existe **apenas na VPS** e nunca é versionado (`RNF-008`).

| Arquivo | O que faz |
|---------|-----------|
| `preparar-vps.sh` | Prepara uma VPS Ubuntu limpa: Docker, usuário `deploy`, firewall, swap. Rode **uma vez**, com sudo |
| `ativar-https.sh` | Emite o certificado Let's Encrypt e liga o HTTPS. Rode **depois** que o domínio apontar para o IP |
| `renovar-https.sh` | Renovação do certificado. Vai no cron, diariamente |
| `nginx/templates/` | Configuração do nginx da borda, com `${DOMINIO}` substituído na partida |
| `nginx/tls-ativo/` | Começa vazio. Ganha conteúdo quando o HTTPS é ativado |

## Por que o HTTPS é um passo separado

Ovo e galinha: o certbot precisa do nginx respondendo em HTTP para a Let's
Encrypt buscar o desafio ACME — e o nginx não sobe apontando para um certificado
que ainda não existe.

A saída é o nginx subir **em HTTP puro**, funcionando, e o bloco de TLS entrar
por um `include` opcional (`include ... *;`) de um diretório que começa vazio.
`ativar-https.sh` emite o certificado, escreve os dois arquivos lá e recarrega.

Consequência prática: **o sistema fica no ar antes de existir domínio.** Dá para
testar pelo IP e resolver o DNS depois — o que importa, já que o domínio ainda
não foi comprado (restrição R5 do levantamento).

## Ordem de execução

```bash
# 1. Na VPS, como root — uma vez só
sudo ./infra/preparar-vps.sh

# 2. Como deploy: copiar docker-compose.yml, infra/ e o .env para /opt/grelhahub
#    Gerar os segredos:
openssl rand -base64 48   # JWT_CHAVE
openssl rand -base64 32   # POSTGRES_PASSWORD
openssl rand -base64 24   # PLATAFORMA_SENHA_INICIAL  (RN-GER-014 — ver abaixo)

# 3. Subir — HTTP, funciona por IP
cd /opt/grelhahub && docker compose up -d
curl -fsS http://localhost/api/health

# 4. Primeira carga do schema (o pipeline faz isso sozinho depois)
#    É ESTE passo que cria o usuário de suporte da plataforma — `up -d` não cria.
docker compose run --rm api dotnet GrelhaHub.Api.dll --migrate

# 5. Com o domínio apontando para o IP
./infra/ativar-https.sh

# 6. Renovação automática
sudo crontab -e
# 0 3 * * * cd /opt/grelhahub && ./infra/renovar-https.sh >> /var/log/grelhahub-certbot.log 2>&1
```

## Usuário de suporte da plataforma — RN-GER-014, ADR-0013

> ⚠⚠ **Este usuário enxerga TODAS as unidades.** O recorte por restaurante (`RN-GER-009`) não se
> aplica a ele. A senha dele é a mais sensível da instalação, e por isso não vem de migration
> nenhuma (`RNF-008`): vem do `.env` da VPS.

`SemeadorDePlataforma` roda **apenas no passo `--migrate`** — em produção o `Program.cs` só semeia
dentro do bloco `args.Contains("--migrate")`; a semeadura no startup é restrita a
`IsDevelopment()`. Consequência: **acrescentar as variáveis ao `.env` e dar `docker compose up -d`
não cria nada.** É preciso rodar o passo de migration outra vez — o que é seguro, porque ele é
idempotente nas duas pontas (sem migration pendente não aplica nada; com usuário de plataforma
existente não toca na senha).

```bash
cd /opt/grelhahub

# 1. As três variáveis, no .env (permissão 600). A senha é gerada AQUI, não no repositório.
cat >> .env <<'FIM'
PLATAFORMA_NOME=Suporte GrelhaHub
PLATAFORMA_EMAIL=suporte@grelhahub.local
PLATAFORMA_SENHA_INICIAL=cole-aqui-o-openssl-rand
FIM

# 2. Conferir que o compose está repassando (tem de listar as três Plataforma__*)
docker compose config | grep Plataforma__

# 3. Semear
docker compose run --rm api dotnet GrelhaHub.Api.dll --migrate
```

O log do passo 3 diz qual dos três caminhos aconteceu:

| Log | Significado |
|-----|-------------|
| `RN-GER-014 — usuário de plataforma criado (...)` | criado agora |
| `RN-GER-014 — usuário de plataforma já existe; nada a fazer.` | já havia um — a senha do `.env` foi **ignorada** |
| `RN-GER-014 — usuário de plataforma não configurado (...)` | as variáveis não chegaram ao container; confira o passo 2 |

⚠ **O e-mail não pode pertencer a um usuário de restaurante.** O e-mail é único globalmente; nesse
caso o semeador lança, o passo sai com código 1 e o `set -e` do pipeline aborta o deploy — antes do
`up -d`, de propósito.

⚠ **O usuário de plataforma nasce com `DeveTrocarSenha = false`** (`Usuario.CriarDePlataforma`):
`RF-SEG-005` **não** o obriga a trocar no primeiro acesso. A senha do `.env` vale até alguém trocar
à mão em `/trocar-senha` — rota que ele alcança, porque `TrocarSenhaCommand` não exige módulo nem
permissão. Se a senha inicial passou por qualquer canal que não seja o `.env`, troque depois de
entrar.

## Backup — RNF-017

> ⚠⚠ **`pg_dump` sozinho DEIXOU de ser backup completo.** Desde a `SPEC-VEN-002` (ADR-0015) as
> imagens de produto e as logos vivem em um **volume de disco**, fora do banco. Restaurar apenas o
> dump devolve um sistema com o cardápio inteiro e **todas as fotos quebradas** — as colunas
> apontam para arquivos que não existem mais. São **duas** cópias, e as duas precisam do mesmo
> instante.

Antes de qualquer apresentação, os dois:

```bash
# 1. O banco
docker compose exec -T db pg_dump -U grelhahub grelhahub | gzip > backup-$(date +%F).sql.gz

# 2. As imagens (ADR-0015)
docker run --rm \
  -v grelhahub_arquivos:/dados:ro \
  -v "$PWD":/saida \
  alpine tar czf /saida/arquivos-$(date +%F).tar.gz -C /dados .
```

Restaurar:

```bash
gunzip -c backup-2026-08-17.sql.gz | docker compose exec -T db psql -U grelhahub -d grelhahub

docker run --rm \
  -v grelhahub_arquivos:/dados \
  -v "$PWD":/entrada \
  alpine tar xzf /entrada/arquivos-2026-08-17.tar.gz -C /dados
```

### Coleta de órfãos — manutenção, não automatismo

`RN-GER-002` proíbe exclusão física, então **trocar a imagem de um produto não apaga a
anterior** — a trilha de auditoria guarda o caminho antigo, e trilha apontando para arquivo
apagado é trilha inútil. A consequência é que o volume cresce com arquivos que nenhuma coluna
referencia.

Isso **não** é resolvido por um `DELETE` no fim do `POST`. É rotina de manutenção, com
retenção declarada, e ainda não existe: enquanto o volume for de centenas de megabytes, o custo
de escrever a rotina é maior do que o do disco. Quando doer, o critério é "sem referência em
`produto.url_imagem` nem em `unidade.url_logo` **e** sem menção em `log_auditoria`".

## Esta VPS — srv1626893.hstgr.cloud

Ubuntu 24.04, 2 vCPU, 8 GB RAM, 96 GB de disco. **Não está vazia.**

| Já roda | Portas |
|---------|--------|
| `traefik` (borda, Let's Encrypt) | 80, 443 — rede `host` |
| `evolution-api` + Postgres 15 + Redis | 32769 |

Por isso o GrelhaHub **não publica porta nenhuma**. O Traefik roda com
`providers.docker.exposedbydefault=false`, então descobre nosso nginx pelos
labels e roteia por domínio, emitindo o certificado sozinho.

### O que a pilha nunca toca

- **Containers, redes e volumes alheios** — tudo aqui vive sob o prefixo `grelhahub`
- **As portas 80 e 443** — nada é publicado; quem recebe é o Traefik
- **O firewall** — o `ufw` está inativo nesta VPS, e ligá-lo derrubaria o
  `evolution-api` na 32769. `preparar-vps.sh` **não mexe** nele
- **Imagens de terceiros** — o `prune` do pipeline é filtrado para as nossas

### Domínio

`grelhahub.com` ainda aponta para o parking da Hostinger. Enquanto o DNS não
muda, o wildcard `*.srv1626893.hstgr.cloud` funciona e o Traefik já emite
certificado nele — dá para subir e ver funcionando antes de mexer no DNS.

Para apontar o domínio, no hPanel → **Domínios → DNS**:

| Tipo | Nome | Valor | TTL |
|------|------|-------|-----|
| A | `@` | `177.7.42.204` | 300 |
| A | `www` | `177.7.42.204` | 300 |
| AAAA | `@` | `2a02:4780:75:b4d9::1` | 300 |

Remova os registros A que apontam para `2.57.91.91` (parking). Depois de
propagar, troque `DOMINIO` no `.env` e rode `docker compose up -d nginx`:
o Traefik emite o certificado novo sozinho.

### Sem Traefik na borda

Noutra VPS, use o override que publica portas:

```bash
docker compose -f docker-compose.yml -f docker-compose.exposto.yml up -d
```

Aí o TLS é por conta própria — ver `infra/borda-propria/`.
