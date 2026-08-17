# SPEC-VEN-001 — Cardápio público e pedido online

| Campo | Valor |
|-------|-------|
| ID | `SPEC-VEN-001-cardapio-publico-e-pedido` |
| Módulo | VEN |
| Situação | `EM IMPLEMENTAÇÃO` |
| Onda | **Fatia vertical antecipada** — ver §0 |
| Regras implementadas | `RN-VEN-036`, `RN-VEN-037`, `RN-VEN-038`, `RN-VEN-001`, `RN-VEN-002`, `RN-VEN-003`, `RN-VEN-008`, `RN-VEN-011`, `RN-VEN-018` *(parcial)*, `RN-VEN-020`, `RN-GER-006`, `RN-GER-009`, `RN-GER-010`, `RN-GER-012`, `RN-CRM-001`, `RN-CRM-003` |
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
| `RN-VEN-011` | Quantidade sempre maior que zero | Modelo | `CHECK` |
| `RN-VEN-018` ⚠ | Ciclo `Aberto → Confirmado → …` | Modelo + Domínio | `ENUM` + método de transição |
| `RN-VEN-020` | Produto inativo não entra em pedido novo | Aplicação | Filtro na consulta e no lançamento |
| `RN-GER-006` ⚠⚠ | Dia operacional 04h–03h59 | Modelo | Coluna gerada + índice |
| `RN-GER-009` ⚠ | `unidade_id` em toda tabela transacional | Modelo + Aplicação | FK + filtro global |
| `RN-GER-010` | Perfil determina operações permitidas | Aplicação | Política de autorização |
| `RN-GER-012` | Custo e margem não são retornados pela API | Aplicação | Ausência do campo na projeção pública |
| `RN-CRM-001` | Cliente identificado pelo telefone | Modelo | `UNIQUE (unidade_id, telefone)` |
| `RN-CRM-003` | Delivery exige cliente identificado | Aplicação | Validator |

## 3. Contrato

### Área autenticada — `/api/v1`

| Verbo | Rota | Descrição | Perfil |
|-------|------|-----------|--------|
| `POST` | `/autenticacao/login` | Autentica e devolve JWT | anônimo |
| `POST` | `/restaurantes` | Cadastra restaurante + usuário administrador | anônimo |
| `GET` | `/restaurantes/meu` | Dados do restaurante e link do cardápio | autenticado |
| `GET` | `/categorias` · `POST` `/categorias` | Categorias | Gerência para escrita |
| `GET` | `/produtos` · `POST` `/produtos` · `PUT` `/produtos/{id}` | Produtos | Gerência para escrita |
| `GET` | `/pedidos` | Pedidos da unidade, com filtro por status | autenticado |
| `POST` | `/pedidos/{id}/confirmar` | `Aberto → Confirmado` | autenticado |
| `POST` | `/pedidos/{id}/avancar` | Avança um estado do ciclo | autenticado |
| `POST` | `/pedidos/{id}/cancelar` | Cancela com motivo | Gerência |

### Área pública — `/api/publico` *(sem autenticação)*

| Verbo | Rota | Descrição |
|-------|------|-----------|
| `GET` | `/{slug}/cardapio` | Restaurante + categorias + produtos ativos |
| `POST` | `/{slug}/pedidos` | Cria pedido; devolve número de acompanhamento |
| `GET` | `/{slug}/pedidos/{numero}` | Status do pedido, para o cliente acompanhar |

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

## 6. Modelo de dados tocado

| Tabela | Constraints principais |
|--------|------------------------|
| `unidade` | `UNIQUE (slug)`; `CHECK (slug ~ '^[a-z0-9-]{3,60}$')`; `UNIQUE (cnpj)` |
| `usuario` | `UNIQUE (unidade_id, email)`; `senha_hash NOT NULL` |
| `categoria` | `UNIQUE (unidade_id, nome)` |
| `produto` | `UNIQUE (unidade_id, nome)`; `CHECK (preco > 0)` |
| `cliente` | `UNIQUE (unidade_id, telefone)` — `RN-CRM-001` |
| `pedido` | `UNIQUE (unidade_id, data_operacional, numero)`; `data_operacional` gerada; canal e status como `ENUM` |
| `item_pedido` | `CHECK (quantidade > 0)`; `preco_praticado NOT NULL`; total como coluna gerada |

**Migration:** `Fundacao`.

## 7. Parâmetros consumidos

Nenhum. O cardápio público não aplica taxa de serviço (`RN-VEN-014` não incide em delivery)
nem desconto.

## 8. Fora de escopo — a dívida declarada

Cada item abaixo é **simplificação consciente**, com a spec que a resolve:

| Simplificação nesta fatia | Regra que fica devendo | Resolve em |
|---------------------------|------------------------|-----------|
| Preço direto em `produto`, sem vigência | `RN-VEN-010` | Onda 1 |
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
| Testes | `tests/GrelhaHub.Domain.Tests/Vendas/` |

## 10. Definição de pronto

- [x] Regras novas catalogadas **antes** do código (`RN-VEN-036` a `038`)
- [x] Fluxo ponta a ponta: cadastro → cardápio público → pedido → acompanhamento
- [x] Testes com o ID da regra no `DisplayName`
- [x] `dotnet build -c Release` e `dotnet test` verdes
- [ ] CA-1 a CA-7 como teste de integração contra PostgreSQL real — **pendente**: os testes
      atuais são de domínio; a integração entra com `SPEC-GER-001`
- [x] Dívida declarada na §8
