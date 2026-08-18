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
#    O usuário de suporte da plataforma vem do passo 4b, não daqui.
docker compose run --rm api dotnet GrelhaHub.Api.dll --migrate

# 4b. Usuário de suporte da plataforma (RN-GER-014) — idempotente, ver seção abaixo
docker compose run --rm api dotnet GrelhaHub.Api.dll --semear

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

A semeadura tem **modo próprio de linha de comando** — `--semear` —, separado do `--migrate`:

```bash
docker compose run --rm api dotnet GrelhaHub.Api.dll --semear
```

**É idempotente e pode ser rodado a qualquer momento.** Não aplica schema, não sobe servidor, não
sobrescreve a senha de um usuário que já existe: aplica a carga inicial de `RN-GER-016` e sai. No
deploy ele é o step **"Semear plataforma"**, logo depois das migrations.

> **Por que step separado, e não um apêndice do `--migrate`.** Era assim até 17/08/2026, e o
> resultado da semeadura saía no meio do log de um step chamado "Migrations EF Core" — que num
> deploy sem migration pendente, o caso comum, ninguém tem motivo para abrir. A instalação ficou
> sem acesso de suporte com o aviso sendo registrado a cada deploy, lido por ninguém. O sintoma
> apareceu do outro lado: "não consigo entrar", numa tela que por `RN-GER-027` **não pode** dizer
> o motivo. Quando o único lugar que explica é um log, esse log precisa de nome próprio.

Em desenvolvimento a semeadura continua acontecendo no startup (`IsDevelopment()`); em produção,
**só** por este comando. Acrescentar as variáveis ao `.env` e dar `docker compose up -d` não cria
nada.

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
docker compose run --rm api dotnet GrelhaHub.Api.dll --semear
```

O log do passo 3 diz qual dos quatro caminhos aconteceu:

| Log | Significado |
|-----|-------------|
| `usuário de plataforma criado (...)` | criado agora |
| `usuário de plataforma já existe (...); nada a fazer` | já havia um, com o e-mail que o log mostra — a senha do `.env` foi **ignorada** |
| `usuário de plataforma não configurado: <chave> sem valor` | a chave nomeada não chegou ao container; confira o `.env` **e** o repasse do passo 2 |
| `a senha configurada tem espaço ou quebra de linha nas bordas` | o valor foi usado como está, e o login vai exigir os mesmos caracteres — quase sempre CRLF de `.env` editado no Windows |

⚠ **O e-mail não pode pertencer a um usuário de restaurante.** O e-mail é único globalmente; nesse
caso o semeador lança, o step sai com código 1 e o `set -e` do pipeline aborta o deploy — antes do
`up -d`, de propósito.

⚠ **O usuário de plataforma nasce com `DeveTrocarSenha = false`** (`Usuario.CriarDePlataforma`):
`RF-SEG-005` **não** o obriga a trocar no primeiro acesso. A senha do `.env` vale até alguém trocar
à mão em `/trocar-senha` — rota que ele alcança, porque `TrocarSenhaCommand` não exige módulo nem
permissão. Se a senha inicial passou por qualquer canal que não seja o `.env`, troque depois de
entrar.

⚠ **Não há como redefinir a senha do usuário de plataforma por fora do sistema**, e é deliberado:
o semeador nunca sobrescreve senha de usuário existente, senão quem tivesse uma cópia antiga do
`.env` teria uma porta. Se a senha do suporte se perder, isso é uma decisão a registrar no catálogo
antes de virar código — não um `UPDATE` no banco.

## Imagens não gravam — 500 no upload

**Sintoma:** a API está no ar, o cardápio responde, e o envio de foto devolve
`{"status": 500, "detail": "Não foi possível concluir a operação."}`. Nada na tela diz o motivo,
porque `UnauthorizedAccessException` não é regra de negócio — cai no 500 genérico.

**Causa:** o container roda como **UID 1001** (ADR-0006, nunca root) e o volume `arquivos` nasceu
`root:root`. Docker cria o ponto de montagem como root quando o caminho **não existe na imagem**, e
até 18/08/2026 a imagem não criava `/app/arquivos`. O processo consegue ler, e não consegue criar a
pasta da unidade para escrever dentro.

**Correção, uma vez, sem parar a pilha:**

```bash
docker run --rm -v grelhahub_arquivos:/d alpine chown -R 1001:1001 /d
docker compose up -d api
```

Instalação nova não precisa disto: o `Dockerfile` passou a criar `/app/arquivos` com dono 1001, e o
Docker copia o dono do caminho da imagem quando monta um volume **vazio** sobre ele. Volume que já
existe guarda o dono de quando nasceu — daí o comando acima.

> ⚠ **A partir da mesma data a API RECUSA A PARTIDA com a raiz de arquivos sem escrita**
> (`GarantirEscritaDeArquivos`), e a mensagem traz o comando. É deliberado, e tem um custo que
> precisa ser dito: **um deploy nesse estado deixa a API fora do ar** em vez de subir quebrada. A
> alternativa era a que já vivemos — deploy verde, healthcheck verde, e o defeito esperando o
> primeiro usuário que tentasse enviar uma foto.
>
> A sonda **escreve e apaga** um arquivo. Conferir se o diretório existe não pegaria nada: ele
> existia, montado, e sem permissão para quem precisava escrever.

## Correio — RN-GER-032, ADR-0016

A caixa do domínio, na Hostinger. **SPF, DKIM e DMARC já publicados** — é o que faz o link de
recuperação chegar na entrada em vez do spam.

```bash
cd /opt/grelhahub

cat >> .env <<'FIM'
EMAIL_HOST=smtp.hostinger.com
EMAIL_PORTA=587
EMAIL_USUARIO=sistema@grelhahub.com
EMAIL_SENHA=a-senha-DA-CAIXA
EMAIL_REMETENTE=noreply@grelhahub.com
EMAIL_NOME_REMETENTE=GrelhaHub
EMAIL_BASE_DO_SITE=https://grelhahub.com
FIM

docker compose config | grep Email__     # os sete precisam aparecer
docker compose up -d api
```

| Campo | Valor | Observação |
|-------|-------|------------|
| Host | `smtp.hostinger.com` | |
| Porta | **587** | STARTTLS. 465 é SSL implícito, e o código escolhe pelo número — trocar um pelo outro **trava a conexão sem erro legível** |
| Usuário | `sistema@grelhahub.com` | ⚠ a senha é a **da caixa**, não a do hPanel |
| Remetente | `noreply@grelhahub.com` | Alias da mesma caixa |
| `EMAIL_BASE_DO_SITE` | `https://grelhahub.com` | ⚠ Monta o link do e-mail. Vem **daqui**, nunca do cabeçalho `Host` da requisição — link montado com o que o cliente mandou é o furo clássico da troca de senha |

**Sem `EMAIL_HOST`, nada é enviado — e isso é estado válido** (`RN-GER-032`). Cada e-mail que
deixou de sair fica no log com destinatário e assunto, e **nenhuma operação falha por causa disso**:
usuário criado continua criado. Confira com:

```bash
docker compose logs api | grep "RN-GER-032"
```

> ⚠ **A fila de envio é em memória** (ADR-0016). Reiniciar o contêiner descarta o que não saiu.
> Aceito porque todo e-mail nosso é reemitível — o usuário pede outro link. Se um dia entrar aqui
> um e-mail que não se possa pedir de novo, o ADR precisa ser revisto **antes**.

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
