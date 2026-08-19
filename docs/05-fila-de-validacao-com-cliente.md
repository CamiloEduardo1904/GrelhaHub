# GrelhaHub — Fila de Validação com o Cliente

> Ordem de prioridade para a primeira entrevista. **Não há tempo de validar 159 regras em
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
| 8 | **RN-VEN-018 × RN-EST-021** ⚠⚠ | *"Em que momento vocês consideram que o ingrediente saiu do estoque: quando aceitam o pedido, ou quando começam a montar o lanche?"* | **Conflito C10.** Decide se a baixa dispara na Confirmação (como `RN-VEN-018` determina hoje) ou na Produção. Muda uma das **sete estruturais** e o sentido de `RN-EST-019` — ver abaixo |
| 9 | **RN-EST-023 × RN-EST-019** ⚠ | *"E se vocês aceitam o pedido e o cliente desiste dois minutos depois, antes de pôr na chapa — o ingrediente volta pro estoque?"* | Confirma a **devolução** na janela entre Confirmado e Em produção. Se a resposta for "nunca tirei do estoque ainda", a resposta real é à pergunta 8, e a baixa é na produção |

> **As perguntas 8 e 9 são a mesma pergunta por dois caminhos, e isso é de propósito.** A 8 pergunta
> o modelo; a 9 pergunta o caso concreto. Quem responde "na confirmação" na 8 e "nunca tirei ainda"
> na 9 está dizendo que a baixa é na produção — e é a 9 que vale, porque descreve o que acontece na
> loja. **Faça as duas.**

### Bloco de vendas

| # | Regra | Pergunta a fazer | O que decide |
|---|-------|------------------|--------------|
| 8 | **RN-VEN-006, 007** | *"Duas mesas juntam. Como fica a conta?"* | Unificação de comanda e a `EXCLUDE` de uma comanda por mesa |
| 9 | **RN-VEN-016** | *"Quando o cliente pede sem cebola ou com bacon extra, muda o preço? E o estoque?"* | Se o ajuste afeta consumo de insumo — o que liga composição do item à baixa |
| 10 | **RN-VEN-021** | *"Já aconteceu de vender e descobrir que acabou o ingrediente?"* | Modo padrão de `PAR-VEN-003`. **A resposta esperada é sim** — ver pergunta 15 |
| 10b | **RN-VEN-021 × RN-EST-009** ⚠ | *"Quando acaba no meio do movimento e vocês vendem mesmo assim, como o estoque fica no papel: negativo, zerado, ou vocês só acertam depois?"* | `RN-EST-009` proíbe saldo de lote negativo, então o modo *permitir* de `PAR-VEN-003` **não pode** significar negativo. Falta decidir o que ele significa: confirmar sem baixar, baixar só o que existe, ou baixar contra lote a receber. Sem isso, `RN-EST-021` não é implementável no modo permitir |
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
| 21 | `RN-VEN-043`, `RN-VEN-044` — implementação | *"No cardápio de vocês, 'X-Tudo' e 'x-tudo' são o mesmo lanche cadastrado duas vezes, ou pode haver dois itens diferentes com o mesmo nome escrito de outro jeito?"* | Se a unicidade de nome é **insensível a caixa**. Hoje aplicação e banco são os dois sensíveis, de propósito (`SPEC-VEN-003` §4): os dois cadastros passam. Resposta "é o mesmo" custa um índice sobre `lower(nome)` e uma migration. **A pergunta existe porque a restrição estava no banco desde a migration `Fundacao` sem regra que a enunciasse** — ninguém sabia se era decisão ou hábito |
| 22 | `RN-VEN-044` — implementação | *"Quando acaba um item no meio do movimento, quem marca isso no sistema: o gerente ou quem está no balcão?"* | Se "acabou hoje" merece permissão própria (`cardapio.disponibilidade`). Hoje exige `cardapio.gerenciar`, que é de gerência, e quem sabe que acabou é o Atendente — dívida declarada em `SPEC-VEN-003` §8 |
| 23 | `RN-VEN-054` ⚠⚠ — implementação | *"Na hora do movimento, quem está montando o lanche olha para onde: para a comanda do pedido ou para uma tela com a fila? E o 'sem cebola' do cliente precisa estar nessa tela?"* | **É a pergunta que a `SPEC-VEN-004` §8 declara aberta.** A fila de monitoramento **não exibe a observação** do cliente, porque é tela de ambiente compartilhado — decisão que só se sustenta enquanto a fila **não for** a tela de quem prepara. Se a cozinha adotar a fila como comanda de preparo, a observação volta e `RN-VEN-052` muda com ela. ⚠ Errar aqui não dá erro nenhum: dá sanduíche com cebola |
| 24 | `RN-VEN-054` — implementação | *"Um pedido que ninguém entregou e ninguém cancelou, do dia anterior — vocês querem ele ainda aparecendo na fila de hoje?"* | Hoje **sim**: a fila não tem recorte de período, porque o pedido que atravessou a virada continua para ser entregue. Se a resposta for "não, isso vira lixo na tela", a saída é `RN-VEN-041` (exclusão lógica) com um caminho de tela — nunca um `Take` que esconde sem dizer |

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
