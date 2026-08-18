# SPEC-GER-009 — Horário de funcionamento, endereço e o dia operacional por unidade

| Campo | Valor |
|-------|-------|
| ID | `SPEC-GER-009-horario-endereco-e-dia-operacional` |
| Módulo | GER |
| Situação | `ENTREGUE` — 18/08/2026 |
| Onda | 0 — **revisa uma regra estrutural** |
| Regras implementadas | `RN-GER-006` ⚠⚠ *(revisada)*, `RN-GER-036`, `RN-GER-037`, `RN-VEN-048` |
| ADRs aplicáveis | [ADR-0011](../adr/0011-dia-operacional.md) *(revisado)*, [ADR-0009](../adr/0009-onde-as-regras-de-negocio-vivem.md) |
| Data | 18/08/2026 |

---

## 1. Objetivo

Tirar o dia operacional do código. Até aqui a virada era **04h00 fixos** dentro da expressão da
coluna gerada, e a decisão do dono do produto foi clara: isso é cadastro da loja, não constante do
sistema.

Junto vêm o **endereço** — que a vitrine precisa exibir — e a consequência mais visível do horário:
**fora dele, a casa não vende** (`RN-VEN-048`).

## 2. Regras implementadas

| Regra | Enunciado resumido | Imposta em | Mecanismo |
|-------|--------------------|------------|-----------|
| `RN-GER-006` ⚠⚠ | O dia operacional começa na virada **da unidade** | Banco + Domínio | Coluna gerada sobre colunas da própria linha |
| `RN-GER-036` ⚠⚠ | Horário por dia da semana; a virada é derivada dele | Modelo + Domínio | `unidade_horario` + `ViradaOperacional.De` |
| `RN-GER-037` | Endereço em campos, opcional | Modelo | Colunas em `unidade` |
| `RN-VEN-048` | Fora do horário, a vitrine informa e recusa pedido | Aplicação | `Unidade.AbertaEm` no handler público |

## 3. Contrato

| Verbo | Rota | Descrição |
|-------|------|-----------|
| `GET` | `/api/v1/restaurante/operacao?unidadeId=` | Endereço, horário e a virada resultante |
| `PUT` | `/api/v1/restaurante/operacao` | Grava a **semana inteira** e rederiva a virada |

`GET /api/publico/{slug}/cardapio` passou a devolver `endereco`, `aberta` e `horarios`.

⚠ **A semana vai inteira, sempre.** A virada é derivada do conjunto: aceitar um dia isolado deixaria
a régua dependente da ordem das chamadas, e no meio do caminho a casa apuraria por um número que
ninguém escolheu.

⚠ **As duas rotas são privativas da plataforma, por ora.** O caminho natural é o restaurante editar
o próprio horário, e falta pouco: uma permissão `unidade.gerenciar` no `CatalogoDeAcesso`. Mas
permissão nova só chega aos perfis das unidades **já criadas** por migration de dados — o catálogo é
semeado no ato da criação (`CadastrarRestaurante`) —, e entregar sem essa migration daria ao
administrador um botão que responde 403. Está na §8.

## 4. Comportamento

### Como a virada é derivada — `RN-GER-036`

| Cadastro | Virada | Por quê |
|----------|--------|---------|
| Nenhum horário | **04h00** (`PAR-GER-001`) | Quem já operava continua apurando igual. Ausência não muda número de ninguém |
| Ninguém atravessa a meia-noite (fecha 23h30) | **00h00** | Não há madrugada a reclassificar. Forçar 04h00 jogaria a venda das 23h50 para o dia seguinte — o erro de `RN-GER-006` ao contrário |
| Alguém atravessa (sexta fecha 04h00, quinta 02h00) | **04h00** | O mais tarde entre os que atravessam. Uma virada menor cortaria a noite de sexta no meio |

### ⚠⚠ O passado não se reclassifica

Cada pedido guarda `hora_virada_operacional` e `fuso_operacional`, **copiados da unidade quando
nasceu** — o mesmo movimento de `RN-VEN-008` com o preço praticado. A coluna gerada calcula a partir
dessas colunas:

```sql
(((dh_criacao AT TIME ZONE fuso_operacional) - hora_virada_operacional)::date)
```

> **O argumento da versão anterior continua válido, e é ele que exige a cópia.** O comentário que
> justificava a constante dizia: "alterar a expressão reclassificaria vendas passadas". Verdade — e
> por isso a virada não é *lida da unidade*, é *copiada para a linha*. Editar o horário muda os
> pedidos **seguintes**; o relatório de um sábado fechado continua com o mesmo número. É o que o
> CA-2 prova.

⚠ **Conferido em PostgreSQL 16 antes de escrever:** coluna gerada aceita referência a outra coluna
da mesma linha, inclusive dentro de `AT TIME ZONE`. O que ela recusa é referência a **outra tabela**
— exatamente o desenho que a cópia evita.

### ⚠ Os índices que caem com a coluna

Trocar a expressão de uma coluna gerada é `DROP` + `ADD`, e `DROP COLUMN` leva embora **todo índice
que a inclua**, sem erro e sem aviso. O EF Core não recria o que não sabe ter perdido: a migration
derruba e recria `uq_pedido_numero` e `ix_pedido_dia_status` à mão. Sem isso, a unicidade do número
do pedido por dia operacional desapareceria em produção, calada, até dois pedidos nascerem com o
mesmo número na mesma noite.

### A trilha de auditoria ficou de fora — divergência declarada

`log_auditoria.data_operacional` continua com **04h00 fixos**, e isso é decisão, não esquecimento: a
trilha registra também o que a **plataforma** faz, e a plataforma não tem unidade (`RN-GER-014`),
logo não tem virada. Uma trilha com o dia de cada casa deixaria as linhas de plataforma sem
resposta. O custo: "o que mudou no dia em que o caixa não fechou" responde pela régua das 04h00, que
pode não ser a da casa. Quando a pergunta aparecer, o caminho é o do pedido — copiar para a linha.

### Fora do horário — `RN-VEN-048`

A vitrine mostra **"Fechado agora"** com o próximo horário de abertura, e o `POST` de pedido é
recusado com **422**. A recusa é do servidor: esconder o botão não impede um `POST`, e pedido aceito
com a cozinha apagada é comida que ninguém faz.

⚠ **Casa sem horário cadastrado é considerada aberta.** A regra nova não pode fechar loja de ninguém
no dia em que entrou.

O cálculo confere **o dia anterior** quando ele atravessa a meia-noite: à 01h00 de domingo quem
manda é o horário de sábado, e olhar só o dia corrente responderia "fechada" com a casa cheia. A
conversão para o fuso da casa acontece **dentro** de `Unidade.AbertaEm` — o contêiner roda em UTC, e
um chamador que esquecesse de converter fecharia a loja três horas antes.

### O endereço — `RN-GER-037`

Campos separados, todos opcionais. A tela consulta o **ViaCEP** ao sair do campo de CEP e preenche o
que vier; se a consulta falhar, avisa e **os campos continuam editáveis**. Formulário que depende de
serviço de terceiro para gravar para de funcionar sem avisar no dia em que o terceiro cai — e quem
está cadastrando não tem como saber que o problema não é ele.

## 5. Critérios de aceite

### CA-1 — A virada sai do horário (`RN-GER-036`)

> **Dado** uma casa que fecha às 03h00,
> **Quando** o horário for gravado,
> **Então** `unidade.hora_virada_operacional` é 03h00 e o endereço fica com o CEP só em dígitos.

### CA-2 — O passado não se reclassifica (`RN-GER-006` ⚠⚠)

> **Dado** um pedido criado com a virada de 03h00,
> **Quando** o horário da casa mudar para fechar às 23h00,
> **Então** o pedido continua com 03h00 e **no mesmo dia operacional**.

### CA-3 — Fora do horário não vende (`RN-VEN-048`)

> **Dado** uma casa fechada em todos os dias,
> **Quando** a vitrine for aberta e um pedido enviado,
> **Então** o cardápio responde `aberta: false` e o pedido é recusado com **422**.

### CA-4 — Sem horário, continua vendendo (`RN-VEN-048`)

> **Dado** uma casa sem horário cadastrado,
> **Quando** um pedido for enviado,
> **Então** ele é aceito.

## 6. Modelo de dados tocado

- `unidade`: `hora_virada_operacional` (interval, default 4h), `cep`, `logradouro`,
  `numero_endereco`, `complemento`, `bairro`, `cidade`, `uf`
- `unidade_horario`: novo — `unidade_id`, `dia_semana`, `abre`, `fecha`, com `CHECK` de par e
  `UNIQUE (unidade_id, dia_semana)`
- `pedido`: `hora_virada_operacional`, `fuso_operacional`, e `data_operacional` recalculada
- Migration `HorarioEnderecoEViradaPorUnidade`

## 7. Parâmetros consumidos

`PAR-GER-001` deixou de ser "a hora de virada" e passou a ser **a virada de quem não cadastrou
horário**. O valor não mudou: 04h00.

## 8. Fora de escopo

- **O restaurante editar o próprio horário** ⚠ — falta a permissão `unidade.gerenciar` e a migration
  de dados que a concede aos perfis das unidades existentes. Hoje é a plataforma que cadastra
- **Feriado e exceção de data** — o cadastro é semanal. "Fechado no dia 25" é regra nova
- **Intervalo no meio do dia** (almoço e jantar com fechamento entre eles) — um par por dia
- **Trilha de auditoria por virada da casa** — ver §4
- **Validar se o CEP existe** — o domínio confere oito dígitos; se o CEP existe é pergunta dos
  Correios

## 9. Rastreabilidade

| Artefato | Caminho |
|----------|---------|
| Domínio | `src/GrelhaHub.Domain/Gerais/HorarioDeFuncionamento.cs`, `Unidade.cs` |
| Expressão SQL | `src/GrelhaHub.Infrastructure/Persistence/Configurations/ConfiguracoesDoModelo.cs` |
| Feature | `src/GrelhaHub.Application/Features/Gerais/Plataforma/AdministrarUnidade.cs` |
| Migration | `.../Migrations/*_HorarioEnderecoEViradaPorUnidade.cs` |
| Telas | `frontend/src/features/plataforma/CadastroDeOperacao.tsx`, `features/publico/CapaDaCasa.tsx` |
| Testes | `tests/GrelhaHub.Domain.Tests/Gerais/HorarioDeFuncionamentoTests.cs`, `tests/GrelhaHub.Integration.Tests/Gerais/HorarioEViradaTests.cs` |

## 10. Definição de pronto

- [x] Virada derivada do horário, em **um** lugar (`ViradaOperacional.De`)
- [x] Coluna gerada sobre colunas da linha, com os índices recriados na migration
- [x] Pedido carrega a virada aplicada — CA-2 prova que o passado não muda
- [x] Vitrine com endereço, "fechado agora" e recusa de pedido no servidor
- [x] Cadastro de endereço com ViaCEP **não bloqueante** e horário semanal
- [x] 8 testes de domínio + CA-1 a CA-4 de integração; `DiaOperacionalParidadeTests` segue verde
- [x] `dotnet build -c Release`, `dotnet test`, `eslint` e `tsc` verdes — **271 testes**

## 11. O que saiu diferente

### 11.1 O 500 mudo custou meia hora, e o harness ganhou voz

Duas falhas desta entrega chegaram como `{"status": 500, "detail": "Não foi possível concluir a
operação"}` — a mensagem certa para o usuário e inútil para quem escreve o teste. `TratadorDeExcecoes`
passa a incluir `exception.ToString()` na extensão `excecao` **fora de produção**. Guardado por
`IsProduction()` e não por `IsDevelopment()`: o ambiente do harness é `Testing`, e um
`IsDevelopment` deixaria justamente ele no escuro.

### 11.2 `UPDATE` onde devia haver `INSERT` — de novo

O horário novo era descoberto pela navegação de uma unidade **já rastreada**, e o EF Core classifica
filho com `Id` preenchido como `Modified`: `UPDATE` de linha que não existe, zero linhas afetadas, e
o erro chega como `DbUpdateConcurrencyException` — que parece conflito de concorrência e manda
investigar o lugar errado. É o mesmo defeito que o log de SQL do harness pegou no CA-9 da
`SPEC-VEN-001`, e a correção é a mesma: **inserir explicitamente** pelo `DbSet`.
