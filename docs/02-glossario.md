# GrelhaHub — Glossário do Domínio

| Campo | Valor |
|-------|-------|
| Versão | 1.1 — revisado na consolidação com o Catálogo de Regras |
| Papel | **Vocabulário controlado.** Todo termo aqui definido é usado com o mesmo sentido no código, no banco de dados, nas specs e na documentação |
| Base de | Dicionário de dados da Sprint 5 & 6 |

> Um termo com dois significados em dois documentos vira duas tabelas no banco. Este
> glossário existe para impedir isso.

---

## A

**Adicional** — Ver *Modificador*. Na prática do balcão, "adicional" designa especificamente
o ajuste que acrescenta item e preço ("dobro de bacon"), enquanto "modificador" é o termo
genérico que inclui também as remoções. `RN-VEN-015`

**Ajuste de estoque** — Movimento de correção do saldo, sem venda nem compra associada.
Origina-se de contagem de inventário e exige justificativa obrigatória. `RN-EST-014`

**Apontamento** — Registro dos horários efetivos de entrada e saída de um colaborador, que
podem divergir do turno previsto na escala. `RN-RH-004`

## B

**Baixa de estoque** — Saída de insumo do estoque disparada pela **confirmação** do pedido.
Consome lotes pelo critério FEFO e pode gerar mais de um movimento por insumo quando a
quantidade atravessa lotes. `RN-EST-007`, `RN-VEN-018`

**Balcão** — Canal de venda em que o cliente pede e retira no ato, sem mesa e sem entrega.

## C

**Caixa diário** — Sessão de trabalho financeira, aberta com valor inicial e fechada com
conferência. Depois de fechada, não reabre. Opera sobre o **dia operacional**, não o civil.
`RN-FIN-007`, `RN-FIN-009`

**Canal de venda** — Origem do pedido: salão, balcão, delivery próprio ou marketplace. Todo
pedido pertence a exatamente um. Não confundir com *forma de pagamento*. `RN-VEN-001`

**Centro de custo** — Classificação de um lançamento financeiro por área da operação
(cozinha, salão, delivery, administrativo). Todo lançamento tem um. `RN-FIN-005`

**CMV (Custo da Mercadoria Vendida)** — Custo dos insumos consumidos para produzir o que foi
vendido, apurado a partir da ficha técnica e do custo real dos lotes consumidos.
`CMV% = custo do produto ÷ preço de venda × 100`. Indicador central de saúde da operação —
em hamburguerias, o alvo usual fica entre 25% e 35%. Visível apenas a perfis de gerência.
`RN-EST-011`, `RN-GER-012`

**Colaborador** — Pessoa que trabalha na operação. Distinto de *usuário*: nem todo
colaborador acessa o sistema, e o entregador é um colaborador com papel próprio.
`RN-RH-001`, `RN-LOG-005`

**Comanda** — Registro do consumo de uma mesa ou cliente durante a permanência. Recebe
lançamentos sucessivos e fecha quando a soma dos pagamentos iguala o total. Uma mesa tem no
máximo uma comanda aberta. `RN-VEN-005`, `RN-VEN-006`, `RN-VEN-024`

**Combo** — Produto que agrupa outros produtos e tem preço próprio, distinto da soma dos
componentes. `RN-VEN-017`

**Cotação** — Consulta de preço a fornecedores, exigida em compras acima do valor
parametrizado. `RN-COM-005`

**Cupom** — Benefício aplicável a um pedido, com validade, tipo (valor fixo ou percentual),
valor mínimo, limite total de usos e limite por cliente. Não é acumulável: um pedido admite
no máximo um. `RN-CRM-004`, `RN-CRM-005`

**Custo específico por lote** — Método de custeio adotado no projeto: o custo de uma saída é
o custo do lote efetivamente consumido, e não uma média. Escolhido porque o domínio é de
perecíveis — sem lote não há validade nem FEFO. `RN-EST-011`,
[ADR-0010](adr/0010-custeio-por-lote-com-fefo.md)

> **Custo médio ponderado** — *método considerado e **descartado**.* Recalcularia um custo
> unitário único do insumo a cada entrada. Seria adequado a insumo fungível e não perecível.
> Ver [conflito C1 do cruzamento](07-cruzamento-catalogo-x-levantamento.md#c1--método-de-custeio-do-estoque--estrutural).

## D

**Delivery** — Canal de venda com entrega no endereço do cliente, taxa de entrega por zona e
entregador atribuído. Exige cliente identificado. `RN-CRM-003`

**Dia operacional** — Período de apuração da operação: das **04h00** às **03h59** do dia
seguinte (`PAR-GER-001`). Uma venda registrada à 01h30 de domingo pertence ao dia
operacional de sábado. **Toda agregação de negócio por dia usa o dia operacional, nunca o
dia civil.** `RN-GER-006` ⚠⚠, [ADR-0011](adr/0011-dia-operacional.md)

**Divisão de conta** — Repartição do total da comanda entre os clientes, por número de
pessoas ou por item. A diferença de arredondamento vai para a primeira parcela, para que a
soma feche exatamente com o total. `RN-VEN-023`, `RN-VEN-034`

**DRE (Demonstração do Resultado do Exercício)** — Resultado gerencial, **sempre derivado**
dos lançamentos financeiros. Nenhum valor de resultado é digitado diretamente. `RN-FIN-012`

## E

**Entrada de estoque** — Movimento que aumenta o saldo, por compra, por produção de
sub-receita ou por transferência. Toda entrada cria ou alimenta um lote com custo unitário
próprio. `RN-EST-002`, `RN-EST-010`

**Entregador** — Colaborador ativo associado a um veículo durante o turno, responsável por
levar o pedido ao cliente. `RN-LOG-005`

**Escala** — Turnos previstos de cada colaborador por dia. Um colaborador não pode ter dois
turnos sobrepostos. `RN-RH-002`, `RN-RH-003`

**Estação de trabalho** — Setor de preparo da cozinha: chapa, fritadeira, montagem ou
bebidas. Toda ordem de produção é roteada para uma. Substitui o termo *praça*, usado na
versão inicial do glossário. `RN-PRO-009`, `RN-PRO-013`

**Estoque de segurança** — Margem de saldo mantida acima da demanda prevista, expressa em
dias (`PAR-EST-002`) e usada no cálculo da sugestão de compra. `RN-COM-009`

## F

**FEFO (*First Expired, First Out*)** — Critério de consumo do estoque: sai primeiro o lote
de **menor data de validade** entre os disponíveis. Distinto de FIFO, que sairia pela ordem
de entrada — um lote comprado depois pode vencer antes. `RN-EST-007`

**Ficha técnica** — Composição de um produto: quais insumos, em que quantidade e com que
percentual de perda de preparo. Serve simultaneamente para calcular o custo e para dar baixa
no estoque. É **versionada**: criar uma nova versão encerra a anterior e não altera o custo
dos pedidos já realizados. Todo produto vendável tem exatamente uma vigente em cada data.
`RN-PRO-001` a `RN-PRO-003`

**Forma de pagamento** — Meio pelo qual o pedido é quitado (dinheiro, débito, crédito, PIX,
vale-refeição). Cada uma tem **taxa de administração** e **prazo de recebimento** próprios, e
declara se movimenta espécie. `RN-VEN-025`, `RN-FIN-002`, `RN-FIN-016`

**Fundo de troco** — Valor em espécie colocado no caixa na abertura. É o `valor inicial` da
sessão. `RN-FIN-007`

## I

**Insumo** — Item comprado e consumido no preparo (pão, carne, queijo, **embalagem,
descartável**). Tem unidade de compra, unidade de consumo com fator de conversão, estoque
mínimo e ponto de pedido. Distinto de *produto*: o insumo é comprado, o produto é vendido —
mas uma sub-receita é os dois. `RN-EST-012`, `RN-EST-016`, `RN-EST-017`

**Inventário** — Contagem física periódica **por lote**, comparada ao saldo derivado para
apurar divergências. Gera movimento de ajuste com justificativa obrigatória. `RN-EST-014`

**Item estocável** — Aquilo que uma linha de ficha técnica pode referenciar: um `insumo` ou
um `produto` que seja sub-receita. Conceito criado para resolver `RN-PRO-004`; a forma de
modelá-lo é decisão em aberto da Sprint 3 & 4.

## L

**Lançamento financeiro** — Registro no razão financeiro, classificado em uma conta do plano
de contas e em um centro de custo, e vinculado a uma conta bancária ou ao caixa da loja.
`RN-FIN-005`, `RN-FIN-006`

**Livro-razão** — Padrão de modelagem adotado para estoque e caixa: cada evento gera um
registro **imutável**, e o saldo é sempre derivado da soma dos registros — nunca um campo
editável. Correção se faz por registro contrário, não por edição. `RN-EST-001`,
[ADR-0008](adr/0008-estoque-como-livro-razao.md)

**Local de estoque** — Posição física onde o insumo é guardado (câmara fria, seco, freezer).
Transferência entre locais gera dois movimentos na mesma transação. `RN-EST-015`

**Lote** — Quantidade de um insumo entrada de uma só vez, com **custo unitário próprio** e,
se perecível, **data de validade**. Todo movimento de estoque refere-se a um lote específico.
O saldo de um lote nunca é negativo. `RN-EST-004` a `RN-EST-006`, `RN-EST-009`

## M

**Margem de contribuição** — `preço de venda − custo do produto`. Quanto cada unidade vendida
contribui para cobrir despesas fixas e gerar lucro. Visível apenas a perfis de gerência.
`RN-GER-012`

**Marketplace** — Canal de venda por aplicativo de terceiro (iFood). O pedido já entra no
sistema como pago, e o valor a receber é líquido da comissão do canal. `RN-VEN-026`,
`RN-FIN-004`

**Mesa** — Posição física do salão. Uma mesa tem no máximo uma comanda aberta por vez.
`RN-VEN-006`

**Modificador** — Ajuste aplicável a um item do pedido ("sem cebola", "dobro de bacon"). Os
ajustes de um produto organizam-se em **grupos** com seleção mínima e máxima; quando o mínimo
é maior ou igual a 1, a seleção é obrigatória. Um modificador altera tanto o preço do item
quanto o consumo de insumos. `RN-VEN-015`, `RN-VEN-016`, `RN-VEN-027`

**Movimento de estoque** — Registro individual e imutável de entrada, saída, perda, ajuste,
transferência ou devolução de um insumo, sempre vinculado a um lote, com custo unitário,
usuário e data. `RN-EST-002` a `RN-EST-004`

## O

**Ordem de produção** — Comando de preparo gerado pela **confirmação** de um pedido, para
cada item que exija preparo, roteado para uma estação. `RN-PRO-008`

## P

**Parâmetro** — Valor configurável da operação (`PAR-*`), guardado no banco com vigência,
nunca cravado como constante no código ou em `CHECK`. Ver
[`04-parametros-do-sistema.md`](04-parametros-do-sistema.md)

**Pedido** — Unidade central de venda. Pertence a um canal e percorre os estados
**Aberto → Confirmado → Em produção → Pronto → Entregue → Finalizado**, podendo ir a
**Cancelado** a partir de qualquer estado anterior a Finalizado. Um pedido Finalizado ou
Cancelado não retorna a nenhum estado anterior. `RN-VEN-018`, `RN-VEN-019`

**Perda** — Movimento de saída sem venda, com motivo obrigatório: vencimento, quebra, queima,
degustação, furto, devolução ao fornecedor ou **cancelamento após produção**. `RN-EST-013`,
`RN-EST-018`

**Percentual de perda de preparo** — Descarte inerente ao processamento de um insumo (casca,
aparas). A quantidade bruta consumida é `líquida ÷ (1 − perda)`. Sem ele, **todo CMV fica
subestimado**. `RN-PRO-005`, `RN-PRO-006`

**Plano de contas** — Estrutura de classificação contábil gerencial dos lançamentos.
`RN-FIN-005`

**Ponto de pedido** — Nível de saldo que dispara a sugestão de compra de um insumo, distinto
do estoque mínimo. `RN-EST-016`

**Praça** — *Termo descontinuado.* Ver **Estação de trabalho**.

**Preço praticado** — Valor efetivamente cobrado por um item, registrado no momento da venda.
Não muda se o preço de tabela for alterado depois. Pode ser inferior ao de tabela mediante
desconto com motivo. `RN-VEN-008`, `RN-VEN-012`

**Preço de tabela** — Preço vigente do produto em um período. Alterá-lo cria uma nova
vigência e preserva a anterior. `RN-VEN-010`

**Produto** — Item do cardápio, vendável ou intermediário. Tem preço com vigência, ficha
técnica vigente e, opcionalmente, grupos de modificadores.

## R

**Rateio** — Distribuição de uma despesa fixa entre centros de custo, segundo critério
definido por despesa. `RN-FIN-011`

**Recebimento** — Entrada física de um pedido de compra, possivelmente **parcial**. Gera
simultaneamente a entrada de estoque e a conta a pagar ao fornecedor. `RN-COM-006`,
`RN-COM-008`

**Rendimento** — Quantas porções uma execução da ficha técnica produz. Relevante sobretudo em
sub-receitas: uma panela de molho rende N porções. `RN-PRO-007`

**RFM (Recência, Frequência, Valor)** — Segmentação de clientes em Campeão, Fiel, Em risco,
Hibernando e Perdido, **inferida** dos pedidos, sem entrada manual. Matéria-prima do BI da
Sprint 7 & 8. `RN-CRM-007`, `RN-CRM-008`

**Rota** — Agrupamento de entregas atribuídas a um mesmo entregador em um mesmo trajeto.
`RN-LOG-002`

## S

**Sangria** — Retirada de dinheiro em espécie do caixa durante o turno. Não pode exceder o
saldo disponível. `RN-FIN-008`, `RN-FIN-013`

**Sub-receita** — Produto intermediário que possui ficha técnica própria e **é estocado** —
o molho da casa. É produto e insumo ao mesmo tempo, o que obriga a ficha técnica a ser
recursiva. Sua produção antecipada ocorre sem pedido vinculado e gera entrada de estoque.
`RN-PRO-004` ⚠⚠, `RN-PRO-012`

**Sugestão de compra** — Demanda prevista para o horizonte de reposição, convertida pela
ficha técnica, menos o saldo disponível, mais o estoque de segurança, arredondada para o
múltiplo de compra do fornecedor. `RN-COM-009`

**Suprimento** — Aporte de dinheiro em espécie ao caixa durante o turno. Oposto da sangria.
`RN-FIN-008`

## T

**Taxa de entrega** — Valor cobrado pelo delivery, definido pela zona e **congelado no
pedido** no momento da criação. Grandeza distinta do **valor pago ao entregador**, registrada
separadamente. `RN-LOG-003`, `RN-LOG-007`, `RN-LOG-009`

**Taxa de serviço** — Percentual opcional (`PAR-VEN-002`) sobre o consumo no salão. Não
incide sobre delivery nem sobre a taxa de entrega. O cliente pode recusá-la. `RN-VEN-014`

**Ticket médio** — `faturamento ÷ número de pedidos` em um período, calculado separadamente
por canal de venda — balcão e delivery têm perfis muito diferentes. Apurado por **dia
operacional**.

**Trilha de auditoria** — Registro imutável de quem fez o quê e quando nas operações
sensíveis: cancelamento, desconto, ajuste de estoque, fechamento de caixa e alteração de
parâmetro. Guarda os valores anterior e posterior. `RN-GER-011`

**Troco** — Diferença entre o valor recebido em espécie e o total do pedido. É registrado e
**não integra a receita**. `RN-VEN-030`

## U

**Unidade** — A unidade de negócio (o restaurante). Raiz de isolamento dos dados: todo
registro transacional pertence a exatamente uma unidade. A operação ocorre hoje em uma só,
mas o vínculo existe desde a base para permitir expansão. Substitui o termo *estabelecimento*,
usado na versão inicial. `RN-GER-009`, [ADR-0005](adr/0005-multi-estabelecimento-por-coluna-discriminadora.md)

**Unidade de medida** — Grandeza em que um insumo é comprado ou consumido. Um insumo pode ser
comprado em uma e consumido em outra, com fator de conversão definido por insumo — compra-se
a caixa, consome-se o grama. `RN-EST-012`

## Z

**Zona de entrega** — Área geográfica com taxa cobrada e tempo estimado próprios. Endereço
fora de qualquer zona atendida não permite a confirmação do pedido de delivery. `RN-LOG-003`,
`RN-LOG-004`

---

## Termos revisados na consolidação

| Termo | Antes | Agora | Motivo |
|-------|-------|-------|--------|
| **Unidade** | `estabelecimento` | `unidade` | Nomenclatura do catálogo, `RN-GER-009` |
| **Estação de trabalho** | `praça` | `estacao` | Nomenclatura do catálogo, `RN-PRO-009` |
| **Custo médio ponderado** | Método adotado | **Descartado** — ver *Custo específico por lote* | Conflito C1 |
| **Baixa de estoque** | Devolvida no cancelamento | Cancelamento após produção gera **perda** | Conflito C2 |
| **Pedido** | 5 estados, terminando em `PAGO` | 6 estados, terminando em `Finalizado` | Conflito C4 |
