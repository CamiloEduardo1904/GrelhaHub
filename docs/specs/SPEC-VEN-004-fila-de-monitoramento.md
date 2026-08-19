# SPEC-VEN-004 — Fila de monitoramento

| Campo | Valor |
|-------|-------|
| ID | `SPEC-VEN-004-fila-de-monitoramento` |
| Módulo | VEN |
| Situação | `ENTREGUE` — 19/08/2026 |
| Onda | Fora da ordem — operação da fatia vertical |
| Regras implementadas | `RN-VEN-054`, `RN-VEN-055`, `RN-VEN-018`, `RN-GER-006`, `RN-GER-009`, `RN-GER-010`, `RN-GER-016`, `RN-VEN-050` (apresentação) |
| ADRs aplicáveis | [ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md), [ADR-0012](../adr/0012-direcao-visual-chapa.md), [ADR-0019](../adr/0019-cqrs-com-modelo-de-leitura-proprio.md) |
| Depende de | `SPEC-VEN-001`, `SPEC-GER-004`, `SPEC-GER-009` |
| Autor / data | 19/08/2026 |

---

## 1. Objetivo

Dar à casa a tela que responde **o que ainda tenho para entregar** — a fila do que está vivo,
atualizada sozinha, dividida entre o que falta preparar e o que espera para sair.

> **As outras duas telas respondem outras perguntas.** O painel de operação (`RN-GER-038`,
> `RN-VEN-049` a `051`) responde **quanto** a casa vendeu; ele é histórico e comparativo, e por
> isso abre em sete dias. A lista de pedidos responde **o que entrou no dia** — e responde bem,
> mas por construção ela **cresce a noite inteira**: às onze da noite, os três pedidos pendentes
> estão espalhados entre quarenta comandas já finalizadas, e a única forma de achá-los é ler o
> selo de cada uma.
>
> Nenhuma das duas responde a pergunta que se faz **de pé, no meio do serviço**. É essa a lacuna.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-VEN-054` | A fila lista só `Aberto`, `Confirmado`, `Em produção` e `Pronto`, em dois blocos; sem telefone e sem observação | Aplicação (leitura) | Filtro de status em `FilaDeMonitoramentoHandler` sobre `vw_pedido_resumo` + **DTO sem os campos** |
| `RN-VEN-055` | Cadência escalonada pelo movimento: 15 s com fila, 60 s com a fila vazia por 4 ciclos | Aplicação (cliente) | `useFilaAoVivo.ts` — e a suspensão da corrente do painel em `Painel.tsx` |
| `RN-VEN-018` | O ciclo de vida é a fonte do que está vivo e do que saiu | Domínio | `StatusPedido`, já existente |
| `RN-GER-006` ⚠⚠ | O pedido carrega o seu `data_operacional`; a fila o **exibe** quando não é o dia corrente | Banco | Coluna gerada, já existente — a fila só lê |
| `RN-GER-009` | Recorte por unidade | Aplicação | `UnidadeDaConsulta.Recorte` + filtro global |
| `RN-GER-010` | Ver a fila é `pedido.visualizar` | Aplicação | `[ExigePermissao]` no request |
| `RN-GER-016` | Restaurante nunca alcança unidade alheia | Aplicação | `UnidadeDaConsulta.Recorte` ignora o parâmetro |
| `RN-VEN-050` | A série do painel não perde dia — **e o de 30 dias tem de caber na tela** | Aplicação (cliente) | Rótulo ralo + tabela dobrável, **sem descartar linha** |

> **Por que a fila não tem permissão própria, ao contrário do painel de operação.** `RN-GER-038`
> separou `operacao.visualizar` porque agregado de venda é o passo anterior a margem
> (`RN-GER-012`). A fila **não agrega nada**: ela é um recorte da mesma lista que
> `pedido.visualizar` já entrega, com **menos** campos. Uma permissão nova daria a impressão de
> proteger algo que a permissão antiga já mostra por inteiro — e permissão que não protege nada é
> pior que permissão nenhuma, porque alguém confia nela.

## 3. Contrato

### Entrada

```
Query: FilaDeMonitoramentoQuery
```

| Campo | Tipo | Obrigatório | Validação | Regra |
|-------|------|:-----------:|-----------|-------|
| `UnidadeId` | `Guid?` | não | **Ignorado** para o restaurante; só a plataforma recorta com ele | `RN-GER-016` |

> ⚠ **Não há `de` nem `ate`, e a ausência é a regra.** `RN-VEN-054` — o que está vivo está na
> fila. Um pedido aberto às 23h50 que atravessou a virada (`RN-GER-006`) **continua para ser
> entregue**, e some da fila se o recorte for o dia corrente.

### Saída

`GET /api/v1/pedidos/fila` → `IReadOnlyList<PedidoNaFilaDto>`

| Campo | Tipo | Observação |
|-------|------|------------|
| `Id` | `Guid` | Identidade. `Numero` é humano e **repete** entre dias e unidades |
| `UnidadeId` | `Guid` | `RN-GER-009` — a plataforma vê várias casas na mesma resposta |
| `Numero` | `int` | Reinicia a cada dia operacional |
| `Canal` | `string` | `RN-VEN-001` |
| `Status` | `string` | Um dos quatro vivos |
| `Cliente` | `string?` | **Fica.** É como se chama quem vem buscar |
| `Total` | `decimal` | `RN-VEN-039`, somado pela visão |
| `QuantidadeDeItens` | `int` | |
| `CriadoEm` | `DateTimeOffset` | Alimenta o tempo de espera na comanda |
| `DataOperacional` | `DateOnly` | A tela marca o que não é do dia corrente |
| `Itens` | `[]` | `Produto`, `Quantidade`, `PrecoPraticado` |

> ⚠⚠ **`Telefone` e `Observacao` não existem neste DTO** — não são `null`, não são mascarados,
> **não estão lá**. É `CHAPA-05` aplicada ao contrato: não há cadeado porque não há campo. A
> observação por item também fica fora, pela mesma razão.

### Erros

| Situação | Código | Mensagem ao usuário | Regra violada |
|----------|--------|---------------------|---------------|
| Sem `pedido.visualizar` | `403` | (do pipeline de autorização) | `RN-GER-010` |
| Módulo VEN desabilitado | `403` | (do pipeline de módulo) | `RN-GER-019` |
| Sessão expirada | `401` | Derruba para `/entrar` | — |

> ⚠ **Falha de rede não é erro de tela nesta rota.** A fila exibida continua sendo a última boa,
> com um aviso discreto — `RN-VEN-055` e a mesma doutrina de `RN-VEN-047`. Trocar a fila por uma
> mensagem de erro tiraria da cozinha a informação que ela já tinha.

### Autorização

| Perfil | Permitido |
|--------|:---------:|
| Administrador | ✔ |
| Gerente | ✔ |
| Atendente | ✔ |
| Cozinha | ✔ |
| Operador | ✔ |
| Entregador | conforme `pedido.visualizar` do perfil |
| Plataforma | ✔ (bypass de `RN-GER-014`) |

## 4. Comportamento

### Fluxo principal

1. A aba **Monitor** monta e consulta `GET /v1/pedidos/fila` imediatamente.
2. O handler recorta a unidade (`UnidadeDaConsulta.Recorte`), filtra os quatro status vivos sobre
   `vw_pedido_resumo` e ordena por `CriadoEm` **crescente** — quem espera há mais tempo primeiro.
3. Os itens vêm em **uma** consulta para todos os pedidos, e não uma por pedido.
4. A tela separa em dois blocos: `Pronto` vai para **pronto**; os outros três, para **em aberto**.
5. Cada comanda traz o botão da próxima etapa (`RN-VEN-018`). Ao avançar, a tela consulta de novo
   na hora, sem esperar o intervalo.
6. Quando o pedido chega a `Entregue`, ele **some da fila na consulta seguinte** — não há
   animação de saída nem estado intermediário.

### Fluxos alternativos e de exceção

| # | Condição | Comportamento |
|---|----------|---------------|
| A1 | Fila vazia | Estado vazio com a `Grelha` apagada e a frase que diz **por que** está vazio. A cadência afrouxa para `PAR-VEN-008` depois de `PAR-VEN-009` ciclos |
| A2 | Aba escondida ou outra aba do painel à frente | **Suspende e reagenda** (`RN-VEN-055`). Ao voltar, consulta na hora |
| A3 | Falha de rede | Recuo exponencial até 60 s; a fila exibida permanece, com aviso |
| A4 | Consulta anterior ainda em voo | Não dispara outra. Quem está em voo reagenda no fim |
| A5 | Pedido de dia operacional anterior | Entra na fila **marcado com a data**, porque `numero` reinicia a cada virada |
| A6 | Usuário sem `pedido.visualizar` | A aba **não aparece**; a rota responde 403 de todo jeito |

### Efeitos colaterais

**Nenhum.** É consulta — `ILeituraDb`, sem `SaveChanges` (ADR-0019). O avanço de status disparado
pela tela é o comando que já existia (`AlterarStatusPedidoCommand`), com os efeitos dele.

## 5. Critérios de aceite

### CA-1 — a fila traz os quatro estados vivos (`RN-VEN-054`)

> **Dado** pedidos em `Aberto`, `Confirmado`, `Em produção` e `Pronto`
> **Quando** a fila é consultada
> **Então** os quatro aparecem

### CA-2 — entregue, finalizado e cancelado saem (`RN-VEN-054`)

> **Dado** um pedido que chegou a `Entregue`, outro `Finalizado` e outro `Cancelado`
> **Quando** a fila é consultada
> **Então** nenhum dos três aparece
> **E** o cancelado não aparece nem no bloco de aberto nem no de pronto

### CA-3 — a fila não devolve telefone nem observação (`RN-VEN-054`)

> **Dado** um pedido criado com telefone e com observação do cliente
> **Quando** a fila é consultada
> **Então** o corpo da resposta **não contém** o telefone nem o texto da observação
> **E** o nome do cliente **está** lá

### CA-4 — o pedido que atravessou a virada continua na fila (`RN-VEN-054`, `RN-GER-006`)

> **Dado** um pedido vivo de dia operacional anterior
> **Quando** a fila é consultada, sem nenhum filtro de período
> **Então** ele aparece
> **E** vem com o `dataOperacional` dele, para a tela poder marcá-lo

### CA-5 — a fila é da unidade, e só dela (`RN-GER-009`, `RN-GER-016`)

> **Dado** dois restaurantes com pedidos vivos
> **Quando** cada um consulta a fila com o seu token
> **Então** cada um vê apenas os próprios
> **E** informar a unidade do vizinho no parâmetro **não** muda a resposta

### CA-6 — a cadência acompanha o movimento (`RN-VEN-055`)

> **Dado** a tela do monitor aberta
> **Quando** há pedido na fila
> **Então** a consulta ocorre a cada `PAR-VEN-005` segundos
> **E quando** a fila fica vazia por `PAR-VEN-009` ciclos, o intervalo passa a `PAR-VEN-008`
> **E** volta ao ritmo curto no primeiro pedido que aparecer
> **E** com a janela escondida a corrente **suspende e reagenda**, nunca cessa

> ⚠ **CA-6 não tem teste automatizado**, e a dívida é a mesma de `RN-VEN-047`: o frontend não tem
> runner. O que dá para verificar no harness — quais pedidos entram na fila — está em CA-1 a CA-5.

### ⚠⚠ O que a validação em navegador encontrou — e o que ela custou provar

A tela foi conferida com a pilha real (PostgreSQL, API e Vite) e um Chrome dirigido, com seis pedidos
vivos em dois dias operacionais. **A fila subiu VAZIA na tela e cheia na API.**

A causa não era da fila: era da trava de *uma requisição por vez*, que estava num `useRef`. **`ref`
sobrevive à remontagem do efeito; o efeito, não.** Toda vez que o efeito refazia — troca de
dependência, `atualizarAgora`, ou o duplo `mount` que o `StrictMode` faz em desenvolvimento —, a
corrente nova encontrava a trava da corrente VELHA ainda ligada, saía pelo `return` seco e **não
agendava nada**; a velha, ao terminar, via o seu `vivo` já `false` e também não reagendava.

| | |
|---|---|
| **O sintoma** | Tela no ar, sem erro nenhum, e nada mais perguntando |
| **Onde estava** | `useFilaAoVivo.ts` **e** `usePedidoAoVivo.ts` — as duas correntes do produto |
| **Desde quando** | `usePedidoAoVivo` carregava o defeito desde 18/08/2026, alcançável por "Tentar agora" com uma requisição em voo |
| **A correção** | `let emVoo` dentro do efeito: a trava vale para **esta** corrente e nenhuma outra |
| **Por que nenhum teste pegou** | É comportamento de cliente, e o frontend não tem runner. Nem `tsc`, nem `eslint`, nem os 356 testes de backend podiam vê-lo |

⚠ **O mais grave é o que isto diz sobre a linha de `RN-VEN-047` no catálogo.** Ela afirmava —
desde 18/08 — que *"suspender nunca solta a corrente"*. A afirmação era **falsa**, e ficou registrada
como entregue por um dia inteiro. Regra sem teste é regra sem evidência, mesmo quando o comentário
no código descreve com precisão o defeito que o código tem.

### O que a validação confirmou, medido

| Verificação | Resultado |
|-------------|-----------|
| CA-1 / CA-2 na tela | 4 comandas em **em aberto**, 2 em **pronto**; entregue, finalizado e cancelado ausentes |
| CA-4 na tela | O pedido de `2026-08-18` aparece na fila, com a data na comanda |
| Cadência com movimento | 2 consultas em 40 s — intervalo de **15 s** |
| Cadência com fila vazia | consultas aos 15 s, 30 s, 45 s e **105 s** — o quarto ciclo vazio afrouxa para 60 s, e a tela diz *"Fila vazia · conferindo a cada minuto"* |
| Suspensão por aba | 0 consultas à fila em 35 s com a aba Cardápio à frente |
| A corrente da lista | **0 consultas** a `/v1/pedidos` durante os 150 s em que o monitor esteve à frente |
| Avançar pela fila | 1 consulta imediata; o pedido entregue **saiu** da fila (6 → 5 comandas) |
| `CHAPA-02` | Uma única barra âmbar entre os dois divisores — a de **pronto** |
| `CHAPA-04` | Botão de avanço com **48 px** de altura no celular |
| `RN-VEN-054` no fio | Nem telefone nem observação no corpo da resposta ou na tela |
| Celular (390 px) | Sem rolagem horizontal |
| Console e rede | Nenhum erro |

## 6. Modelo de dados tocado

| Tabela | Operação | Constraints envolvidas |
|--------|:--------:|------------------------|
| `vw_pedido_resumo` | leitura | — (visão; já filtra `dh_exclusao IS NULL`, `RN-VEN-041`) |
| `vw_pedido_item` | leitura | — |

**Migration necessária:** **não.** As duas visões existem desde a `VisoesDeLeitura` (ADR-0019), e
a fila é um recorte delas. Uma visão nova só se justificaria se houvesse agregação — e não há.

## 7. Parâmetros consumidos

| Parâmetro | Uso |
|-----------|-----|
| `PAR-VEN-005` | Intervalo com a fila em movimento — 15 s |
| `PAR-VEN-008` | Intervalo com a fila ociosa — 60 s |
| `PAR-VEN-009` | Ciclos vazios antes de afrouxar — 4 |

> Os três são **constantes no cliente**, nomeando o código que copiam — §1.1 de
> [`04-parametros`](../04-parametros-do-sistema.md) diz o que falta para promovê-los.

## 8. Fora de escopo — a dívida declarada

| O que não entra | Por quê | Onde resolve |
|-----------------|---------|--------------|
| **Cancelar pela fila** | Cancelamento exige motivo (`RN-GER-002`), e um diálogo de texto no meio de uma tela de parede é a operação errada no lugar errado | Segue na aba Pedidos |
| **A observação do cliente na fila** | ⚠⚠ **É o ponto que a próxima entrevista precisa resolver.** `RN-VEN-052` diz que a observação é instrução de preparo e vai **inteira** à comanda. A fila a omite por ser tela de ambiente compartilhado — e essa decisão só se sustenta enquanto a fila **não for** a tela de quem monta o sanduíche. Se a cozinha adotar a fila como comanda de preparo, a observação volta, e a regra muda com ela | Fila de validação com cliente |
| **Som ou alerta de pedido novo** | Não há regra que o descreva, e alerta sonoro sem regra vira ruído desligado no primeiro dia | Regra nova, se a entrevista pedir |
| **Teto de pedidos na fila** | A fila não trunca. Pedido esquecido em `Aberto` acumula, e a saída correta é a exclusão lógica de `RN-VEN-041` — não um `Take` que esconde | `RN-VEN-041`, já existente |
| **Contrato do painel de operação** | `RN-GER-038` e `RN-VEN-049` a `051` foram implementados em 18/08/2026 **sem spec**. Esta spec assume só a **apresentação** deles (§2) | Dívida de rastreabilidade, declarada no catálogo §16 |

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Query + Handler | `backend/src/GrelhaHub.Application/Features/Vendas/FilaDeMonitoramento/FilaDeMonitoramento.cs` |
| Rota | `backend/src/GrelhaHub.Api/Controllers/GestaoController.cs` — `PedidosController.Fila` |
| Validator | — (a consulta não tem entrada validável: o único campo é ignorado por `RN-GER-016`) |
| Migration | — (nenhuma) |
| Teste de integração | `backend/tests/GrelhaHub.Integration.Tests/Vendas/FilaDeMonitoramentoTests.cs` |
| Tela | `frontend/src/features/admin/Monitor.tsx` |
| Cadência | `frontend/src/features/admin/useFilaAoVivo.ts` |
| Bloco da fila | `frontend/src/services/ciclo.ts` — `blocoDaFila` |
| Design system | [`08-design-system.md`](../08-design-system.md) §3 — `<CapaDaCasa />` e o divisor de bloco |

## 10. Definição de pronto

- [x] Todas as regras do cabeçalho implementadas na camada declarada
- [x] Um teste por regra servida, com o ID no `DisplayName`
- [x] Critérios de aceite CA-1 a CA-5 passando como teste de integração
- [x] CA-6 declarado **sem** teste, com o motivo — não escondido
- [x] Migration: nenhuma necessária, e a §6 diz por quê
- [x] Nenhum literal de negócio no código — os três intervalos citam o código do parâmetro
- [x] Rastreabilidade do [catálogo §16](../03-catalogo-de-regras-de-negocio.md#16-rastreabilidade-regra--spec) atualizada
- [x] Regra nova descoberta foi para o catálogo antes do código (`RN-VEN-054`, `RN-VEN-055`)
- [x] **Se esta regra recusar a operação, para onde o usuário vai?** — a aba não aparece sem
      `pedido.visualizar`, e a fila vazia diz por que está vazia em vez de mostrar nada
- [x] `dotnet build -c Release` e `dotnet test -c Release` verdes
