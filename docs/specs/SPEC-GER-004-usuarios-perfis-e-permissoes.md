# SPEC-GER-004 — Usuários, perfis e permissões

| Campo | Valor |
|-------|-------|
| ID | `SPEC-GER-004-usuarios-perfis-e-permissoes` |
| Módulo | GER |
| Situação | `ENTREGUE` — 17/08/2026, com pendências declaradas na §10 |
| Onda | 0 — fundação |
| Regras implementadas | `RN-GER-010`, `RN-GER-012`, `RN-GER-009`, `RN-RH-006`, `RN-GER-014` ⚠⚠, `RN-GER-015` ⚠, `RN-GER-016` |
| ADRs aplicáveis | [ADR-0003](../adr/0003-frontend-e-backend-desacoplados.md), [ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md), [ADR-0013](../adr/0013-acesso-de-plataforma.md) ⚠⚠ |
| Depende de | `SPEC-GER-001` |
| Data | 17/08/2026 |

---

## 1. Objetivo

Autenticar o operador e decidir o que ele pode fazer — incluindo **quais dados ele sequer
recebe**, que é o ponto de `RN-GER-012` e a parte que mais costuma ser implementada errado.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-GER-010` | Todo usuário pertence a um perfil, que determina as operações permitidas | Modelo + Aplicação | FK `NOT NULL` + behavior de autorização no MediatR |
| `RN-GER-012` | Custo, CMV e margem só para gerência — **a API não retorna o campo** | Aplicação | Projeção condicional na query, não filtro na resposta |
| `RN-GER-009` | Vínculo à unidade | Modelo | FK `NOT NULL`; o `unidade_id` viaja no token |
| `RN-RH-006` | Colaborador inativo não registra operação no sistema | Aplicação + Modelo | `CHECK` de coerência + verificação no login |
| `RN-GER-014` ⚠⚠ | Usuário de plataforma não tem unidade e **enxerga todas** | Aplicação | Filtro global condicionado a `EhPlataforma` |
| `RN-GER-015` ⚠ | Leitura de plataforma registrada; **escrita exige sessão de suporte com motivo** | Aplicação + Modelo | `sessao_suporte` + behavior no pipeline |
| `RN-GER-016` | Usuário de plataforma é invisível ao restaurante | Aplicação | Filtro na listagem + recusa na criação |

## 3. Contrato

### `AutenticarCommand`

| Entrada | Tipo | Obrigatório | Validação |
|---------|------|:-----------:|-----------|
| `email` | `string` | ✔ | Formato de e-mail |
| `senha` | `string` | ✔ | Não vazia |

| Saída | Tipo |
|-------|------|
| `token` | `string` (JWT) |
| `expiraEm` | `DateTimeOffset` |
| `usuario` | `{ id, nome, perfil, permissoes[] }` |

**Claims do token:** `sub` (usuário), `unidade` (`RN-GER-009`), `perfil`, `permissoes`,
`exp`.

### `CriarUsuarioCommand` · `AlterarUsuarioCommand` · `DesativarUsuarioCommand`

| Entrada | Tipo | Obrigatório | Validação | Regra |
|---------|------|:-----------:|-----------|-------|
| `nome` | `string` | ✔ | 3 a 200 caracteres | |
| `email` | `string` | ✔ | Único por unidade | |
| `perfilId` | `uuid` | ✔ | Perfil existente | `RN-GER-010` |
| `colaboradorId` | `uuid?` | | Se informado, colaborador **ativo** | `RN-RH-006` |

### Erros

| Situação | Código | Mensagem | Regra |
|----------|--------|----------|-------|
| Credencial inválida | 401 | "E-mail ou senha inválidos." | |
| Usuário inativo | 401 | "E-mail ou senha inválidos." | `RN-RH-006` |
| Colaborador inativo | 422 | "Não é possível vincular um colaborador inativo." | `RN-RH-006` |
| Sem permissão | 403 | "Você não tem permissão para esta operação." | `RN-GER-010` |
| E-mail duplicado | 409 | "Já existe um usuário com este e-mail." | |

> **Usuário inativo devolve a mesma mensagem de credencial inválida, de propósito.**
> Diferenciar as duas confirmaria a existência da conta para quem está tentando adivinhar.

### Autorização

Gestão de usuários: apenas **Administrador**. Consulta do próprio perfil: qualquer
autenticado.

## 4. Comportamento

### Perfis e permissões iniciais

`RN-GER-010` não fixa os perfis — vêm do levantamento (§4 do
[documento de levantamento](../01-documento-de-levantamento.md)):

| Perfil | Permissões |
|--------|------------|
| **Administrador** | Todas |
| **Gerente** | Operação + `pedido.cancelar`, `pedido.descontar`, `estoque.ajustar`, `caixa.fechar`, `custo.visualizar`, `parametro.alterar` |
| **Atendente** | `pedido.*` exceto cancelar e descontar, `comanda.*`, `caixa.abrir`, `caixa.fechar` (próprio) |
| **Cozinha** | `producao.visualizar`, `producao.concluir` — **sem** `custo.visualizar` nem `preco.visualizar` |
| **Entregador** | `entrega.visualizar`, `entrega.concluir` |

A permissão é **granular** (`recurso.acao`), não o nome do perfil: verificar
`perfil == "Gerente"` espalhado pelo código torna impossível criar um perfil novo sem caçar
condicionais.

### Autorização no pipeline

Um behavior do MediatR lê o atributo `[ExigePermissao("pedido.cancelar")]` do command e
recusa antes de o handler executar. Nenhum handler começa com `if (!usuario.Pode(...))`.

### `RN-GER-012` — a parte que costuma ser feita errado

> **A API não retorna o campo.** Não é ocultar na tela, nem devolver e esconder no
> frontend — é **não projetar**.

```csharp
// Correto: a projeção decide o que existe na resposta.
var produtos = query.Select(p => new ProdutoResponse {
    Id = p.Id,
    Nome = p.Nome,
    PrecoVenda = p.PrecoVigente,
    // Custo e margem só entram na projeção quando há permissão.
    Custo  = podeVerCusto ? p.CustoApurado : null,
    Margem = podeVerCusto ? p.PrecoVigente - p.CustoApurado : null,
});
```

Motivo: o campo devolvido e escondido no front continua visível em qualquer inspeção de
rede. Se o atendente não pode saber a margem, o dado não pode sair do servidor.

### Acesso de plataforma — `RN-GER-014`, `RN-GER-015`, `RN-GER-016`

O usuário que dá **suporte ao restaurante** não é usuário do restaurante. Ele não tem unidade,
e o recorte de `RN-GER-009` não se aplica a ele — decisão registrada em
[ADR-0013](../adr/0013-acesso-de-plataforma.md), que **revisa a invariante do ADR-0005**.

| | Usuário do restaurante | Usuário de plataforma |
|---|---|---|
| `unidade_id` | obrigatório | **nulo** |
| Alcance da consulta | só a sua unidade | **todas** |
| Perfil | Administrador, Gerente, Atendente, Cozinha, Entregador | `Plataforma` |
| Criado por | administrador da unidade | outro usuário de plataforma ou carga inicial |
| Visível ao restaurante | sim | **não** |

> ⚠⚠ **O que se perdeu, dito com clareza.** Antes, esquecer um filtro era inofensivo: o filtro
> global cobria. Agora, um defeito que marque `EhPlataforma` indevidamente **vaza dado de um
> cliente para outro**. É por isso que `EhPlataforma` deriva de **uma única claim**, e que toda
> spec que toque consulta passa a incluir um critério de aceite provando que o usuário comum
> continua isolado.

**Escrita exige sessão de suporte.** Ler para diagnosticar é rotina; alterar o dado de um
cliente sem dizer por quê, não. A sessão registra unidade, usuário, instante e **motivo**, e sem
ela toda operação de escrita de um usuário de plataforma é recusada.

### Senha

Hash com **ASP.NET Core Identity `PasswordHasher`** (PBKDF2, salt por senha) — `RNF-005`.
Nunca texto claro, nunca hash sem salt, nunca algoritmo próprio.

Troca obrigatória no primeiro acesso (`RF-SEG-005`): `deve_trocar_senha` bloqueia todas as
rotas exceto a de troca.

### Usuário × Colaborador

São entidades distintas, ligadas opcionalmente:

- **Colaborador** (`RH`) é quem trabalha — tem cargo, escala, custo por hora
- **Usuário** (`GER`) é quem acessa o sistema

Nem todo colaborador acessa o sistema; o administrador pode ser usuário sem ser
colaborador. `RN-LOG-005` exige que o entregador seja colaborador ativo — daí o vínculo
existir.

## 5. Critérios de aceite

### CA-1 — Autenticação emite token com unidade (`RN-GER-009`, `RN-GER-010`)

> **Dado** um usuário ativo com perfil Atendente,
> **Quando** autenticar com credenciais corretas,
> **Então** recebe um JWT contendo `sub`, `unidade`, `perfil` e as permissões do perfil,
> **e** o token expira conforme `Jwt:ExpiracaoEmMinutos`.

### CA-2 — Operação sem permissão é recusada antes do handler (`RN-GER-010`)

> **Dado** um usuário com perfil Atendente,
> **Quando** ele emitir um comando marcado com `[ExigePermissao("pedido.cancelar")]`,
> **Então** a resposta é 403,
> **e** o handler **não é executado** — nada é lido nem gravado.

### CA-3 — Custo não sai do servidor (`RN-GER-012`)

> **Dado** um usuário com perfil Atendente,
> **Quando** ele consultar a listagem de produtos,
> **Então** o JSON da resposta **não contém** as chaves `custo`, `cmv` nem `margem`,
> **e** o mesmo é verdade para todo relatório,
> **e** o teste inspeciona o **corpo bruto da resposta**, não o objeto desserializado.

### CA-4 — Gerente vê custo (`RN-GER-012`)

> **Dado** um usuário com perfil Gerente,
> **Quando** consultar a mesma listagem,
> **Então** `custo` e `margem` vêm preenchidos.

### CA-5 — Usuário inativo não autentica (`RN-RH-006`)

> **Dado** um usuário desativado,
> **Quando** tentar autenticar com a senha correta,
> **Então** recebe 401 com a **mesma mensagem** de credencial inválida,
> **e** nenhum token é emitido.

### CA-6 — Colaborador inativo não vira usuário (`RN-RH-006`)

> **Dado** um colaborador com situação inativa,
> **Quando** for tentado vincular um usuário a ele,
> **Então** a operação é recusada com 422.

### CA-7 — Senha nunca em texto claro (`RNF-005`)

> **Dado** um usuário criado,
> **Quando** a linha for lida diretamente do banco,
> **Então** a coluna de senha contém um hash com salt,
> **e** a senha original não aparece em nenhuma coluna, log ou resposta de API.

### CA-9 — Usuário de plataforma enxerga todas as unidades (`RN-GER-014`)

> **Dado** dois restaurantes com pedidos,
> **Quando** um usuário de plataforma listar pedidos,
> **Então** vê os pedidos dos dois,
> **e** um usuário Administrador de um deles continua vendo **apenas** os seus.
>
> A segunda metade é a que importa: ela prova que o recorte foi **afrouxado para um perfil**, e
> não removido para todos.

### CA-10 — Escrita de plataforma exige sessão de suporte com motivo (`RN-GER-015`)

> **Dado** um usuário de plataforma sem sessão de suporte aberta,
> **Quando** tentar qualquer operação de escrita sobre dado de unidade,
> **Então** a operação é recusada,
> **e** após abrir sessão com motivo, a mesma operação é aceita,
> **e** a sessão registra unidade, usuário, instante e o motivo informado.

### CA-11 — Usuário de plataforma é invisível ao restaurante (`RN-GER-016`)

> **Dado** um usuário de plataforma existente,
> **Quando** o Administrador de um restaurante listar os usuários da sua unidade,
> **Então** o usuário de plataforma **não aparece**,
> **e** a tentativa de criar usuário com perfil de plataforma é recusada.

### CA-8 — Troca no primeiro acesso (`RF-SEG-005`)

> **Dado** um usuário recém-criado,
> **Quando** ele autenticar e tentar qualquer rota que não seja a de troca de senha,
> **Então** recebe 403 indicando a necessidade de trocar a senha.

## 6. Modelo de dados tocado

| Tabela | Constraints principais |
|--------|------------------------|
| `perfil` | `UNIQUE (unidade_id, nome)` |
| `permissao` | `UNIQUE (codigo)`; `CHECK (codigo ~ '^[a-z_]+\.[a-z_]+$')` |
| `perfil_permissao` | PK composta `(perfil_id, permissao_id)` |
| `usuario` | `UNIQUE (unidade_id, email)`; `senha_hash NOT NULL`; `ativo NOT NULL DEFAULT true`; `deve_trocar_senha NOT NULL DEFAULT true`; FK opcional para `colaborador` |

`unidade_id` é a **primeira coluna** de cada chave única — `RN-GER-009`.

**Migration necessária:** sim — `0002_Acesso`.

**Carga inicial:** um perfil de cada tipo com as permissões da §4, e um usuário
administrador cuja senha inicial vem de variável de ambiente, com `deve_trocar_senha = true`.

## 7. Parâmetros consumidos

Nenhum. A expiração do token é configuração de aplicação (`Jwt:ExpiracaoEmMinutos`), não
valor de negócio.

## 8. Fora de escopo

- **Refresh token e revogação antes da expiração** — custo declarado no
  [ADR-0003](../adr/0003-frontend-e-backend-desacoplados.md). O MVP usa expiração curta
- Recuperação de senha por e-mail — exige serviço de envio, fora do MVP
- Autenticação de dois fatores
- Auditoria de login — a trilha de `SPEC-GER-005` cobre operações de negócio, não acessos

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Entidades | `src/GrelhaHub.Domain/Gerais/Usuario.cs`, `Perfil.cs` (com `Permissao` e `PerfilPermissao`) ✔ |
| Catálogo de acesso | `src/GrelhaHub.Domain/Gerais/CatalogoDeAcesso.cs` ✔ — perfis e permissões declarados |
| Contexto | `IContextoDeExecucao.Permissoes` / `Pode()` ✔ |
| Behavior | `src/GrelhaHub.Application/Common/Behaviors/PermissaoBehavior.cs` ✔ |
| Atributos | `src/GrelhaHub.Application/Common/Modulos/DeclaracaoDePermissao.cs` ✔ |
| Validação na partida | `ValidadorDeDeclaracaoDeModulo` ✔ — exige declaração de módulo **e** de permissão |
| Usuário de plataforma | `Usuario.CriarDePlataforma`, `Persistence/SemeadorDePlataforma.cs` ✔ |
| Token | `Identity/ServicosDeSeguranca.cs` ✔ — claim `permissao` por código, claim `plataforma` |
| Carga inicial | `CadastrarRestauranteHandler.SemearPerfisAsync` ✔ |
| Migration | `.../Migrations/20260817174715_PerfilPermissaoEExclusaoLogica.cs` ✔ — **com migração do dado existente** |
| Exclusão lógica | `Pedido.ExcluirLogicamente`, `Features/Vendas/CancelarEExcluirPedido/` ✔ (`RN-VEN-041`) |
| Usuários | `src/GrelhaHub.Application/Features/Gerais/Usuarios/` ✔ — listar, criar, desativar, trocar senha |
| Sessão de suporte | `src/GrelhaHub.Domain/Gerais/SessaoDeSuporte.cs`, `Features/Gerais/SessaoDeSuporte/` ✔ |
| Behaviors | `TrocaDeSenhaBehavior` (`RF-SEG-005`), `SessaoDeSuporteBehavior` (`RN-GER-015`) ✔ |
| Testes | `Gerais/AcessoTests.cs` ✔ (7 casos), `Gerais/UsuariosESuporteTests.cs` ✔ (6 casos) |

## 10. Definição de pronto

- [x] Perfis, permissões granulares e vínculo com usuário — `perfil`, `permissao`,
      `perfil_permissao`; o `enum PerfilUsuario` sobrevive apenas como **rótulo** da carga
- [x] `PermissaoBehavior` recusando **antes** do handler (CA-2), com teste provando que o pedido
      continua `Confirmado` e não `Cancelado`
- [x] **Nenhuma decisão por nome de perfil** — nem no backend nem no frontend. `ehGerencia()`, que
      comparava `perfil === 'Administrador' || 'Gerente'`, virou `pode(sessao, 'pedido.cancelar')`
- [x] A aplicação **recusa subir** se alguma operação não declarar permissão — mesmo desenho do
      gate de módulo
- [x] Hash com salt (`PasswordHasher` do Identity); senha nunca em log nem em resposta
- [x] **CA-1, CA-2, CA-3, CA-5, CA-7, CA-8, CA-9, CA-10 e CA-11** e `RN-VEN-041` como teste de integração
- [x] Carga inicial com os **seis** perfis (`RN-GER-022` acrescentou o Operador) e o administrador
- [x] Rastreabilidade do catálogo §16 atualizada
- [x] **`RF-SEG-005` — tela de troca no primeiro acesso, entregue em 17/08/2026.**
      `/trocar-senha`, com `ExigeSessao` interceptando a pendência antes de qualquer rota
      autenticada. ⚠ **O detalhe que engana:** trocar a senha não derruba a pendência do token
      que já está na mão — o claim `trocar_senha` viaja nele —, então a tela **reautentica**
      com a senha nova. Por isso `AutenticarResponse` passou a devolver o `Email`: sem ele, a
      tela pediria ao usuário que digitasse o e-mail de novo, dois campos depois de ele já ter
      entrado. Dois testes em `AdministracaoTests` guardam o ciclo: o beco (todas as rotas 403),
      a saída (a troca é a única isenta), o token antigo **ainda bloqueando**, e a senha atual
      sendo exigida — sem ela, um token roubado tomaria a conta.
      O registro do que estava aberto, para histórico:
- [ ] ~~⚠ **`RF-SEG-005` não tem tela de troca de senha** — encontrado em 17/08/2026, clicando.~~
      O usuário criado por outra pessoa nasce com `deveTrocarSenha = true`, o
      `TrocaDeSenhaBehavior` recusa **todas** as rotas com "Troque a sua senha antes de
      continuar", e **não existe onde trocá-la**: a conta nasce inutilizável.
      **A API está completa** — `POST /api/v1/usuarios/minha-senha` (`TrocarSenhaCommand`), e a
      resposta de erro já carrega `trocarSenha: true` na extensão do `ProblemDetails`, além de
      `deveTrocarSenha` vir no login. **Só falta a tela**, que precisa interceptar a sessão com
      a marca e levar à troca antes de qualquer outra rota. Hoje o front apenas **exibe** a
      marca como selo na lista de outros usuários, e nunca age sobre a do próprio usuário
- [x] `dotnet build -c Release`, `dotnet test`, `eslint` e `tsc` verdes — **115 testes** na
      entrega desta spec; **125** depois da `SPEC-GER-005`

### Fechados na segunda passada — 17/08/2026

- [x] **CA-3 — custo não sai do servidor.** Varredura no **corpo bruto** de *todas* as sete
      leituras que a API expõe hoje, procurando as **chaves** `custo`, `cmv` e `margem`. Endpoint
      novo entra na lista e passa a ser coberto
- [x] **CA-5 — usuário desativado não autentica**, com a **mesma** resposta de credencial inválida:
      o teste compara os dois corpos, porque diferenciá-los confirmaria a existência da conta
- [x] **CA-7 — senha com hash e salt.** Dois usuários com a *mesma* senha têm hashes diferentes;
      nem o valor nem chave de senha aparecem em resposta
- [x] **CA-8 — troca no primeiro acesso.** `TrocaDeSenhaBehavior` bloqueia **toda** operação exceto
      a troca. Usuário criado por outra pessoa nasce com a pendência; o fundador, que escolheu a
      própria senha no cadastro, não
- [x] **CA-10 — sessão de suporte.** `sessao_suporte` com motivo obrigatório de dez caracteres e
      validade de duas horas. **Ler passa sem sessão; escrever, não** — e o teste prova as duas
      metades na mesma execução
- [x] **CA-11 — plataforma invisível ao restaurante.** `GET /api/v1/usuarios` recorta por unidade
      **e** exclui `eh_plataforma`; o teste confirma que o usuário existe no banco, para que a
      ausência seja filtro e não base vazia
- [x] **CRUD de usuário** — listar, criar, desativar (`RN-GER-002`: desativa, não exclui) e trocar
      a própria senha

### Seguem abertos, e por quê

- [ ] **CA-4 — gerente vê custo.** Não há **campo de custo em resposta alguma**: ele nasce da ficha
      técnica (`RN-PRO-001`), onda 2, hoje bloqueada por `RN-PRO-004` em `HIP`. A permissão
      `custo.visualizar` e `contexto.PodeVerCusto` existem e esperam pela projeção. Fechar isto
      agora exigiria inventar um campo de custo sem ficha técnica — regra nascendo no código
- [ ] **CA-6 — colaborador inativo não vira usuário.** A tabela `colaborador` não existe: é módulo
      RH, onda 10. A FK opcional de `usuario` para `colaborador` entra com ela

> Os dois restantes não são dívida de mecanismo: são **tabelas que ainda não existem**. Cada um
> tem a onda que o destrava nomeada acima.
>
> ### O que a implementação obrigou a mudar
>
> **`RN-GER-009` ganhou uma exceção declarada:** o e-mail do usuário é único **globalmente**, e
> não por unidade. O login recebe apenas e-mail e senha (contrato de `SPEC-VEN-001`) e precisa
> resolver o usuário antes de saber a unidade — com unicidade por unidade, dois restaurantes
> poderiam registrar o mesmo e-mail e o login escolheria um deles arbitrariamente. Era um defeito
> presente antes desta spec.
>
> **`AlterarStatusPedidoCommand` foi partido.** Cancelar exige `pedido.cancelar` e avançar exige
> `pedido.avancar`; um comando com `enum` de ação não declara duas permissões, e a verificação
> teria de voltar para dentro do handler — o que `RN-GER-010` manda evitar. Nasceram
> `CancelarPedidoCommand` e `ExcluirPedidoCommand`.
>
> **A convenção de nome virou garantia.** O gate de sessão de suporte identifica **escrita** pelo
> sufixo `Command`, e isso só é confiável porque a partida agora **recusa** operação cujo nome não
> termine em `Command` ou `Query`. Sem essa checagem, a convenção seria suposição: uma operação
> chamada `ExcluirTudo` passaria como leitura e escaparia da exigência de motivo.
>
> **Habilitar módulo NÃO exige sessão de suporte, e é decisão registrada.** `AlterarModuloDaUnidade`
> já carrega `Justificativa` obrigatória — a explicação fica na linha de `unidade_modulo`, junto do
> que ela explica, o que é mais rastreável que uma sessão genérica de duas horas. E é ato
> **comercial**, não de suporte: exigir "abra uma sessão de suporte" para registrar uma venda seria
> pedir o motivo errado.
>
> **A migration não é só DDL.** Havia base em produção com o perfil como inteiro. A migration
> semeia as permissões, cria os seis perfis para cada unidade existente, mapeia cada usuário do
> inteiro antigo para o perfil novo **na sua unidade**, e então **falha ruidosamente** se sobrou
> usuário de restaurante sem perfil. Sem isso, o deploy tiraria o acesso de quem já usava o
> sistema, e o sintoma seria "perdi o acesso depois do deploy".
