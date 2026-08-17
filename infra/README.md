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

## Se já roda outra coisa nesta VPS

A pilha se isola pelo `name: grelhahub` no compose — rede, volumes e containers
ficam todos sob esse prefixo, sem colidir com o que já existe.

**O que colide é a porta**, e por isso o padrão é alto: `PORTA_HTTP=8080` e
`PORTA_HTTPS=8443`. Subir a pilha não toma a 80 de ninguém.

A partir daí, dois caminhos:

| Situação | O que fazer |
|----------|-------------|
| 80 e 443 **livres** | `PORTA_HTTP=80`, `PORTA_HTTPS=443` no `.env`. O GrelhaHub vira a borda |
| Já há um nginx/traefik na borda | Deixe 8080/8443 e configure o que já existe para fazer proxy de `grelhahub.seudominio` para `127.0.0.1:8080` |

No segundo caso, o `ativar-https.sh` **não** se aplica: quem termina o TLS é o
proxy da borda, e o GrelhaHub fica em HTTP atrás dele. Me diga qual é o proxy
que eu escrevo o trecho de configuração.

### O que a pilha nunca toca

- **Containers e volumes de outros projetos** — tudo aqui vive sob o prefixo `grelhahub`
- **Imagens de terceiros** — o `prune` do pipeline é filtrado para as nossas.
  Um `docker image prune -f` sem filtro apagaria imagem dangling de qualquer
  projeto do servidor, inclusive de outro build em andamento
- **O Docker daemon e a rede do host** — só a rede `grelhahub_interna`, criada e
  removida com a pilha
