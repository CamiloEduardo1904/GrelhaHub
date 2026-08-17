# GrelhaHub — Fila de Validação com o Cliente

> Ordem de prioridade para a primeira entrevista. **Não há tempo de validar 138 regras em
> uma hora.** Estas vinte são as que, se estiverem erradas, comprometem o modelo inteiro.

As perguntas 1 a 14 vêm do Catálogo v0.1. As perguntas 15 a 20 nasceram do cruzamento entre
o catálogo e o levantamento — são justamente os pontos em que os dois analistas discordaram,
o que as torna especialmente valiosas.

---

## 1. Como conduzir

**Não leia a regra para o entrevistado.** Ler "o saldo de um lote nunca é negativo" produz
concordância educada e nenhuma informação. Faça a pergunta operacional e deixe a pessoa
descrever o que faz — a regra se confirma ou se rejeita no relato.

**Registre a rejeição com o mesmo cuidado da confirmação.** Uma hipótese derrubada é
levantamento efetivo, e é o que separa este projeto de um brainstorm interno.

**Depois da entrevista**, cada regra tocada muda de estado em
[`03-catalogo-de-regras-de-negocio.md`](03-catalogo-de-regras-de-negocio.md):
`HIP` → `VAL`, `REJ`, `CNF` ou `ESC`, com data e fonte no histórico de versões.

---

## 2. Fila prioritária

### Bloco crítico — as sete estruturais

| # | Regra | Pergunta a fazer | Por que trava o modelo |
|---|-------|------------------|------------------------|
| 1 | **RN-GER-006** ⚠⚠ | *"Quando vocês fecham o caixa? A venda de 1h da manhã conta pra que dia?"* | Define a dimensão tempo de todo o BI. Se errar, o faturamento de sábado à noite cai em domingo — em definitivo |
| 2 | **RN-PRO-001, 002** | *"A receita de cada lanche está escrita? Já mudou alguma? Como vocês saberiam o custo de antes?"* | Define se a ficha técnica é versionada e se o custo histórico é reconstituível |
| 3 | **RN-PRO-004** ⚠⚠ | *"Tem algum molho ou preparo que vocês fazem antes e guardam?"* | Se sim, a ficha técnica precisa ser recursiva. Descobrir na Sprint 7 custa uma sprint inteira |
| 4 | **RN-PRO-005** | *"De cada 10 kg de alface que compram, quanto sobra depois de limpar?"* | Sem percentual de perda, **todo CMV fica subestimado** |
| 5 | **RN-EST-007** | *"Quando pega carne na geladeira, pega a mais velha ou a que está na frente?"* | Confirma ou derruba o FEFO. Se a resposta for "a da frente", o modelo mantém o lote mas o consumo é outro |
| 6 | **RN-EST-011** | *"Se comprou carne a R$ 30 e depois a R$ 35, qual preço vocês consideram no custo do lanche?"* | Decide entre custo por lote e custo médio — **é o conflito C1 do cruzamento** |
| 7 | **RN-EST-018** | *"Se o cliente desiste depois que o lanche já foi feito, o que acontece com o lanche?"* | Decide se o cancelamento devolve estoque ou gera perda — **conflito C2** |

### Bloco de vendas

| # | Regra | Pergunta a fazer | O que decide |
|---|-------|------------------|--------------|
| 8 | **RN-VEN-006, 007** | *"Duas mesas juntam. Como fica a conta?"* | Unificação de comanda e a `EXCLUDE` de uma comanda por mesa |
| 9 | **RN-VEN-016** | *"Quando o cliente pede sem cebola ou com bacon extra, muda o preço? E o estoque?"* | Se o ajuste afeta consumo de insumo — o que liga composição do item à baixa |
| 10 | **RN-VEN-021** | *"Já aconteceu de vender e descobrir que acabou o ingrediente?"* | Modo padrão de `PAR-VEN-003`. **A resposta esperada é sim** — ver pergunta 15 |
| 11 | **RN-VEN-023, 024** | *"Como funciona quando a mesa quer dividir a conta?"* | Divisão por pessoa ou por item, e a regra de arredondamento |

### Bloco financeiro, compras e logística

| # | Regra | Pergunta a fazer | O que decide |
|---|-------|------------------|--------------|
| 12 | **RN-FIN-002** | *"Quando cai o dinheiro de cada forma de pagamento?"* | Prazo de recebimento por forma — base do fluxo de caixa |
| 13 | **RN-COM-004** | *"Compra o mesmo item de mais de um fornecedor?"* | Se `insumo_fornecedor` é N:N com preço próprio |
| 14 | **RN-LOG-007** | *"Quanto cobra de entrega e quanto paga pro entregador?"* | Se as duas grandezas são distintas — e a margem real do delivery |

---

## 3. Perguntas acrescentadas no cruzamento

Nasceram dos pontos em que o catálogo e o levantamento discordaram. Cada uma resolve um
conflito registrado.

| # | Origem | Pergunta a fazer | O que resolve |
|---|--------|------------------|---------------|
| 15 | Conflito C3 | *"Quando falta ingrediente no meio do movimento, o que vocês fazem? Faz sentido o sistema avisar na tela e deixar você decidir se segue?"* | **Já decidido pelo time** (`RN-VEN-035`): o alerta é obrigatório. A pergunta agora afere apenas a *fricção* — modo padrão de `PAR-VEN-003`. Pergunte **depois** da 10 |
| 16 | Conflito C8 | *"Já entregaram para um cliente fora da área normal? Como decidiram a taxa?"* | Se RN-LOG-004 bloqueia mesmo, ou admite exceção com autorização |
| 17 | RN-VEN-027 | *"Quando o cliente monta o lanche, tem escolha obrigatória? Tem limite de adicionais?"* | A estrutura de grupos de modificadores com mínimo e máximo |
| 18 | RN-EST-017 | *"Vocês contam embalagem e sacola no custo do lanche do delivery?"* | Se descartável entra na ficha técnica — muda o CMV do canal delivery |
| 19 | RN-VEN-030 | *"Como registram o troco hoje? Entra em algum controle?"* | Se o troco é registrado ou apenas dado — afeta o fechamento de caixa |
| 20 | Divergência de escopo | *(com o professor, não com o cliente)* Os nove módulos precisam estar **modelados** ou **implementados e funcionando**? RN-FIS-001 significa emitir NFC-e de verdade? | O tamanho real do MVP. Ver [cruzamento §7](07-cruzamento-catalogo-x-levantamento.md#7-divergência-de-escopo--a-decisão-que-precisa-da-sua-confirmação) |

---

## 4. Perguntas de encerramento

Depois da fila, três perguntas abertas que costumam render mais que o roteiro inteiro:

1. *"O que mais te dá dor de cabeça no fim do mês?"*
2. *"Se você pudesse saber uma coisa que hoje não sabe sobre o negócio, qual seria?"*
3. *"Tem alguma coisa que vocês fazem 'no jeitinho' porque o sistema de vocês não deixa?"*

A terceira é a mais produtiva: ela revela as regras que o operador nem sabe que tem.

---

## 5. Registro do resultado

Para cada regra tocada na entrevista, anote na tabela abaixo e depois transporte para o
histórico do catálogo.

| Regra | Estado anterior | Estado novo | Data | Fonte | Observação do entrevistado |
|-------|-----------------|-------------|------|-------|-----------------------------|
| | HIP | | | | |

**Fonte** = nome e função de quem respondeu. Duas respostas divergentes sobre a mesma regra
produzem estado `CNF` (conflito), não uma escolha do analista.
