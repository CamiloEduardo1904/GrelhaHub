# SPEC-GER-008 — Recuperação de acesso por e-mail

| Campo | Valor |
|-------|-------|
| ID | `SPEC-GER-008-recuperacao-de-acesso` |
| Módulo | GER |
| Situação | `ENTREGUE` — 18/08/2026 |
| Onda | 0 — fecha um beco aberto na `SPEC-GER-007` |
| Regras implementadas | `RN-GER-032`, `RN-GER-033`, `RN-GER-034`, `RN-GER-035`, `RF-SEG-005` *(revisita)* |
| ADRs aplicáveis | [ADR-0016](../adr/0016-envio-de-email-transacional.md), [ADR-0013](../adr/0013-acesso-de-plataforma.md) |
| Data | 18/08/2026 |

---

## 1. Objetivo

Permitir que quem esqueceu a senha volte a entrar **sem depender de outra pessoa**, e que quem
acaba de ser cadastrado receba um caminho para definir a própria senha.

**A motivação não é conveniência.** `RN-GER-028` resolvia o caso do restaurante — o administrador
redefine —, e acima do **acesso de plataforma não há ninguém**: a §8 da `SPEC-GER-007` registrou
ontem que quem perdesse aquela senha dependeria de acesso ao servidor, porque a carga inicial não
sobrescreve usuário existente. Esta spec fecha esse beco.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-GER-032` ⚠ | O envio não entra na transação; falha não desfaz a operação | Aplicação | Fila em memória + `BackgroundService` (ADR-0016) |
| `RN-GER-033` ⚠⚠ | Token opaco, uso único, validade curta, **guardado como hash** | Modelo + Aplicação | `token_acesso` com `UNIQUE` no hash |
| `RN-GER-034` ⚠ | A solicitação responde igual exista ou não a conta | Aplicação | `202` sem corpo, sempre |
| `RN-GER-035` | Senha escolhida pelo dono encerra a troca obrigatória | Domínio | `Usuario.TrocarSenha` |

## 3. Contrato

| Verbo | Rota | Descrição | Resposta |
|-------|------|-----------|----------|
| `POST` | `/api/v1/autenticacao/esqueci-minha-senha` | Pede o link | **`202`, sempre** |
| `POST` | `/api/v1/autenticacao/definir-senha` | Consome o link e define a senha | `204`, ou `422` |

**`202` e não `200`**: nada foi concluído — a resposta é "recebido; se houver conta, o e-mail sai".
E é a mesma para conta inexistente, usuário inativo e unidade inativa.

`422` na definição cobre **quatro** casos com **uma** mensagem — "Este link não é mais válido. Peça
um novo": inexistente, expirado, já usado e invalidado. Distinguir diria a um estranho que o token
existiu.

## 4. Comportamento

### O token — `RN-GER-033`

32 bytes aleatórios em Base64Url. O que vai para o banco é o **SHA-256** dele; o valor em claro
existe uma vez, na memória de quem gerou, e segue para o e-mail.

> **Por que hash determinístico aqui e hash com salt na senha.** Senha é escolhida por gente, tem
> pouca entropia e precisa resistir a força bruta sobre um banco vazado — daí o salt por registro.
> Token é aleatório, não se adivinha, e **precisa ser procurável**: o resgate recebe o token e tem
> de achar a linha. Salt por registro obrigaria a varrer a tabela comparando um a um.

| Propriedade | Como |
|-------------|------|
| Uso único | `dh_uso` preenchido no consumo |
| Validade | `PAR-GER-002` (60 min) para redefinição, `PAR-GER-003` (72 h) para convite |
| Emitir invalida os anteriores | `dh_invalidacao` nos vigentes do mesmo tipo |
| Sem `unidade_id` | **Exceção declarada a `RN-GER-009`** — o token é do usuário, e o de plataforma não tem unidade. Era exatamente quem mais precisava disto |

`dh_uso` e `dh_invalidacao` são colunas **separadas** de propósito: "o link que mandei não valeu
porque pedi outro" e "o link foi usado" são histórias diferentes para quem atende, e uma coluna só
perderia justamente a distinção que a pergunta do suporte precisa.

### O envio — `RN-GER-032`

`IEnviadorDeEmail.Enfileirar` **não é `async` e não devolve sucesso**, e a assinatura é o contrato:
quem chama não pode esperar o correio nem decidir com base nele. Uma assinatura `Task<bool>`
convidaria ao `await` dentro do handler — que é exatamente o acoplamento que a regra proíbe.

O e-mail é enfileirado **depois** do commit. Enfileirar antes seria prometer um link que uma falha
de gravação deixaria sem lastro.

### Onde o convite entra

Os três caminhos de criação de usuário emitem convite: `CriarUsuarioCommand` (restaurante),
`CriarUsuarioDePlataformaCommand` (plataforma cria para uma casa) e `CriarUsuarioDeSuporteCommand`
(plataforma cria colega).

⚠ **A partir de 18/08/2026 nenhuma das três pede senha — ver §12.** O convite deixou de ser um
extra e passou a ser o caminho: o usuário nasce com credencial aleatória e define a própria pelo
link. `senhaInicial` segue aceito para entrega presencial, e `RF-SEG-005` obriga a troca de
qualquer forma.

> ⚠ **A senha nunca viaja no e-mail.** O que viaja é o link. Senha em texto claro fica arquivada na
> caixa de quem recebeu — e em qualquer servidor pelo caminho.

### O link — de onde sai a base do endereço

De **configuração** (`Email__BaseDoSite`), nunca do cabeçalho `Host` da requisição. Montar o link
com o que o cliente mandou é o furo clássico da troca de senha: um `Host` forjado devolve um e-mail
legítimo, do nosso domínio, com um link que entrega o token a outro servidor.

## 5. Critérios de aceite

### CA-1 — Conta inexistente responde igual (`RN-GER-034`)

> **Dado** um e-mail que não pertence a ninguém,
> **Quando** o link for solicitado,
> **Então** a resposta é **202**, nenhum e-mail sai e nenhum token é criado.

### CA-2 — O link redefine a senha (`RN-GER-033`)

> **Dado** um usuário ativo que pediu o link,
> **Quando** ele definir a senha pelo link,
> **Então** a senha antiga deixa de autenticar e a nova autentica — e o banco guarda **hash**, não
> o token.

### CA-3 — Senha do dono encerra a pendência (`RN-GER-035`)

> **Dado** um usuário recém-criado, com troca pendente e convite recebido,
> **Quando** ele definir a senha pelo link,
> **Então** o login **não** pede troca.

### CA-4 — Uso único (`RN-GER-033`)

> **Dado** um link já usado,
> **Quando** for usado de novo,
> **Então** a resposta é **422** e a senha permanece a da primeira vez.

### CA-5 — Link novo invalida o anterior (`RN-GER-033`)

> **Dado** dois links pedidos em sequência,
> **Quando** o primeiro for usado,
> **Então** ele é recusado, e o segundo funciona.

### CA-6 — Link vencido (`RN-GER-033`)

> **Dado** um link cuja validade passou,
> **Quando** for usado,
> **Então** é recusado com a **mesma** mensagem dos demais casos.

### CA-7 — Usuário inativo (`RN-GER-034`)

> **Dado** um usuário inativo,
> **Quando** pedir o link,
> **Então** a resposta é **202** e nenhum e-mail é enviado a ele.

### CA-8 — O token é gravado antes do envio (`RN-GER-032`)

> **Dado** uma solicitação de link,
> **Quando** ela terminar,
> **Então** o token está no banco, vigente — independentemente do que aconteça com o correio.

## 6. Modelo de dados tocado

`token_acesso` — `usuario_id`, `tipo`, `hash_token` (**único**), `dh_expiracao`, `dh_criacao`,
`dh_uso`, `dh_invalidacao`. Migration `TokenDeAcessoEEmail`.

## 7. Parâmetros consumidos

`PAR-GER-002` (60 min) e `PAR-GER-003` (72 h). ⚠ **Hoje são constantes no código**, nomeando o
parâmetro que copiam: `parametro_valor` tem escopo de **unidade** (`SPEC-GER-003`), e este fluxo é
anônimo — no instante da solicitação não se sabe de que unidade é a pessoa, e para o usuário de
plataforma não há unidade nenhuma. Parâmetro de escopo global é a dívida registrada.

## 8. Fora de escopo

- **Retentativa de envio** — ADR-0016: todo e-mail nosso é reemitível; o usuário pede outro link
- **Confirmação de e-mail no cadastro** — o e-mail é dado por quem cria a conta, não auto-declarado
- **Limite de solicitações por e-mail** ⚠ — hoje nada impede pedir mil links seguidos. Cada pedido
  invalida o anterior, então não há acúmulo de token válido, mas há envio. Um teto por janela de
  tempo é regra nova, e regra nova vem antes do código
- **Faxina dos tokens vencidos** — a tabela cresce. No volume previsto, décadas para incomodar

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Entidade | `src/GrelhaHub.Domain/Gerais/TokenDeAcesso.cs` |
| Feature | `src/GrelhaHub.Application/Features/Gerais/Acesso/RecuperacaoDeAcesso.cs` |
| Correio | `src/GrelhaHub.Infrastructure/Email/` |
| Migration | `.../Migrations/*_TokenDeAcessoEEmail.cs` |
| Telas | `frontend/src/features/admin/RecuperarAcesso.tsx` |
| Testes | `tests/GrelhaHub.Integration.Tests/Gerais/RecuperacaoDeAcessoTests.cs` |

## 10. Definição de pronto

- [x] `token_acesso` com hash único, validade, consumo e invalidação
- [x] Envio fora da transação, por fila em memória (ADR-0016)
- [x] Resposta uniforme na solicitação (`RN-GER-034`)
- [x] Convite emitido nos **três** caminhos de criação de usuário
- [x] Telas `/esqueci-minha-senha`, `/definir-senha` e `/redefinir-senha`, com a porta no login
- [x] CA-1 a CA-8 como teste de integração — 8 testes em `RecuperacaoDeAcessoTests`
- [x] `dotnet build -c Release`, `dotnet test`, `eslint` e `tsc` verdes

## 11. O que saiu diferente da spec

### 11.1 O correio é o primeiro dublê da suíte de integração

A doutrina do harness é **nada de dublê** (ADR-0009). Aqui há um: `CaixaDeSaidaDeTeste` substitui
`IEnviadorDeEmail`. A alternativa seria subir um servidor SMTP no contêiner para provar regras que
não são sobre SMTP — e o que os testes precisam do e-mail é o **conteúdo**: o link com o token, que
**só existe na mensagem**, porque o banco guarda apenas o hash. A propriedade que `RN-GER-033`
garante é justamente a que impede o teste de espiar pelo banco.

O caminho continua real até a fila: handler, token, gravação e consumo são os de produção.

### 11.2 A caixa de saída é compartilhada, e a primeira asserção estava errada

`CA-1` afirmava "a caixa está vazia". Passava sozinha e falhava na suíte: a caixa é estática, e o
que ela continha era o e-mail de **outro** teste. A asserção certa é sobre o **destinatário** —
"não há mensagem para este endereço" —, que é o que a regra realmente diz.

É o mesmo erro de escopo que `SPEC-VEN-003` §11.7 registrou com o `Guid?` na árvore de expressão:
teste que passa isolado e falha em conjunto.

## 12. Criar usuário deixou de pedir senha — 18/08/2026

**Decisão do dono do produto:** criar qualquer usuário dispara o **convite por e-mail**; substituir
senha é operação de **edição**. Os dois fluxos passam a existir com papéis distintos:

| Fluxo | O que acontece | Pendência de `RF-SEG-005` |
|-------|----------------|----------------------------|
| **Criar** (`POST /usuarios`, as duas rotas de plataforma) | Sem senha no corpo. O usuário nasce com credencial **aleatória que ninguém conhece** e recebe o convite | Cai quando ele define a própria pelo link (`RN-GER-035`) |
| **Editar → redefinir senha** (`POST /usuarios/{id}/senha`) | O administrador escolhe a senha e a entrega | **Reativa** (`RN-GER-028`): a senha que um terceiro conhece não é senha |

⚠ **Credencial aleatória, e não senha em branco nem senha padrão.** As duas seriam porta aberta
enquanto o convite não fosse usado. O teste `CriadoSemSenha_DefinePeloConvite` tenta entrar com
vazio, `12345678`, `senha` e o próprio e-mail antes de usar o link.

`senhaInicial` **continua sendo aceito** e é o caso de entrega presencial — o operador contratado na
hora, sem e-mail próprio. Quem informa assume que duas pessoas conhecem a senha, e `RF-SEG-005`
obriga a troca no primeiro acesso de qualquer forma.

## 13. ⚠⚠ O defeito relatado: "dá erro ao trocar a senha mesmo alterando certo"

Investigado com um teste que percorre a sequência inteira — criar, entrar com a inicial, trocar,
entrar com a nova — nos **três** caminhos de criação. **A API estava correta em todos.** O defeito
eram dois, e os dois no cliente:

**1. `401` onde devia ser `422`.** Senha atual incorreta lançava `NaoAutorizadoException` → 401. O
interceptor do axios trata 401 como sessão expirada: limpa o token e faz `location.href = '/entrar'`.
Resultado — **quem errava um caractere era expulso da tela de troca sem ler a mensagem**, e voltava
achando que a troca havia falhado. Quem já está autenticado não tem falta de autenticação: o que
falhou foi o valor digitado, que é regra de negócio. É a mesma correção que a `SPEC-GER-007` §11.2
fez com o 401 que devia ser 403.

**2. O interceptor não distinguia os dois sentidos de 401.** Mesmo com o status certo, um 401 vindo
de `/autenticacao/login` — senha errada na reautenticação — destruiria a tela do mesmo jeito. As
rotas de credencial (`login`, `minha-senha`, `definir-senha`, `esqueci-minha-senha`) passaram a ser
exceção explícita: ali o 401 é **resposta da operação**, não sessão morta.

> **Os dois defeitos eram invisíveis para o backend.** Nenhum teste de API os pegaria: o servidor
> respondia exatamente o que devia responder. O que estava errado era o significado que o cliente
> dava à resposta — e é por isso que o registro está aqui, e não só no comentário do código.
