# SPEC-VEN-001 — Cardápio público e pedido online

| Campo | Valor |
|-------|-------|
| ID | `SPEC-VEN-001-cardapio-publico-e-pedido` |
| Módulo | VEN |
| Situação | `ENTREGUE` — 17/08/2026 |
| Onda | **Fatia vertical antecipada** — ver §0 |
| Regras implementadas | `RN-VEN-036`, `RN-VEN-037`, `RN-VEN-038`, `RN-VEN-001`, `RN-VEN-002`, `RN-VEN-003`, `RN-VEN-008`, `RN-VEN-009`, `RN-VEN-011`, `RN-VEN-018` *(parcial)*, `RN-VEN-019`, `RN-VEN-020`, `RN-VEN-032`, `RN-VEN-039`, `RN-VEN-040`, `RN-GER-002` *(parcial)*, `RN-GER-003`, `RN-GER-005`, `RN-GER-006` *(parcial)*, `RN-GER-009` *(parcial)*, `RN-GER-010` *(parcial)*, `RN-GER-012`, `RN-GER-013`, `RN-CRM-001`, `RN-CRM-003`, `RN-RH-006` *(parcial)* |
| ADRs aplicáveis | [ADR-0002](../adr/0002-clean-architecture-com-cqrs.md), [ADR-0003](../adr/0003-frontend-e-backend-desacoplados.md), [ADR-0005](../adr/0005-multi-estabelecimento-por-coluna-discriminadora.md), [ADR-0011](../adr/0011-dia-operacional.md) |
| Data | 17/08/2026 |

---

## 0. Por que esta spec vem fora da ordem

O [guia de specs](../06-guia-de-specs.md#5-ordem-de-escrita-das-specs) coloca venda na onda 4.
Esta spec é uma **fatia vertical antecipada**, pedida pelo time para ver o sistema
funcionando ponta a ponta e iniciar o repositório.

**Isso é uma decisão consciente com dívida declarada.** A fatia atravessa camadas que ainda
não têm spec própria, e o faz de forma **simplificada**. O que foi simplificado está na §8 —
cada item é dívida a pagar quando a onda correspondente chegar, **não** comportamento
definitivo.

O critério que a fatia respeita: **nada aqui contradiz uma regra do catálogo.** Ela
implementa menos do que o catálogo exige, nunca diferente.

## 1. Objetivo

Permitir que um restaurante se cadastre, publique seu cardápio em um endereço público e
receba pedidos por ele — e que a equipe do restaurante veja esses pedidos chegando.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-VEN-036` | Unidade tem `slug` público único e imutável | Modelo | `UNIQUE` + `CHECK` de formato |
| `RN-VEN-037` | Cardápio público sem autenticação, só produtos ativos da unidade do slug | Aplicação | Rota anônima + projeção filtrada |
| `RN-VEN-038` | Pedido público entra em `delivery próprio`, estado `Aberto`, com nome e telefone | Aplicação + Modelo | `ENUM` + validator |
| `RN-VEN-001` | Todo pedido pertence a um canal de venda | Modelo | `ENUM` `NOT NULL` |
| `RN-VEN-002` | Pedido tem ao menos um item | Aplicação | Validator |
| `RN-VEN-003` | Item refere-se a exatamente um produto | Modelo | FK `NOT NULL` |
| `RN-VEN-008` ⚠ | Preço praticado é copiado no item e não muda depois | Modelo | Coluna própria em `item_pedido` |
| `RN-VEN-009` | Total do item é quantidade × preço praticado | Domínio | `ItemPedido.Total` |
| `RN-VEN-011` | Quantidade sempre maior que zero | Modelo | `CHECK` |
| `RN-VEN-018` ⚠ | Ciclo `Aberto → Confirmado → …` | Modelo + Domínio | `ENUM` + método de transição |
| `RN-VEN-019` | Finalizado ou Cancelado não retorna a estado anterior | Domínio | Guarda em `Pedido.Avancar` e `Cancelar` |
| `RN-VEN-020` | Produto inativo não entra em pedido novo | Aplicação | Filtro na consulta e no lançamento |
| `RN-VEN-032` | Preço de tabela sempre maior que zero | Modelo | `CHECK (preco > 0)` + validator |
| `RN-VEN-039` `IMP` | Total do pedido é a soma dos itens | Domínio | `Pedido.Total` |
| `RN-VEN-040` `IMP` | Composição congelada a partir de Em produção | Domínio | `Pedido.AceitaAlteracaoDeItens` |
| `RN-GER-002` | Não se exclui; cancela-se com motivo | Domínio + Modelo | `Cancelar(motivo)`; `ON DELETE RESTRICT` |
| `RN-GER-003` | Moeda com duas casas decimais | Modelo | `numeric(12,2)` por convenção do `DbContext` |
| `RN-GER-005` | Data e hora com fuso; operação em `America/Sao_Paulo` | Modelo | `timestamptz`; `unidade.fuso_horario` |
| `RN-GER-006` ⚠⚠ | Dia operacional 04h–03h59 | Modelo | Coluna gerada + índice |
| `RN-GER-009` ⚠ | `unidade_id` em toda tabela transacional | Modelo + Aplicação | FK + filtro global |
| `RN-GER-010` | Perfil determina operações permitidas | Aplicação | Política de autorização |
| `RN-GER-012` | Custo e margem não são retornados pela API | Aplicação | Ausência do campo na projeção pública |
| `RN-GER-013` `IMP` | Documento e telefone sem máscara | Modelo + Domínio | `CHECK (cnpj ~ '^[0-9]{14}$')` + normalização |
| `RN-CRM-001` | Cliente identificado pelo telefone | Modelo | `UNIQUE (unidade_id, telefone)` |
| `RN-CRM-003` | Delivery exige cliente identificado | Aplicação | Validator |
| `RN-RH-006` | Usuário inativo não registra operação | Aplicação | Login recusa `ativo = false` |

> **Três destas regras nasceram desta implementação** — `RN-VEN-039`, `RN-VEN-040` e
> `RN-GER-013`, origem `IMP` no catálogo v1.3. O comportamento já estava no código, citando
> por aproximação o ID de outra regra; a auditoria de rastreabilidade o corrigiu. É o
> procedimento da [§7 do guia de specs](../06-guia-de-specs.md#7-quando-a-implementação-descobre-uma-regra)
> aplicado **depois** do fato — e o custo de tê-lo aplicado tarde foi justamente esta auditoria.

## 3. Contrato

### Área autenticada — `/api/v1`

| Verbo | Rota | Descrição | Perfil |
|-------|------|-----------|--------|
| `POST` | `/autenticacao/login` | Autentica e devolve JWT | anônimo |
| `POST` | `/autenticacao/cadastrar-restaurante` | Cadastra restaurante + usuário administrador | anônimo |
| `GET` | `/restaurante/meu` | Dados do restaurante e link do cardápio | autenticado |
| `GET` | `/cardapio` | Categorias com seus produtos, incluindo os inativos | autenticado |
| `POST` | `/cardapio/produtos` | Cria **ou atualiza** produto — `id` nulo cria | Gerência |
| `PATCH` | `/cardapio/produtos/{id}/disponibilidade` | Liga e desliga o "acabou hoje" | Gerência |
| `GET` | `/pedidos` | Pedidos da unidade, filtro por status e dia operacional | autenticado |
| `POST` | `/pedidos/{id}/confirmar` | `Aberto → Confirmado` | autenticado |
| `POST` | `/pedidos/{id}/avancar` | Avança um estado do ciclo | autenticado |
| `POST` | `/pedidos/{id}/cancelar` | Cancela com motivo | Gerência |

> **Divergências desta seção em relação à versão anterior da spec** — corrigidas em
> 17/08/2026 pela regra 4 do processo, que manda ajustar a spec depois da implementação:
>
> - `POST /restaurantes` virou `POST /autenticacao/cadastrar-restaurante`, e
>   `/restaurantes/meu` virou `/restaurante/meu` — a unidade é singular no MVP (premissa P4)
> - `/categorias` e `/produtos` viraram `/cardapio` e `/cardapio/produtos`: a gestão do
>   cardápio é uma tela só, e uma rota por agregado evitava duas chamadas para montá-la
> - `PUT /produtos/{id}` não existe. `POST /cardapio/produtos` cria e atualiza — o
>   `SalvarProdutoCommand` carrega `id` opcional
> - **Não há rota de escrita de categoria.** As categorias vêm do cardápio de exemplo
>   semeado no cadastro; criar categoria entra na onda 1, com `SPEC-VEN-002`

### Área pública — `/api/publico` *(sem autenticação)*

| Verbo | Rota | Descrição |
|-------|------|-----------|
| `GET` | `/{slug}/cardapio` | Restaurante + categorias + produtos ativos |
| `POST` | `/{slug}/pedidos` | Cria pedido; devolve número de acompanhamento |
| `GET` | `/{slug}/pedidos/{numero}?telefone=` | Status do pedido, para o cliente acompanhar |

O `telefone` é obrigatório no acompanhamento: o número do pedido é sequencial por dia
operacional e, sozinho, seria adivinhável — qualquer pessoa leria o pedido alheio. O par
número + telefone é o que autoriza a leitura sem exigir login (`RN-CRM-001`).

### Erros

| Situação | Código | Mensagem | Regra |
|----------|--------|----------|-------|
| Slug inexistente ou unidade inativa | 404 | "Restaurante não encontrado." | `RN-VEN-036` |
| Pedido sem itens | 422 | "O pedido precisa ter ao menos um item." | `RN-VEN-002` |
| Produto de outra unidade ou inativo | 422 | "Produto indisponível." | `RN-VEN-020`, `RN-GER-009` |
| Quantidade menor ou igual a zero | 422 | "A quantidade deve ser maior que zero." | `RN-VEN-011` |
| Transição de estado inválida | 422 | "Não é possível ir de {atual} para {destino}." | `RN-VEN-018` |
| Credencial inválida | 401 | "E-mail ou senha inválidos." | |

## 4. Comportamento

### Cadastro do restaurante

Uma operação, uma transação: cria a `unidade` (com `slug`), o `usuario` administrador com
senha hasheada, e um cardápio de exemplo — categorias e produtos de hamburgueria — para que
o link público já mostre alguma coisa.

O `slug` é derivado do nome fantasia (sem acento, minúsculo, hifenizado) e sufixado se já
existir. É **imutável**: o link circula em rede social e impressos, e mudá-lo quebraria o que
já foi distribuído.

### Cardápio público

1. Resolve a unidade pelo `slug` — `unidade` não é filtrada por `unidade_id` (ela **é** a unidade)
2. Fixa o escopo de unidade no contexto de execução
3. A partir daí, o filtro global do EF Core garante que nada de outra unidade apareça

**Nunca projeta custo, CMV ou margem** (`RN-GER-012`) — o cardápio público é o lugar onde
vazar custo seria mais grave, porque qualquer pessoa o acessa.

### Pedido público

- Canal `DeliveryProprio`, estado inicial **`Aberto`** (`RN-VEN-038`)
- Cliente identificado por telefone (`RN-CRM-001`): se já existir na unidade, é reaproveitado
- `preco_praticado` é **copiado** de cada produto no momento do pedido (`RN-VEN-008`)
- `data_operacional` é gerada pelo banco (`RN-GER-006`)
- Devolve um `numero` sequencial por dia operacional, para o cliente acompanhar

> **O pedido público não é confirmado automaticamente.** A confirmação é ato do restaurante,
> e é ela que dispara baixa de estoque e ordem de produção (`RN-VEN-018`). Confirmar sozinho
> comprometeria insumo por um pedido que o restaurante ainda não viu.

## 5. Critérios de aceite

### CA-1 — Cardápio público não vaza custo (`RN-GER-012`, `RN-VEN-037`)

> **Dado** um restaurante com produtos cadastrados,
> **Quando** o cardápio público for consultado **sem token**,
> **Então** a resposta traz nome, descrição e preço,
> **e** o corpo bruto **não contém** `custo`, `cmv` nem `margem`.

### CA-2 — Isolamento entre restaurantes (`RN-GER-009`, `RN-VEN-037`)

> **Dado** dois restaurantes com produtos,
> **Quando** o cardápio do slug A for consultado,
> **Então** nenhum produto do restaurante B aparece.

### CA-3 — Preço é congelado no pedido (`RN-VEN-008`)

> **Dado** um pedido criado com um produto a R$ 32,00,
> **Quando** o preço do produto for alterado para R$ 38,00,
> **Então** o item do pedido continua a R$ 32,00,
> **e** o total do pedido não muda.

### CA-4 — Pedido público entra Aberto (`RN-VEN-038`, `RN-VEN-018`)

> **Dado** um pedido enviado pelo cardápio público,
> **Quando** for criado,
> **Então** o estado é `Aberto` e o canal é `DeliveryProprio`,
> **e** nenhum movimento de estoque nem ordem de produção é gerado.

### CA-5 — Transição inválida é recusada (`RN-VEN-018`)

> **Dado** um pedido `Aberto`,
> **Quando** houver tentativa de levá-lo direto a `Entregue`,
> **Então** a operação é recusada,
> **e** um pedido `Cancelado` não volta a nenhum estado anterior.

### CA-6 — Dia operacional no pedido (`RN-GER-006`)

> **Dado** um pedido criado à 01h30 de domingo,
> **Quando** `data_operacional` for lida,
> **Então** o valor é o sábado anterior.

### CA-7 — Produto inativo não entra em pedido (`RN-VEN-020`)

> **Dado** um produto inativado,
> **Quando** o cardápio for consultado, ele não aparece,
> **e** se seu id for enviado num pedido, a operação é recusada com 422.

### CA-8 — Total do pedido é a soma dos itens (`RN-VEN-039`, `RN-VEN-009`)

> **Dado** um pedido com dois itens — 2 × R$ 28,00 e 1 × R$ 18,00,
> **Quando** o total for lido,
> **Então** o valor é R$ 74,00,
> **e** o total de cada item é quantidade × preço praticado, sem desconto, taxa de serviço
> nem taxa de entrega — nenhuma delas incide nesta fatia.

### CA-9 — Composição congelada a partir da produção (`RN-VEN-040`)

> **Dado** um pedido em `Em produção`,
> **Quando** houver tentativa de acrescentar um item,
> **Então** a operação é recusada,
> **e** o mesmo pedido em `Aberto` ou `Confirmado` aceita o item.
>
> A fronteira é a produção, não a confirmação: entre confirmar e produzir ainda há fila, e
> nesse intervalo o restaurante consegue acomodar um "esqueci a batata".

### CA-10 — Documento e telefone sem máscara (`RN-GER-013`, `RN-CRM-001`)

> **Dado** um cadastro com CNPJ `12.345.678/0001-99` e telefone `(11) 98765-4321`,
> **Quando** os valores forem persistidos,
> **Então** o banco guarda `12345678000199` e `11987654321`,
> **e** um segundo pedido com o telefone digitado como `11 98765 4321` resolve para o
> **mesmo** cliente, em vez de criar um duplicado.

## 6. Modelo de dados tocado

| Tabela | Constraints principais |
|--------|------------------------|
| `unidade` | `UNIQUE (slug)`; `CHECK (slug ~ '^[a-z0-9-]{3,60}$')`; `UNIQUE (cnpj)` |
| `usuario` | `UNIQUE (unidade_id, email)`; `senha_hash NOT NULL` |
| `categoria` | `UNIQUE (unidade_id, nome)` |
| `produto` | `UNIQUE (unidade_id, nome)`; `CHECK (preco > 0)` |
| `cliente` | `UNIQUE (unidade_id, telefone)` — `RN-CRM-001` |
| `pedido` | `UNIQUE (unidade_id, data_operacional, numero)`; `data_operacional` gerada; canal e status como `ENUM` |
| `item_pedido` | `CHECK (quantidade > 0)`; `CHECK (preco_praticado >= 0)`; `preco_praticado NOT NULL` |

**Chave primária declarada `ValueGeneratedNever`** para toda entidade, no `DbContext`: o `Id`
é gerado pela aplicação (`Guid.CreateVersion7()`), e dizer isso ao EF Core é o que faz um
filho novo em agregado já rastreado sair como `INSERT`. Ver a §10 — foi o que o CA-9 revelou.

**Migration:** `Fundacao`.

## 7. Parâmetros consumidos

Nenhum. O cardápio público não aplica taxa de serviço (`RN-VEN-014` não incide em delivery)
nem desconto.

## 7-A. Acompanhamento ao vivo — acrescentado em 17/08/2026

A tela de acompanhamento do cliente **se atualiza sozinha**: enquanto o pedido pode mudar de
estado, ela consulta `GET /{slug}/pedidos/{numero}` e a régua avança sem ninguém apertar nada.

### Por que polling, e não SSE nem WebSocket

ADR-0006 — **uma VPS, Nginx na frente.** Conexão persistente exigiria configuração de proxy, teto
de conexões simultâneas e um caminho próprio de reconexão, para entregar **segundos** de diferença
em um fluxo que leva **minutos**. Polling é um `GET` na rota que a tela já usava, que já é pública
e anônima (`RN-VEN-037`) e já exige o telefone junto do número.

> Quando houver mais de uma máquina ou fila de pedidos grande o suficiente para o intervalo
> incomodar, a troca é justificada. Hoje seria complexidade sem cliente pedindo.

### As quatro coisas que fazem isto ser educado

| Cuidado | Por quê |
|---------|---------|
| **Para em estado terminal** | `faseDe(status)` devolve `fim` (Entregue, Finalizado) ou `morto` (Cancelado). A aba esquecida aberta pararia de bater no servidor para sempre — e reusar `faseDe` em vez de listar os estados é o que impede esta tela de divergir de `RN-VEN-018` quando o ciclo mudar |
| **Pausa com a aba escondida** | Celular no bolso não precisa perguntar nada. Ao voltar, consulta **na hora**: quem volta para a aba quer ver agora, não em 10 s |
| **Nunca sobrepõe requisição** | Rede lenta empilhando pedidos transforma um polling manso em enxurrada, justamente no cliente com sinal ruim |
| **Recua quando falha** | Erro consecutivo dobra o intervalo até 1 min. Se a API cair, cada cliente esperando **deixa de somar carga** exatamente quando ela é o problema |

**Intervalo: 10 s.** O ciclo leva minutos, então 1 s não dá nada ao cliente e custa caro ao
servidor — numa noite de 30 pedidos simultâneos seriam 30 requisições por segundo de gente só
esperando. **Não é `PAR-*`:** parâmetro existe para o que o restaurante configura, e cadência de
polling é restrição técnica igual em toda unidade.

### Falha de rede não apaga a tela

O pedido exibido continua sendo **o último bom**, com um aviso discreto e um botão "tentar agora".
Trocar a comanda por uma mensagem de erro tiraria do cliente a informação que ele **já tinha**.

### O que o design system permite aqui

`CHAPA-06` — o sinal de "vivo" é a **listra âmbar**, não bolinha pulsando nem emoji. `CHAPA-08` —
nada pisca: um indicador que pulsa a cada segundo é ruído numa tela que a pessoa fica olhando.
`CHAPA-07` — o pedido ficar **Pronto** não ganha comemoração; confete é do fechamento de caixa,
uma vez por dia.

### O que o teste garante

O polling vale exatamente o que a rota entregar. `AcompanhamentoPublicoTests` percorre **o ciclo
inteiro** conferindo que cada avanço aparece na consulta do cliente — sem isso, a tela se
atualizaria para mostrar sempre a mesma coisa, e o defeito pareceria ser do frontend.

## 8. Fora de escopo — a dívida declarada

Cada item abaixo é **simplificação consciente**, com a spec que a resolve:

| Simplificação nesta fatia | Regra que fica devendo | Resolve em |
|---------------------------|------------------------|-----------|
| Preço direto em `produto`, sem vigência | `RN-VEN-010` | Onda 1 |
| **Total do item não é coluna gerada** — `RN-VEN-009` está previsto no catálogo como `GEN` e é calculado no domínio (`ItemPedido.Total`, com `Ignore` no mapeamento). Funciona e está testado, mas a soma vive na aplicação, não no modelo — contra a ordem de preferência do ADR-0009 | `RN-VEN-009` mecanismo `GEN` | Onda 4 — junto com ajustes e desconto, que mudam a fórmula (`RN-VEN-028`) |
| **Quantidade de insumo em 4 casas decimais não existe** — a convenção do `DbContext` aplica `numeric(12,2)` a *todo* `decimal`, e o comentário que menciona `numeric(14,4)` descreve a intenção, não o código. Enquanto não houver value object `Quantidade`, o primeiro item de ficha técnica gravaria `0,1563` como `0,16` **sem erro** | `RN-GER-004` | `SPEC-GER-001` — **antes** da onda 2 |
| `unidade_id` atribuído a mão no handler, sem interceptor | `RN-GER-009` completo | `SPEC-GER-001` |
| Perfil como `enum`, sem tabela de permissões granulares | `RN-GER-010` completo | `SPEC-GER-004` |
| Parâmetros sem tabela nem vigência | 9 regras `PAR` | `SPEC-GER-003` |
| Sem trilha de auditoria | `RN-GER-001`, `RN-GER-011` | `SPEC-GER-005` |
| Sem ficha técnica, estoque, lote ou FEFO | Módulos PRO e EST | Ondas 2 e 3 |
| **Sem alerta de falta de insumo** | `RN-VEN-035` | Onda 4 — depende de estoque |
| Sem modificadores | `RN-VEN-015`, `RN-VEN-016`, `RN-VEN-027` | Onda 1 |
| Sem taxa de entrega nem zona | `RN-LOG-003`, `RN-LOG-009` | Onda 5 |
| Sem pagamento nem caixa | Módulo FIN | Onda 6 |
| Sem comanda e mesa | `RN-VEN-004` a `RN-VEN-007` | Onda 5 |
| Cancelamento não gera perda | `RN-EST-018` | Onda 4 — depende de estoque |

> **Nenhum desses itens é comportamento definitivo.** Um pedido que não baixa estoque hoje
> não significa que a regra mudou — significa que o estoque ainda não existe. Quem retomar
> este código precisa saber a diferença, e é para isso que esta tabela existe.

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Domínio | `src/GrelhaHub.Domain/{Gerais,Cardapio,Vendas}/` |
| Features | `src/GrelhaHub.Application/Features/{Gerais,Cardapio,Vendas}/` |
| Persistência | `src/GrelhaHub.Infrastructure/Persistence/` |
| Controllers | `src/GrelhaHub.Api/Controllers/` |
| Front do restaurante | `frontend/src/features/admin/` |
| Front público | `frontend/src/features/publico/` |
| Testes de domínio | `tests/GrelhaHub.Domain.Tests/{Gerais,Vendas}/` |
| Testes de integração | `tests/GrelhaHub.Integration.Tests/Vendas/CardapioPublicoTests.cs`, `PedidoPublicoTests.cs` |
| Harness de integração | `tests/GrelhaHub.Integration.Tests/Infraestrutura/` |

## 10. Definição de pronto

- [x] Regras novas catalogadas **antes** do código (`RN-VEN-036` a `038`)
- [x] Fluxo ponta a ponta: cadastro → cardápio público → pedido → acompanhamento
- [x] Acompanhamento **ao vivo** (§7-A): polling com parada em estado terminal, pausa com aba
      escondida, sem sobreposição e com recuo no erro — 5 testes em `AcompanhamentoPublicoTests`
- [x] Testes com o ID da regra no `DisplayName`
- [x] `dotnet build -c Release` e `dotnet test` verdes — **44 testes**: 26 de domínio e 18 de
      integração
- [x] Dívida declarada na §8
- [x] Rastreabilidade do [catálogo §16](../03-catalogo-de-regras-de-negocio.md#16-rastreabilidade-regra--spec) preenchida
- [x] **`RN-VEN-039`, `RN-VEN-040` e `RN-GER-013` catalogadas *depois* do código** — corrigido
      no catálogo v1.3, e os IDs renomeados no código e nos testes. `grep` por cada um
      encontra a implementação e o teste
- [x] **CA-1 a CA-8 e CA-10 como teste de integração contra PostgreSQL real** — 18 testes em
      `GrelhaHub.Integration.Tests`, com contêiner Testcontainers, API real e nenhum dublê
- [x] **CA-9 fechado por reidratação, não por rota.** Nenhuma rota acrescenta item a pedido
      existente — mas o que havia de específico de integração era outro: provar que o agregado
      **relido do PostgreSQL** ainda impõe a invariante. O EF materializa `Pedido` por
      construtor privado e *backing field* (`_itens`, `PropertyAccessMode.Field`); um
      mapeamento errado ali daria um agregado que não valida, e o teste de domínio — que
      sempre constrói em memória — nunca veria
- [x] `npm run lint && npm run build` verdes — verificado em 17/08/2026 após `npm ci`
      (204 pacotes, 0 vulnerabilidades); `tsc -b` sem erro e bundle de 302 kB / 98,8 kB gzip

> ### O que o CA-9 encontrou — e por que valeu insistir nele
>
> Ao persistir o item acrescentado a um pedido **lido do banco**, o EF Core emitiu
> `UPDATE item_pedido ... WHERE id = @id` em uma linha que nunca existiu, e falhou com
> `DbUpdateConcurrencyException` — "esperava afetar 1 linha, afetou 0", que **parece conflito
> de concorrência e não é**.
>
> Causa: `Entidade.Id` já vem preenchido por `Guid.CreateVersion7()`, mas o modelo não dizia
> isso ao EF. A convenção para chave `Guid` é `ValueGeneratedOnAdd`, e ao encontrar um filho
> novo em agregado **já rastreado** com chave não-vazia, o EF conclui "isto já existe" e marca
> `Modified`.
>
> **O defeito era latente:** o único caminho que chama `AdicionarItem` hoje cria o pedido
> junto, então o grafo inteiro entra como `Added` e os `INSERT` saem certos. O lançamento de
> item da onda 4 bateria nele de frente — com uma exceção que aponta para o lugar errado.
>
> Corrigido em `GrelhaHubDbContext.AplicarChaveGeradaPelaAplicacao`, que declara
> `ValueGeneratedNever` para toda entidade. **Não exige migration:** `dotnet ef migrations
> has-pending-model-changes` reporta nenhuma mudança de modelo — o ajuste é de rastreamento em
> memória, não de schema, e o banco de produção não é tocado.
>
> Este é o argumento de banca para o ADR-0009 em uma frase: o teste de domínio passava, o
> código parecia certo, e só o PostgreSQL real mostrou que a gravação estava errada.
