# ADR-0007 — Core genérico de restaurante com nicho por configuração

**Status:** Aceito · **Data:** Agosto/2026

## Contexto

O sub-nicho declarado do GrelhaHub é hamburgueria. A tentação natural seria modelar o
domínio em torno disso: entidade `Lanche`, campo `ponto_da_carne`, tabela `adicionais`.
Isso entregaria o nicho mais rápido — e travaria o produto nele.

Por outro lado, um sistema genérico demais ("qualquer negócio que venda qualquer coisa")
não atende bem ninguém e perde a proposta de valor específica: CMV de lanche,
personalização, taxa por zona.

## Decisão

Modelar um **core genérico de restaurante** — produto, pedido, mesa, delivery, estoque,
financeiro — e tratar o "sabor hamburgueria" como **camada de configuração** sobre ele:

| Necessidade do nicho | Como é atendida no core genérico |
|----------------------|----------------------------------|
| "Ponto da carne" | Grupo de modificadores com seleção obrigatória (mín. 1, máx. 1) |
| "Dobro de bacon" | Opção de modificador com acréscimo de preço |
| "Sem cebola" | Opção de modificador com acréscimo zero |
| Combo lanche + batata + refri | Produto do tipo combo com composição própria (fase 2) |
| Custo do lanche | Ficha técnica genérica de insumos |
| Fila da chapa | Praça de preparo vinculada a categoria (fase 2) |

Nenhuma dessas capacidades exige código específico de hamburgueria. Uma pizzaria
configura "tamanho" e "borda" nos mesmos grupos de modificadores; um restaurante à la
carte configura "ponto" e "acompanhamento".

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| Modelar direto para hamburgueria | Entrega mais rápida, mas cada novo tipo de cliente exigiria alteração de schema e de código — o oposto de um produto |
| Genérico total (motor de e-commerce) | Perderia as regras que dão valor: ficha técnica, CMV, comanda, zona de entrega. Sem essas, é só um catálogo |
| Configuração por plugins/extensões | Complexidade de arquitetura injustificada antes de existir o segundo nicho |

## Consequências

**Positivas**
- O modelo de dados serve pizzaria, lanchonete e restaurante à la carte sem alteração
- Nichar depois vira trabalho de *cadastro e telas*, não de schema
- Modificadores como conceito de primeira classe cobrem a dor de personalização de forma
  reutilizável (RN-003, RN-004)

**Negativas / custos aceitos**
- O cadastro inicial é mais trabalhoso: o dono precisa montar grupos de modificadores em
  vez de encontrar "ponto da carne" pronto. **Mitigação:** carga inicial com um template
  de cardápio de hamburgueria já configurado (a ser entregue na Sprint 7 & 8)
- Termos genéricos na interface ("modificador") são menos naturais que os do jargão do
  nicho ("adicional"). **Mitigação:** rótulo configurável na apresentação, mantendo o
  nome técnico no modelo
- Certas otimizações específicas de hamburgueria ficam inacessíveis enquanto o core não
  as generalizar
