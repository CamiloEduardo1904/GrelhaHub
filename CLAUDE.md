# GrelhaHub — instruções para agentes

ERP para restaurantes e deliveries (nicho hamburgueria). Trabalho acadêmico de 9 sprints,
que precisa ser **defensável em banca**: decisão sem justificativa registrada é decisão
errada, mesmo quando o código funciona.

> **Existe um agente dedicado a este projeto:** `grelhahub-engineer`
> (`.claude/agents/grelhahub-engineer.md`). Use-o para implementar, escrever specs, modelar
> o banco ou responder dúvidas de contexto — ele carrega o catálogo de regras e os ADRs.

---

## A regra inegociável

> **Nenhuma regra de negócio nasce no código.**

O projeto é conduzido em **Document First** e **Spec-Driven Development**:

```
catálogo de regras  →  spec  →  código + teste que cita a regra
```

Se aparecer um comportamento necessário que não está no catálogo:
**pare o código**, escreva a regra em `docs/03-catalogo-de-regras-de-negocio.md` com estado
`HIP` e origem `IMP`, acrescente-a ao cabeçalho da spec, e só então continue.
Não implemente "provisoriamente".

> **O modo de falha real, observado neste repositório em 17/08/2026:** ninguém inventa uma
> regra do nada. O que acontece é **citar o ID mais parecido** — o cancelamento com motivo
> citou `RN-VEN-014`, que é taxa de serviço; o total do pedido citou `RN-VEN-012`, que é
> desconto. O código fica plausível, o comentário fica plausível, e a rastreabilidade morre
> em silêncio: um `grep` pelo ID devolve o teste errado. **Se a regra não descreve exatamente
> o comportamento, ela não é a regra** — leia o enunciado inteiro antes de citar o ID, nunca
> se baseie na lembrança do número.

---

## Leia antes de agir

| Arquivo | Para quê |
|---------|----------|
| `docs/README.md` | Mapa e estado atual |
| `docs/03-catalogo-de-regras-de-negocio.md` | **Fonte de verdade** — 180 regras |
| `docs/06-guia-de-specs.md` | Formato da spec, ondas de implementação, definição de pronto |
| `docs/adr/README.md` | 16 decisões e 4 em aberto |
| `docs/02-glossario.md` | Vocabulário controlado — use os termos exatos |
| `docs/04-parametros-do-sistema.md` | Valores configuráveis |
| `docs/08-design-system.md` | **Base visual obrigatória** — leia antes de qualquer tela |

**Nunca responda de memória sobre uma regra** — `grep` pelo ID. As regras mudam de estado a
cada entrevista.

---

## As sete regras estruturais

Errar aqui custa retrabalho de projeto, não de código.

| Regra | O que exige |
|-------|-------------|
| `RN-GER-006` ⚠⚠ | Dia operacional começa na **virada cadastrada da unidade** (`RN-GER-036`) — não é mais 04h00 fixo. **Nunca agrupe por data de criação** — use a coluna gerada `data_operacional`, que lê `hora_virada_operacional` e `fuso_operacional` **da própria linha do pedido** |
| `RN-PRO-004` ⚠⚠ | Ficha técnica recursiva: o molho da casa é produto **e** insumo. Exige proteção contra ciclo |
| `RN-EST-001` | Saldo **nunca** é coluna editável — é soma dos movimentos, materializada em view |
| `RN-EST-004` + `RN-EST-011` | Todo movimento referencia um **lote**; custo é o do lote consumido, consumo **FEFO**. Nunca custo médio |
| `RN-VEN-008` + `RN-VEN-010` | Preço praticado é copiado na venda; preço de tabela tem vigência |
| `RN-VEN-018` | `Aberto → Confirmado → Em produção → Pronto → Entregue → Finalizado`. Baixa de estoque e ordem de produção disparam na **Confirmação** |
| `RN-GER-009` | `unidade_id` em toda tabela transacional, **primeira coluna** de toda chave única e índice |

### Armadilhas

- Cancelamento **não** devolve estoque se o item já entrou em produção — gera **perda**
  (`RN-EST-018`). Antes da produção, não gera movimento (`RN-EST-019`)
- Nenhum literal de negócio no código nem em `CHECK`. `10%` é `PAR-VEN-002`.
  `CHECK` valida domínio (`entre 0 e 100`), nunca política (`<= 10`)
- **Dependência entre módulos vive em `FechoDeDependencia`, e só lá** (`RN-GER-020`): quem
  habilitar módulo por um caminho novo chama essa função. O fecho é **transitivo**, e o núcleo
  conta como presente
- **Ninguém concede acesso que não possui** (`RN-GER-024` ⚠⚠): criar usuário passa pela
  `MatrizDeDelegacao`, e `ValidadorDeDelegacao` **recusa a partida** se um perfil novo não
  declarar o que delega. Perfil novo em `CatalogoDeAcesso` exige linha na matriz
- **Arquivo do usuário: o formato vem da assinatura, nunca da extensão** (`RN-VEN-042`). E o
  nome no disco é gerado pelo sistema — o nome recebido nunca toca o sistema de arquivos
- Quantidade de insumo: **4** casas decimais. Moeda: 2
- Custo, CMV e margem **não são retornados pela API** sem permissão (`RN-GER-012`)
- Nenhum registro transacional é excluído fisicamente (`RN-GER-002`)
- O termo é `unidade`, não `estabelecimento`; `estacao`, não `praça`

---

## Convenções

### Backend

```
backend/src/
├── GrelhaHub.Domain/          sem dependências — nem EF Core, nem MediatR
├── GrelhaHub.Application/     Features/<Modulo>/<Caso>/ com Command|Query + Handler + Validator
├── GrelhaHub.Infrastructure/  EF Core, migrations, PostgreSQL
└── GrelhaHub.Api/             controllers, auth, DI
```

- Domínio em **português** (`Pedido`, `FichaTecnica`, `MovimentoEstoque`); termos de
  framework em inglês (`Handler`, `Command`)
- Banco em `snake_case`, PK `uuid`, `timestamptz` em UTC, `numeric` para dinheiro —
  nunca `float`
- Versões de pacote **só** em `backend/Directory.Packages.props` (CPM)
- Release ativa `TreatWarningsAsErrors` — build de Release limpo é obrigatório

### Frontend — direção Chapa

Base visual oficial desde 17/08/2026 (ADR-0012). Detalhe em `docs/08-design-system.md`.

**A regra central:** o kit de marca diz que *"a barra da ligadura sempre em cor de destaque
diferente das demais"*. Na interface isso vira: **em qualquer grupo, exatamente uma barra é
âmbar — a ativa**. Estado do pedido, aba, etapa, seleção.

**Cartoon por fora, sem graça por dentro.** Personalidade na moldura; dentro do dado, nada.

- Sombra **sólida deslocada** (`5px 5px 0` em carvão). Raio de desfoque é plástico
- Dado em **mono** com `tabular-nums`; prosa em sans; peso só no número do pedido e no total
- **Prosa usa a cor do texto, nunca `--muted`** (`CHAPA-10`) — `--muted` é rótulo e metadado. E
  texto **nunca** leva `opacity`: o contraste composto não aparece em ferramenta de verificação
- **Quem declara `background` declara `color`** (`CHAPA-11`): sem isso a superfície herda a cor
  da seção que a contém — foi assim que a comanda da capa ficou creme sobre creme
- Alvo de toque **48 px** na operação (acima dos 44 do `RNF-010`)
- Sem webfont, sem Inter/Geist, sem gradiente decorativo, sem glassmorphism
- Emoji não é ícone nem estado — status é selo com borda e cor
- **A vitrine não anuncia o que não existe** (`CHAPA-09`): ela lista **apenas o que está no ar**.
  Sem depoimento inventado, sem número de adoção, sem preço nem contato fixados no código — o
  texto vive em `frontend/src/features/site/conteudo.ts`, e o botão de contato **não renderiza**
  enquanto não houver canal configurado
- **Não existe cadastro anônimo de restaurante** (`SPEC-GER-007` §4): ninguém ganha conta sem
  contrato. Quem cria estabelecimento é o usuário de plataforma, e o **harness de teste cria por
  essa rota** — não reintroduza um atalho anônimo para facilitar teste
- **Sem cadeado em campo que a API não devolve** (`RN-GER-012`, `CHAPA-05`): se o perfil não
  vê o custo, não há blur nem "desbloqueie" — o campo não existe

Componente novo entra no design system **antes** de entrar no código, mesma regra do catálogo.

### Testes

O ID da regra vai no `DisplayName`. É isso que torna a rastreabilidade real:

```csharp
[Fact(DisplayName = "RN-EST-018 — item cancelado após produção gera perda, não devolução")]
```

Teste de integração usa **PostgreSQL real** (Testcontainers). O provider in-memory do
EF Core não executa constraint, trigger nem coluna gerada — daria falsa confiança
justamente nas regras que mais importam (ADR-0009). O harness está pronto: veja
"O harness de integração" em Estado atual. **Precisa de Docker rodando.**

Quando a regra existe nas duas camadas — C# e banco —, o teste que importa é o de
**paridade**: `DiaOperacionalParidadeTests` compara `DiaOperacional.De()` com a coluna gerada
nas 96 amostras do dia. Duas implementações da mesma regra divergem em silêncio; é esse teste
que transforma a divergência em falha.

### Onde cada regra é imposta — ADR-0009

O projeto usa **as duas camadas**, deliberadamente:

> O banco garante que o dado nunca fica inválido. A aplicação garante que o usuário nunca
> chega até lá.

Ordem de preferência: `MOD → constraints → GEN → TRG → VW → APP`. A aplicação é o último
recurso — o que a disciplina avalia é o modelo. Mas a **mensagem de erro ao usuário vem
sempre da aplicação**: erro de constraint que vaza para o usuário é defeito da aplicação.

---

## Comandos

```bash
# Backend
cd backend
dotnet build -c Release      # Release trata warning como erro
dotnet test -c Release       # integração sobe PostgreSQL efêmero: exige Docker rodando

# Frontend
cd frontend
npm ci                       # sem isto, o `npm run` abaixo sai 0 sem rodar nada
npm run lint && npm run build
```

---

## Estado atual

Sprint 1 & 2 — documentação entregue. **180 regras catalogadas: 154 em `HIP`, 26 em `DEC`;
nenhuma validada com cliente.** A **onda 0 está entregue** — `SPEC-GER-001` a `007`. Da **onda 1**, o cadastro do cardápio
está entregue (`SPEC-VEN-003`). Em 18/08/2026 a `SPEC-GER-007` ganhou a **equipe da plataforma** (`RN-GER-030`, `RN-GER-031`) e a `SPEC-GER-008` entregou a **recuperação de acesso por e-mail** (`RN-GER-032` a `035`, ADR-0016): link de uso único, convite na criação de usuário e "esqueci minha senha" — o que fecha o beco de quem perdia a senha do acesso de plataforma.

Restam **dois critérios abertos por dependência de onda**, ambos declarados na sua spec:
`SPEC-GER-004` CA-4 (gerente vê custo → espera a ficha técnica, onda 2) e CA-6 (colaborador
inativo → espera o módulo RH, onda 10).

**Existe uma fatia vertical funcionando** — `SPEC-VEN-001`, fora da ordem das ondas:
cadastro de restaurante → cardápio público em `/c/:slug` → pedido → painel do restaurante.
Backend, frontend, migration `Fundacao` e pipeline de deploy. 26 regras com código, e
**56 regras têm o ID citado no `DisplayName` de um teste** ao todo. A dívida de cada simplificação está na §8 da spec — **um pedido
que não baixa estoque hoje não significa que a regra mudou**, significa que o estoque ainda
não existe.

### O que está verde e o que não está

| Gate | Estado |
|------|--------|
| `dotnet build -c Release` | ✔ 0 avisos |
| `dotnet test -c Release` | ✔ **289 testes** — 76 de domínio + 13 de aplicação + 200 de integração |
| Testes de integração | ✔ PostgreSQL real via Testcontainers (`postgres:16-alpine`). **Exige Docker rodando** |
| `npm run lint && npm run build` | ✔ limpo — `npm ci` primeiro (204 pacotes, 0 vulnerabilidades) |

> ⚠ **`npm run` mascara ausência de dependência.** Sem `node_modules`, `npm run lint` imprime
> "eslint não é reconhecido" e ainda assim **sai com código 0** — o gate parece verde e não
> rodou nada. Confira o código de saída do `eslint`/`tsc`, não o do `npm run`.

### `RF-SEG-005` — a troca no primeiro acesso, e o detalhe que engana

`/trocar-senha` existe, e `ExigeSessao` manda para lá quando a pendência está aberta.

⚠ **Trocar a senha NÃO derruba a pendência do token que já está na mão:** o claim
`trocar_senha` viaja nele. Depois do `POST /v1/usuarios/minha-senha`, a tela **reautentica** com a
senha nova para receber um token limpo — e é por isso que o e-mail vem na resposta do login. Quem
mexer nesse fluxo e só navegar para o painel vai ver "Troque a sua senha" de novo, com a senha já
trocada.

> **Este foi o terceiro gate que subiu sem porta na interface** — os outros dois: sessão de suporte
> e dependência de módulo. A pergunta de revisão está na definição de pronto do guia de specs:
> **se esta regra recusar a operação, para onde o usuário vai?**
>
> **O quarto apareceu na `SPEC-VEN-003`, e é o primeiro que o item de revisão pegou** em vez de um
> usuário travado: `ListarCardapioInterno` filtrava `c.Ativa`, então **categoria inativada
> desaparecia da tela de gestão e não havia como reativá-la** — os produtos ficavam presos num
> grupo invisível. Beco **sem** mensagem de erro é mais difícil de ver do que beco com mensagem:
> percorra o caminho de volta de toda operação reversível, não só o de ida.

### O harness de integração

`tests/GrelhaHub.Integration.Tests/Infraestrutura/` — contêiner PostgreSQL + a API real, sem
nenhum dublê. Um contêiner por assembly; cada teste limpa o banco antes de rodar, com a lista
de tabelas descoberta no catálogo do PostgreSQL (não escrita a mão, senão as ondas seguintes
deixam resíduo silencioso).

- Herde de `TesteDeIntegracao` e marque a classe com `[Collection(ColecaoDeIntegracao.Nome)]`
- Monte o cenário pelas **rotas da API** (`CadastrarRestauranteAsync`, `CriarProdutoAsync`),
  não por `INSERT`: dado que entra por caminho que o sistema não tem escapa de validator,
  de autorização e do vínculo de unidade
- Use `Contexto(restaurante)` só para **conferir** o que foi gravado
- Para verificar isolamento entre unidades, use token ou `slug` — nunca `ContextoFixo`, que
  provaria apenas que o teste soube preencher um campo

**Próximo trabalho de código:** o resto da **onda 1 — cadastro**: **preço com vigência**
(`RN-VEN-010`), **insumo**, **unidade de medida e conversão**. Já entregues: a onda 0 inteira —
inclusive `SPEC-GER-007` (CPF, delegação de perfil e telas de administração) —, `SPEC-VEN-002`
(imagem de produto e logo) e `SPEC-VEN-003` (**cadastro do cardápio**: categoria e produto, com
`ativo` cadastral separado de `disponível` do turno). O índice está em
[`docs/06-guia-de-specs.md`](docs/06-guia-de-specs.md#8-índice-de-specs).

⚠ **`ativo` e `disponível` no produto não são o mesmo campo com dois nomes** (`RN-VEN-044`). O
efeito no cliente é idêntico — o produto não aparece —, e é por isso que fundi-los é tentador. São
duas rotas separadas de propósito: `PATCH .../situacao` (cadastral) e `PATCH .../disponibilidade`
(do turno).

⚠⚠ **A onda 3 começa com um conflito aberto, não com código.** `RN-EST-021` a `023` já enunciam o
gatilho da baixa — confirmação, ficha explodida, FEFO, mesma transação, idempotência e devolução
na janela antes da produção. Mas `RN-EST-023` **revisa** `RN-EST-019`, e o **conflito C10** está
aberto: se a baixa dispara na Confirmação (`RN-VEN-018`, estrutural) ou na Produção **só a
entrevista decide** — perguntas 8 e 9 da fila. E o modo *permitir* de `PAR-VEN-003` não é
implementável enquanto ninguém disser o que ele significa, já que `RN-EST-009` proíbe saldo
negativo (pergunta 10b). **Não implemente a baixa antes dessas três respostas.**

### Dinheiro e quantidade nunca são `decimal` — `SPEC-GER-001` §4

> **Um `decimal` solto não distingue R$ 12,00 de 12 gramas**, e o compilador não impede somar
> os dois. Pior: quantidade tratada com a precisão do dinheiro grava `0,1563` como `0,16`
> **sem erro nenhum**, e o desvio entra direto no CMV com um número plausível.

- **`Dinheiro`** — `numeric(12,2)`, `RN-GER-003`. Construção só por `Dinheiro.De(valor)`;
  arredonda `AwayFromZero`, que é o arredondamento comercial e não o padrão bancário do .NET
- **`Quantidade`** — `numeric(14,4)`, `RN-GER-004`. Para insumo, que se **mede**
- **Contagem de produto vendido é `int`** — dois lanches, não 2,0000 lanches (`RN-VEN-011`)
- **Não há convenção para `decimal` cru, de propósito.** `GarantirAusenciaDeDecimalCru`
  **recusa o modelo na partida** e diz qual propriedade corrigir
- Os DTOs de API seguem em `decimal`: value object é conceito de domínio, não formato de fio

⚠ **Nunca compare `Guid` com `Guid?` dentro de árvore de expressão.** `p.Id != produtoIgnorado`
com `produtoIgnorado` nulo é `p.id <> NULL` em SQL — que filtra **todas** as linhas e devolve
contagem zero. O EF Core compensa gerando `(p.id <> @p OR @p IS NULL)`, mas **a consulta compilada é
reaproveitada**, e o resultado passa a depender de qual chamada compilou primeiro. O sintoma é o pior
que existe: a classe de teste passa sozinha e falha com a suíte inteira (`SPEC-VEN-003` §11.7). Use
`Where` condicional: `if (id is Guid x) consulta = consulta.Where(p => p.Id != x);`

⚠ **O EF Core não traduz acesso a membro de propriedade com conversor de valor.**
`p.Preco.Valor` não vira SQL, e `Sum(i => i.PrecoPraticado * i.Quantidade)` não compila para
consulta. Filtre e ordene no banco; monte o DTO em memória. Para agregação de verdade, use
view (mecanismo `VW` do catálogo), não `Sum` sobre value object.

Enquanto `RN-GER-006` e `RN-PRO-004` estiverem em `HIP`, qualquer modelagem da ficha técnica
ou da dimensão tempo é provisória. Diga isso ao pedir ou receber trabalho nessas áreas — não
recuse, mas registre que a decisão pode virar retrabalho.

### Decisões do time — 17/08/2026

- **FIS é o último módulo** (onda 11). Nada depende dele, e o valor exige homologação
  externa. As regras seguem catalogadas e a modelagem reserva o espaço
- **RH permanece no MVP**, em nível Modelo — `RN-LOG-005` depende dele
- **`RN-VEN-035`** — falta de insumo **sempre** gera alerta na tela, nos três modos de
  `PAR-VEN-003`. O parâmetro decide a fricção (recusar, exigir confirmação, informar),
  **nunca se o operador é avisado**

---

## Idioma

Documentação, domínio e mensagens em **português do Brasil**.
