# ADR-0022 — ASP.NET Core Identity como modelo de acesso: claims, roles e lockout

| Campo | Valor |
|-------|-------|
| Situação | **Aceita** — decisão do dono do produto, 19/08/2026 |
| Data | 19/08/2026 |
| Decide sobre | Identidade, autenticação e autorização do sistema inteiro |
| **Reverte** | A recomendação da [ADR-0021](0021-identity-do-aspnet-core.md), que propunha *não* migrar |
| Emenda | [ADR-0003](0003-frontend-e-backend-desacoplados.md) · [ADR-0009](0009-onde-as-regras-de-negocio-vivem.md) |
| Relacionada | [ADR-0013](0013-acesso-de-plataforma.md), [ADR-0016](0016-envio-de-email-transacional.md), [ADR-0019](0019-cqrs-com-modelo-de-leitura-proprio.md) |

---

## Contexto

A [ADR-0021](0021-identity-do-aspnet-core.md) levantou sete conflitos e recomendou **não** migrar,
propondo fechar só as duas lacunas de segurança encontradas. O dono do produto leu, e decidiu o
contrário com um argumento que a ADR-0021 não pesou:

> *"Essa mudança agora custa menos do que na versão com dados reais de produção."*

Ele está certo, e é a variável que faltava. A ADR-0021 mediu o custo **hoje** — 17 regras, 8 classes
de teste, uma migration grande — e não mediu o custo **depois**. Com dados reais, a mesma migração
deixa de ser reescrita de teste e passa a ser migração de credencial de gente que trabalha: hash
diferente, sessão derrubada no meio do serviço, recuperação de senha em pane. **A janela em que isso
é barato é agora, e ela não reabre.**

Ele acrescentou o alvo: *"todo o fluxo de permissão deve ser atribuído a claim e roles"*, com
**lockout** e mensagem tratada. E autorizou reconstruir o banco do zero — o projeto não está em
produção.

---

## Decisão

**Adotar o ASP.NET Core Identity como modelo de identidade e acesso**, com quatro escolhas que
resolvem os conflitos que a ADR-0021 levantou. Cada uma abaixo é um problema real e a saída
escolhida.

### 1. `Usuario : IdentityUser<Guid>` — e a emenda declarada ao Domínio

O CLAUDE.md diz: *"`GrelhaHub.Domain/` sem dependências — nem EF Core, nem MediatR"*. Esta decisão
**abre uma exceção**, e ela precisa ser explícita para não virar precedente solto.

> **`GrelhaHub.Domain` passa a referenciar `Microsoft.Extensions.Identity.Stores`, e nada mais.**

| Alternativa | Por que não |
|-------------|-------------|
| Identity na Infraestrutura, `Usuario` de negócio no Domínio, duas tabelas | **Duas linhas para uma pessoa.** CPF numa, senha na outra, e toda leitura vira `JOIN`. O pior desenho dos três |
| As duas classes na mesma tabela (*table splitting*) | Funciona no EF e é frágil: chave compartilhada, dependente obrigatório, e um erro de mapeamento silencioso separa credencial de pessoa |
| **`Usuario` herda `IdentityUser<Guid>`** | Uma pessoa, uma linha, todas as regras (`RN-GER-014`, `023`, `024`, `028`) continuam no domínio |

⚠ **O que se está aceitando, dito com precisão:** `Microsoft.Extensions.Identity.Stores` traz POCOs
(`IdentityUser`, `IdentityRole`) e abstrações de store. **Não** traz EF Core, **não** traz ASP.NET
runtime, **não** traz MediatR. A regra original existe para o domínio não depender de *infraestrutura
de persistência ou mensageria* — e essa parte continua valendo. Quem quiser reverter, reverte
trocando a herança por campos próprios; nenhuma regra de negócio muda de lugar.

### 2. `Perfil : IdentityRole<Guid>` com `UnidadeId` — o conflito nº 1 da ADR-0021, resolvido

Era o conflito decisivo: papel do Identity é **global** (índice único em `NormalizedName`) e o perfil
do GrelhaHub é **por unidade**.

> O índice único passa a ser **`(unidade_id, normalized_name)`** — que é exatamente o que
> `RN-GER-009` já exigia de toda chave única do sistema.

O `RoleStore` do Identity não precisa ser reescrito: ele consulta por `NormalizedName`, e o filtro
global de unidade já recorta. ⚠ **O usuário de plataforma (`RN-GER-014`) tem `UnidadeId = null`**, e
os perfis dele também — a coluna é anulável, e o índice único aceita nulo como "linha distinta" no
PostgreSQL. É a mesma exceção declarada que `usuario.unidade_id` já era.

### 3. Permissão = **claim de papel**, e o `CatalogoDeAcesso` continua sendo a fonte

`AspNetRoleClaims`, tipo `permissao`, valor `recurso.acao`. O `CatalogoDeAcesso` (código) segue sendo
**a lista do que existe**; o banco guarda **o que foi concedido**. A autorização passa a ser
**política do ASP.NET** — uma por permissão, registrada a partir do catálogo na partida.

⚠ **`ExigePermissao` no request PERMANECE**, e o behavior passa a chamar `IAuthorizationService`. A
tentação é trocar tudo por `[Authorize(Policy = ...)]` no controller, e seria uma perda: o atributo
no *request* é o que garante que **nenhum caso de uso novo suba sem declarar o que exige** — o
controller pode ser esquecido, o request não, porque `SeparacaoDeCqrsTests` e o próprio pipeline o
enxergam. O framework passa a **executar** a decisão; quem a **declara** continua sendo o caso de uso.

### 4. Lockout de verdade — `RN-GER-040`

`SignInManager.CheckPasswordSignInAsync(lockoutOnFailure: true)`, com `LockoutOptions` configurado
por parâmetro.

> ### ⚠⚠ E aqui há um conflito com `RN-GER-027`, que precisa ser decidido e não escondido
>
> `RN-GER-027` decidiu que o login **responde igual** para senha errada e conta inexistente — para
> quem sonda não descobrir quem é cliente. **A mensagem de bloqueio revela que a conta existe.**
>
> **Decisão: a mensagem de bloqueio aparece, e a fresta é aceita.** Quem quiser enumerar contas
> passa a precisar de `PAR-GER-004` tentativas **por endereço** — o ataque encarece na mesma
> proporção em que a força bruta encarece. E o custo do silêncio recai sobre a pessoa errada: o
> operador legítimo, no meio do serviço, com a senha certa e uma tela dizendo "credenciais
> inválidas" sem explicar por quê. `RN-GER-040` registra a troca.

### 5. Invalidação de token na troca de senha — `RN-GER-041`

O `SecurityStamp` do Identity vai no JWT e é conferido a cada requisição. `UserManager` já o
regenera em toda troca de senha.

⚠ **Isso custa uma consulta por requisição, e o JWT deixa de ser puramente *stateless*** — o que
emenda a ADR-0003. A alternativa é o buraco que a ADR-0021 encontrou: senha vazada, senha trocada, e
o atacante dentro por mais oito horas. **Sessão que não morre com a senha não é sessão, é chave
copiada.**

### 6. Banco reconstruído do zero — uma migration só

As 12 migrations existentes são substituídas por **uma**, `Fundacao`. Autorizado pelo dono do
produto: o projeto não está em produção.

| Por quê | |
|---|---|
| **Retrofit de Identity em 12 migrations** deixaria um histórico que conta uma história que não aconteceu | Quem ler o `Migrations/` em três meses reconstrói um caminho que ninguém percorreu |
| O esquema do Identity muda **nomes de coluna** de `usuario` e `perfil` | `ALTER` em cima de `ALTER` para chegar ao mesmo lugar |
| Não há dado a preservar | O harness recria o banco a cada execução |

⚠ **Esta é a decisão de maior risco desta ADR**, e o que a torna aceitável é a janela: **é a última
vez que ela é barata.** A partir do primeiro cliente real, migration se soma — nunca se apaga.

---

## Consequências

**Boas**

- As duas lacunas de segurança da ADR-0021 fecham com o mecanismo do framework, não com código nosso
- Autorização passa a ser a do ASP.NET: políticas, claims, roles — vocabulário que qualquer pessoa
  de .NET lê sem manual
- Login externo e 2FA deixam de ser reescrita e passam a ser configuração
- O hash já era do Identity (`RNF-005`); agora a política de senha e o lockout também

**Ruins, assumidas**

- ⚠ O Domínio ganha **uma** dependência, contra a regra original. Declarada aqui, e limitada a um
  pacote de POCOs
- ⚠ O JWT deixa de ser puramente *stateless* (§5) — uma consulta por requisição
- ⚠ `RN-GER-027` perde parte da proteção contra enumeração (§4), em troca de uma mensagem honesta
- `UserManager.DeleteAsync` existe e **nunca pode ser chamado** (`RN-GER-002`). Um teste de
  arquitetura passa a recusar a build se aparecer

**O que NÃO muda**

- `MatrizDeDelegacao` (`RN-GER-024`) continua custom — o Identity não tem equivalente, e ela nasceu
  de um furo real de escalação de privilégio
- O usuário de plataforma (`RN-GER-014`) continua com `UnidadeId = null` e bypass próprio
- O CPF (`RN-GER-023`) continua *value object* com unicidade global
- `TokenDeAcesso` (`RN-GER-032` a `035`) continua nosso: token opaco, uso único, guardado só como
  hash. O provedor do Identity não guarda token, e a regra decidiu guardar
