# SPEC-VEN-003 — Cadastro do cardápio

| Campo | Valor |
|-------|-------|
| ID | `SPEC-VEN-003-cadastro-do-cardapio` |
| Módulo | VEN |
| Situação | `ENTREGUE` — 17/08/2026 |
| Onda | 1 — cadastro |
| Regras implementadas | `RN-VEN-043`, `RN-VEN-044`, `RN-VEN-045`, `RN-VEN-046`, `RN-VEN-020`, `RN-VEN-032`, `RN-GER-003`, `RN-GER-009`, `RN-GER-010`, `RN-GER-012` |
| ADRs aplicáveis | [ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md), [ADR-0013](../adr/0013-acesso-de-plataforma.md) |
| Depende de | `SPEC-VEN-001`, `SPEC-VEN-002`, `SPEC-GER-004` |
| Data | 17/08/2026 |
| Entregue em | 17/08/2026 |

---

## 1. Objetivo

Dar ao restaurante o **cadastro do próprio cardápio**: criar e organizar categorias, criar e
editar produtos, e tirar item do ar — de duas maneiras diferentes, porque são duas decisões
diferentes.

> **O que existia antes desta spec era um cardápio que só o `INSERT` de teste sabia povoar.**
> `SalvarProdutoCommand` estava escrito desde a `SPEC-VEN-001` e **não tinha tela**. Categoria não
> tinha nem comando: o domínio trazia `Categoria.Renomear` e `Categoria.Inativar` sem um único
> chamador em todo o repositório. Um restaurante que se cadastrasse pelo caminho normal chegava
> ao painel com o cardápio vazio e **nenhum botão** — a vitrine pública funcionava e não havia
> como pôr nada nela.

Esta spec também **paga uma dívida de rastreabilidade em sentido incomum**: três restrições de
banco existiam desde a migration `Fundacao` sem regra que as enunciasse.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-VEN-043` | Categoria com nome único na unidade e ordem de exibição; categoria inativa **esconde** os produtos sem inativá-los | Banco + aplicação | `uq_categoria_nome` (UQ) + validação no handler + filtro na consulta pública |
| `RN-VEN-044` | Produto com nome único na unidade, preço positivo, e `ativo` (cadastral) ≠ `disponível` (do turno) | Banco + aplicação | `uq_produto_nome`, `ck_produto_preco` + dois comandos distintos |
| `RN-VEN-045` | Categoria e produto **não são excluídos**, apenas inativados | Aplicação | **Ausência de rota de exclusão** + FK de `item_pedido` |
| `RN-VEN-046` | No máximo `PAR-VEN-004` produtos **ativos** por categoria — 50 por padrão | Aplicação | `TetoDeProdutos`, chamado pelos **dois** caminhos de entrada |
| `RN-GER-003` | Dinheiro em duas casas | Aplicação | Máscara acumuladora de centavos no campo de preço |
| `RN-VEN-020` | Produto inativo não entra em pedido novo nem aparece no cardápio | Aplicação | Filtro em `ObterCardapioPublico` e conferência ao abrir pedido |
| `RN-VEN-032` | Preço vigente maior que zero | Banco + aplicação | `ck_produto_preco` + validator |
| `RN-GER-009` | `unidade_id` em toda transacional, primeira coluna da chave única | Banco | `uq_categoria_nome (unidade_id, nome)`, `uq_produto_nome (unidade_id, nome)` |
| `RN-GER-010` | Cada perfil vê e faz o que a função exige | Aplicação | `cardapio.gerenciar` na escrita, `cardapio.visualizar` na leitura |
| `RN-GER-012` | Custo, CMV e margem não são retornados sem permissão | Aplicação | Nenhum campo de custo em nenhum DTO desta spec |

### As três restrições que estavam no banco sem estar no catálogo

| Objeto no banco | Desde | Regra que agora a enuncia |
|-----------------|-------|---------------------------|
| `uq_categoria_nome (unidade_id, nome)` | migration `Fundacao` | `RN-VEN-043` |
| `uq_produto_nome (unidade_id, nome)` | migration `Fundacao` | `RN-VEN-044` |
| `ck_produto_preco (preco > 0)` | migration `Fundacao` | `RN-VEN-044` (e `RN-VEN-032`, que já existia) |

> **Comportamento sem regra é o inverso do defeito habitual do projeto, e igualmente grave.** O
> modo de falha que o `CLAUDE.md` descreve é citar o ID errado: o código fica plausível e a
> rastreabilidade morre. Aqui não havia ID nenhum — o banco recusava nome repetido e **ninguém
> sabia se era decisão de negócio ou hábito de quem escreveu a migration**. A diferença é
> prática: na entrevista, decisão se valida e hábito não tem o que validar. Nenhum comportamento
> mudou nesta spec por causa disso; o que mudou é que agora há o que perguntar ao cliente.

## 3. Contrato

### `GET /api/v1/cardapio` — cardápio da gestão

Já existia. **Muda uma coisa:** passa a devolver **categorias inativas** também, com o campo
`ativa`. Ver §4.

```json
[
  {
    "id": "…", "nome": "Lanches", "ordem": 1, "ativa": true,
    "produtos": [
      { "id": "…", "nome": "X-Tudo", "descricao": "…", "preco": 32.90,
        "ativo": true, "disponivel": true, "categoriaId": "…", "urlImagem": "/api/v1/arquivos/…" }
    ]
  }
]
```

### `POST /api/v1/cardapio/categorias` — cria ou atualiza

| Campo | Tipo | Obrigatório | Observação |
|-------|------|:-----------:|------------|
| `id` | `uuid?` | | Nulo cria; preenchido atualiza |
| `nome` | `string` | ✔ | Até 100, único na unidade (`RN-VEN-043`) |
| `ordem` | `int` | ✔ | 0 a 999 — ordem de exibição |

**Saída:** `uuid` da categoria.

### `PATCH /api/v1/cardapio/categorias/{id}/situacao`

```json
{ "ativa": false }
```

Devolve `204`. **Não apaga** (`RN-VEN-045`) e **não toca nos produtos** (`RN-VEN-043`).

### `POST /api/v1/cardapio/produtos` — cria ou atualiza

Já existia; ganhou a conferência de nome único com mensagem legível.

| Campo | Tipo | Obrigatório |
|-------|------|:-----------:|
| `id` | `uuid?` | |
| `categoriaId` | `uuid` | ✔ |
| `nome` | `string` | ✔ |
| `descricao` | `string?` | |
| `preco` | `decimal` | ✔ |

### `PATCH /api/v1/cardapio/produtos/{id}/situacao` — **novo**

```json
{ "ativo": false }
```

O estado **cadastral** (`RN-VEN-044`). Devolve `204`.

### `PATCH /api/v1/cardapio/produtos/{id}/disponibilidade` — já existia

```json
{ "disponivel": false }
```

O estado **do turno**. Devolve `204`.

> **Duas rotas para dois estados, e não uma rota com um enum.** É a decisão de desenho mais
> importante desta spec e está em §4.

### Erros

| Situação | Código | Mensagem |
|----------|--------|----------|
| Nome de categoria repetido na unidade | 422 | "Já existe uma categoria com este nome." |
| Nome de produto repetido na unidade | 422 | "Já existe um produto com este nome." |
| Preço zero ou negativo | 422 | "O preço deve ser maior que zero." |
| Categoria informada não existe (ou é de outra unidade) | 422 | "Categoria não encontrada." |
| Categoria/produto de outra unidade | 404 | "… não encontrada." / "Produto não encontrado." |
| Sem `cardapio.gerenciar` | 403 | Mensagem do pipeline de permissão |

### Autorização

| Operação | Permissão |
|----------|-----------|
| Ler o cardápio da gestão | `cardapio.visualizar` |
| Criar/editar categoria e produto | `cardapio.gerenciar` |
| Alternar **situação** (cadastral) | `cardapio.gerenciar` |
| Alternar **disponibilidade** (turno) | `cardapio.gerenciar` |

> **`disponibilidade` deveria ser permissão de quem está no salão, e hoje não é.** Quem fica no
> balcão é Atendente, e é ele quem sabe que a picanha acabou — mas `cardapio.gerenciar` é de
> gerência. A separação exige uma permissão nova (`cardapio.disponibilidade`), que é mudança em
> `SPEC-GER-004` e não aqui. **Dívida declarada em §8**, não simplificação silenciosa.

## 4. Comportamento

### `ativo` e `disponível` são dois estados, e confundi-los é o erro previsível — `RN-VEN-044`

| | `ativo` | `disponível` |
|-|---------|--------------|
| Pergunta que responde | "este produto ainda faz parte do nosso cardápio?" | "tem hoje?" |
| Quem decide | quem cadastra — gerência | quem está no salão |
| Horizonte | temporada | turno |
| Volta sozinho? | não; alguém reativa | sim, amanhã |
| Efeito no público | desaparece | desaparece |
| Efeito no cadastro | sai da vitrine de gestão como inativo | continua normal, riscado |

O efeito no cliente final é **o mesmo** — o produto não aparece. É justamente por isso que a
tentação de fundir os dois num campo é forte, e é por isso que a regra precisa ser explícita: no
dia em que existir relatório de mix de produtos, "saiu do cardápio em março" e "acabou na
terça" são histórias diferentes, e um único booleano já teria perdido a diferença.

**Duas rotas, e não uma com `{ "estado": "ativo" | "disponivel" }`.** Um enum obrigaria o
frontend a mandar qual estado quer mudar, e o servidor a fazer `switch` — com o custo de que
qualquer erro de digitação viraria "mudou o outro estado" em silêncio. Rota separada erra 404,
que é ruído honesto.

### Categoria inativa esconde os produtos sem inativá-los — `RN-VEN-043`

`ObterCardapioPublico` filtra `c.Ativa`, e os produtos dentro dela **não são tocados**:
continuam com `ativo = true`. Reativar a categoria devolve o grupo inteiro exatamente como
estava.

> **A alternativa — inativar os produtos em cascata — é destrutiva e irreversível.** Ao
> reativar a categoria, o sistema não teria como saber quais produtos estavam inativos *antes*
> por decisão de alguém. A informação se perde na primeira operação. Esconder é reversível;
> propagar não é.

Isto é o que faz "esconder a seção de sobremesas no inverno" ser uma operação de um clique, e
não uma tarde de trabalho em março.

### ⚠ A consulta da gestão escondia categoria inativa — e isso era um beco

`ListarCardapioInterno` tinha `.Where(c => c.Ativa)`. Consequência:

> **Categoria inativada desaparecia da tela de gestão, e não havia como reativá-la.** Os
> produtos continuavam no banco, presos numa categoria invisível. A única saída era `UPDATE` na
> mão, no banco de produção.

É o quarto caso do padrão **gate sem porta** neste projeto — regra imposta sem caminho na
interface para cumpri-la —, e o primeiro descoberto **pelo item de definição de pronto** que os
três anteriores motivaram, em vez de por um usuário travado. O filtro saiu; a consulta da gestão
devolve tudo, com `ativa` no DTO, e a tela mostra a categoria inativa apagada com um botão
"Reativar".

**Vitrine da gestão ≠ vitrine do cliente.** O cliente vê o que pode comprar; a gestão vê o que
existe. Aplicar o filtro do público na consulta interna é o erro, e ele estava lá.

### Nome único: o banco garante, a aplicação explica — ADR-0009

`uq_categoria_nome` e `uq_produto_nome` já recusavam o duplicado. Sem a conferência no handler,
o usuário recebia **500 com texto de violação de índice do PostgreSQL** — que, pelo ADR-0009, é
defeito da aplicação e não do banco:

> O banco garante que o dado nunca fica inválido. A aplicação garante que o usuário nunca chega
> até lá.

A conferência no handler **não substitui** o índice: entre o `SELECT` que confere e o `INSERT`
existe uma corrida, e duas requisições simultâneas passariam as duas. O índice é o que fecha —
e a mensagem legível é o que a aplicação deve.

**A comparação é sensível a caixa, exatamente como o índice** — e isso foi decisão, não descuido.
"X-Tudo" e "x-tudo" são cadastráveis os dois hoje.

> A primeira versão deste handler usava `ILIKE` para ser insensível a caixa, com o índice
> continuando sensível. **Estava errado por dois motivos.** O primeiro é que insensibilidade não
> está em `RN-VEN-043` nem em `RN-VEN-044` — seria regra nascendo no código. O segundo é pior:
> deixar a aplicação mais restritiva que o banco põe **duas camadas discordando sobre a mesma
> regra**, que é exatamente o modo de falha que o teste de paridade do dia operacional existe para
> pegar. Divergência em silêncio é o defeito; a limitação declarada não é.
>
> Como efeito colateral, `EF.Functions.ILike` também obrigaria a camada de aplicação a referenciar
> o pacote do Npgsql, que ela recusa de propósito.

Fechar a insensibilidade de verdade exige índice sobre `lower(nome)` — migration — **e antes disso
uma decisão do cliente**, registrada como pergunta 21 da fila de validação. Até lá, aplicação e
banco dizem a mesma coisa.

### Ordem de exibição

`ordem` é `int` livre, e a listagem ordena por `ordem` e depois por `nome`. Não há renumeração
automática nem arrastar-e-soltar: duas categorias com `ordem = 1` empatam e o desempate é
alfabético — determinístico, e ninguém fica sem resposta.

### Exclusão não existe — `RN-VEN-045`

**Não há rota de `DELETE` para categoria nem para produto, e a ausência é a implementação da
regra.** Produto excluído levaria consigo o item de pedidos históricos (`RN-VEN-020` guarda o
produto vendido); categoria excluída deixaria produto órfão. A FK de `item_pedido` para `produto`
recusaria de todo jeito — mas com erro de banco, não com decisão de projeto.

## 5. Critérios de aceite

### CA-1 — Categoria é criada e aparece na gestão (`RN-VEN-043`)

> **Dado** um usuário com `cardapio.gerenciar`,
> **Quando** criar a categoria "Lanches" com ordem 1,
> **Então** a resposta é 200 com o `id` e a categoria aparece em `GET /cardapio` com `ativa: true`.

### CA-2 — Nome de categoria repetido na unidade é recusado (`RN-VEN-043`)

> **Dado** a categoria "Lanches" já cadastrada,
> **Quando** outra "Lanches" for criada na mesma unidade,
> **Então** a resposta é 422 com "Já existe uma categoria com este nome" — **e não 500 de índice**.

### CA-3 — O mesmo nome em outra unidade é aceito (`RN-GER-009`)

> **Dado** "Lanches" no restaurante A,
> **Quando** o restaurante B criar "Lanches",
> **Então** é aceito — a chave única começa por `unidade_id`.

### CA-4 — Categoria é renomeada e reordenada (`RN-VEN-043`)

> **Dado** a categoria "Lanche" com ordem 5,
> **Quando** for salva como "Lanches" com ordem 1,
> **Então** o `id` é o mesmo e a listagem reflete o nome e a nova ordem.

### CA-5 — Categoria inativa esconde os produtos do público sem inativá-los (`RN-VEN-043`) ⚠

> **Dado** a categoria "Sobremesas" com dois produtos ativos,
> **Quando** a categoria for inativada,
> **Então** o cardápio público não traz a categoria nem os produtos, **e os dois produtos
> continuam com `ativo: true`** na consulta da gestão.

### CA-6 — Reativar a categoria devolve os produtos (`RN-VEN-043`) ⚠

> **Dado** a categoria do CA-5 inativada,
> **Quando** for reativada,
> **Então** os dois produtos voltam ao cardápio público — sem que ninguém os reative um a um.

### CA-7 — Categoria inativa continua visível para a gestão (`RN-VEN-043`) ⚠

> **Dado** uma categoria inativada,
> **Quando** a gestão listar o cardápio,
> **Então** a categoria aparece com `ativa: false` — **é o que torna reativá-la possível**.

### CA-8 — Nome de produto repetido na unidade é recusado (`RN-VEN-044`)

> **Dado** o produto "X-Tudo",
> **Quando** outro "X-Tudo" for criado na mesma unidade,
> **Então** a resposta é 422 com mensagem legível.

### CA-9 — Preço zero é recusado (`RN-VEN-032`, `RN-VEN-044`)

> **Dado** um produto novo com preço 0,
> **Quando** for salvo,
> **Então** a resposta é 422 e nada é gravado.

### CA-10 — Produto inativo sai do público e fica na gestão (`RN-VEN-020`, `RN-VEN-044`)

> **Dado** um produto ativo e disponível,
> **Quando** for inativado por `PATCH .../situacao`,
> **Então** desaparece do cardápio público e aparece na gestão com `ativo: false`.

### CA-11 — Inativar não é o mesmo que indisponibilizar (`RN-VEN-044`) ⚠

> **Dado** um produto ativo e disponível,
> **Quando** for marcado como **indisponível**,
> **Então** `ativo` continua `true` e `disponivel` fica `false`;
> **E quando** for **inativado**, `disponivel` **não muda** — os dois estados são independentes.

### CA-12 — Produto inativo não entra em pedido novo (`RN-VEN-020`)

> **Dado** um produto inativado,
> **Quando** um cliente tentar pedi-lo pela rota pública,
> **Então** o pedido é recusado.

### CA-13 — Não existe exclusão (`RN-VEN-045`)

> **Dado** um produto com pedido histórico,
> **Quando** for inativado,
> **Então** o item do pedido antigo continua legível com nome e preço praticados.

### CA-14 — Produto de outra unidade não é alterado (`RN-GER-009`)

> **Dado** o produto do restaurante A,
> **Quando** o restaurante B tentar salvá-lo ou alternar a situação,
> **Então** a resposta é 404.

### CA-15 — Sem `cardapio.gerenciar` não escreve (`RN-GER-010`)

> **Dado** um usuário com perfil Cozinha,
> **Quando** tentar criar categoria ou produto,
> **Então** a resposta é 403.

### CA-16 — Nenhum campo de custo (`RN-GER-012`)

> **Quando** o cardápio da gestão for consultado,
> **Então** o corpo não contém `custo`, `cmv` nem `margem`.

### CA-17 — o teto padrão é 50 (`RN-VEN-046`)

> **Quando** o parâmetro `PAR-VEN-004` for consultado numa unidade nova,
> **Então** o valor é `50`.

### CA-18 — produto além do teto é recusado, e o teto é por categoria (`RN-VEN-046`)

> **Dado** o teto ajustado para 2 e uma categoria com 2 produtos ativos,
> **Quando** o terceiro for criado,
> **Então** a resposta é 422, a mensagem diz **qual** é o limite, e nada é gravado;
> **E** outra categoria continua aceitando produto — o teto é do grupo, não do restaurante.

### CA-19 — o teto conta só os ativos (`RN-VEN-046`, `RN-VEN-045`)

> **Dado** uma categoria no teto,
> **Quando** um produto for tirado do cardápio,
> **Então** abre vaga — porque contar o histórico entupiria a categoria para sempre.

### CA-20 — reativar não contorna o teto (`RN-VEN-046`) ⚠

> **Dado** uma categoria no teto e um produto inativo dela,
> **Quando** o produto for reativado,
> **Então** a resposta é 422 e o produto continua inativo.

### CA-21 — editar produto de categoria acima do teto continua permitido (`RN-VEN-046`)

> **Dado** uma categoria com 3 produtos e o teto **reduzido depois** para 1,
> **Quando** o preço de um deles for corrigido,
> **Então** a alteração é aceita — senão o teto travaria a correção do próprio dado.

## 6. Modelo de dados tocado

Uma migration, e **só de dado**: `20260818002151_TetoDeProdutosPorCategoria` insere a definição
de `PAR-VEN-004` em `parametro`. A migration `ParametrosComVigencia` **não** foi editada — ela já
rodou, e o comentário dela diz exatamente por que não se toca numa migration que é história.

O resto do que esta spec impõe já existia no banco:

```sql
-- migration Fundacao, 17/08/2026 — agora com regra que os enuncia
CREATE UNIQUE INDEX uq_categoria_nome ON categoria (unidade_id, nome);
CREATE UNIQUE INDEX uq_produto_nome   ON produto   (unidade_id, nome);
ALTER TABLE produto ADD CONSTRAINT ck_produto_preco CHECK (preco > 0);
-- categoria.ativa e produto.ativo/disponivel também já existiam
```

O único acréscimo é `Categoria.Reativar()` no domínio — método, não coluna. `Categoria.Inativar`
existia e **nunca havia sido chamado**; sem o par, inativar era um caminho de mão única.

## 7. Parâmetros consumidos

| Parâmetro | Papel | Padrão |
|-----------|-------|--------|
| `PAR-VEN-004` | Máximo de produtos **ativos** por categoria (`RN-VEN-046`) | **50** |

Nome, ordem e preço continuam sendo dado de cadastro, não política. O teto é o contrário: é decisão
comercial que muda por unidade, e por isso é parâmetro.

> **Pedido como "uma flag de 50 por categoria, ajustável via banco por enquanto". Virou
> parâmetro.** Coluna em `unidade` entregaria o número e nada mais; `parametro_valor` já entrega
> recorte por unidade, **vigência** e trilha de quem mudou (`SPEC-GER-003`, `SPEC-GER-005`) — e o
> "ajustável via banco" continua sendo literalmente um `INSERT`, documentado em
> [`docs/04-parametros-do-sistema.md`](../04-parametros-do-sistema.md).

**Não há tela para editar o teto**, e a ausência é declarada: a rota `PUT /v1/parametros/{codigo}`
da `SPEC-GER-003` atende. Tela de configuração de cardápio é outra spec.

## 8. Fora de escopo — a dívida declarada

- **Preço com vigência (`RN-VEN-010`).** Continua como na `SPEC-VEN-001`: o preço está no
  produto e alterá-lo não cria vigência. O histórico da venda está protegido pelo preço praticado
  copiado no item (`RN-VEN-008`), que é a metade que mais importa. A tela de preço com vigência é
  a próxima da onda 1
- **Tela para o teto de produtos.** `PAR-VEN-004` se ajusta pela rota de parâmetro
  (`SPEC-GER-003`), não por tela de cardápio
- **`cardapio.disponibilidade` como permissão própria.** Hoje "acabou hoje" exige
  `cardapio.gerenciar`, e quem sabe que acabou é o Atendente. Mudança em `SPEC-GER-004`
- **Unicidade insensível a caixa.** "X-Tudo" e "x-tudo" convivem: aplicação e índice são os dois
  sensíveis. **A paridade entre as camadas foi preferida à conveniência** (§4), e fechar exige
  índice sobre `lower(nome)` mais uma decisão do cliente — pergunta 21 da fila de validação
- **Ficha técnica e insumo.** `RN-PRO-004` é onda 2. Produto aqui é nome, descrição, preço e foto
- **Arrastar para reordenar.** `ordem` é campo numérico digitado
- **Categoria dentro de categoria.** O cardápio é de um nível
- **Produto em mais de uma categoria.** `RN-VEN-043` fala de grupo, e o produto tem um
- **Combo, adicional e opção.** Nada disso está no catálogo ainda

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Domínio | `src/GrelhaHub.Domain/Cardapio/Categoria.cs`, `Produto.cs` |
| Categoria | `src/GrelhaHub.Application/Features/Cardapio/SalvarCategoria/` |
| Situação da categoria | `.../Features/Cardapio/AlternarSituacaoDaCategoria/` |
| Situação do produto | `.../Features/Cardapio/AlternarSituacaoDoProduto/` |
| Produto | `.../Features/Cardapio/SalvarProduto/SalvarProduto.cs` |
| Consulta da gestão | `.../Features/Cardapio/ListarCardapioInterno/` |
| Rotas | `src/GrelhaHub.Api/Controllers/GestaoController.cs` — `CardapioController` |
| Teto por categoria | `src/GrelhaHub.Application/Features/Cardapio/TetoDeProdutos.cs` |
| Parâmetro | `src/GrelhaHub.Domain/Gerais/CatalogoDeParametros.cs` · migration `*_TetoDeProdutosPorCategoria` |
| Tela | `frontend/src/features/admin/Cardapio.tsx` |
| Máscara de moeda | `frontend/src/services/dinheiro.ts` |
| Testes | `tests/GrelhaHub.Integration.Tests/Cardapio/CadastroDoCardapioTests.cs` |

## 10. Definição de pronto

- [x] Regras `RN-VEN-043`, `044` e `045` no catálogo **antes** do código
- [x] As três restrições de banco sem regra agora têm regra que as enuncia
- [x] `ativo` e `disponível` com comandos separados
- [x] Categoria inativa escondendo produtos **sem** inativá-los
- [x] Categoria inativa **visível para a gestão** — o beco fechado
- [x] Nome repetido recusado com 422 e mensagem legível, não 500 de índice
- [x] Nenhuma rota de exclusão
- [x] Nenhum campo de custo em nenhum DTO
- [x] Tela de cadastro com criação de categoria e produto, edição e reativação
- [x] CA-1 a CA-16 como teste de integração citando o ID da regra no `DisplayName`
- [x] `dotnet build -c Release`, `dotnet test`, `eslint` e `tsc` verdes
- [x] `RN-VEN-046` imposta nos **dois** caminhos de entrada, num lugar só (`TetoDeProdutos`)
- [x] `PAR-VEN-004` nas três fontes que o teste de paridade compara: catálogo C#, documento e
      tabela `parametro`
- [x] Campo de preço com máscara de moeda — `RN-GER-003`
- [x] Verificação por mutação registrada em §12

## 11. O que saiu diferente da spec

### 11.1 A tela de cadastro do cardápio não existia — nem para produto

Ao começar, esperava encontrar a tela de produto pronta e faltando só a de categoria.
`SalvarProdutoCommand` existia desde a `SPEC-VEN-001`, com validator e permissão, **e nenhum
chamador no frontend**. A aba "Cardápio" do painel listava e alternava disponibilidade; não
criava nada.

**O comando pronto sem tela é o mesmo defeito da coluna morta da `SPEC-VEN-002` §11.1, uma camada
acima.** Parece entregue no `grep` e não é alcançável por ninguém.

### 11.2 `Categoria.Inativar()` existia e nunca havia sido chamado

O método estava no domínio desde a `SPEC-VEN-001`, com o comentário citando `RN-GER-002`. Sem
comando, sem rota, sem tela. E sem `Reativar()`: **se alguém o tivesse chamado, a categoria
ficaria inativa para sempre.** O par foi criado junto com o comando.

### 11.3 O filtro `.Where(c => c.Ativa)` na consulta da gestão

Descrito em §4. Não estava previsto nesta spec — apareceu ao percorrer o caminho de reativação
exigido pelo item novo da definição de pronto do guia de specs. **O item funcionou: o beco foi
encontrado por leitura, e não por alguém travado no painel.**

### 11.4 O comentário de `Categoria.Inativar` citava `RN-GER-002`, que é outra regra

`RN-GER-002` fala de **registro transacional**. Categoria é cadastro. A citação estava plausível
e errada — exatamente o modo de falha que o `CLAUDE.md` descreve. Corrigida para `RN-VEN-045`,
que é a regra que esta spec escreveu porque a lacuna apareceu aqui.

### 11.5 O campo de preço não tinha máscara

Descoberto por captura de tela do dono do produto: `333333333333` digitado inteiro num campo chamado
"Preço", com o formulário se oferecendo para salvar. O servidor recusaria — o validator tem teto de
100 000 —, mas **recusar depois da viagem não é o ponto**: o campo não parecia moeda em nenhum
momento da digitação.

Trocado por **máscara acumuladora de centavos**: quem digita `3390` vê `33,90`, e a vírgula anda em
vez de ser digitada. É o comportamento de terminal de cartão, e é o único que não exige que o
operador acerte onde fica o separador enquanto atende alguém.

Dois detalhes que valem registro:

- **`inputMode="numeric"`, não `type="number"`.** O `number` aceita `e`, `+`, `-` e notação
  científica, e no celular abre um teclado que não é o de moeda
- **O `R$` é moldura do campo, não conteúdo do valor.** Dentro do texto digitado, a máscara teria de
  preservá-lo a cada tecla, e o primeiro `backspace` no começo da linha o levaria embora

⚠ **A máscara de quantidade NÃO é esta.** `RN-GER-004` são quatro casas; reusar a de dinheiro
gravaria `0,1563` como `0,16` sem erro nenhum — o defeito que `SPEC-GER-001` §4 descreve.

### 11.6 As tabelas do cardápio estavam desalinhadas entre categorias

Também por captura de tela. Cada categoria é uma tabela própria — elas são seções independentes, com
cabeçalho e ações próprias —, e **tabela automática mede as colunas pelo conteúdo**: "Lanches", com
descrições longas, punha PREÇO num `x`; "Acompanhamentos", noutro. A página ficava desalinhada de
seção para seção.

Corrigido com `table-layout: fixed` e `<colgroup>` de larguras declaradas: as tabelas viram a mesma
grade. **Preço é justamente o dado que se confere descendo a coluna com o olho** — coluna que anda de
seção para seção anula o "dado em mono com `tabular-nums`" de `CHAPA-03`.

Na mesma passada, a célula da foto: `TROCAR` e `REMOVER` empilhados davam **130 px de altura em toda
linha**, para uma linha cujo conteúdo é um nome e um preço. Miniatura menor e botões compactos.
⚠ Ali o alvo de toque cai abaixo dos 44 px de `RNF-010`, e é decisão consciente registrada no CSS:
trocar foto é cadastro feito no computador, não no tablet no meio do serviço.

### 11.7 ⚠ `p.Id != produtoIgnorado` com um `Guid?` dentro da expressão

O defeito mais custoso desta spec, e o mais silencioso. A contagem do teto era:

```csharp
.CountAsync(p => p.CategoriaId == categoriaId && p.Ativo && p.Id != produtoIgnorado, ct)
```

`produtoIgnorado` é `Guid?`, e é **nulo na criação**. Em SQL, `p.id <> NULL` é `NULL`, o que filtra
todas as linhas e devolve **contagem zero** — teto nunca alcançado. O EF Core compensa gerando
`(p.id <> @p OR @p IS NULL)`, e é aí que fica interessante: **a consulta compilada é reaproveitada**,
e o resultado passou a depender de qual chamada compilou primeiro — criar (id nulo) ou reativar (id
preenchido).

O sintoma foi o pior que existe: **a classe de teste passava sozinha e falhava com a suíte inteira**,
em dois testes que nada tinham a ver com ordem de execução. A primeira hipótese foi o cache de
parâmetro, que é singleton — e estava inocente.

Corrigido com `Where` condicional, que não tem como ser ambíguo:

```csharp
if (produtoIgnorado is Guid ignorado) consulta = consulta.Where(p => p.Id != ignorado);
```

**A lição não é sobre este handler.** Comparar `Guid` com `Guid?` dentro de árvore de expressão é
armadilha em qualquer consulta do projeto, e o modo de falha — resultado que depende da ordem de
compilação — não aparece em teste isolado.

## 12. Verificação por mutação

| Mutação | Testes que caíram |
|---------|-------------------|
| Inativar a categoria propaga para os produtos (`p.Inativar()` em cascata) | **2** |
| `ListarCardapioInterno` volta a filtrar `c.Ativa` | **1** |
| `AlternarSituacaoDoProduto` mexe em `Disponivel` em vez de `Ativo` | **2** |
| Sem a conferência de nome único no handler de categoria | **1** |
| Sem a conferência de nome único no handler de produto | **1** |
| `ObterCardapioPublico` deixa de filtrar `c.Ativa` | **1** |

Nenhuma passou. A primeira e a terceira são as que importam: elas reproduzem a confusão que
`RN-VEN-043` e `RN-VEN-044` foram escritas para impedir — tratar cadastro e turno como o mesmo
estado. A terceira é o teste do CA-11 fazendo o trabalho dele: trocar um estado pelo outro deixa o
comportamento visível ao cliente **idêntico** — o produto não aparece nos dois casos —, e é só a
asserção sobre os dois campos que separa uma coisa da outra.

### `RN-VEN-046` — o teto

| Mutação | Testes que caíram |
|---------|-------------------|
| Conta **todos** os produtos, e não só os ativos | **2** |
| Reativar **não** confere o teto | **1** |
| Off-by-one: `>` em vez de `>=` | **2** |
| Confere o teto em **toda** edição, não só na entrada | **1** |
| Teto fixo em 50 no código, ignorando o parâmetro | **2** |

> ⚠ **A quarta mutação passou na primeira rodada, e o teste é que estava errado.** O CA-21 original
> editava um produto numa categoria *exatamente* no teto — e o guarda já ignora o próprio produto na
> contagem, então a mutação não mudava nada. O cenário que a regra realmente protege é a categoria
> **acima** do teto, que só existe quando o parâmetro é reduzido em cima do que já estava
> cadastrado. Reescrito assim, a mutação cai.
>
> Foi a mutação encontrando um **teste fraco**, e não um código errado — o segundo uso da técnica, e
> o menos comentado.

> **O filtro do CA-7 (mutação 2) derruba um teste só, e isso é correto.** A categoria escondida
> continua fora do cardápio público pelas outras vias, e o único dano da mutação é o beco: a
> gestão perde a categoria de vista. Um teste, exatamente onde a regra mora.
