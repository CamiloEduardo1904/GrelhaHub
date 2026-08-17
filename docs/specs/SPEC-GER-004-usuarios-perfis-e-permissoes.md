# SPEC-GER-004 — Usuários, perfis e permissões

| Campo | Valor |
|-------|-------|
| ID | `SPEC-GER-004-usuarios-perfis-e-permissoes` |
| Módulo | GER |
| Situação | `PRONTA` |
| Onda | 0 — fundação |
| Regras implementadas | `RN-GER-010`, `RN-GER-012`, `RN-GER-009`, `RN-RH-006` |
| ADRs aplicáveis | [ADR-0003](../adr/0003-frontend-e-backend-desacoplados.md), [ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md) |
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
| Entidades | `src/GrelhaHub.Domain/Gerais/Usuario.cs`, `Perfil.cs`, `Permissao.cs` |
| Contexto | `IContextoDeExecucao` ✔ |
| Behavior | `src/GrelhaHub.Application/Common/Behaviors/AutorizacaoBehavior.cs` |
| Atributo | `src/GrelhaHub.Application/Common/Seguranca/ExigePermissaoAttribute.cs` |
| Features | `src/GrelhaHub.Application/Features/Gerais/Autenticar/`, `CriarUsuario/`, … |
| Migration | `.../Migrations/0002_Acesso` |
| Testes | `tests/GrelhaHub.Integration.Tests/Gerais/AcessoTests.cs` |

## 10. Definição de pronto

- [ ] Perfis, permissões granulares e vínculo com usuário
- [ ] `AutorizacaoBehavior` recusando **antes** do handler (CA-2)
- [ ] **CA-3 inspecionando o corpo bruto da resposta** — não o objeto desserializado
- [ ] Hash com salt; nenhum log contendo senha
- [ ] CA-1 a CA-8 passando como teste de integração
- [ ] Carga inicial com os cinco perfis e o administrador
- [ ] Rastreabilidade do catálogo §16 atualizada
- [ ] `dotnet build -c Release` e `dotnet test` verdes
