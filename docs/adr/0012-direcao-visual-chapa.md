# ADR-0012 — Direção visual Chapa

**Status:** Aceito · **Data:** 17/08/2026 · **Decide:** identidade da interface do painel do
restaurante e do cardápio público · **Detalhamento:** [`08-design-system.md`](../08-design-system.md)

## Contexto

O frontend nasceu com a paleta da marca aplicada e nenhuma direção por trás dela: cartões
cinza, cantos arredondados, sombra borrada. Funcional e anônimo — o que hoje se reconhece
como "cara de coisa feita por IA".

Esse visual é reconhecível justamente porque **não decide nada**: gradiente violeta→azul,
glassmorphism, `rounded-2xl` em tudo, sombra cinza uniforme, Inter em dois pesos, emoji como
ícone. É a média de tudo, e por isso não é de ninguém.

O time pediu algo **cartunesco e sério**, com referências de interfaces que têm
materialidade — metal escovado, display de LED âmbar, plástico de brinquedo, papel impresso.
O que essas referências têm em comum não é estilo: é que **as coisas parecem feitas de
alguma coisa**, e cada uma escolhe uma ideia e a executa até o fim.

Há um agravante que não é de gosto. O painel roda em tablet, numa cozinha quente, operado de
pé, com pressa e às vezes com luva. `RNF-010` já exige alvo de 44 px e `RNF-012` exige
1024×768. Personalidade que atrapalhe ler um preço a dois metros é defeito, não estilo.

## Decisão

Adotar a direção **Chapa** — impressa e carimbada — como base visual do produto.

O ponto de partida não é invenção: é uma regra que já existia no kit de marca e não estava
sendo usada. O monograma é um GH construído inteiramente de listras de grelha, e o kit
determina que *"a barra da ligadura sempre em cor de destaque diferente das demais barras"*.

**A regra da marca vira a regra da interface:**

> Em qualquer grupo, exatamente **uma** barra é âmbar — a ativa.

Serve para estado do pedido, aba, etapa de preparo e item selecionado. Um único mecanismo,
repetido em toda tela: o operador aprende a lê-lo uma vez e usa em todo lugar.

Os quatro pilares:

| Pilar | O que significa |
|-------|-----------------|
| **Sombra sólida deslocada** | `box-shadow: 5px 5px 0` em carvão. Sem raio de desfoque. Carimbado, com peso e lugar — não flutuando |
| **A listra é estrutural** | Não é ornamento: é a régua de progresso, o marcador de aba, o indicador de fase |
| **Carvão dominante** | Chão escuro na operação, papel no administrativo. Menos ofuscamento sob luz de cozinha, e a cor certa para grelha |
| **Cartoon por fora, sem graça por dentro** | Personalidade na moldura — bordas, cabeçalhos, estados vazios, cardápio. Dentro do dado, nada |

O último pilar é a disciplina que separa sério de fofo, e é o mais fácil de violar.

### Tipografia

Três papéis, stacks de sistema — **sem webfont**, por decisão: o painel roda em tablet de
cozinha e não pode depender de fonte baixando no meio do serviço.

| Papel | Stack | Onde |
|-------|-------|------|
| Peso | `"Arial Black", "Helvetica Neue", system-ui` | Número do pedido e total. Uma ocorrência por cartão |
| Mono | `ui-monospace, "Cascadia Mono", "SF Mono", Menlo, Consolas` | Preço, telefone, ID de regra, status |
| Sans | `system-ui, -apple-system, "Segoe UI", Roboto` | Só onde há frase |

Dado em mono com `tabular-nums` alinha em coluna sozinho. E a escolha da fonte já informa
que tipo de coisa é aquilo, antes de o usuário ler.

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| **Painel** — display de LED âmbar sobre carvão, tudo mono, transição por flip | Excelente para o KDS e provavelmente será a linguagem dele na onda 7. Mas formulário denso de cadastro e tabela de produtos ficam hostis nessa língua — e o MVP tem muito mais cadastro que cozinha |
| **Esmalte** — placa esmaltada de lanchonete, contorno grosso, cor chapada, granulado | A mais calorosa e a melhor no cardápio público. Rejeitada porque, sem disciplina constante, escorrega para fofo e perde a seriedade no painel de gestão — e o painel é onde o sistema é julgado |
| **Manter o visual neutro atual** | Mais barato e sem risco, mas entrega um ERP indistinguível de qualquer outro. O projeto é defendido em banca: identidade é argumento |
| **Biblioteca pronta** (shadcn, MUI, Chakra) | Resolveria acessibilidade e consistência de graça. Rejeitada porque toda a proposta é justamente **não** parecer com o resto — e adaptar uma biblioteca para fugir da cara dela costuma custar mais que construir os poucos componentes que este produto precisa |
| **Creme dominante** com laranja | Era o caminho óbvio pela paleta. Rejeitado porque bege editorial com acento terracota é exatamente um dos clichês visuais do momento. Carvão dominante foge dele e é mais honesto com o domínio |

## Consequências

**Positivas**

- Identidade que nasce da própria marca, com uma regra rastreável ao kit — defensável em
  banca sem apelo a gosto pessoal
- Um mecanismo visual único (a listra âmbar) cobre progresso, seleção e fase, em vez de três
  linguagens diferentes
- Alvo de 48 px na operação, acima do exigido por `RNF-010`
- Sem webfont: uma requisição a menos e nenhum salto de layout na partida
- A direção tem regras nomeadas (`CHAPA-01` a `CHAPA-08`), então revisão de código pode
  recusar com referência em vez de "não gostei"

**Negativas / custos aceitos**

- **A stack de sistema é imprecisa.** `Arial Black` é tosca de perto e renderiza diferente
  entre Windows e macOS. Aceito por ora; se incomodar, a saída é empacotar **Archivo**
  variável como arquivo local — nunca CDN
- Sombra sólida e borda grossa **consomem espaço**. Em 1024×768 isso pesa, e a densidade de
  tabela precisa ser vigiada
- Componentes próprios significam **acessibilidade por nossa conta**: foco visível, ordem de
  tabulação e contraste ficam sob nossa responsabilidade, sem rede de proteção de biblioteca
- Um visual opinativo **divide opinião**. Se a banca ou o cliente reagir mal, o custo de
  reverter é de CSS e três componentes — não de arquitetura. Foi por isso que a direção ficou
  concentrada em tokens

## Escopo desta decisão

Vale para o painel do restaurante e o cardápio público. **Não** decide a linguagem do KDS
(onda 7) — a direção *Painel* segue candidata natural lá, e as duas convivem: o KDS pode ser
um modo de exibição da Chapa, não um sistema concorrente.

## Rastreabilidade

| Artefato | Onde |
|----------|------|
| Regras de uso e tokens | [`docs/08-design-system.md`](../08-design-system.md) |
| Proposta navegável | [`docs/design/proposta-chapa.html`](../design/proposta-chapa.html) |
| PDF de apresentação | [`docs/design/GrelhaHub-Direcao-Chapa.pdf`](../design/GrelhaHub-Direcao-Chapa.pdf) |
| Origem da regra da listra | `assets/brand/README.md` |
