# SPEC-VEN-002 — Imagem do produto e logo da unidade

| Campo | Valor |
|-------|-------|
| ID | `SPEC-VEN-002-imagem-de-produto-e-logo` |
| Módulo | VEN |
| Situação | `ENTREGUE` — 17/08/2026 |
| Onda | 1 — cadastro |
| Regras implementadas | `RN-VEN-042`, `RN-GER-026`, `RN-VEN-037`, `RN-GER-002` |
| ADRs aplicáveis | [ADR-0018](../adr/0018-imagem-como-binario-no-banco.md) ⚠ **substitui a ADR-0015**, [ADR-0017](../adr/0017-tres-bancos-aplicacao-arquivo-e-log.md), [ADR-0006](../adr/0006-deploy-em-vps-unica.md) |

> ## ⚠⚠ O armazenamento MUDOU em 18/08/2026
>
> Esta spec foi escrita quando a imagem vivia num **volume de disco** (ADR-0015). A
> [ADR-0018](../adr/0018-imagem-como-binario-no-banco.md) moveu o binário para o banco
> `grelhahub_arquivo`. **As regras não mudaram** — formato pela assinatura, 5 MB, nome do usuário
> descartado, referência antiga preservada —, mas três coisas na letra desta spec ficaram velhas:
>
> | Onde | Era | É |
> |------|-----|---|
> | A saída do upload | `/arquivos/{unidade}/produto/{uuid}.jpg` | `/api/v1/arquivos/{id}` |
> | Onde os bytes ficam | volume de disco, servido pelo Nginx | tabela `arquivo_binario` |
> | O que garante `RN-GER-009` | a unidade **no caminho** | a unidade **no metadado**, sob filtro global |
>
> ⚠ A terceira é a que mais importa: **caminho não é vínculo.** Nada impedia o caminho de apontar
> para uma pasta e a linha do banco dizer outra coisa. O teste CA-6 desceu junto, e o comentário
> dele explica a mudança.
| Depende de | `SPEC-VEN-001`, `SPEC-GER-004` |
| Data | 17/08/2026 |
| Entregue em | 17/08/2026 |

---

## 1. Objetivo

Dar rosto ao cardápio público: **foto no produto** e **logo na casa**. É a primeira vez que o
sistema recebe arquivo binário do usuário, e é por isso que esta spec tem mais texto sobre
recusa do que sobre gravação.

> **`Produto.UrlImagem` já existia — como coluna morta.** Estava no modelo desde a migration
> `Fundacao`, nunca gravada, nunca lida, ausente de todo DTO e do frontend. Coluna sem caminho
> de escrita nem de leitura é dívida silenciosa: parece pronto e não faz nada. Esta spec a liga.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-VEN-042` | Produto tem uma imagem: JPEG ou PNG, até 5 MB, formato conferido pelo conteúdo | Aplicação | Validator + leitura da assinatura do arquivo |
| `RN-GER-026` | Unidade tem logo, exibida no cardápio público | Aplicação | Mesmo caminho da imagem do produto |
| `RN-VEN-037` | Cardápio público não expõe dado de outra unidade | Aplicação | Caminho da imagem contém o `id` da unidade |
| `RN-GER-002` | Nada é excluído fisicamente | Aplicação | Trocar a imagem **não** apaga o arquivo anterior |

## 3. Contrato

### `POST /api/v1/produtos/{id}/imagem` — `multipart/form-data`

| Campo | Tipo | Obrigatório |
|-------|------|:-----------:|
| `arquivo` | binário | ✔ |

**Saída:** `{ "url": "/api/v1/arquivos/{id}" }` — ⚠ ADR-0018, ver o aviso no topo

### `POST /api/v1/unidade/logo` — `multipart/form-data`

Mesma forma. **Saída:** `{ "url": "/api/v1/arquivos/{id}" }`

> **O campo é `url` nas duas, e não `urlImagem`/`urlLogo` como esta spec dizia.** As duas
> operações devolvem a mesma coisa — o caminho de um arquivo que acabou de ser gravado —, e dois
> nomes para um valor idêntico obrigariam o componente de envio do frontend a saber em qual rota
> está para ler a resposta. É `EnvioDeImagem` reusado nas duas telas justamente porque a resposta
> é a mesma.

### `DELETE /api/v1/produtos/{id}/imagem`

**Limpa a coluna** e não toca o arquivo (`RN-GER-002`, §4). Devolve `204`.

Não há `DELETE` para a logo: remover a logo da vitrine sem pôr outra é caso que ninguém pediu, e
a troca já cobre a correção de uma logo errada.

### Erros

| Situação | Código | Mensagem |
|----------|--------|----------|
| Arquivo ausente | 422 | "Envie um arquivo de imagem." |
| Acima de 5 MB | 422 (ou **413** na borda) | "A imagem não pode passar de 5 MB." |
| Formato não aceito | 422 | "Aceitamos apenas JPEG e PNG." |
| Assinatura não corresponde | 422 | "O arquivo não é uma imagem JPEG ou PNG válida." |
| Produto de outra unidade | 404 | "Produto não encontrado." |

> **422 e não 400**, pela mesma razão da `SPEC-GER-007` §3: `RegraDeNegocioException` é a convenção
> do projeto para regra violada. E **413 é possível no caso do tamanho**: quando o corpo excede o
> `[RequestSizeLimit]`, quem recusa é o Kestrel, **antes** de a aplicação ver o pedido — que é
> exatamente o comportamento desejado. Os testes afirmam "não teve sucesso" nesse caso, e não um
> código específico, porque fixar 413 amarraria a asserção ao servidor em vez à regra.

### Autorização

| Operação | Permissão |
|----------|-----------|
| Imagem do produto | `cardapio.gerenciar` |
| Logo da unidade | `cardapio.gerenciar` |
| Ler a imagem | **nenhuma** — é arquivo estático público (`RN-VEN-037`) |

## 4. Comportamento

### A validação tem quatro camadas, e a ordem importa

1. **Tamanho, antes de ler o corpo.** O limite do ASP.NET recusa a requisição grande **antes**
   de o servidor carregar 5 MB na memória. Validar depois de ler é aceitar o ataque e reclamar
2. **Extensão** — `.jpg`, `.jpeg`, `.png`. Barato, e recusa o engano honesto
3. **`Content-Type`** — `image/jpeg`, `image/png`. Também barato, também insuficiente
4. **Assinatura do arquivo** — a que decide

```
JPEG  FF D8 FF
PNG   89 50 4E 47 0D 0A 1A 0A
```

> **Por que a assinatura é a que vale.** Extensão e `Content-Type` são **declarados por quem
> envia**. Um arquivo executável renomeado para `.jpg`, com `Content-Type: image/jpeg`, passa
> nas camadas 2 e 3 sem esforço. Só o conteúdo não mente — e é isso que `RN-VEN-042` quer dizer
> com "conferido pelo conteúdo".

### O nome do arquivo nunca vem do usuário

O nome enviado é **descartado**. O sistema gera `{uuid}.{extensão canônica}`, e a extensão vem
da **assinatura conferida**, não do que o usuário escreveu.

Isso fecha travessia de diretório (`../../etc/algo`), colisão de nome entre unidades e
caractere que o sistema de arquivos interpreta. É a defesa mais barata e a mais esquecida.

### O caminho carrega a unidade

```
{raiz}/{unidadeId}/produto/{uuid}.jpg
{raiz}/{unidadeId}/logo/{uuid}.png
```

`unidadeId` no caminho não é organização de pasta: é `RN-GER-009` chegando ao disco. Um
caminho sem unidade permitiria que o `id` de um arquivo, vazado, servisse arquivo de qualquer
restaurante — e o diretório é público por desenho.

### Trocar a imagem não apaga a anterior — `RN-GER-002`

A coluna passa a apontar para o arquivo novo; o antigo **fica no disco**. Parece desperdício e
é decisão:

- `RN-GER-002` diz que registro transacional não é excluído fisicamente. Apagar arquivo é a
  exclusão física mais irreversível que existe — não há trilha que traga o byte de volta
- A troca é auditada (`SPEC-GER-005`), e a trilha guarda o caminho anterior. Trilha apontando
  para arquivo apagado é trilha inútil

**A consequência aceita:** o disco cresce com órfãos. A limpeza é rotina de manutenção com
retenção declarada — não efeito colateral de um `POST`. Está em `infra/README.md`.

### Cardápio público

`ObterCardapioPublico` passa a devolver `urlImagem` no produto e `urlLogo` na unidade. Nenhum
dado novo sensível: são arquivos públicos. `RN-GER-012` continua valendo — custo não entra.

## 5. Critérios de aceite

### CA-1 — JPEG válido é aceito (`RN-VEN-042`)

> **Dado** um JPEG de 1 MB e um usuário com `cardapio.gerenciar`,
> **Quando** enviado ao produto,
> **Então** a resposta é 200, a coluna aponta para o arquivo e o arquivo existe no disco.

### CA-2 — Acima de 5 MB é recusado (`RN-VEN-042`)

> **Dado** um arquivo de 6 MB,
> **Quando** enviado,
> **Então** é recusado e a coluna não muda.

### CA-3 — Formato não aceito é recusado (`RN-VEN-042`)

> **Dado** um arquivo `.gif` ou `.svg`,
> **Quando** enviado,
> **Então** é recusado com 400.

### CA-4 — Extensão mentirosa é recusada pela assinatura (`RN-VEN-042`) ⚠

> **Dado** um arquivo de texto renomeado para `foto.jpg`, com `Content-Type: image/jpeg`,
> **Quando** enviado,
> **Então** é recusado — **a extensão e o `Content-Type` passaram; a assinatura não**.

### CA-5 — O nome enviado não vira nome no disco (`RN-VEN-042`)

> **Dado** um JPEG válido chamado `../../../etc/passwd.jpg`,
> **Quando** enviado,
> **Então** o arquivo gravado tem nome gerado pelo sistema e nada é escrito fora da raiz.

### CA-6 — O caminho contém a unidade (`RN-GER-009`)

> **Dado** dois restaurantes com imagem,
> **Quando** os caminhos forem comparados,
> **Então** cada um contém o `id` da sua unidade.

### CA-7 — Produto de outra unidade não recebe imagem (`RN-GER-009`)

> **Dado** o produto do restaurante A,
> **Quando** o restaurante B enviar imagem para ele,
> **Então** a resposta é 404.

### CA-8 — Sem `cardapio.gerenciar` não envia (`RN-GER-010`)

> **Dado** um usuário com perfil Cozinha,
> **Quando** tentar enviar imagem,
> **Então** a resposta é 403.

### CA-9 — Trocar a imagem preserva o arquivo anterior (`RN-GER-002`)

> **Dado** um produto com imagem,
> **Quando** uma imagem nova for enviada,
> **Então** a coluna aponta para a nova **e o arquivo anterior continua no disco**.

### CA-10 — O cardápio público devolve imagem e logo (`RN-VEN-037`)

> **Dado** um restaurante com logo e um produto com imagem,
> **Quando** o cardápio público for consultado **sem autenticação**,
> **Então** a resposta traz `urlLogo` e `urlImagem`, e **nenhum campo de custo**.

### CA-11 — Remover a imagem limpa a coluna e mantém o arquivo (`RN-GER-002`)

> **Dado** um produto com imagem,
> **Quando** a imagem for removida,
> **Então** a coluna fica nula e o arquivo permanece.

## 6. Modelo de dados tocado

```sql
-- produto.url_imagem JÁ EXISTE desde a migration Fundacao. Nada a criar.
ALTER TABLE unidade ADD COLUMN url_logo varchar(500);
```

**Migration:** `20260817195207_CpfSobrenomeELogo` — a mesma da `SPEC-GER-007`. O EF gera uma
migration por conjunto de mudanças pendentes do modelo; separar exigiria gerar, aplicar e
reeditar o modelo, por duas migrations em vez de um nome.

⚠ **Isto valia até 18/08/2026.** A ADR-0018 pôs o `bytea` no banco `grelhahub_arquivo`, com um
metadado na tabela `arquivo`. A coluna do produto **continua sendo texto** — ela guarda a
referência `/api/v1/arquivos/{id}` —, e é o que permite as referências antigas conviverem com as
novas sem migração obrigatória de dado.

## 7. Parâmetros consumidos

Nenhum. **O limite de 5 MB é literal de negócio e deveria ser parâmetro?** Não: `PAR-*` existe
para o que o restaurante configura. O teto de upload é restrição de infraestrutura, igual em
toda unidade, e vive em `ArquivoDeImagem.TamanhoMaximo` com o ID da regra no comentário.

## 8. Fora de escopo

- **Miniatura e redimensionamento.** Guardamos o que veio. Uma foto de 4 MB é baixada inteira
  no celular do cliente — dívida declarada no ADR-0015
- **Recorte na tela.** O restaurante envia a imagem já como quer
- **Múltiplas imagens por produto.** `RN-VEN-042` diz **uma**
- **Moderação de conteúdo.** Formato válido não é conteúdo apropriado. Sem moderação no MVP
- **Coleta de órfãos.** Rotina de manutenção, não efeito de requisição
- **CDN.** ADR-0015: uma VPS, Nginx servindo estático

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Value object | `src/GrelhaHub.Domain/Cardapio/ArquivoDeImagem.cs` |
| Porta | `src/GrelhaHub.Application/Common/Interfaces/IArmazenamentoDeArquivo.cs` |
| Implementação | `src/GrelhaHub.Infrastructure/Arquivos/ArmazenamentoEmDisco.cs` |
| Features | `src/GrelhaHub.Application/Features/Cardapio/Imagens/` |
| Migration | `.../Migrations/*_LogoDaUnidade.cs` |
| Nginx | `infra/nginx/default.conf` — `location /arquivos/` |
| Telas | `frontend/src/features/admin/Painel.tsx`, `frontend/src/features/publico/Cardapio.tsx` |
| Testes | `tests/GrelhaHub.Integration.Tests/Cardapio/ImagemTests.cs` |

## 10. Definição de pronto

- [x] Assinatura do arquivo conferida, não só extensão e `Content-Type`
- [x] Nome gerado pelo sistema; nome do usuário descartado
- [x] `unidadeId` no caminho
- [x] Troca de imagem preservando o arquivo anterior
- [x] Cardápio público devolvendo `urlImagem` e `urlLogo`, sem campo de custo
- [x] Limite de 5 MB recusado **antes** de o corpo ser lido — `[RequestSizeLimit]` na borda,
      com folga de 64 KB para o envelope `multipart`, e a checagem do handler como rede
- [x] CA-1 a CA-11 passando como teste de integração — **15 testes** em `ImagemTests`
- [x] `location /arquivos/` no Nginx, sem interpretador atrás
- [x] Volume `arquivos` no `docker-compose.yml` (escrita na API, **leitura** no Nginx) e comando
      de backup documentado junto da declaração
- [x] Rastreabilidade do catálogo §16 atualizada
- [x] `dotnet build -c Release`, `dotnet test`, `eslint` e `tsc` verdes — **174 testes**

## 11. O que saiu diferente da spec

### 11.1 `Produto.UrlImagem` já existia — e estava morta

A coluna estava no modelo desde a migration `Fundacao`: nunca gravada, nunca lida, ausente de todo
DTO e do frontend. Só `unidade.url_logo` precisou ser criada. **Coluna com tipo e sem caminho de
escrita nem de leitura é dívida silenciosa** — parece pronta e não faz nada.

### 11.2 O arquivo é materializado em `byte[]`, não passado como `Stream`

A assinatura precisa ser lida **antes** da gravação e o conteúdo gravado depois. Sobre um `Stream`
de requisição, que só avança, isso exigiria rebobinar o que não rebobina. Com o teto de 5 MB
recusado na borda **antes** de o corpo ser lido, materializar é seguro — e é o que torna a ordem
"conferir, depois gravar" possível sem buffer manual.

### 11.3 Não existe `RemoverAsync` na interface de armazenamento

`IArmazenamentoDeArquivo` tem `GravarAsync` e `Existe`. **A ausência é a decisão:** `RN-GER-002`
não permite apagar, e um método `RemoverAsync` na interface seria um convite permanente a
chamá-lo — inclusive por quem não leu a regra.

## 12. Verificação por mutação

| Mutação | Testes que caíram |
|---------|-------------------|
| Formato decidido pela **extensão** em vez da assinatura | 4 |
| Sem o limite de 5 MB no handler | 1 |
| `unidadeId` fora do caminho do arquivo | 1 |
| Nome do arquivo vindo do usuário | 1 |

Nenhuma passou. A primeira é a que importa: ela reproduz exatamente o erro que `RN-VEN-042` foi
escrita para impedir — confiar no que o cliente declara —, e quatro testes caem.
