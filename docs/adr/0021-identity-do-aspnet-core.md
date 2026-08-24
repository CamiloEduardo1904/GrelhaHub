# ADR-0021 — ASP.NET Core Identity: o que adotar, e o que não

| Campo | Valor |
|-------|-------|
| Situação | ⚠ **Recomendação REVERTIDA pela [ADR-0022](0022-identity-como-modelo-de-acesso.md)** — a análise segue válida, a recomendação não. O dono do produto decidiu migrar, com o argumento que esta ADR não pesou: *a janela em que isso é barato é agora* |
| Data | 19/08/2026 |
| Decide sobre | Se o modelo de usuário, perfil e permissão migra para o ASP.NET Core Identity |
| Relacionada | [ADR-0003](0003-frontend-e-backend-desacoplados.md), [ADR-0009](0009-onde-as-regras-de-negocio-vivem.md), [ADR-0013](0013-acesso-de-plataforma.md), [ADR-0016](0016-envio-de-email-transacional.md), [ADR-0019](0019-cqrs-com-modelo-de-leitura-proprio.md) |

---

## Contexto

A pergunta veio do dono do produto em 19/08/2026, ao ver que `cancelado_por_usuario_id` não tinha
chave estrangeira: *"se não usa FK, usa com ASP.NET — tem bibliotecas pra isso, authentication and
authorization. Acho que é uma estrutura mais adequada."*

A pergunta é boa e merece resposta com o código na mão, não com preferência.

### ⚠ O primeiro achado é que o Identity **já está no projeto**

```
Directory.Packages.props:36   Microsoft.AspNetCore.Identity 2.3.1
Infrastructure/Identity/ServicosDeSeguranca.cs:18   PasswordHasher<string>
```

`RNF-005` já é atendido pelo `PasswordHasher` do Identity — **PBKDF2 com salt por senha** —,
justamente na única parte em que escrever algoritmo próprio seria irresponsável. A pasta se chama
`Infrastructure/Identity/`. O que **não** foi adotado é o resto: `IdentityDbContext`,
`UserManager`, `SignInManager`, `AspNetUsers`, `AspNetRoles`.

Então a decisão real não é "usar ou não usar Identity". É **quanto** dele usar.

### O que existe hoje

| Peça | Onde | Regras |
|------|------|--------|
| `Usuario` — agregado de domínio | `Domain/Gerais/Usuario.cs` | `RN-GER-009`, `010`, `014`, `023`, `028` |
| `Perfil` + `PerfilPermissao` — **por unidade** | `Domain/Gerais/Perfil.cs` | `RN-GER-010`, `022` |
| `CatalogoDeAcesso` — permissões `recurso.acao`, com o módulo de cada uma | `Domain/Gerais/CatalogoDeAcesso.cs` | `RN-GER-010`, `017` |
| `MatrizDeDelegacao` — quem pode conceder o quê | `Domain/Gerais/MatrizDeDelegacao.cs` | `RN-GER-024` ⚠⚠ |
| `TokenDeAcesso` — opaco, uso único, guardado só como hash | `Domain/Gerais/TokenDeAcesso.cs` | `RN-GER-032` a `035` |
| JWT com uma claim por permissão | `Infrastructure/Identity/ServicosDeSeguranca.cs` | `RN-GER-010`, ADR-0003 |
| `ExigePermissao` / `ExigeModulo` no pipeline | `Application/Common/Modulos/` | `RN-GER-010`, `017` |

**17 regras** do catálogo dependem disso, com **71 citações** de `RN-GER-014` e **45** de
`RN-GER-010` no código. Oito classes de teste de integração cobrem a área: `AcessoTests`,
`AdministracaoTests`, `DelegacaoTests`, `RecuperacaoDeAcessoTests`, `TrocaDeSenhaAposCriacaoTests`,
`UsuariosDePlataformaTests`, `UsuariosESuporteTests`, `LeituraDePlataformaTests`.

---

## O que o Identity completo daria, e o que ele cobraria

### Daria

| Peça | Substituiria |
|------|--------------|
| `UserManager` / `SignInManager` | Os handlers de criar, alterar e autenticar usuário |
| `AspNetRoles` + `AspNetRoleClaims` | `Perfil` + `PerfilPermissao` |
| **Bloqueio por tentativa** (`Lockout`) | ⚠ **Nada — o projeto não tem** |
| **Security stamp** | ⚠ **Nada — o projeto não tem** |
| `PasswordOptions` | O `MinimumLength(8)` espalhado em três validators |
| Provedores de token (reset, confirmação de e-mail) | `TokenDeAcesso` |
| Login externo, 2FA | Nada — não são requisito |

### Cobraria — e é aqui que a conta vira

**1. ⚠⚠ Papel do Identity é GLOBAL; o perfil do GrelhaHub é POR UNIDADE.**

`AspNetRoles` tem índice único em `NormalizedName` — um "Gerente" no sistema inteiro. O projeto
semeia os perfis **em cada unidade**, e `RN-GER-009` exige `unidade_id` como **primeira coluna de
toda chave única**. Reconciliar isso significa ou nomes como `Gerente@unidade-x` (que quebra o
`RN-GER-022` de criar perfil novo) ou reescrever o `RoleStore`. **Não é configuração — é
substituir a peça que se estava adotando.**

**2. `RN-GER-024` continuaria custom.** A `MatrizDeDelegacao` — *ninguém concede acesso que não
possui* — não tem equivalente no Identity. Ela nasceu de um furo de escalação de privilégio
encontrado ao especificar "o gerente cadastra operadores", e o `ValidadorDeDelegacao` **recusa a
partida da aplicação** quando um perfil novo não declara o que delega. Identity não faz isso.

**3. `RN-GER-014` continuaria custom.** O usuário de plataforma tem `UnidadeId = null` e não
pertence a perfil nenhum; ele atravessa o filtro global (ADR-0013). No Identity ele seria um
usuário sem role, e o bypass continuaria sendo código nosso.

**4. `RN-GER-023` continuaria custom.** O CPF é *value object*, **único em todo o sistema** —
exceção declarada a `RN-GER-009`. Vira coluna extra no `AspNetUsers`, com o índice escrito à mão.

**5. `RN-GER-002` briga com a API do Identity.** `UserManager.DeleteAsync` apaga. O projeto proíbe
exclusão física de registro transacional; a rota teria de existir e nunca ser chamada — porta que
o `RN-GER-002` fecha e o Identity reabre.

**6. `RN-GER-033` seria satisfeita por outro mecanismo, não pelo enunciado.** A regra decidiu token
**opaco, guardado só como hash, uso único, e emitir um novo invalida os anteriores**. O provedor
padrão do Identity não guarda token nenhum — ele valida por assinatura e security stamp. O efeito
é parecido; o enunciado da regra **muda**, e a regra é `HIP` não validada. Trocar mecanismo antes
da entrevista é decidir por ela.

**7. ADR-0019 fica sem guarda naquele trecho.** `SeparacaoDeCqrsTests` recusa a build quando um
handler de `Query` alcança o contexto de escrita — ele procura `IAppDbContext`. `UserManager`
**escreve** e não é `IAppDbContext`: passaria pela catraca. A guarda precisaria aprender o nome
novo, e guarda que não conhece o infrator não é guarda.

### O custo, em números

| Item | Tamanho |
|------|--------:|
| Regras a reenunciar ou reconfirmar | 17 |
| Classes de teste de integração a reescrever | 8 |
| Migrations de esquema (tabelas `AspNet*` + migração de dados) | 1 grande, irreversível na prática |
| ADRs a emendar | 0003, 0013, 0016, 0019 |
| Problema de produto que isso resolve | **nenhum conhecido** |

---

## Decisão proposta

> **Não migrar para o Identity completo. Adotar dele as três peças que resolvem lacunas reais, e
> declarar por escrito por que o resto fica de fora.**

O Identity é excelente onde o projeto **não** tem opinião — hash de senha, política de senha,
bloqueio por tentativa. Ele atrapalha onde o projeto **tem** opinião registrada em regra: perfil por
unidade, delegação, usuário de plataforma, exclusão lógica, token opaco.

⚠ **E a pergunta que originou tudo — a chave estrangeira — já foi resolvida sem Identity**, no
commit `8ddb0a4`: `criado_por`, `cancelado_por_usuario_id` e `excluido_por_usuario_id` viraram
referência com `Restrict`. O que faltava era integridade referencial, não um framework.

---

## ⚠⚠ As duas lacunas reais que a investigação encontrou

Estas **não são hipóteses**. Foram procuradas no código e não existem:

### 1. Não há bloqueio por tentativa no login

`grep -rn "Lockout\|RateLimit"` em `src/` não devolve nada. `RN-GER-027` decidiu que o login
responde igual para credencial errada e conta inexistente — o que protege contra **enumeração de
contas** e não contra **força bruta**. Uma senha de 8 caracteres sem limite de tentativas é
atacável.

### 2. O JWT sobrevive à troca de senha

Não há *security stamp* nem lista de revogação. Um token emitido antes da troca continua válido até
expirar — **até 8 horas** (`Jwt:ExpiracaoEmMinutos: 480`).

⚠ Isso já aparece na documentação sem ser reconhecido como falha: o CLAUDE.md registra que *"trocar
a senha NÃO derruba a pendência do token que já está na mão"* e que a tela reautentica para
contornar. O contorno é de usabilidade; o buraco de segurança continua aberto do outro lado —
**quem teve a senha vazada troca a senha e o atacante segue dentro por oito horas.**

---

## Plano de ação proposto

### Fase 1 — fechar as duas lacunas *(1 a 2 dias, sem migração)*

| # | O quê | Regra | Onde |
|---|-------|-------|------|
| 1.1 | **Regra nova** `RN-GER-040` — tentativas de login e bloqueio temporário | escrever primeiro | catálogo |
| 1.2 | **Regra nova** `RN-GER-041` — a troca de senha invalida os tokens anteriores | escrever primeiro | catálogo |
| 1.3 | Coluna `versao_credencial` em `usuario`, incrementada na troca de senha; claim no JWT; o pipeline recusa token com versão antiga | `RN-GER-041` | domínio + `ExigePermissao` |
| 1.4 | Contador de tentativas + janela de bloqueio, com o **mesmo** corpo de resposta de `RN-GER-027` | `RN-GER-040` | `AutenticarHandler` |
| 1.5 | Parâmetros `PAR-GER-004` (tentativas) e `PAR-GER-005` (janela) | — | `04-parametros` §1.1 |
| 1.6 | Testes de integração: força bruta bloqueada; token velho recusado após troca | — | `AcessoTests` |

> ⚠ **`versao_credencial` e não *security stamp* do Identity**, e a diferença importa: com JWT
> *stateless* (ADR-0003), **nenhum** dos dois evita uma consulta por requisição. Um `int` na claim,
> comparado com a coluna, é o mesmo custo com um décimo do acoplamento. Adotar o Identity aqui
> traria `SignInManager` e `IdentityDbContext` para resolver o que uma coluna resolve.

### Fase 2 — adotar a política de senha do Identity *(meio dia)*

| # | O quê |
|---|-------|
| 2.1 | Trocar os três `MinimumLength(8)` espalhados por um `IPasswordValidator` configurado uma vez |
| 2.2 | Revisar `RNF-005` para citar a política concreta em vez de "hash + salt" |

### Fase 3 — o que **não** se faz, registrado

Fica escrito neste ADR que `AspNetUsers`, `AspNetRoles`, `UserManager` e `SignInManager` **não
entram**, com os sete motivos acima. ⚠ Sem este registro, a mesma pergunta volta em três meses e
alguém a responde com preferência em vez de código — que é exatamente o que os ADRs existem para
impedir.

### O que dispararia uma revisão desta decisão

| Gatilho | Por quê |
|---------|---------|
| **Login externo** (Google, Apple) virar requisito | `AspNetUserLogins` resolve de graça o que seria semanas |
| **2FA** virar requisito | idem, com `AspNetUserTokens` |
| O perfil deixar de ser por unidade | Some o conflito estrutural nº 1, que é o mais caro |
| Auditoria externa exigir certificação de um provedor conhecido | Argumento de conformidade, não técnico |

---

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| **Migrar tudo para Identity** | Sete conflitos estruturais, 17 regras reenunciadas, 8 classes de teste reescritas, nenhum problema de produto resolvido |
| **Identity só para autenticação, perfil próprio** | O acoplamento caro é justamente `AspNetUsers`; o barato (hasher) já está adotado. Ficaria com duas tabelas de usuário ou uma tabela do Identity com metade das colunas custom |
| **Não fazer nada** | Deixa as duas lacunas de segurança abertas — e uma delas está *documentada* no CLAUDE.md sem ser tratada como falha |
| **A proposta acima** | Fecha as lacunas com o custo do problema, não com o custo do framework |

---

## Consequências

**Boas**

- As duas lacunas de segurança fecham em dias, não em sprint
- O modelo de acesso — que é o mais bem documentado do projeto — continua defensável em banca por
  decisão, e não por inércia
- A pergunta fica respondida **com o código na mão**, e o gatilho de revisão está escrito

**Ruins, assumidas**

- Continuamos mantendo código de autenticação que um framework manteria por nós
- Login externo e 2FA custarão mais caro no dia em que forem pedidos — a Fase 3 diz exatamente
  quando reabrir esta decisão
- `versao_credencial` é mais uma coluna e mais uma claim; a alternativa era mais um framework

**⚠ O que esta proposta NÃO resolve**

A duplicação entre `CatalogoDeAcesso` (código) e as tabelas `permissao`/`perfil_permissao` (banco)
segue existindo. Ela é intencional — o catálogo é a fonte, o banco é o que foi concedido —, mas
nunca foi enunciada em regra. Vale uma linha no catálogo, e não tem relação com Identity.
