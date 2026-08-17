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

# 3. Subir — HTTP, funciona por IP
cd /opt/grelhahub && docker compose up -d
curl -fsS http://localhost/api/health

# 4. Primeira carga do schema (o pipeline faz isso sozinho depois)
docker compose run --rm api dotnet GrelhaHub.Api.dll --migrate

# 5. Com o domínio apontando para o IP
./infra/ativar-https.sh

# 6. Renovação automática
sudo crontab -e
# 0 3 * * * cd /opt/grelhahub && ./infra/renovar-https.sh >> /var/log/grelhahub-certbot.log 2>&1
```

## Backup — RNF-017

Antes de qualquer apresentação:

```bash
docker compose exec -T db pg_dump -U grelhahub grelhahub | gzip > backup-$(date +%F).sql.gz
```

Restaurar:

```bash
gunzip -c backup-2026-08-17.sql.gz | docker compose exec -T db psql -U grelhahub -d grelhahub
```

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
