# GrelhaHub — Parâmetros do Sistema

> Valores configuráveis da operação. **Não devem virar constante no código nem `CHECK` com
> número cravado** — o enunciado da regra referencia o parâmetro, e o parâmetro guarda o
> valor.

O motivo é operacional, não estético: taxa de serviço, teto de desconto e hora de virada do
caixa são decisões comerciais do dono. Mudam sem aviso, e não podem exigir deploy nem
migration.

---

## 1. Catálogo de parâmetros

| ID | Parâmetro | Valor inicial | Tipo | Usado por |
|----|-----------|---------------|------|-----------|
| `PAR-GER-001` | Hora de virada do dia operacional | **04:00** | `time` | RN-GER-006 |
| `PAR-VEN-001` | Desconto máximo sem autorização | **10%** | `numeric(5,2)` | RN-VEN-013 |
| `PAR-VEN-002` | Percentual da taxa de serviço | **10%** | `numeric(5,2)` | RN-VEN-014 |
| `PAR-VEN-003` `CRZ` | Fricção na falta de saldo de insumo — o alerta é sempre exibido | **avisar** | `enum(bloquear, avisar, permitir)` | RN-VEN-021, RN-VEN-035 |
| `PAR-VEN-004` | Máximo de produtos **ativos** por categoria | **50** | `integer` | RN-VEN-046 |
| `PAR-EST-001` | Validade mínima aceita no recebimento | **7 dias** | `integer` | RN-EST-008 |
| `PAR-EST-002` | Dias de estoque de segurança | **3 dias** | `integer` | RN-COM-009 |
| `PAR-COM-001` | Valor mínimo que exige cotação | **R$ 500,00** | `numeric(12,2)` | RN-COM-005 |
| `PAR-CRM-001` | Multiplicador do intervalo médio para "em risco" | **1,5** | `numeric(4,2)` | RN-CRM-008 |
| `PAR-FIS-001` | Prazo legal para cancelamento fiscal | **30 min** | `interval` | RN-FIS-003 |

---


### 1.1 Decididos, e ainda **não** parametrizados

⚠ **Estes valores estão no código, como constante, nomeando o código do parâmetro que copiam.**
Não entram na tabela acima de propósito: aquela tabela é semeada por migration e lida em
`parametro_valor`, e uma linha lá que ninguém lê seria pior do que uma constante — alguém trocaria
o número no banco e nada aconteceria.

| Código | Decisão | Valor | Onde está hoje | O que falta para promover |
|--------|---------|-------|----------------|---------------------------|
| PAR-VEN-005 | Intervalo inicial do acompanhamento do pedido **e da fila** (`RN-VEN-047`, `RN-VEN-055`) | 15 s | `usePedidoAoVivo.ts` e `useFilaAoVivo.ts` | Rota **anônima** que devolva parâmetro — a tela do cliente não autentica |
| PAR-VEN-006 | Consultas antes de afrouxar | 10 | `usePedidoAoVivo.ts` | idem |
| PAR-VEN-007 | Intervalo depois de afrouxar | 25 s | `usePedidoAoVivo.ts` | idem |
| PAR-VEN-008 | Intervalo da fila de monitoramento **ociosa** — `RN-VEN-055` | 60 s | `useFilaAoVivo.ts` | Rota de leitura de parâmetro **por código na tela** — a fila é autenticada, então o obstáculo aqui **não** é o de `PAR-VEN-005`: é que o valor é por unidade e a tela precisaria de uma requisição extra na montagem, antes de saber a que ritmo perguntar |
| PAR-VEN-009 | Ciclos com a fila vazia antes de afrouxar — `RN-VEN-055` | 4 | `useFilaAoVivo.ts` | idem |
| PAR-GER-002 | Validade do link de redefinição | 60 min | `EmissorDeLinkDeAcesso` | Parâmetro de **escopo global**: `parametro_valor` é por unidade, e a solicitação é anônima — no instante do pedido ainda não se sabe de que unidade é a pessoa, e o usuário de plataforma não tem nenhuma |
| PAR-GER-003 | Validade do convite de primeiro acesso | 72 h | `EmissorDeLinkDeAcesso` | idem |

Os três obstáculos são reais e diferentes entre si: um é **de rota** (a tela do cliente é pública
e não autentica), outro é **de escopo** (o parâmetro é por unidade, e quem pede o link de acesso
ainda não tem uma), e o terceiro é **de momento** — `PAR-VEN-008` e `PAR-VEN-009` vivem numa tela
autenticada, que **poderia** ler o parâmetro, mas precisaria de uma requisição a mais antes de saber
a que ritmo perguntar. Enquanto não forem resolvidos, quem mudar o número muda a constante — e o
código do parâmetro no comentário é o que faz o `grep` encontrar o lugar.
---

## 2. `PAR-VEN-003` — falta de saldo de insumo

Nasceu da resolução do conflito C3, entre o catálogo (que bloqueia a venda sem saldo) e o
levantamento (que a permitia com alerta). Ver
[cruzamento §4-C3](07-cruzamento-catalogo-x-levantamento.md#c3--saldo-pode-ser-negativo).

> **Decisão do time, 17/08/2026 (`RN-VEN-035`):** o alerta na tela é **obrigatório nos três
> modos**. O parâmetro decide a *fricção* — se o lançamento é recusado, exige confirmação ou
> apenas informa — **nunca se o operador é avisado**.
>
> A versão anterior deste parâmetro previa um modo `permitir` silencioso. Foi removida: um
> sistema que deixa passar sem avisar transforma ruptura de insumo em surpresa na cozinha,
> que é exatamente a dor mapeada no início do projeto.

| Modo | Alerta na tela | Comportamento ao lançar produto sem saldo suficiente |
|------|:--------------:|------------------------------------------------------|
| `bloquear` | ✔ | O lançamento é **recusado**. Adequado a insumo caro e controlado |
| `avisar` | ✔ | O lançamento **exige confirmação explícita** do operador. A saída consome um **lote de acerto** marcado, que aparece no relatório de divergência de estoque |
| `permitir` | ✔ | O lançamento prossegue **sem exigir confirmação**; o alerta é informativo e a divergência entra no mesmo relatório |

**Valor inicial `avisar`**, definido **por produto**, com queda para o valor global quando o
produto não define o seu.

**Por que não bloquear tudo:** travar a venda no pico faz o atendente contornar o sistema —
anotar no papel, lançar outro produto — e aí o dado se perde de vez, que é pior do que o
saldo divergente. O alerta preserva a informação; o bloqueio, quando contornado, a destrói.

### O que o alerta precisa dizer

Não basta "sem estoque". O operador está no balcão, com fila, e precisa decidir em segundos:

- **qual insumo** falta (não qual produto — ele já sabe o produto)
- **quanto** falta, na unidade de consumo
- **quantas unidades** do produto ainda dá para montar com o saldo existente

O detalhamento vai na spec de lançamento de item (onda 4).

---

## 3. Modelagem

Parâmetro é dado, não configuração de aplicação. Vive no banco, por unidade
(RN-GER-009), com histórico de vigência — trocar a taxa de serviço não pode reescrever o
passado, pelo mesmo princípio de RN-VEN-010.

```
parametro           (id, codigo, descricao, tipo_valor, escopo)
parametro_valor     (id, parametro_id, unidade_id, valor, vigencia_inicio, vigencia_fim)
```

**Consequências:**

- A leitura de um parâmetro é sempre *na data do evento*, nunca "o valor de agora". Um
  pedido de três meses atrás foi calculado com a taxa de serviço daquela época
- `EXCLUDE` constraint impede vigências sobrepostas para o mesmo parâmetro e unidade
- A aplicação lê parâmetros por um único ponto, com cache invalidado na escrita — nenhum
  handler acessa a tabela diretamente

## 4. Regra de uso

1. Nenhum literal numérico de negócio no código C#, no SQL ou no frontend. Se um número
   tem significado comercial, ele é parâmetro.
2. `CHECK` constraint pode validar **domínio** (`percentual entre 0 e 100`), nunca
   **política** (`percentual <= 10`).
3. Parâmetro novo entra neste documento **antes** de ser usado, com valor inicial
   justificado e a regra que o consome.
4. Toda alteração de valor em produção fica na trilha de auditoria (RN-GER-011).

---

## `PAR-VEN-004` — teto de produtos por categoria

Pedido como *"uma flag de 50 produtos por categoria, ajustável via banco por enquanto"*. Virou
**parâmetro**, e não coluna em `unidade`, por três motivos que a coluna não entrega:

| `parametro_valor` dá | Coluna daria |
|----------------------|--------------|
| Recorte por unidade, com valor padrão global quando a unidade não tem o seu | Um número por unidade, e nada quando ninguém preencheu |
| **Vigência** — o teto de dezembro não reescreve o de junho (`SPEC-GER-003`) | O valor de agora, sem passado |
| Trilha de quem mudou, quando e de quanto para quanto (`SPEC-GER-005`) | `UPDATE` sem história |

E o "ajustável via banco" continua sendo literalmente isso — um `INSERT` em `parametro_valor`:

```sql
-- Teto de 80 produtos por categoria, só para esta unidade, a partir de agora
INSERT INTO parametro_valor (id, unidade_id, parametro_id, valor, vigencia_inicio)
SELECT gen_random_uuid(), :unidade_id, p.id, '80', now()
  FROM parametro p WHERE p.codigo = 'PAR-VEN-004';
```

> **Por que o limite conta apenas os produtos ativos.** `RN-VEN-045` proíbe exclusão: produto
> retirado do cardápio continua na tabela para que o item de pedido histórico não perca a
> referência. Contar o histórico faria a categoria **entupir permanentemente** — depois de 50
> produtos retirados ao longo de dois anos, a seção "Lanches" não aceitaria mais nada, e a única
> explicação disponível ao usuário seria um erro que ele não teria como resolver.

**Não há tela para editar este parâmetro**, e a ausência é declarada: a `SPEC-GER-003` entregou a
rota `PUT /v1/parametros/{codigo}`, que atende. Tela própria de configuração de cardápio é trabalho
de outra spec.
