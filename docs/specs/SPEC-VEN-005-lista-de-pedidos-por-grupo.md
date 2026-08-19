# SPEC-VEN-005 — Lista de pedidos por grupo

| Campo | Valor |
|-------|-------|
| ID | `SPEC-VEN-005-lista-de-pedidos-por-grupo` |
| Módulo | VEN |
| Situação | `ENTREGUE` — 19/08/2026 |
| Onda | Fora da ordem — operação da fatia vertical |
| Regras implementadas | `RN-VEN-057`, `RN-VEN-058` ⚠⚠, `RN-GER-002`, `RN-GER-006` ⚠⚠, `RN-VEN-018`, `RN-VEN-052`, `RN-GER-039` |
| ADRs aplicáveis | [ADR-0012](../adr/0012-direcao-visual-chapa.md) |
| Depende de | `SPEC-VEN-001`, `SPEC-VEN-004` |
| Autor / data | 19/08/2026 |

---

## 1. Objetivo

Fazer a lista de pedidos parar de tratar **o que falta preparar** e **o que já saiu** como a mesma
coisa, no mesmo espaço.

> **A queixa, do dono do produto:** *"pedidos novos e finalizados/cancelados do dia atual dividem o
> mesmo espaço. A exibição está enchendo a tela de informação."*
>
> Medido: com 62 pedidos de uma noite — 23 na casa, 33 concluídos, 6 cancelados —, a lista ocupava
> **8,4 telas** de rolagem. O que ainda falta preparar estava espalhado entre 39 comandas inteiras
> que ninguém vai reler.

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-VEN-057` | Três grupos por dia operacional, filtro em tela, e o concluído chega colapsado | Aplicação (cliente) | `grupoDaLista` + `chegaColapsado` em `ciclo.ts`; `Pedidos` em `Painel.tsx` |
| `RN-GER-006` ⚠⚠ | O divisor de **dia** continua sendo o de fora | Banco → tela | `data_operacional` (coluna gerada) + `agruparPorDiaOperacional` |
| `RN-VEN-018` | O ciclo é o que define em qual grupo o pedido cai | Domínio | `StatusPedido`, já existente |
| `RN-VEN-052` | A observação volta **inteira** ao abrir a comanda | Aplicação | Nada foi truncado — a comanda aberta é a mesma de sempre |
| `RN-GER-039` | Fechada, a comanda não mostra nome nem telefone | Aplicação (cliente) | O clique abre; dado pessoal não fica à vista sem necessidade |
| `RN-VEN-058` ⚠⚠ | O cancelamento exibe **motivo, instante e responsável** | Modelo + banco + aplicação | `dh_cancelamento` e `cancelado_por_usuario_id` no pedido; o nome resolvido na visão |
| `RN-GER-002` | O desfazimento é registrado com motivo e responsável | Domínio | `Pedido.Cancelar(usuarioId, motivo)` — o `usuarioId` **passou a ser exigido** |

> **Nenhuma mudança no servidor, e a ausência é decisão.** `ListarPedidosQuery` já aceita um
> `status`, e usá-lo faria o filtro ir ao banco. Não foi usado: a lista do período **já está na
> tela**, o filtro é sobre ela, e as contagens dos três chips precisam ser do período inteiro para
> não mentirem. Filtrar no servidor traria um número por vez e obrigaria três requisições para
> desenhar três contadores.

## 3. Contrato

**Nenhum.** É reorganização da tela sobre `GET /v1/pedidos`, que não mudou.

## 4. Comportamento

### Os três grupos — `grupoDaLista`

| Grupo | Estados | Chega |
|-------|---------|-------|
| **Na casa** | `Aberto`, `Confirmado`, `Em produção`, `Pronto` | **aberto** |
| **Concluídos** | `Entregue`, `Finalizado` | **fechado** |
| **Cancelados** | `Cancelado` | **fechado** |

> ⚠ **Não é `faseDe` com outro nome.** `faseDe` tem quatro valores e serve à `<Grelha>` e ao
> `<Selo>`, onde `Pronto` é `andamento` e `Entregue` é `fim`. Aqui `Pronto` é **na casa** — o pedido
> está na mão de alguém —, e é essa a distinção que o agrupamento existe para fazer. Duas perguntas
> parecidas, respostas diferentes; por isso são duas funções, como `pedidoEncerrado` e `faseDe` já
> eram.
>
> ⚠ **Cancelado tem grupo próprio, e não é preciosismo:** ele **não é conclusão** — não entra no
> faturamento (`RN-VEN-049`) e é o que alguém procura ao conferir a taxa de cancelamento do dia.

### Hierarquia da tela

```
filtro (Todos · Na casa · Concluídos · Cancelados)     ← contagens do PERÍODO
└── dia operacional          RN-GER-006 — `numero` reinicia a cada virada
    └── grupo                RN-VEN-057 — só quando o filtro é "todos"
        └── comandas
```

### O colapso

- Estado inicial pelo **estado do pedido**, não pelo grupo selecionado
- Um conjunto `alternados` guarda os ids **invertidos** em relação ao padrão — um, e não dois:
  guardar "os que o usuário abriu" exigiria um segundo conjunto para "os que fechou", e o segundo é
  o que alguém esquece
- ⚠ **Efeito colateral desejado:** o pedido que **anda** enquanto a tela está aberta reassume o
  padrão do estado novo. O que acabou de ser entregue fecha sozinho na consulta seguinte — a menos
  que quem estava olhando o tenha aberto de propósito

### Fluxos alternativos

| # | Condição | Comportamento |
|---|----------|---------------|
| A1 | Filtro sem resultado | Vazio que **nomeia o filtro ativo** e oferece "Ver todos os N". ⚠ Um "nenhum pedido" seco contradiz o chip que acabou de anunciar o total, e é assim que alguém conclui que o sistema perdeu pedidos |
| A2 | Grupo vazio no dia | O subdivisor não aparece. Moldura sem conteúdo é ruído |
| A3 | Um grupo em foco | O subdivisor some — o chip já disse o que está na tela |
| A4 | Comanda fechada, teclado | `<button>` com `aria-expanded`. `div` com `onClick` deixaria o pedido inalcançável por `Tab` |

## 5. Critérios de aceite

### CA-1 — os três grupos, separados (`RN-VEN-057`)

> **Dado** um dia com pedidos nos três grupos
> **Quando** o filtro está em *todos*
> **Então** os três aparecem, cada um com divisor e contagem próprios, dentro do bloco do dia

### CA-2 — o que já saiu chega fechado (`RN-VEN-057`)

> **Dado** pedidos concluídos e cancelados
> **Quando** a lista carrega
> **Então** todos eles estão colapsados
> **E** nenhum pedido do grupo *na casa* está

### CA-3 — colapsar não é esconder (`RN-VEN-057`, `RN-VEN-052`)

> **Dado** uma comanda fechada
> **Quando** ela é clicada
> **Então** volta a comanda inteira, com itens, total e a **observação do cliente não truncada**
> **E** um novo clique a fecha de volta

### CA-4 — as contagens não mentem (`RN-VEN-057`)

> **Dado** um filtro ativo
> **Então** os chips continuam mostrando os totais **do período**
> **E** cada bloco de dia conta o que está **exibido**

### CA-5 — a tela encolhe, medido (`RN-VEN-057`)

> **Dado** 62 pedidos de uma noite real
> **Então** a lista ocupa **4,3 telas** como chega, contra **8,4** com tudo aberto

### CA-6 — o cancelamento diz por quê, quando e por quem (`RN-VEN-058`)

> **Dado** um pedido cancelado com motivo
> **Quando** a comanda é aberta
> **Então** aparece o motivo, o instante **com hora** e o nome **com sobrenome** de quem cancelou
> **E** o rótulo vai em `--muted` e o motivo na cor do texto (`CHAPA-10`)
> **E** pedido cancelado **antes de 19/08/2026** exibe motivo e instante, e **omite** o responsável

> ⚠⚠ **Duas dívidas antigas morreram aqui, e nenhuma tinha quem a denunciasse.** `Pedido.Cancelar`
> gravava o instante em `dh_finalizacao` — deixando cancelado e finalizado **indistinguíveis por
> data** — e o resumo XML do método afirmava *"com motivo e responsável"* quando o responsável não
> era gravado em lugar nenhum. É a mesma família do que a validação em navegador achou em
> `RN-VEN-047`: documentação afirmando o que o código não faz. `CancelamentoRegistradoTests` são os
> quatro casos que passam a medir.
>
> ⚠ **E as três colunas de responsável do pedido viraram REFERÊNCIA** — `criado_por`,
> `cancelado_por_usuario_id` e `excluido_por_usuario_id` eram `uuid` soltos, sem chave: nada impedia
> gravar um id inexistente, e o modelo não sabia que aquelas três colunas falavam de gente. O banco
> é a **primeira** camada da ordem de ADR-0009, e integridade referencial é o que ela existe para
> dar. `Restrict` nos três: apagar usuário jamais pode levar pedido junto.

> ⚠ **CA-1 a CA-5 não têm teste automatizado, e é a mesma dívida de sempre:** o frontend não tem
> runner. Os cinco foram conferidos em Chrome real pela skill `subir-e-validar`
> (`driver/exemplos/lista.mjs`), que **falha com código 1** quando qualquer um deles quebra — é o
> mais perto de um teste que este projeto tem no cliente hoje.

## 6. Modelo de dados tocado

**Nenhum.** Nem leitura nova, nem migration.

## 7. Parâmetros consumidos

Nenhum. ⚠ Os limiares de colapso são **de apresentação**, não de política: nada aqui vira `PAR-*`.

## 8. Fora de escopo — a dívida declarada

| O que não entra | Por quê |
|-----------------|---------|
| **Filtro no servidor** | §3. A lista do período já está na tela, e as contagens precisam ser do período |
| **Lembrar o filtro entre visitas** | Filtro que persiste é filtro que alguém esquece ligado — e a tela passa a esconder pedido sem que ninguém tenha pedido. Se a entrevista pedir, vira regra antes de virar código |
| **"Abrir todos" / "fechar todos"** | Não foi pedido, e um botão a mais na barra que já tem quatro chips custa mais do que entrega |
| **Ordenar dentro do grupo** | Segue por `numero` decrescente, como já era |

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Agrupamento | `frontend/src/services/ciclo.ts` — `grupoDaLista`, `chegaColapsado`, `GRUPOS_DA_LISTA` |
| Tela | `frontend/src/features/admin/Painel.tsx` — `Pedidos` |
| Comanda fechada | `frontend/src/components/Comanda.tsx` — `colapsada`, `aoAlternar`, `canalCurto` |
| Estilo | `frontend/src/app/estilos-globais.css` — "GRUPOS DA LISTA DE PEDIDOS" |
| Validação em tela | `.claude/skills/subir-e-validar/driver/exemplos/lista.mjs` e `cancelamento.mjs` |
| Migration | `20260819194338_CancelamentoComResponsavel` — colunas, as três FKs, o backfill e a visão |
| Teste de integração | `backend/tests/GrelhaHub.Integration.Tests/Vendas/CancelamentoRegistradoTests.cs` |
| Design system | [`08-design-system.md`](../08-design-system.md) §3 — `<Comanda />` fechada |

## 10. Definição de pronto

- [x] Regra no catálogo **antes** do código (`RN-VEN-057`)
- [x] Rastreabilidade do [catálogo §16](../03-catalogo-de-regras-de-negocio.md#16-rastreabilidade-regra--spec) atualizada
- [x] Nenhum literal de negócio no código
- [x] CA-1 a CA-5 conferidos em navegador real, com o script versionado
- [x] Dívida do runner de frontend declarada, e não escondida
- [x] **Se esta regra recusar a operação, para onde o usuário vai?** — o filtro sem resultado nomeia
      o filtro ativo e oferece o caminho de volta
- [x] `dotnet build -c Release` e `dotnet test -c Release` verdes (nada no servidor mudou)
- [x] `tsc`, `eslint` e `vite build` limpos
