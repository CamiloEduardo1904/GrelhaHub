# SPEC-GER-007 — Pessoas, delegação de acesso e telas de administração

| Campo | Valor |
|-------|-------|
| ID | `SPEC-GER-007-pessoas-e-delegacao-de-acesso` |
| Módulo | GER |
| Situação | `ENTREGUE` — 17/08/2026 |
| Onda | 0 — fundação (complemento) |
| Regras implementadas | `RN-GER-023`, `RN-GER-024`, `RN-GER-025`, `RN-GER-013`, `RN-GER-010` |
| ADRs aplicáveis | [ADR-0013](../adr/0013-acesso-de-plataforma.md), [ADR-0014](../adr/0014-modularidade-comercial.md) |
| Depende de | `SPEC-GER-004`, `SPEC-GER-005`, `SPEC-GER-006` |
| Data | 17/08/2026 |
| Entregue em | 17/08/2026 |

---

## 1. Objetivo

Fechar a lacuna entre **ter perfis** e **conseguir usar perfis**. A `SPEC-GER-004` entregou
seis perfis por unidade e a API de usuário; o produto nasceu com **um usuário só**, e nenhuma
tela para criar os outros. Esta spec entrega:

1. A pessoa **identificada** — nome, sobrenome e CPF (`RN-GER-023`)
2. A **matriz de delegação**, que impede escalação de privilégio (`RN-GER-024`)
3. As telas: usuários da unidade (Administrador e Gerente) e administração de plataforma

> **Esta spec nasceu de um defeito.** O pedido era "o gerente cadastra operadores". Conceder
> `usuario.gerenciar` ao Gerente resolveria o pedido e abriria escalação: ele criaria um
> usuário Administrador e passaria a ter o acesso que não lhe foi dado. A regra que faltava é
> `RN-GER-024`, e ela foi escrita antes desta spec.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-GER-023` | Usuário tem nome, sobrenome e CPF único, validado por dígito verificador | Modelo + Banco + Aplicação | Value object `Cpf` + `UNIQUE` + validator |
| `RN-GER-024` ⚠⚠ | Ninguém concede perfil que a matriz de delegação não autoriza | Aplicação | `MatrizDeDelegacao` + validação na partida |
| `RN-GER-025` | Gerente administra os usuários da própria unidade | Aplicação | `usuario.gerenciar` no perfil Gerente |
| `RN-GER-013` | Documento sem máscara | Aplicação | Só dígitos entram no banco; a máscara é da tela |
| `RN-GER-010` | Permissão granular | Aplicação | `[ExigePermissao]` |
| `RN-GER-030` | A plataforma cria e lista os seus próprios usuários, sem unidade e sem perfil | Aplicação | `CriarUsuarioDeSuporteCommand` + isenção declarada no `SessaoDeSuporteBehavior` |
| `RN-GER-031` ⚠ | Não se inativa a si mesmo nem o último acesso de plataforma ativo | Aplicação | Contagem na mesma transação da escrita |

## 3. Contrato

### `CadastrarRestauranteCommand` — campos novos

| Entrada | Tipo | Obrigatório | Observação |
|---------|------|:-----------:|------------|
| `nomeAdministrador` | `string` | ✔ | Só o primeiro nome agora |
| `sobrenomeAdministrador` | `string` | ✔ | **Novo** |
| `cpfAdministrador` | `string` | ✔ | **Novo.** Com ou sem máscara; grava 11 dígitos |

### `CriarUsuarioCommand`

| Entrada | Tipo | Obrigatório | Observação |
|---------|------|:-----------:|------------|
| `nome` | `string` | ✔ | |
| `sobrenome` | `string` | ✔ | **Novo** |
| `cpf` | `string` | ✔ | **Novo** |
| `email` | `string` | ✔ | Único em todo o sistema |
| `perfilId` | `uuid` | ✔ | **Sujeito à matriz de delegação** |
| `senhaInicial` | `string` | ✔ | Nasce com `deveTrocarSenha = true` |

### Erros

| Situação | Código | Mensagem |
|----------|--------|----------|
| CPF inválido | 400 | "O CPF informado não é válido." |
| CPF já usado | **422** | "Já existe um usuário com este CPF." |
| E-mail já usado | **422** | "Já existe um usuário com este e-mail." |
| Perfil fora da delegação | 403 | "RN-GER-024 — o seu perfil não pode atribuir o perfil {nome}." |

> **409 virou 422, e a spec estava errada.** `RegraDeNegocioException` é a convenção do projeto
> para regra de negócio violada e já mapeava para 422 — é o que o CNPJ duplicado devolvia desde
> `SPEC-VEN-001`. Mudar o mapeamento só por causa desta regra tornaria a API incoerente consigo
> mesma: dois conflitos idênticos com códigos diferentes conforme quem escreveu a spec.

### Autorização

| Operação | Permissão | Quem tem |
|----------|-----------|----------|
| Listar usuários | `usuario.visualizar` | Administrador, Gerente |
| Criar, desativar | `usuario.gerenciar` | Administrador, **Gerente (novo)** |
| Módulos da unidade | — | **Somente plataforma** (`RN-GER-017`, ADR-0014) |
| Cadastrar estabelecimento | anônimo **ou** plataforma | Ambos — ver §4 |

## 4. Comportamento

### CPF — `RN-GER-023`

Value object `Cpf`, no domínio, pelo mesmo motivo de `Dinheiro` e `Quantidade`
(`SPEC-GER-001` §4): `string` crua não distingue CPF de CNPJ de telefone, e o compilador não
impede passar um no lugar do outro.

- Construção só por `Cpf.De(texto)`; aceita com ou sem máscara e guarda **11 dígitos**
- **Dois dígitos verificadores conferidos.** CPF com dígito errado é recusado
- **Sequência repetida é recusada** — `111.111.111-11` passa no cálculo dos verificadores e
  não é CPF de ninguém. Validar só a conta deixaria entrar onze CPFs falsos
- Único em todo o sistema, não por unidade: **exceção declarada a `RN-GER-009`**, pela mesma
  razão do e-mail em `SPEC-GER-004` — a pessoa é a mesma pessoa em qualquer unidade

**A coluna é nulável, e é decisão, não descuido:** o usuário de plataforma (`RN-GER-014`) não
tem CPF, e os usuários criados antes desta spec não têm. `NOT NULL` exigiria inventar um CPF
para eles, o que é pior do que registrar a ausência.

### Matriz de delegação — `RN-GER-024`

```
Administrador → Administrador, Gerente, Atendente, Cozinha, Operador, Entregador
Gerente       → Atendente, Cozinha, Operador, Entregador
demais        → (nenhum)
```

O time escolheu **lista fixa**, e não a regra geral de contenção de permissões
(`permissões(alvo) ⊆ permissões(criador)`). A lista é mais simples de explicar e de defender.

**O custo aceito e como ele foi neutralizado:** perfil novo exige acrescentar linha na matriz,
e esquecer disso seria silencioso — o perfil novo simplesmente nunca poderia ser atribuído por
ninguém, ou pior, seria atribuível por engano. Por isso a matriz é **validada na partida**:
`ValidadorDeDelegacao` compara a matriz com `CatalogoDeAcesso.Perfis` e **recusa o boot** se
algum perfil não estiver declarado como delegável ou explicitamente como não-delegável. Mesmo
mecanismo de `ValidadorDeDeclaracaoDeModulo` (`SPEC-GER-006`).

> A validação da matriz é do lado do **servidor**. A tela esconde o que não pode ser atribuído,
> mas esconder não é impedir: o `<select>` é do cliente.

### Uma porta para cadastrar estabelecimento — revisado no mesmo dia

A primeira versão desta spec registrava **duas** portas, auto-serviço e assistida, por escolha do
time. **Revertido horas depois, pelo dono do produto:**

> *"Não faz sentido a tela de criar restaurante sendo que o cliente não assinou nada."*

E está certo. A porta anônima criava **um cliente inteiro** — unidade, seis perfis, nove
parâmetros, módulos, cardápio de exemplo e um administrador com acesso — sem nada assinado do
outro lado. Um `POST` público bastava.

| Porta | Quem | Rota | Situação |
|-------|------|------|----------|
| ~~Auto-serviço~~ | ~~Anônimo, pelo site~~ | ~~`POST /api/v1/autenticacao/cadastrar-restaurante`~~ | **Removida** |
| Assistida | Usuário de plataforma | `POST /api/v1/plataforma/estabelecimentos` | Única |

**O que foi removido, e o que ficou.** A rota anônima, o DTO público que a protegia do campo
`Modulos` e a tela `/cadastrar` saíram. O handler `CadastrarRestauranteCommand` **ficou**: ele é o
caminho único de criação de unidade, e reabrir o auto-serviço um dia é acrescentar um controller,
não reescrever a criação.

**O harness de integração acompanhou.** `CadastrarRestauranteAsync` passou a autenticar como
plataforma e criar pela rota assistida. Manter o atalho anônimo no teste contrariaria a própria
doutrina do harness — *o cenário se monta pelas rotas que o sistema tem* —, e testaria uma criação
que não existe mais.

**Duas asserções guardam a decisão**, porque "removi o botão da tela" e "fechei a porta" são
coisas diferentes: uma confere que a rota anônima devolve **404**, outra que um administrador de
restaurante recebe **403** na rota de plataforma. Sem elas, a rota continuaria aceitando `POST` de
quem a conhecesse.

### Senha de usuário criado por outra pessoa

Continua valendo o que a `SPEC-GER-004` decidiu: usuário criado **por** alguém nasce com
`deveTrocarSenha = true`, porque a senha inicial é conhecida por quem não vai usá-la. O admin
fundador, que escolheu a própria senha no cadastro, nasce com `false`.

### A equipe da plataforma — `RN-GER-030`, `RN-GER-031`

`RN-GER-016` sempre disse que usuário de plataforma é criado "por outro usuário de plataforma
**ou** pela carga inicial". **Só a carga inicial existia.** A lacuna apareceu do pior jeito
possível: a instalação de produção subiu sem nenhum usuário de plataforma — as variáveis
`Plataforma__*` não eram repassadas pelo `docker-compose.yml` —, e não havia porta na interface.
A recuperação exigiu acesso ao servidor.

| Rota | O que faz |
|------|-----------|
| `GET /api/v1/plataforma/usuarios` | Lista os usuários de plataforma |
| `POST /api/v1/plataforma/usuarios` | Cria outro — `nome`, `email`, `senhaInicial` |
| `POST /api/v1/plataforma/usuarios/{id}/situacao` | Inativa ou reativa |

**Três campos, e as ausências são o conteúdo da regra.** Sem `unidadeId` porque ele não pertence a
restaurante nenhum (`RN-GER-014`); sem `perfilNome` porque perfil é de unidade (`RN-GER-010`); sem
`cpf` pela exceção que `RN-GER-023` já declarava. O usuário nasce com `deveTrocarSenha = true`
(`RF-SEG-005`) — são duas pessoas conhecendo a senha, o que não acontece na carga inicial, em que a
senha vem do `.env` e não foi entregue a ninguém.

#### Por que **não** exige sessão de suporte

`RN-GER-015` obriga sessão aberta com motivo antes de qualquer escrita de plataforma, e a isenção
aqui segue a linha divisória que o `SessaoDeSuporteBehavior` já documenta — **comercial × suporte**,
com o teste sendo "há dado de cliente sendo tocado?". Aqui não há cliente algum. E há um argumento
que fecha a questão: a sessão é aberta **para uma unidade**. Exigi-la obrigaria a escolher um
restaurante ao acaso e registrar um motivo de suporte falso na conta dele — menos rastreável, não
mais. A autoria continua na trilha (`SPEC-GER-005`).

#### ⚠ O deadlock que a troca de senha obrigatória criava

Ao pôr `deveTrocarSenha = true` no usuário novo, a conta nasceria **inutilizável**:

1. `TrocaDeSenhaBehavior` libera **uma** operação com a troca pendente: `TrocarSenhaCommand`
2. `SessaoDeSuporteBehavior` exigia sessão de suporte para **todo** `Command` de plataforma — e
   `TrocarSenhaCommand` é um deles
3. Abrir a sessão exige `AbrirSessaoDeSuporteCommand`, que a troca pendente bloqueia

Círculo fechado, sem nenhuma operação possível e sem mensagem que o explicasse. `TrocarSenhaCommand`
entrou na lista de isenções: trocar a **própria** senha não é escrita sobre dado de unidade.

> Era **latente** antes desta spec — nenhum caminho criava usuário de plataforma com a troca
> pendente. Virou alcançável no mesmo commit que abriu a porta, e é o quinto caso da série "regra
> que recusa a operação sem dizer para onde ir".

#### O último acesso — `RN-GER-031`

Duas recusas, e a segunda não é redundante:

| Caso | Por quê |
|------|---------|
| Inativar a si mesmo | Perde a sessão no request seguinte e não tem como desfazer sozinho |
| Inativar o **último ativo** | Não há desfazer por caminho nenhum: a interface não recria acesso de plataforma, e a carga inicial **não sobrescreve** usuário existente |

⚠ O segundo caso é alcançável de verdade, e o teste percorre o caminho real: `Ativo` é conferido no
**login** (`RN-RH-006`) e em requisição nenhuma depois — o token de quem acabou de ser inativado
continua valendo até expirar. Com ele, o inativado desligaria o único ativo que restou.

A contagem é feita **no banco, na mesma transação** da escrita. Contar antes e decidir depois é uma
corrida entre dois usuários de plataforma se inativando no mesmo instante, com as duas operações
"bem-sucedidas" e zero acessos de suporte no fim.

## 5. Critérios de aceite

### CA-1 — CPF inválido é recusado (`RN-GER-023`)

> **Dado** um cadastro com CPF de dígito verificador errado,
> **Quando** enviado,
> **Então** a resposta é 400 e nenhum usuário é criado.

### CA-2 — CPF de sequência repetida é recusado (`RN-GER-023`)

> **Dado** o CPF `111.111.111-11`,
> **Quando** enviado,
> **Então** é recusado, **ainda que o cálculo dos verificadores feche**.

### CA-3 — CPF é gravado sem máscara (`RN-GER-013`)

> **Dado** o CPF enviado como `529.982.247-25`,
> **Quando** o usuário for gravado,
> **Então** a coluna contém `52998224725`.

### CA-4 — CPF é único em todo o sistema (`RN-GER-023`)

> **Dado** um CPF já usado em **outra unidade**,
> **Quando** um segundo usuário for criado com ele,
> **Então** a resposta é 409.

### CA-5 — Gerente cria Operador (`RN-GER-025`)

> **Dado** um usuário com perfil Gerente,
> **Quando** criar um usuário com perfil Operador,
> **Então** o usuário é criado na **unidade do gerente**, com `deveTrocarSenha = true`.

### CA-6 — Gerente **não** cria Administrador (`RN-GER-024`) ⚠⚠

> **Dado** um usuário com perfil Gerente,
> **Quando** tentar criar um usuário com perfil Administrador,
> **Então** a resposta é 403 citando `RN-GER-024`, e nenhum usuário é criado.

### CA-7 — Gerente não cria outro Gerente (`RN-GER-024`)

> **Dado** um usuário com perfil Gerente,
> **Quando** tentar criar um usuário com perfil Gerente,
> **Então** a resposta é 403 — a matriz não inclui Gerente na delegação do Gerente.

### CA-8 — A matriz cobre todo perfil do catálogo (`RN-GER-024`)

> **Dado** um perfil acrescentado a `CatalogoDeAcesso.Perfis` e ausente da matriz,
> **Quando** a aplicação subir,
> **Então** ela **recusa a partida**, nomeando o perfil que falta.

### CA-9 — Gerente não atravessa unidade (`RN-GER-009`)

> **Dado** dois restaurantes,
> **Quando** o gerente de um listar usuários,
> **Então** vê apenas os da própria unidade.

### CA-10 — Somente plataforma altera módulo (`RN-GER-017`)

> **Dado** um usuário Administrador **de restaurante**,
> **Quando** tentar habilitar um módulo,
> **Então** a resposta é 403.

### CA-11 — Plataforma cadastra estabelecimento com módulos

> **Dado** o usuário de plataforma,
> **Quando** cadastrar um estabelecimento informando os módulos vendidos,
> **Então** a unidade nasce com perfis, parâmetros, admin fundador e **os módulos informados**.

### CA-12 — A criação de usuário entra na trilha (`RN-GER-011`)

> **Dado** um usuário criado pelo gerente,
> **Quando** a trilha for consultada,
> **Então** existe registro de criação com o autor, e **sem o hash de senha** (`SPEC-GER-005`
> CA-6).

### CA-13 — Plataforma cria outro usuário de plataforma (`RN-GER-030`)

> **Dado** um usuário de plataforma autenticado,
> **Quando** ele criar um acesso com nome, e-mail e senha inicial,
> **Então** o usuário nasce **sem unidade e sem perfil**, autentica com `ehPlataforma`, e chega com
> a troca de senha **pendente** (`RF-SEG-005`).

### CA-14 — Criar não exige sessão de suporte; alterar cliente continua exigindo (`RN-GER-030`)

> **Dado** nenhuma sessão de suporte aberta,
> **Quando** a plataforma criar um usuário de plataforma **e** tentar alterar o cadastro de um
> estabelecimento,
> **Então** a criação passa e a alteração é recusada com **403**.

*É o par que importa: sozinha, a criação bem-sucedida não distingue "isenção declarada" de "gate
desligado por acidente".*

### CA-15 — Visível para a plataforma, invisível para o restaurante (`RN-GER-030`, `RN-GER-016`)

> **Dado** um usuário de plataforma recém-criado,
> **Quando** a plataforma listar `/plataforma/usuarios` e o restaurante listar `/usuarios`,
> **Então** ele aparece na primeira e **não** aparece na segunda.

### CA-16 — Ninguém inativa o próprio acesso (`RN-GER-031`)

> **Dado** o usuário de plataforma autenticado,
> **Quando** ele tentar inativar a si mesmo,
> **Então** a operação é recusada com **422** e ele continua ativo.

### CA-17 — O último acesso ativo não pode ser inativado (`RN-GER-031`) ⚠

> **Dado** dois usuários de plataforma, um deles já inativado — **com o token ainda válido**, porque
> `Ativo` só é conferido no login,
> **Quando** o inativado tentar inativar o único que restou ativo,
> **Então** a operação é recusada com **422** e a instalação continua com um acesso de suporte.

### CA-17b — Inativar não apaga (`RN-GER-031`, `RN-GER-002`)

> **Dado** um usuário de plataforma inativado, que já não autentica,
> **Quando** ele for reativado,
> **Então** ele autentica de novo com a mesma senha — nada foi restaurado porque nada foi removido.

### CA-18 — O usuário novo consegue trocar a senha (`RF-SEG-005`) ⚠

> **Dado** um usuário de plataforma recém-criado, com a troca pendente,
> **Quando** ele trocar a própria senha e reautenticar,
> **Então** a operação responde **204** e o token novo opera normalmente.

*Sem a isenção de `TrocarSenhaCommand` no `SessaoDeSuporteBehavior`, este caso responde 403 e a
conta nasce inutilizável — ver §4.*

### CA-19 — Restaurante não cria usuário de plataforma (`RN-GER-016`)

> **Dado** o administrador de um restaurante,
> **Quando** ele chamar `POST /plataforma/usuarios`,
> **Então** a resposta é **403**.

## 6. Modelo de dados tocado

```sql
ALTER TABLE usuario ADD COLUMN sobrenome varchar(200);
ALTER TABLE usuario ADD COLUMN cpf       char(11);

-- Único em todo o sistema. NULL não conflita com NULL no PostgreSQL, então a plataforma
-- e os usuários anteriores a esta spec convivem com a restrição.
CREATE UNIQUE INDEX ux_usuario_cpf ON usuario (cpf);

ALTER TABLE usuario ADD CONSTRAINT ck_usuario_cpf
    CHECK (cpf IS NULL OR cpf ~ '^[0-9]{11}$');
```

`CHECK` valida **domínio** (onze dígitos), nunca **política** (dígito verificador): a conta do
verificador é regra de negócio e vive no value object, onde a mensagem de erro é legível.
`sobrenome` é nulável pelo mesmo motivo do CPF — o usuário de plataforma tem um nome só.

**Migration:** `PessoaEDelegacao`.

## 7. Parâmetros consumidos

Nenhum.

## 8. Fora de escopo

- **Alterar o próprio cadastro** (nome, CPF) — só criação e desativação nesta spec
- **Redefinir senha de um usuário de PLATAFORMA** — ⚠ consequência a decidir: quem perder a senha do acesso de plataforma não tem recuperação por caminho nenhum, porque a carga inicial não sobrescreve usuário existente. Outro usuário de plataforma pode inativá-lo e criar um acesso novo (`RN-GER-030`), o que resolve na prática e deixa o rastro certo — mas a redefinição em si é regra que ainda não existe no catálogo
- Convite por e-mail: exige serviço de envio, que não existe
- Perfil personalizado pela unidade — os seis do catálogo bastam até alguém pedir o sétimo
- **Colaborador** (`RN-RH-006`): usuário é acesso, colaborador é vínculo. Onda 10

### CPF em listagem — `RN-GER-039`, acrescentado em 19/08/2026

Uma revisão externa de design encontrou o CPF **completo e sem máscara** em duas tabelas: a equipe
de uma loja e, pior, `/plataforma > Usuários`, que lista os usuários de **todas** as casas. Numa
base com centenas de lojas, essa segunda tela é um cadastro consolidado de pessoas físicas de
cliente numa única rolagem.

A tela passa a mostrar `•••.752.818-••`, com revelação por ação explícita numa linha
(`<CpfNaListagem />`).

#### ⚠⚠ O que isto NÃO é, e a frase precisa existir

**Reduz exposição de tela, não acesso.** O número continua vindo na resposta da API, e quem abre o
inspetor o lê. Chamar a máscara de proteção sem dizer isso seria exatamente o cadeado que a
`CHAPA-05` recusa: desenho que sugere que o dado foi retido no cliente quando ele chegou inteiro.

A diferença com `RN-GER-012` é a que importa aqui:

| | `RN-GER-012` (custo, CMV, margem) | `RN-GER-039` (CPF) |
|---|---|---|
| A API devolve? | **Não**, para quem não tem permissão | **Sim** — quem olha tem direito ao dado |
| Na tela | O campo **não existe** (`CHAPA-05`) | Existe, mascarado, e acende sob ação |
| O que se controla | Acesso | Quando o número aparece |

#### Dívida declarada

A minimização de verdade é a **listagem parar de devolver o CPF**, e ela não foi feita. Exige
separar duas leituras que hoje são uma: a da tabela (que não precisa do número) e a que alimenta o
formulário de edição (que precisa, por `RN-GER-028`). Enquanto forem o mesmo DTO, a tabela carrega
o dado do formulário.

⚠ Também não há registro de **quem revelou o quê**. A revisão sugeriu isso e é o complemento
natural de `RN-GER-015`, mas trilha de leitura de campo é escopo próprio — e prometê-la sem
implementar seria pior que a ausência.

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Value object | `src/GrelhaHub.Domain/Common/Cpf.cs` |
| Matriz | `src/GrelhaHub.Domain/Gerais/MatrizDeDelegacao.cs` |
| Validação na partida | `src/GrelhaHub.Application/Common/Modulos/ValidadorDeDelegacao.cs` |
| Feature | `src/GrelhaHub.Application/Features/Gerais/Usuarios/Usuarios.cs` |
| Plataforma | `src/GrelhaHub.Application/Features/Gerais/Plataforma/` |
| Migration | `.../Migrations/*_PessoaEDelegacao.cs` |
| Telas | `frontend/src/features/admin/Usuarios.tsx`, `frontend/src/features/plataforma/` |
| Testes | `tests/GrelhaHub.Integration.Tests/Gerais/DelegacaoTests.cs`, `tests/GrelhaHub.Domain.Tests/Common/CpfTests.cs`, `tests/GrelhaHub.Integration.Tests/Gerais/UsuariosDePlataformaTests.cs` |

## 10. Definição de pronto

- [x] `Cpf` com verificador e recusa de sequência repetida — **23 testes de domínio**
- [x] Matriz de delegação imposta no servidor, não só escondida na tela
- [x] Validação na partida recusando perfil ausente da matriz
- [x] Gerente com `usuario.gerenciar`, limitado pela matriz
- [x] Tela de usuários no painel (aba **Equipe**), com máscara de CPF
- [x] Tela de plataforma em `/plataforma`: estabelecimentos, módulos e usuários
- [x] CA-1 a CA-12 passando como teste de integração — 11 testes em `DelegacaoTests`
- [x] CA-13 a CA-19 — 7 testes em `UsuariosDePlataformaTests` (`RN-GER-030`, `RN-GER-031`)
- [x] Aba **Equipe** em `/plataforma`: criar, listar, inativar e reativar acesso de plataforma
- [x] Rastreabilidade do catálogo §16 atualizada
- [x] `dotnet build -c Release`, `dotnet test`, `eslint` e `tsc` verdes — **174 testes** na
      entrega; **190** depois de fechar a §11.5 (`RN-GER-020` na criação)

## 11. O que saiu diferente da spec, e por quê

### 11.1 Nasceu um endpoint que a spec não previa: `GET /api/v1/usuarios/perfis`

A primeira versão da tela espelhava a `MatrizDeDelegacao` numa constante do TypeScript e derivava
o `perfilId` dos usuários já cadastrados. **Duas coisas erradas.** A cópia da matriz divergiria da
do C# na primeira mudança, e a divergência apareceria ao usuário como "o sistema me ofereceu e
depois recusou". Pior: numa unidade **recém-criada só existe o administrador**, então não havia de
onde tirar o `perfilId` de Operador — e o gerente nunca conseguiria criar o primeiro operador, que
é exatamente o caso de uso que esta spec existe para entregar.

O endpoint devolve os perfis da unidade com `delegavel` calculado **pelo domínio**. A tela filtra
por ele; o servidor continua recusando com 403.

### 11.2 Um defeito anterior encontrado: 401 onde devia ser 403

`NaoAutorizadoException` conflatava duas coisas e mapeava as duas para **401**:

| Caso | 401 está… |
|------|-----------|
| Login errado, sessão inválida, senha atual incorreta | **certo** — falta autenticação |
| Autenticado, mas não é plataforma | **errado** — autenticação não é o que falta |

O CA-10 caiu com 401 quando esperava 403, e a causa não era o teste. Um 401 faz a interface
mandar o usuário fazer login de novo, o que não resolve nada. Criada
`ExclusivoDaPlataformaException` → **403**, aplicada às quatro checagens de plataforma
(`AdministrarModulos`, `AdministrarEstabelecimentos`, e as duas de `SessaoDeSuporte`). O mapeamento
de `NaoAutorizadoException` **não** mudou: para login, 401 continua correto.

### 11.3 A rota anônima recebeu um DTO próprio

`CadastrarRestauranteCommand` ganhou o campo `Modulos` para a porta assistida. Exposto na rota
anônima, um cadastro qualquer se autoprovisionaria os dez módulos — e `RN-GER-017` viraria enfeite.
`CadastrarRestaurantePublicoRequest` existe para que o campo **não seja alcançável** de fora.
Filtrá-lo no handler dependeria de o handler saber por qual porta a chamada veio, e ele não sabe.

### 11.4 Migration única, e não duas

`20260817195207_CpfSobrenomeELogo` cobre esta spec e a `SPEC-VEN-002`. O EF gera uma migration por
conjunto de mudanças pendentes; separar exigiria gerar, aplicar e reeditar o modelo — duas
migrations pelo benefício de um nome.

### 11.5 `RN-GER-020` na criação — **fechada em 17/08/2026**

> **A recomendação original desta seção estava errada, e o registro fica.** Eu havia escrito
> "precisa ser fechado na onda 3, quando EST existir". **Não precisava esperar nada:** o grafo de
> dependência (`modulo_dependencia`) já estava semeado com as **nove arestas dos dez módulos** desde
> a `SPEC-GER-006`, e a lógica de imposição já existia em `AlterarModuloDaUnidadeCommand`. O que
> faltava era **chamar** a regra no caminho de criação. Confundi *quando o defeito se torna
> alcançável* com *quando ele pode ser corrigido*.

A regra passou a viver em **um lugar só** — `FechoDeDependencia`, na camada de aplicação —, e os
dois caminhos a chamam:

| Caminho | Conjunto avaliado |
|---------|-------------------|
| `AlterarModuloDaUnidadeCommand` | o que está habilitado **mais** o módulo sendo ligado |
| `CadastrarRestauranteCommand` (porta assistida) | a lista de módulos vendidos |

A pergunta é a mesma nos dois: **este conjunto é fechado sob dependência?** Habilitar um módulo é
o caso particular. Deixar cada handler com a sua cópia era o modo de falha que o ADR-0009 nomeia —
duas implementações da mesma regra divergindo em silêncio.

**Duas decisões de comportamento:**

1. **Recusa, não completa.** Pedir EST sem PRO é recusado com a lista do que falta — o sistema
   **não** acrescenta PRO sozinho. Completar seria o sistema *vendendo um módulo que ninguém
   contratou*: a fatura não fecharia com o contrato, e o erro apareceria na cobrança, não aqui
2. **O fecho é transitivo.** Pedir EST devolve `VEN, PRO`, e não apenas `PRO`. Parar na primeira
   camada obrigaria quem vende a descobrir a cadeia por tentativa e erro, uma recusa por vez

**Isenção de `RN-GER-015` para o cadastro assistido.** `CadastrarEstabelecimentoCommand` é escrita
de plataforma e caiu no gate de sessão de suporte com 403. A isenção não é conveniência: a sessão
de suporte é aberta **para uma unidade**, e no cadastro a unidade ainda não existe — exigi-la
tornaria a operação impossível, não mais controlada. O argumento de fundo é o mesmo de
`AlterarModuloDaUnidadeCommand`, já isento: não há dado de cliente sendo tocado, e a autoria fica
na trilha.

### 11.6 Uma rota duplicada, removida

Eu havia criado `PUT /api/v1/plataforma/estabelecimentos/{id}/modulos/{sigla}` sem notar que
`POST /api/v1/plataforma/unidades/{id}/modulos/{codigo}/habilitar|desabilitar` **já existia** desde
a `SPEC-GER-006`, com teste. Duas rotas para o mesmo comando é a mesma armadilha de duas
implementações da mesma regra: uma ganha correção e a outra não. A minha foi removida; a tela de
plataforma usa a original.

### 11.7 O catálogo de módulos passou a ser servido

`GET /api/v1/plataforma/modulos` devolve os dez módulos com `dependeDe`. Existe porque o
formulário de cadastro precisa do grafo **antes** de a unidade existir, e a alternativa era uma
cópia do grafo no TypeScript — o mesmo problema que `GET /usuarios/perfis` resolveu para a matriz
de delegação (§11.1). Na tela, marcar EST marca PRO e VEN **à vista**, e desmarcar PRO leva EST
embora.

## 12. Verificação por mutação

| Mutação | Testes que caíram |
|---------|-------------------|
| Sem a checagem da matriz de delegação | 2 (CA-6, CA-7) |
| Perfil de quem cria vindo do claim em vez do banco | 4 |
| Sem recusa de sequência repetida no CPF | 4 |
| Sem conferir o dígito verificador | 5 |
| Criação não confere dependência (a lacuna da §11.5) | 3 |
| Fecho para na dependência direta, sem transitivo | 1 |
| Núcleo não conta como presente | 5 — **e nenhum, na primeira tentativa** |
| Dependentes habilitados ignorados | 1 |

> ### A mutação que sobreviveu, e o que ela mostrou
>
> Remover o tratamento de `Nucleo` de `FechoDeDependencia` **não quebrou teste nenhum** na primeira
> rodada. O código estava certo; a **cobertura** estava errada. Os dois chamadores já colocam GER no
> conjunto por conta própria, então o ramo nunca era exercitado por eles — testar pelo caminho do
> chamador media o que o chamador faz, não o que a função promete.
>
> A resposta foi `FechoDeDependenciaTests`, **7 testes de contrato** direto sobre a função
> (`tests/GrelhaHub.Application.Tests`), inclusive um grafo com ciclo de três arestas para provar
> que a busca termina. Com eles, a mesma mutação derruba **5** testes.

Nenhuma passou. A segunda mutação merece nota: ela simula usar o claim `role` do token em vez de
consultar o banco. Um token emitido **antes** de o perfil do usuário mudar continua afirmando o
perfil antigo até expirar — autorizar por dado que pode estar vencido é o defeito que a matriz
existe para impedir.
