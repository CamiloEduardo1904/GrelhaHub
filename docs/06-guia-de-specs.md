# GrelhaHub — Guia de Specs (SDD / Document First)

> Como uma regra de negócio vira spec, e como uma spec vira código. Este documento define o
> processo; o [catálogo de regras](03-catalogo-de-regras-de-negocio.md) define o conteúdo.

---

## 1. O princípio

**Document First:** nenhum comportamento existe no sistema sem estar documentado antes.

**Spec-Driven Development:** o código não é escrito a partir de uma conversa, de um ticket
ou de uma tela — é escrito a partir de uma spec, que por sua vez transcreve regras do
catálogo.

```
   catálogo de regras          spec de implementação            código
   ┌──────────────────┐        ┌──────────────────┐        ┌──────────────┐
   │ RN-VEN-018       │───────►│ SPEC-VEN-003     │───────►│ Command +    │
   │ RN-EST-018/019   │  cita  │ cancelar-pedido  │  guia  │ Handler +    │
   │ RN-GER-011       │        │                  │        │ teste        │
   └──────────────────┘        └──────────────────┘        └──────────────┘
            ▲                                                      │
            │        regra nova descoberta durante o trabalho       │
            └───────────────────────────────────────────────────────┘
                     volta ao catálogo ANTES de virar código
```

### As quatro regras do processo

1. **Nenhuma regra de negócio nasce no código.** Se durante a implementação aparecer um
   comportamento necessário que não está no catálogo, a implementação **para**, a regra é
   escrita no catálogo, e só então o código continua. É o único ponto inegociável.
2. **Toda spec cita as regras que implementa**, por ID, no cabeçalho.
3. **Todo teste cita a regra que prova.** Uma regra em estado `HIP` que for rejeitada na
   entrevista precisa encontrar, por busca de texto, o código que a assumia.
4. **A spec é escrita antes do código e atualizada depois dele.** Se a implementação
   revelou que a spec estava errada, corrige-se a spec — não se deixa a divergência.

---

## 2. Anatomia de uma spec

Arquivo em `docs/specs/SPEC-<MOD>-<nnn>-<slug>.md`. O template está em
[`docs/specs/_template.md`](specs/_template.md).

| Seção | Conteúdo |
|-------|----------|
| **Cabeçalho** | ID, título, módulo, situação, regras implementadas, ADRs aplicáveis |
| **Objetivo** | Uma frase: qual capacidade de negócio esta spec entrega |
| **Regras implementadas** | Tabela com ID, enunciado resumido e **onde** cada uma é imposta (modelo, banco, aplicação) |
| **Contrato** | Comando/consulta, entrada, saída, erros — o contrato de API |
| **Comportamento** | Fluxo principal e alternativos, em prosa ou pseudo-passos |
| **Critérios de aceite** | Given-When-Then, um por regra implementada, no mínimo |
| **Modelo de dados tocado** | Tabelas lidas e escritas, e as constraints envolvidas |
| **Fora de escopo** | O que esta spec explicitamente **não** faz |
| **Rastreabilidade** | Arquivos de código e de teste, preenchidos na entrega |

### Numeração

`SPEC-<MOD>-<nnn>-<slug>` — o `<MOD>` é o mesmo do catálogo (`GER`, `VEN`, `PRO`, `EST`,
`COM`, `FIN`, `CRM`, `LOG`, `RH`, `FIS`). Numeração sequencial por módulo, nunca reutilizada.

Exemplos: `SPEC-VEN-001-abrir-pedido`, `SPEC-EST-004-baixa-por-venda-fefo`,
`SPEC-FIN-002-fechar-caixa`.

### Situação da spec

| | |
|---|---|
| `RASCUNHO` | Em escrita; não implementar |
| `PRONTA` | Revisada; liberada para implementação |
| `EM IMPLEMENTAÇÃO` | Código em andamento |
| `ENTREGUE` | Código e testes entregues; rastreabilidade preenchida |
| `SUPERADA` | Substituída por outra spec, referenciada no cabeçalho |

---

## 3. Onde cada regra é imposta

Toda spec preenche a coluna **Imposta em** para cada regra, escolhendo entre as camadas
decididas em [ADR-0009](adr/0009-onde-as-regras-de-negocio-vivem.md):

| Camada | Quando usar | Como aparece no código |
|--------|-------------|------------------------|
| **Modelo** | Invariante que nunca pode ser violada por nenhum caminho | Migration EF Core: `CHECK`, `UNIQUE`, `EXCLUDE`, FK, `ENUM`, coluna gerada |
| **Banco** | Derivação ou consistência multi-tabela | Trigger ou view na migration |
| **Domínio** | Invariante de agregado, expressa em C# | Método na entidade; construtor privado; value object |
| **Aplicação** | Fluxo, autorização, mensagem ao usuário, política parametrizada | Validator FluentValidation, behavior do MediatR, handler |

A coluna **Impl.** do catálogo é a *previsão*; a spec é onde a decisão vira compromisso. Se
a spec divergir da previsão do catálogo, ela justifica a divergência — e o catálogo é
atualizado.

---

## 4. Como uma spec vira código CQRS

Cada spec mapeia para uma *feature* na camada `Application`:

```
src/GrelhaHub.Application/Features/<Modulo>/<NomeDoCaso>/
├── <NomeDoCaso>Command.cs          # ou Query
├── <NomeDoCaso>Handler.cs
├── <NomeDoCaso>Validator.cs        # FluentValidation
└── <NomeDoCaso>Response.cs
```

E para um teste que cita as regras:

```
tests/GrelhaHub.Application.Tests/Features/<Modulo>/<NomeDoCaso>Tests.cs
tests/GrelhaHub.Integration.Tests/<Modulo>/<NomeDoCaso>IntegrationTests.cs
```

**Convenção de teste — obrigatória:**

```csharp
[Fact(DisplayName = "RN-EST-018 — item cancelado após produção gera perda, não devolução")]
public async Task CancelarItemEmProducao_GeraPerda()
{
    // ...
}
```

O ID da regra no `DisplayName` é o que torna a rastreabilidade real: quando RN-EST-018 mudar
de estado para `REJ`, um `grep RN-EST-018` encontra a spec, o handler e o teste.

---

## 5. Ordem de escrita das specs

As specs seguem a dependência do domínio, não a ordem dos módulos. A cadeia crítica é
`venda → ficha técnica → baixa de insumo`, e ela só fecha se a base existir antes.

| Onda | Specs | Depende de |
|------|-------|-----------|
| **0 — fundação** | Unidade, parâmetros com vigência, usuário/perfil/permissão, auditoria, dia operacional | Nada. **RN-GER-006 e RN-GER-009 precisam estar decididas** |
| **1 — cadastro** | Categoria, produto, preço com vigência, insumo, unidade e conversão, grupos de modificadores | Onda 0 |
| **2 — ficha técnica** | Ficha técnica versionada, item estocável recursivo (sub-receita), percentual de perda, rendimento | Onda 1. **Bloqueada por RN-PRO-004** |
| **3 — estoque** | Lote, movimento, saldo derivado, entrada por compra, FEFO, perda, inventário | Onda 2 |
| **4 — venda** | Abrir pedido, lançar item com ajustes, ciclo de vida, confirmar (dispara baixa e ordem de produção), cancelar | Ondas 2 e 3 |
| **5 — salão e delivery** | Comanda, mesa, transferência e unificação, zona, entrega, rota | Onda 4 |
| **6 — pagamento e caixa** | Formas de pagamento, pagamento múltiplo, troco, abrir/fechar caixa, sangria e suprimento | Onda 4 |
| **7 — produção** | Ordem de produção, estação, etapas, KDS | Onda 4 |
| **8 — financeiro** | Plano de contas, centro de custo, contas a pagar e receber, DRE derivado | Onda 6 |
| **9 — analítico** | CMV por produto, ticket médio, curva de vendas, RFM, sugestão de compra | Todas |
| **10 — periféricos** | Compras, CRM/cupom, RH | Nível Modelo — cadastro e estrutura |
| **11 — fiscal** | Documento fiscal, NCM, CFOP, Simples Nacional | **Por último.** Nada depende dele; o valor exige homologação externa |

**A onda 0 não é opcional e não é "infra".** Dia operacional e vínculo a unidade entram em
toda tabela; adicioná-los na onda 4 é refazer as ondas 1 a 3.

---

## 6. Definição de pronto

Uma spec só passa a `ENTREGUE` quando **todos** os itens abaixo estão verdadeiros:

- [ ] Todas as regras citadas no cabeçalho estão implementadas na camada declarada
- [ ] Existe ao menos um teste por regra, com o ID no `DisplayName`
- [ ] Os critérios de aceite Given-When-Then passam como teste de integração
- [ ] A migration EF Core reflete as constraints declaradas na seção de modelo
- [ ] Nenhum literal de negócio no código — valores comerciais vieram de parâmetro
      ([`04-parametros`](04-parametros-do-sistema.md))
- [ ] A tabela de rastreabilidade do [catálogo §16](03-catalogo-de-regras-de-negocio.md#16-rastreabilidade-regra--spec) foi atualizada
- [ ] Se surgiu regra nova, ela está no catálogo — **não** apenas na spec
- [ ] **Toda regra que BLOQUEIA o usuário tem, na interface, o caminho para cumpri-la**
      — ver o aviso abaixo
- [ ] `dotnet build` e `dotnet test` verdes

> ### ⚠⚠ O modo de falha mais repetido deste projeto: gate sem porta
>
> Aconteceu **quatro vezes** até 17/08/2026 — todas corrigidas —, sempre igual: a regra é imposta no pipeline, tem teste
> de integração, cita o ID na mensagem de erro — e **a tela não oferece como obedecer**. O usuário
> lê o identificador da regra e não tem o que fazer com ele.
>
> | Regra | O beco | Situação |
> |-------|--------|-----------|
> | `RN-GER-015` | "Abra uma sessão de suporte" — sem nenhum lugar para abrir | Corrigido |
> | `RN-GER-020` | Recusava o módulo sem dizer qual dependência faltava | Corrigido |
> | `RF-SEG-005` | "Troque a sua senha antes de continuar" — sem tela de troca | Corrigido |
> | `RN-VEN-043` | Categoria inativada **desaparecia da tela de gestão** — sem como reativá-la, com os produtos presos num grupo invisível | Corrigido |
>
> A causa é estrutural, não descuido: a definição de pronto media *"o gate é imposto e testado"*
> e **nada media** *"o usuário consegue cumprir"*. O item acrescentado acima é a correção.
>
> **O quarto caso é o primeiro encontrado pelo próprio item, e não por alguém travado na tela.**
> Ele não veio de um gate que recusa: veio de uma consulta que filtrava. A pergunta de revisão
> — *para onde o usuário vai?* — não pergunta se existe mensagem de erro; pergunta se existe
> caminho. Categoria escondida não dava erro nenhum: ela simplesmente sumia, e a única saída era
> `UPDATE` na mão no banco de produção. **Beco sem mensagem é mais difícil de ver do que beco com
> mensagem**, e é por isso que a varredura tem de percorrer o caminho de volta de toda operação
> reversível — não só o de ida.
>
> A pergunta de revisão é uma só: **se esta regra recusar a operação, para onde o usuário vai?**
> Se a resposta for "para o suporte", falta tela.

---

## 7. Quando a implementação descobre uma regra

Acontece o tempo todo, e é saudável. O procedimento:

1. **Pare o código.** Não implemente "provisoriamente".
2. Escreva a regra no catálogo, no módulo certo, com ID sequencial, estado `HIP` e origem
   `IMP` (descoberta em implementação).
3. Se a regra tiver valor comercial configurável, crie o parâmetro em
   [`04-parametros`](04-parametros-do-sistema.md).
4. Se ela contradisser uma regra existente, **não** edite a existente em silêncio: registre
   o conflito, marque a regra afetada como `CNF` e leve à validação.
5. Acrescente a regra ao cabeçalho da spec e escreva o critério de aceite.
6. Volte ao código.

O custo desse desvio é de minutos. O custo de não fazê-lo é um sistema cujo comportamento
real não está em lugar nenhum — que é exatamente o que o Document First existe para evitar.

---

## 8. Índice de specs

### Onda 0 — fundação

| Spec | Título | Situação | Regras |
|------|--------|----------|--------|
| [SPEC-GER-001](specs/SPEC-GER-001-unidade-e-convencoes-do-modelo.md) | Unidade e convenções do modelo | **`ENTREGUE`** | `RN-GER-002`, `003`, `004`, `005`, `009` |
| [SPEC-GER-002](specs/SPEC-GER-002-dia-operacional.md) ⚠⚠ | Dia operacional | **`ENTREGUE`** | `RN-GER-006` |
| [SPEC-GER-003](specs/SPEC-GER-003-parametros-com-vigencia.md) | Parâmetros com vigência | **`ENTREGUE`** — CA-6 fechado pela `005` | habilita as 9 regras `PAR` |
| [SPEC-GER-004](specs/SPEC-GER-004-usuarios-perfis-e-permissoes.md) | Usuários, perfis e permissões **+ acesso de plataforma** | **`ENTREGUE`** — CA-4 e CA-6 abertos por dependência de onda | `RN-GER-010`, `012`, `014`, `015`, `016`, `022`, `RN-RH-006`, `RN-VEN-041` |
| [SPEC-GER-006](specs/SPEC-GER-006-modulos-por-unidade.md) | Módulos habilitados por unidade | **`ENTREGUE`** | `RN-GER-017` a `021` |
| [SPEC-GER-005](specs/SPEC-GER-005-trilha-de-auditoria.md) | Trilha de auditoria | **`ENTREGUE`** 17/08/2026 | `RN-GER-001`, `011`, `002`, `012` |
| [SPEC-GER-007](specs/SPEC-GER-007-pessoas-e-delegacao-de-acesso.md) ⚠⚠ | Pessoas, delegação de acesso e telas de administração | **`ENTREGUE`** 17/08/2026 | `RN-GER-023`, `024`, `025`, `013`, `010` |
| [SPEC-GER-008](specs/SPEC-GER-008-recuperacao-de-acesso.md) ⚠ | Recuperação de acesso por e-mail — link de uso único, convite e "esqueci minha senha" | **`ENTREGUE`** 18/08/2026 | `RN-GER-032`, `033`, `034`, `035` |
| [SPEC-GER-009](specs/SPEC-GER-009-horario-endereco-e-dia-operacional.md) ⚠⚠ | Horário de funcionamento, endereço e o dia operacional por unidade — **revisa `RN-GER-006`** | **`ENTREGUE`** 18/08/2026 | `RN-GER-006`, `036`, `037`, `RN-VEN-048` |

**Ordem de implementação:** 001 → 002 → 003 → 004 → 005. As duas primeiras entram na mesma
migration de fundação; 004 precisa de 001; 005 precisa de 001 e 004 (a trilha referencia o
usuário).

### Fora da ordem — fatia vertical antecipada

| Spec | Título | Situação | Regras |
|------|--------|----------|--------|
| [SPEC-VEN-001](specs/SPEC-VEN-001-cardapio-publico-e-pedido.md) | Cardápio público e pedido online | **`ENTREGUE`** 17/08/2026 | 26 regras — ver cabeçalho |
| [SPEC-VEN-002](specs/SPEC-VEN-002-imagem-de-produto-e-logo.md) | Imagem do produto e logo da unidade | **`ENTREGUE`** 17/08/2026 — antecipada da onda 1 | `RN-VEN-042`, `RN-GER-026` |
| [SPEC-VEN-004](specs/SPEC-VEN-004-fila-de-monitoramento.md) ⚠ | Fila de monitoramento — o que ainda está na casa | **`ENTREGUE`** 19/08/2026 | `RN-VEN-054`, `RN-VEN-055` |

### Onda 1 — cadastro

| Spec | Título | Situação | Regras |
|------|--------|----------|--------|
| [SPEC-VEN-003](specs/SPEC-VEN-003-cadastro-do-cardapio.md) | Cadastro do cardápio — categoria e produto | **`ENTREGUE`** 17/08/2026 | `RN-VEN-043`, `044`, `045`, `020`, `032` |

Falta da onda: **preço com vigência** (`RN-VEN-010`), **insumo**, **unidade de medida e
conversão**. A `SPEC-VEN-003` é a primeira, e é ela que fecha a lacuna de o cardápio não ter tela
de cadastro nenhuma — nem para produto, cujo comando existia desde a `SPEC-VEN-001` sem chamador.

Pedida pelo time para ver o sistema funcionando ponta a ponta e iniciar o repositório.
Atravessa camadas que ainda não têm spec própria, e o faz de forma **simplificada**: a
[§8 da spec](specs/SPEC-VEN-001-cardapio-publico-e-pedido.md#8-fora-de-escopo--a-dívida-declarada)
lista cada simplificação e a onda que a resolve. O critério que ela respeita é implementar
**menos** do que o catálogo exige, nunca **diferente**.

> **O que essa antecipação custou, registrado para a banca:** três comportamentos foram
> escritos no código antes de existirem no catálogo, e citaram por aproximação o ID de outra
> regra. A auditoria de rastreabilidade os transformou em `RN-VEN-039`, `RN-VEN-040` e
> `RN-GER-013` (catálogo v1.3). É a demonstração prática de por que a regra 1 do processo é a
> única inegociável — e de que o desvio é detectável, desde que alguém audite.

### Três padrões que a onda 0 estabelece

O valor destas cinco specs não é a funcionalidade — é fixar decisões que as 40 tabelas
seguintes vão herdar sem redecidir:

1. **Vigência** (`SPEC-GER-003`) — `vigencia_inicio`, `vigencia_fim` e `EXCLUDE` contra
   sobreposição. O mesmo desenho serve `preco_produto` (`RN-VEN-010`) e `ficha_tecnica`
   (`RN-PRO-002`). Vale extrair para um helper de migration em vez de reescrever três vezes.
2. **Livro-razão** (`SPEC-GER-005`) — registro imutável, correção por registro contrário.
   O mesmo desenho serve `movimento_estoque` (`RN-EST-001`) e `movimento_caixa`.
3. **Interceptor em vez de chamada** (`SPEC-GER-001` e `005`) — `unidade_id`, `criado_em`,
   `criado_por` e auditoria são preenchidos automaticamente. Nenhum handler os atribui: o
   que depende de o desenvolvedor lembrar é o que falta justamente quando importa.

### Ondas seguintes

Escritas quando a onda 0 estiver entregue e — para as ondas 2 e 3 — quando `RN-PRO-004` e
`RN-EST-011` saírem de `HIP` na entrevista.
