# GrelhaHub — Design System **Chapa**

| Campo | Valor |
|-------|-------|
| Versão | 1.8 |
| Decisão | [ADR-0012](adr/0012-direcao-visual-chapa.md) |
| Situação | **Base oficial** — aplicada ao app em 17/08/2026; §8 (vitrine), o formulário dentro do `<Dialogo />` e a **§9 (movimento)** acrescentados no mesmo dia |
| Ver funcionando | [`design/proposta-chapa.html`](design/proposta-chapa.html) · [PDF](design/GrelhaHub-Direcao-Chapa.pdf) |

---

> ## A regra que sustenta tudo
>
> O kit de marca determina que *"a barra da ligadura sempre em cor de destaque diferente das
> demais barras"*. Traduzida para interface:
>
> ### Em qualquer grupo, exatamente **uma** barra é âmbar — a ativa.
>
> Estado do pedido, aba, etapa de preparo, item selecionado. Um mecanismo só, em toda tela.
> Duas âmbares significam dois "agora", e o operador perde a referência.

---

## 1. A disciplina

**Cartoon por fora, sem graça por dentro.**

Personalidade mora na moldura: bordas, cabeçalhos, estados vazios, o cardápio público.
**Dentro do dado, nada.** Uma tabela de produtos não precisa de charme — precisa de
tabulação, contraste e nenhuma surpresa.

Isso não é gosto. O painel roda em tablet, numa cozinha quente, operado de pé, com pressa e
às vezes com luva. No momento em que a personalidade atrapalha ler um preço a dois metros,
ela está errada.

---

## 2. Tokens

### 2.1 Cor

A paleta **não é escolha de design** — vem de [`assets/brand/README.md`](../assets/brand/README.md)
e não se altera. O que é escolha é o tratamento: carvão dominante na operação, papel no
administrativo.

| Token | Valor | Uso |
|-------|-------|-----|
| `--laranja-brasa` | `#FF6B2C` | Ação primária, barras já percorridas, **e texto sobre carvão** (7:1) |
| `--brasa-texto` | `#C23A00` | A brasa **para texto sobre fundo claro** — ver abaixo |
| `--ambar-chama` | `#FFB020` | **A barra ativa.** Uma por grupo |
| `--creme` | `#FFE3D3` | Texto sobre carvão |
| `--carvao` | `#14100C` | Chão escuro, traço, sombra |
| `--cinza-quente` | `#9C8E7E` | Secundário **sobre carvão** — 5,9:1. Rótulo, nunca frase |
| `--papel` | `#EBE2D6` | Chão claro — neutro quente, nunca cinza puro |
| `--papel-alto` | `#F8F3EC` | Superfície sobre o papel |

> ### `--brasa-texto` — a brasa da marca não serve para texto sobre claro
>
> `#FF6B2C` sobre o papel dá **2,22:1**. Está abaixo do mínimo de **3:1 até para texto grande**, e
> muito abaixo do 4,5:1 de texto corrido. Sobre carvão a mesma cor dá **7:1** — é por isso que a
> wordmark da barra escura sempre pareceu boa, e ninguém notou que a do rodapé da vitrine e a da
> tela de login estavam a 2,57:1.
>
> `--brasa-texto` é **derivada, não uma cor nova**: mesmo matiz (17,9°) e mesma saturação (100%),
> só mais escura. Dá **4,20:1** no papel e **4,88:1** na superfície. A paleta de marca continua
> intocada — o kit manda, e isto está para ela como os neutros derivados estão para o carvão.
>
> No tema escuro o token **volta a ser** `--laranja-brasa`: ali o tom derivado seria escuro sobre
> escuro.
>
> `.wordmark span` usa `--brasa-texto` **por padrão**, e a brasa pura só dentro das superfícies de
> carvão enumeradas (`.barra-app`, `.barra-site`, `.capa-site`, `.faixa.escura`, `.faixa-cta`). O
> padrão é o fundo claro porque é onde a wordmark mais aparece: login, troca de senha, cartões do
> painel e rodapé.

**Semântico — separado do acento de marca.** Laranja é ação, não sucesso.

| Token | Claro | Escuro |
|-------|-------|--------|
| `--ok` | `#2F6B3A` | `#6EC27F` |
| `--alerta` | `#9A5B00` | `#FFB020` |
| `--erro` | `#A3271F` | `#EF6A5E` |

### O bloco da logomarca — a única exceção ao alinhamento à esquerda

A moldura de 200×150, o botão e a nota de formato ficam **centralizados**. É o único campo
centralizado do produto, e a exceção tem razão: **aqui não se preenche nada — se olha**. O bloco
existe para responder *"a minha marca ficou boa no cardápio público?"*, e um objeto de exposição
encostado à esquerda de um cartão largo lê como sobra de tela.

⚠ **Formulário continua alinhado à esquerda em todo o resto.** Rótulo e campo têm de partir da mesma
coluna, senão o olho reancora a cada linha. Centralizar campo de digitação é o erro que esta exceção
**não** autoriza.

⚠ **A legenda não vai junto.** `align-items: center` a arrastaria para o meio da borda de cima, e ela
é rótulo de moldura — em todo o resto do produto fica no canto superior esquerdo. Centralizá-la aqui
faria dela a única do sistema, e a pergunta *"por que esta é diferente?"* não teria resposta. **O que
se centraliza é o que se olha; o que nomeia a caixa fica onde sempre esteve.**

### A escolha do tema — `<AlternadorDeTema />`, 19/08/2026

Os dois temas existiam na folha desde o início; o que faltava era **a escolha**, na barra do
aplicativo, à esquerda de *Sair*.

| Posição | `data-theme` | O que faz |
|---------|--------------|-----------|
| **Sistema** *(padrão)* | *(ausente)* | Acompanha o aparelho, via `prefers-color-scheme` |
| **Claro** | `light` | Fixa o claro, mesmo com o aparelho no escuro |
| **Escuro** | `dark` | Fixa o escuro |

⚠ **Três posições, e não duas.** `Sistema` **não é o mesmo que `Claro`**: ele muda sozinho à noite
em quase todo celular. Um alternador de duas posições obrigaria a escolher para sempre no primeiro
clique, sem caminho de volta.

⚠ **`Sistema` REMOVE o atributo**, não escreve `data-theme="sistema"`. A folha decide pela ausência,
e um valor inventado ali deixaria a página presa no claro — o oposto de "acompanhe o aparelho".

⚠⚠ **O tema é aplicado ANTES da primeira pintura**, por um `<script>` inline em `index.html`. Sem
ele, quem escolheu claro vê a tela **escura por um quadro** antes de o React montar — e o inverso na
cozinha, que é pior: um lampejo branco no escuro, a meio metro do rosto, no meio do serviço. É o
mesmo motivo de a Chapa não usar webfont: **o que aparece antes de o pacote carregar também é
interface.** O conteúdo do script vive em `services/tema.ts` para os dois não divergirem.

⚠ **A preferência é do APARELHO — `localStorage`, não o perfil no banco.** A mesma gerente usa o
tablet da cozinha no escuro e o computador do escritório no claro; guardar no servidor levaria a
escolha de um para o outro, que é o contrário do que ela quis.

O controle reusa `.chips` e a `<Grelha>`: `CHAPA-02`, uma barra âmbar no grupo. Um alternador com
vocabulário próprio seria a quarta forma de dizer "escolha uma destas" na mesma tela. E `CHAPA-06` —
nada de 🌞/🌙: o estado é **palavra**.

### CHAPA-10 merece explicação

> **`--muted` é para rótulo e metadado. Frase usa a cor do texto.**

O secundário original era `#6B5F52`: **4,84:1** sobre o papel — o piso exato do AA. Passa na
ferramenta e cansa na leitura, e estava sendo usado para **parágrafo inteiro** a 0,92 rem. Agora é
`#544A40`, que dá **6,74:1** sobre o papel e **7,83:1** sobre a superfície, sem sair do marrom
quente da marca.

Mas o token não era o problema principal — o **uso** era. A correção real é a regra:

| Papel | Cor | Exemplo |
|-------|-----|---------|
| Frase, parágrafo, resposta | `--text` / `--prosa-escura` | corpo de cartão, resposta de FAQ, subtítulo da capa |
| Rótulo, unidade, instante, contagem | `--muted` / `--cinza-quente` | `Total`, `12:04:31`, `por unidade / mês` |

A hierarquia entre título e corpo vem de **mono-versalete × sans** — que a Chapa já usa — e não de
apagar a frase. Cinza claro em cima de papel bege é bonito em captura de tela e ruim às 22h no
tablet da cozinha, que é onde o produto vive.

> **E texto nunca leva `opacity`.** O subtítulo da capa tinha `opacity: 0.82`, que levava o
> contraste de 15,5:1 para ~10:1 no texto **mais importante da página**. O valor composto não
> aparece em nenhuma ferramenta de verificação — ela mede a cor declarada. Se o texto precisa
> recuar, recue com **tamanho**; se precisa de outra cor, declare a cor.

`--prosa-escura` existe para isso: é `--creme` cheio, para frase sobre carvão.

### CHAPA-11 merece explicação

> **Superfície que declara fundo e não declara cor de texto herda a cor de quem a contém.**

Custou um defeito visível na vitrine. A capa é carvão e declara `color: var(--creme)` na seção. A
`<Comanda>` — reusada ali como prova visual — declarava `background: var(--surface)` e **não**
declarava `color`. Resultado: número do pedido, itens e total em **creme sobre papel creme**.

O detalhe que fez o diagnóstico demorar: **partes do cartão continuaram legíveis**. Eram justamente
as que tinham cor própria — `.rotulo`, `.secundario`, `.obs`, todas `--muted`. O canal, o cliente,
a observação e a palavra "TOTAL" liam-se bem; o número e os valores, não. Parecia problema de um
elemento, e era da herança de todos.

A regra é mecânica e vale para toda superfície:

```css
.superficie {
  background: var(--surface);
  color: var(--text);   /* ← sem esta linha, a cor vem de fora */
}
```

Vale mesmo quando o componente **hoje** só aparece sobre fundo neutro: `.plano`, `.cartao-dor` e
`.bloco-recurso` ganharam a linha porque nada impede alguém de pôr um deles numa `.faixa.escura`
amanhã — e o defeito reaparece idêntico, num arquivo diferente.

### 2.2 Elevação

```css
--elev-1: 3px 3px 0 var(--carvao);   /* elemento interno: botão, contador */
--elev-2: 5px 5px 0 var(--carvao);   /* cartão: comanda, moldura, tabela */
--traco:  2px solid var(--carvao);   /* toda borda */
```

**Nunca há raio de desfoque.** Se aparecer, é plástico.

### 2.3 Tipografia

```css
--peso: "Arial Black", "Helvetica Neue", system-ui, sans-serif;
--mono: ui-monospace, "Cascadia Mono", "Segoe UI Mono", "SF Mono", Menlo, Consolas, monospace;
--sans: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
```

| Papel | Onde | Regra |
|-------|------|-------|
| **Peso** | Número do pedido, total | **Uma ocorrência por cartão.** Dois pesos brigando anulam a hierarquia |
| **Mono** | Preço, telefone, ID de regra, status, código | Sempre com `font-variant-numeric: tabular-nums` |
| **Sans** | Descrição, ajuda, mensagem de erro | Só onde há frase |

Sem webfont — decisão do [ADR-0012](adr/0012-direcao-visual-chapa.md): o painel não pode
depender de fonte baixando no meio do serviço.

#### A escala — `CHAPA-13`, desde 19/08/2026

Oito degraus, razão ~1,25, ancorados em 16 px. Antes **não havia escala**, e a ausência foi
medida de fora:

| Superfície | Tamanhos distintos |
|------------|-------------------:|
| Vitrine | **21** |
| Painel da loja | 11 |
| Cardápio público | 9 |
| Plataforma | 7 |

```css
--texto-2xs: 0.75rem;   /* 12px — rótulo mono, eyebrow, selo */
--texto-xs:  0.875rem;  /* 14px — legenda, metadado */
--texto-sm:  1rem;      /* 16px — corpo */
--texto-md:  1.25rem;   /* 20px — corpo destacado, lead */
--texto-lg:  1.5rem;    /* 24px — título de cartão */
--texto-xl:  2rem;      /* 32px — título de seção */
--texto-2xl: 3rem;      /* 48px — display secundário */
--texto-3xl: 3.75rem;   /* 60px — capa */
```

**Duas regras, e a segunda explica a primeira:**

1. Nenhum `font-size` fora destes tokens
2. **Nunca `em` para tamanho de fonte** — só `rem`

> ⚠ **O valor fracionário é o sintoma, não a doença.** A vitrine tinha `17.28px` e `15.68px` na
> tela, e ninguém escreveu esses números: eles são `1.08em` dentro de um elemento que já era
> `1.2em`. Cada nível multiplica o anterior, e o degrau final não foi escolhido por ninguém — o
> que o olho registra é que um rótulo de 13,28px e outro de 13,6px deveriam ser o mesmo nível de
> hierarquia e não são. **`rem` não compõe**, e é por isso que a regra é sobre a unidade e não
> sobre o valor.

E o **ritmo vertical**, na mesma escala:

```css
--esp-1: 0.5rem;  --esp-2: 1rem;   --esp-3: 1.5rem;  --esp-4: 2rem;
--esp-6: 3rem;    --esp-8: 4rem;   --esp-12: 6rem;   --esp-16: 8rem;
```

> ⚠ **Adoção: a vitrine está convertida; painel e cardápio não.** A conversão foi feita onde o
> problema é maior — 21 degraus reduzidos aos oito, um a um, com o de/para registrado no topo de
> `vitrine.css`. Painel (11) e cardápio (9) seguem com valores crus, e a varredura deles é
> **decisão pendente**: são as telas que o cliente usa todos os dias, mexer nos 82 `font-size` da
> folha global muda todas de uma vez, e não existe verificação visual automatizada para isso
> (`RN-VEN-047` já registra que o frontend não tem harness). Convergem por regra, não por sweep:
> **valor novo em qualquer folha entra por token.**

### 2.4 Toque

| Contexto | Mínimo |
|----------|--------|
| Operação (painel, cardápio, KDS) | **48 px** |
| Administrativo (cadastro, relatório) | 44 px — `RNF-010` |

---

## 3. Componentes

Nove novos. O resto é token.

### `<Grelha etapa={n} de={6} />`

A régua. Barras anteriores em laranja, a atual em âmbar, as seguintes apagadas.

```css
.grelha       { display: inline-flex; gap: 3px; }
.grelha i     { width: 5px; height: 20px; background: var(--text); opacity: .28; }
.grelha i.feita { opacity: 1; background: var(--laranja-brasa); }
.grelha i.ativa { opacity: 1; background: var(--ambar-chama); }
```

### `<Comanda />`

O cartão de pedido, desenhado como comanda impressa: furo de tracionador no topo, filete
tracejado separando os itens, número dominante, mono no dado.

#### A observação do cliente — `RN-VEN-052`, 18/08/2026

Bloco próprio, entre os itens e o total, com **rótulo** e **prosa na cor do texto**:

```
┌──────────────────────────────┐
│ 1× X-Burger            28,00 │
├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤
│▌OBSERVAÇÃO DO CLIENTE        │  ← rótulo: mono, caixa alta, --muted
│▌Sem bacon e capricha na maio │  ← prosa: sans, --text, inteira
├──────────────────────────────┤
│ TOTAL              R$  28,00 │
└──────────────────────────────┘
```

**Três decisões, e as três já eram regra em outro lugar:**

| Decisão | Regra |
|---------|-------|
| Prosa em `--text`, rótulo em `--muted` | `CHAPA-10` — a tela do cliente já fazia assim; a da cozinha, não |
| Nunca truncada, nunca com reticências | `RN-VEN-052` — o limite de 80 caracteres existe para que caiba inteira |
| Filete âmbar, e ele **não** conta como a barra ativa | `CHAPA-02` — a barra do grupo é a `<Grelha>` no cabeçalho; este é filete de citação, sem estado |

⚠ **`overflow-wrap: anywhere`.** Oitenta caracteres sem espaço são um texto válido, e sem isto
eles estouram a largura do cartão em vez de quebrar.

A observação **por item** segue o mesmo tratamento em escala menor: rótulo `obs` em `--muted`,
texto em `--text`, recuado sob o produto a que pertence.

#### A comanda FECHADA — `RN-VEN-057`, 19/08/2026

O concluído e o cancelado chegam numa linha só. Um clique devolve a comanda inteira.

```
┌─────────────────────────────────────────────────┐
│ ▌▌▌▌▌▌  #59  DELIVERY  [CANCELADO]   R$ 102,00  │  ← 48 px, `<button aria-expanded>`
└ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘  ← tracejada quando cancelado
```

| Decisão | Por quê |
|---------|---------|
| **`<button>`, não `div` com `onClick`** | Teclado e leitor de tela pelo mesmo caminho do mouse. Numa tela de operação, `Tab` que não alcança o pedido é o operador de mão ocupada perdendo o acesso |
| **Total sim, cliente não** | Fechada, ela responde *"qual pedido é este e em que pé está"*. Nome e telefone são o detalhe que o clique abre — `RN-GER-039`, dado pessoal não fica à vista sem necessidade |
| **Canal em rótulo curto** | "Delivery próprio" não cabe, e cortado vira `D.` — que não é rótulo, é ruído ocupando o lugar de um. `Delivery`, porque o outro delivery (`Marketplace`) tem nome próprio |
| **Coluna mais larga nos grupos fechados** | Cinco elementos somam ~300 px numa coluna de 255: o total transbordava e a **página** rolava na horizontal. Cartão fechado é baixo — largura é o que ele tem de sobra |
| **A grade não muda ao abrir** | A largura é por **grupo**, não por estado do cartão. Alternar a grade a cada clique reorganizaria a tela inteira, que é o oposto do que o colapso dá |
| **Cancelado: traço tracejado** | Não fundo vermelho. Fundo colorido num cartão que alguém só está conferindo **comemora o erro** (§5) |

⚠⚠ **Colapsar não é esconder, e a distinção é a regra.** O pedido continua na lista, contado no
grupo dele, e o clique devolve tudo — inclusive a observação do cliente (`RN-VEN-052`), que nunca é
truncada. Filtro que remove sem dizer quantos removeu é o defeito que `RN-VEN-050` já enuncia do
lado do gráfico.

#### O cancelamento na comanda — `RN-VEN-058`, 19/08/2026

```
┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐
│▌MOTIVO DO CANCELAMENTO      │  ← rótulo: mono, --muted
│▌Cliente desistiu na porta   │  ← prosa: sans, --text (CHAPA-10)
│▌19/08/2026, 16:48  Dona Da… │  ← assinatura do fato: metadado
└ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘
```

Filete na cor do **erro**, do mesmo jeito que a observação do cliente tem o âmbar: é **citação**,
não selo. ⚠ Fundo vermelho aqui **comemoraria** o cancelamento numa tela que alguém está só
conferindo — §5.

⚠ **Instante com HORA, não só data.** "Cancelado em 19/08" não distingue o que aconteceu na abertura
do que aconteceu no fecho — e cancelamento é o registro que alguém relê justamente para reconstruir
a sequência de um turno.

⚠ **Cada linha aparece só se existir.** Pedido cancelado antes da regra não tem responsável, e a
alternativa a omitir a linha seria escrever "sistema" — que é afirmar algo falso sobre quem
cancelou.

#### O filtro de grupo — `RN-VEN-057`

Quatro chips: `Todos · Na casa · Concluídos · Cancelados`. `CHAPA-02` — exatamente um é âmbar.

⚠ **As contagens são do PERÍODO, nunca do filtro ativo.** Quem olha o chip quer saber o que
encontraria clicando nele; um contador que respeitasse o filtro mostraria o total do grupo
selecionado e zero nos outros.

⚠ **`position: sticky`.** Com três dias na tela, o filtro sai do campo de visão justamente quando é
mais útil — e quem rolou até o fim não sabe mais qual grupo está vendo.

⚠ **O vazio do filtro nomeia o filtro.** Um "nenhum pedido" seco contradiz o chip que acabou de
anunciar o total do período, e é assim que alguém conclui que o sistema perdeu os pedidos. A saída
é um botão: *"Ver todos os 62"*.

### Campo com limite de caracteres

Onde há teto — observação do pedido (80, `RN-VEN-052`), descrição do produto (200,
`RN-VEN-053`) — o campo mostra **quantos couberam**, em mono com `tabular-nums`, alinhado à
direita sob o campo:

```
 62/80        ← normal, --muted
 213/200      ← excedido, --erro, e o botão de salvar trava
```

**O número vive nos dois lados de propósito**, como o mínimo do `motivo` no `<Dialogo />`: a tela
evita a viagem, o servidor decide. O do frontend está em `services/limites.ts`, o do backend em
constante de domínio, e o do catálogo é o que manda nos dois.

⚠ **O estado excedido não é hipotético** — ele existe para o cadastro feito quando o limite era
outro. `maxLength` impede digitar, não encurta o que já estava gravado, e truncar o texto de
alguém ao abrir o formulário é perda silenciosa de dado.

### `<Dialogo />`

Substitui `confirm()` e `prompt()` do navegador — que **não entram** (§5).

> Os nativos não são estilizáveis: aparecem roxos, com cantos arredondados e o nome do host —
> *"localhost:5173 diz"* —, exatamente o repertório que o ADR-0012 recusa. E o `prompt()` é pior que
> feio: **não valida nada**, então um motivo de um caractere chegava ao servidor para tomar 422
> depois de o operador já ter fechado a caixa.

Construído sobre o **`<dialog>` nativo**, e não sobre uma `div` posicionada. De graça vêm quatro
coisas, e três são de acessibilidade — que o ADR-0012 assumiu como custo nosso:

| Vem do elemento | Modal artesanal costuma errar |
|-----------------|-------------------------------|
| `::backdrop` | fundo com `z-index` disputando com a barra fixa |
| Armadilha de foco | `Tab` escapa para a página atrás |
| `Esc` fecha | só o clique no X fecha |
| `aria-modal` | leitor de tela continua lendo a página de trás |

**Duas armadilhas do elemento**, e as duas estão no componente: abrir com o atributo `open` em vez
de `showModal()` mostra o diálogo **sem** backdrop e **sem** armadilha de foco; e `Esc` dispara
`cancel`, que sem `preventDefault` + callback fecha o diálogo deixando o estado de quem o abriu
dizendo que está aberto — e ele nunca mais reabre.

O campo de motivo espelha o **mínimo do validator do servidor** e diz quantos caracteres faltam. O
número vive nos dois lados de propósito: a tela evita a viagem, o servidor decide.

`::backdrop` é **carvão sólido a 72%** — escurece sem desfocar. `backdrop-filter` é o
glassmorphism que a §5 recusa.

#### Formulário dentro do diálogo — desde a `SPEC-VEN-003`

O componente aceita **`children`**: os campos próprios de quem o abriu, renderizados entre a
descrição e as ações. É o que atende ao cadastro de categoria e de produto — nome, ordem, preço,
categoria — sem uma segunda família de modais.

> **Por que estender em vez de criar `<FormularioModal />`.** Um segundo componente com
> `<dialog>` duplicaria as duas armadilhas do quadro acima, e a segunda cópia é a que erra: quem
> escreve o modal número dois copia a estrutura e esquece o `preventDefault` no `cancel`.

Duas regras para quem usa `children`:

1. **Quem passa campos valida os campos**, com a prop `impedido`. O diálogo sabe conferir texto com
   mínimo de caracteres (o `motivo`); não tem como saber se um preço é positivo
2. **Os campos vêm antes do `motivo`.** Quando as duas coisas existem, o motivo é a última
   pergunta — ele justifica o que foi preenchido acima

A área dos campos rola em `max-height: 60vh`. Sem isso, quatro campos mais texto de apoio empurram
o botão de confirmar para fora da tela do celular deitado — o botão que a pessoa veio apertar.

### `<Selo />`

O status. Borda de 2 px, fundo cheio, mono em caixa alta com `letter-spacing`.

| Fase | Fundo |
|------|-------|
| Aberto | `--ambar-chama` sobre carvão |
| Confirmado, Em produção, Pronto | `--laranja-brasa` sobre branco |
| Entregue, Finalizado | `--ok` sobre branco |
| Cancelado | `--erro` sobre branco |

### `<CampoDeData />`

Data digitada em `DD/MM/AAAA`, mono com `tabular-nums`. Entra e sai em **ISO** (`AAAA-MM-DD`),
que é o que a API recebe; a tradução vive no componente.

**Substitui `<input type="date">`** pelo mesmo motivo que `<Dialogo />` substituiu `confirm()`: o
popover nativo **não aceita estilo**. Ele chega com azul de sistema, cantos arredondados,
tipografia do navegador e um ícone que não é nosso — dentro de uma interface que recusa gradiente,
raio de desfoque e cor pastel. Nenhuma regra de CSS alcança aquele calendário:
`::-webkit-calendar-picker-indicator` troca o ícone e para por aí.

> **E não trocamos por um calendário artesanal.** Calendário próprio é um mês de teclado, foco e
> leitor de tela para resolver o que, nas telas que temos, é escolher um intervalo de dia
> operacional. Os intervalos que a operação pede de verdade vivem nos **atalhos** ao lado do campo
> — `Hoje`, `Ontem`, `7 dias`, `30 dias` —, e é o que torna a digitação o caminho de exceção. Se
> aparecer tela que precise navegar meses, o calendário entra **dentro deste componente** e
> nenhuma chamada muda.

| Detalhe | Como |
|---------|------|
| Erro | Borda em `--erro`, sem sombra colorida nem fundo pastel (`CHAPA-05`) |
| Data que não existe | `31/02` é recusado por **ida e volta** no `Date`, não por faixa de dia |
| Incompleto | Não é erro enquanto se digita; vira erro ao sair do campo |
| Atalhos | `.chips` com uma barra âmbar no ativo (`CHAPA-02`) |

### `<CampoDeHora />`

Hora digitada em `HH:MM`, mono com `tabular-nums`. Mesma decisão de `<CampoDeData />` e o mesmo
motivo: `<input type="time">` traz o ícone de relógio do navegador, o seletor de AM/PM de alguns
locales e a tipografia do sistema. **Numa lista de sete dias o efeito é pior que num campo isolado**
— as sete linhas parecem formulário de outro produto.

⚠ `24:00` é recusado. Casa que fecha "às 24h" fecha à meia-noite, que é `00:00`; aceitar `24:00`
produziria uma virada de dia operacional de 24 horas de deslocamento (`RN-GER-006`).

### `<MenuDePerfis />`

O perfil do usuário e **o que ele concede** — `RN-GER-010`, `RN-GER-024`. Cada perfil é um cartão
com a lista de permissões em português, mais o código de cada uma — é o código que aparece na
mensagem de erro do servidor, e ligar um ao outro é o que permite ao suporte responder sem
adivinhar.

As permissões **vêm do servidor**. Cópia no TypeScript divergiria do `CatalogoDeAcesso` na primeira
permissão nova, e a tela passaria a prometer o que o servidor recusa.

> #### ⚠ Onde ele entra, e onde NÃO entra — revisado em 18/08/2026
>
> O componente nasceu substituindo o `<select>` **nas duas telas de criação de usuário**, com o
> argumento de que "Gerente" num `<option>` não diz o que está sendo entregue e quem atribui assina
> em branco. O argumento continua de pé — mas **não vale igual nos dois lados do produto**, e essa
> parte faltava:
>
> | Tela | Controle | Por quê |
> |------|----------|---------|
> | Plataforma criando usuário **de uma casa** (`UsuariosDaPlataforma`) | **`<MenuDePerfis>`** | Quem escolhe está concedendo acesso numa operação que **não é a dele**, para gente que ele não conhece, sob sessão de suporte. É aqui que assinar em branco custa caro |
> | Restaurante criando usuário da própria casa (`Usuarios`) | **`<select>` simples** | Quem escolhe é o administrador da casa, que convive com os perfis todo dia. Seis cartões e até quinze permissões abertas empurravam o resto do formulário para fora da tela para dizer o que ele já sabe |
> | Equipe da plataforma (`EquipeDaPlataforma`) | nenhum | Usuário de plataforma não tem unidade nem perfil (`RN-GER-030`) |
>
> **Esconder o detalhe não afrouxa nada, e é o que torna a troca aceitável:** `RN-GER-024` recusa no
> servidor com a regra citada, e a lista que chega ao restaurante já vem filtrada por `delegavel`,
> calculado pela `MatrizDeDelegacao` do **domínio**. O que muda é quanto a tela **explica**, não o
> que ela **permite** — e explicação em excesso, na tela de quem já sabe, custa a tela inteira.
>
> Decisão do dono do produto. Se um dia o restaurante ganhar perfil personalizado — em que o nome
> deixa de dizer o conteúdo —, esta linha volta a ser discutida.

### `<CapaDaCasa />`

A identidade da casa no topo da tela: **logo, nome, e o que a tela é**. `RN-GER-026` — a logo do
restaurante manda; a listra do GrelhaHub só aparece quando não há logo, porque numa vitrine de
restaurante quem assina é o restaurante.

Nasceu na vitrine pública e no acompanhamento do pedido. Em 19/08/2026 ganhou o **terceiro
chamador** — a fila de monitoramento — e por isso **mudou de casa**: saiu de `features/publico/`
para `components/`. Componente usado por duas áreas do produto que mora dentro de uma delas é o
começo de uma cópia.

| Propriedade | Quando entra |
|-------------|--------------|
| `urlLogo`, `nomeFantasia` | sempre |
| `endereco` | **só no cardápio público.** Endereço é para quem vai até lá; na fila e no acompanhamento é linha morta ocupando o topo |
| `aberta` + `horarios` | só onde há venda a recusar (`RN-VEN-048`) |
| `chamada` | a assinatura da tela. `undefined` cai em "Do pedido à brasa" |

### O painel de senha — `RN-VEN-054` ⚠⚠, `RN-VEN-056`

Duas colunas lado a lado, separadas por **traço** e não por vão, e dentro delas **só número**.

```
┌ PREPARANDO ───────── 40 ┬ PRONTO ──────────── 20 ┐   ← cabeçalho em carvão
│  3   1   2   4  10  11  │   5    6   12   15     │   ← papel  │  carvão invertido
│ 2026-08-18              │                        │
│ 13  14  16  17  19  20  │  18   21   24   27     │   ← âmbar, e um degrau maior
└─────────────────────────┴────────────────────────┘
```

| Decisão | Por quê |
|---------|---------|
| **Só o número** | A comanda completa dava ~6 telas de rolagem com 60 pedidos, com a coluna **pronto** embaixo de todas elas. Medido: passou a **1 tela** |
| **Fluxo VERTICAL** ⚠⚠ | Os números descem a coluna e só então abrem a próxima — `columns` + **`column-fill: auto`**. Numa fila, encher da esquerda para a direita obriga o olho a varrer na horizontal para achar um número |
| **Divisão vertical, por traço** | Empilhar desperdiça a largura e joga "pronto" para baixo da dobra. O que separa na Chapa é o traço de 2px; vão leria como duas placas soltas |
| **Pronto invertido, em âmbar** | ⚠ A referência que originou a tela usava **verde**, e o design system recusa: verde é `--ok`, que já significa FINALIZADO no selo. Duas cores para dois sentidos apaga os dois (§5). Âmbar é a cor do que exige ação agora |
| **Pronto um degrau maior** | Quem está de pé procura **um** número, e é o dele saindo. "Preparando" é contexto, e contexto não precisa ser lido do outro lado do salão |
| **A coluna vazia não some** | Ela vira um `—`. Coluna que desaparece faz a outra saltar para o meio da tela, e a parede se reorganiza toda vez que o último pedido sai |
| **`--peso` no número** | Mesmo tratamento de `.numero-pedido` na comanda — o único lugar do produto que usa Arial Black. Aqui ele só cresce, e perde o `#` |

#### ⚠⚠ O tamanho acompanha a lotação, e o componente é quem conta

`--mural-base` é o degrau da **tela** (`clamp`, um patamar acima em tela cheia). `--mural-numero` é
o que cada **coluna** resolve a partir dele, por `data-lotacao`:

| Lotação | Pedidos | Multiplicador |
|---------|--------:|---------------|
| `baixa` | até 12 | 1,0 |
| `media` | até 30 | 0,7 |
| `alta` | acima | 0,5 |

**Sem isto o painel esconde número exatamente quando mais gente está esperando.** No degrau grande
cabem ~20 por coluna em tela cheia; no pico de 60, os de baixo saíam da área visível — e a coluna
rola, o que numa parede significa *número que ninguém vai ler*, porque não há mão para rolar a
parede. O CSS não conta filhos para dimensionar, então quem conta é o componente.

⚠ **Três degraus nomeados, e não uma fórmula contínua.** Degrau é escolha; tamanho calculado por
item é o valor fracionário que `CHAPA-13` recusa.

> ### ⚠⚠ `column-fill: auto` não é detalhe — sem ele o fluxo vertical não acontece
>
> O padrão do multi-coluna é `balance`: com seis números e uma coluna alta, o navegador os
> distribui **um por coluna** para igualar as alturas — exatamente a leitura horizontal que se
> estava tirando. A medida denunciou (`segundoAbaixoDoPrimeiro: false`) antes de qualquer olho.
>
> ⚠ E `columns`, não `grid-auto-flow: column`: a grade exigiria dizer **quantas linhas** cabem, e
> isso muda com a altura da tela e com o degrau de lotação. O multi-coluna descobre sozinho.

#### A exceção declarada a `CHAPA-13`

`--mural-base` é `clamp(…, vw, …)` — **relativo à viewport**, o que a regra normalmente proíbe. A
exceção é da superfície: um painel de parede existe para preencher a tela em que está pendurado, e
o mesmo número tem de servir a um monitor de 24" e a um tablet de 10". O `clamp` fixa piso e teto;
só o meio varia, e continua sendo **um token nomeado**, decidido uma vez.

#### `RN-VEN-056` — o modo mural

Tela cheia por botão, sem barra e sem abas. ⚠ **A tela do dispositivo não apaga enquanto ele está
ligado** (`navigator.wakeLock`): sem isso o modo funciona por dez minutos e o monitor apaga sozinho,
que é o mesmo que não existir — e ninguém reporta, porque parece problema do computador.

⚠ O bloqueio é **oportunista, nunca condição**: navegador sem a API entra em tela cheia do mesmo
jeito. E a trava **cai sozinha** quando a aba é escondida — é o contrato da API —, por isso ela é
readquirida no `visibilitychange`. Sem essa reaquisição, minimizar a janela uma vez desliga o
bloqueio para sempre, com a tela cheia ainda no ar dizendo que está tudo certo.

⚠ **O botão some quando `document.fullscreenEnabled` é falso.** Botão que não faz nada é pior que
botão ausente: quem clica conclui que o sistema está quebrado.

---

## 4. As treze regras — checklist de revisão de código

| ID | Regra |
|----|-------|
| **CHAPA-01** | Sombra é sólida e deslocada. Nunca borrada |
| **CHAPA-02** | Uma barra âmbar por grupo — nunca duas |
| **CHAPA-03** | Dado em mono, prosa em sans |
| **CHAPA-04** | Alvo de toque mínimo de 48 px na operação |
| **CHAPA-05** | Sem cadeado em campo que a API não devolve |
| **CHAPA-06** | Emoji não é ícone, e muito menos estado |
| **CHAPA-07** | Comemoração tem hora marcada |
| **CHAPA-08** | Movimento tem massa — §9 |
| **CHAPA-09** | A vitrine não promete o que a API não faz — §8 |
| **CHAPA-10** | Prosa não usa cor secundária, e texto nunca usa `opacity` — §2.1 |
| **CHAPA-11** | Quem declara `background` declara `color` — §2.1 |
| **CHAPA-12** | O rótulo da aba nomeia o que a aba mostra — §4 |
| **CHAPA-13** | Tamanho de fonte e espaçamento só por token, e nunca em `em` — §2.3 |

### CHAPA-12 merece explicação

Nasceu de um defeito de 19/08/2026, na área de estabelecimentos da plataforma: a aba
**"Horário e endereço"** carregava a **lista de pedidos**, a aba **"Painel"** carregava o painel
de operação, e o horário e o endereço estavam escondidos no fim de **"Dados e situação"**. Três
rótulos, quatro telas, nenhuma correspondência.

**Não foi descuido de nomenclatura — foi o roteamento espalhado.** O conteúdo saía de três blocos
aninhados, um deles condicionado por `(aba === 'dados' || aba === 'modulos')`, e nesse arranjo não
há como ler a lista de abas ao lado do que cada uma renderiza. O comentário que já estava no
arquivo registrava um incidente anterior *no mesmo lugar*: **"foi assim que o painel entrou num
bloco que não era dele"**.

A regra, então, é de forma e não só de texto:

| Exigência | Por quê |
|-----------|---------|
| O rótulo diz o que a tela mostra | Aba é promessa; quem clica em "Horário e endereço" foi buscar horário |
| A chave do `type` usa a mesma palavra do rótulo | Chave que diverge do rótulo esconde a troca de quem lê só o `switch` |
| **Uma linha por aba, na mesma ordem da lista** | É o que torna a divergência visível em revisão — condição enumerada, nunca por negação |
| Vocabulário igual ao do painel do restaurante | Suporte e cliente conversam por telefone olhando telas diferentes: "vá em Operação" precisa levar os dois ao mesmo lugar |

> ⚠ **Tela trocada não dá erro.** Ela carrega, preenche e parece funcionar — o defeito só aparece
> para quem sabe o que deveria estar ali. É a mesma família do beco sem mensagem de erro que a
> `SPEC-VEN-003` §11.3 descreve, e por isso entra no checklist em vez de contar com o olho.

### CHAPA-05 merece explicação

`RN-GER-012` diz que custo, CMV e margem **não são retornados pela API** para perfil sem
permissão. Então na tela **não existe** blur, cadeado nem "desbloqueie". O campo simplesmente
não está lá.

Fingir que o dado existe é mentir sobre o modelo — e um cadeado desenhado sugere que o dado
veio e foi escondido no cliente, que é exatamente o defeito que a regra existe para evitar.

### CHAPA-07 merece explicação

Confete existe em **um** lugar: o fechamento de caixa batendo certo. Uma vez por dia, quando
o número fecha. Em cada pedido salvo, vira brinquedo — e o produto perde a metade "séria".

### CHAPA-08 merece explicação — e ganhou vocabulário em §9

"Movimento tem massa" era slogan sem medida até 17/08/2026: nada dizia quanto é curto, quanto é
longe, nem o que **nunca** se move. A §9 fixa os três números e a lista do que não anima.

O resumo: o objeto **entra no lugar dele**, subindo pouco e parando seco. Nada desliza pela tela,
nada escala, nada gira, nada pisca. Se o movimento chama atenção para si, ele está errado.

---

## 5. O que **não** entra

Nomeado para poder ser recusado em revisão com referência, e não com "não gostei".

| Anti-padrão | Por quê |
|-------------|---------|
| Gradiente violeta→azul, ou qualquer gradiente decorativo | Não tem relação com brasa. É a assinatura do visual genérico |
| `backdrop-filter: blur` / glassmorphism | Sem materialidade; some sobre fundo claro |
| `border-radius` uniforme em tudo | Elimina hierarquia de forma |
| Sombra cinza borrada | O cartão flutua em vez de pesar |
| `confirm()`, `prompt()`, `alert()` | Não são estilizáveis, mostram o host ao usuário e não validam nada — use `<Dialogo>` |
| Emoji como ícone ou estado | Renderiza diferente por sistema, sem contraste garantido |
| Inter, Geist, Space Grotesk | Fonte "segura" que entrega exatamente a cara que estamos evitando |
| Cor semântica igual ao acento | Laranja é ação. Verde é sucesso. Misturar apaga os dois |
| Chip pastel de status | Sem peso, ilegível de longe |

---

## 6. Como aplicar em tela nova

1. Comece pelos tokens — nenhuma cor literal no componente
2. Escolha o papel da fonte antes de escrever a marcação: é dado ou é frase?
3. Se a tela tem progresso, seleção ou fase, use `<Grelha>` — não invente outro indicador
4. Passe o checklist da §4 antes de abrir o PR
5. Se precisar de algo que o sistema não tem, **acrescente aqui primeiro** — mesma regra do
   catálogo de regras: nada nasce direto no código

---

## 7. Acessibilidade

Componentes próprios significam acessibilidade por nossa conta — é o custo assumido no
[ADR-0012](adr/0012-direcao-visual-chapa.md).

- Foco visível sempre: `outline: 3px solid var(--laranja-brasa); outline-offset: 3px`
- A listra é **decorativa**: `aria-hidden="true"`. O estado vai em texto no `<Selo>`, porque
  cor sozinha não comunica
- Contraste mínimo 4.5:1 em texto. Âmbar sobre carvão passa; âmbar sobre papel **não** —
  por isso âmbar é fundo, nunca texto sobre claro
- `prefers-reduced-motion` desliga toda transição — **e devolve o estado final**, nunca o inicial
  (§9.4: a armadilha que deixaria a página em branco)
- Alvo de toque conforme §2.4

---

## 8. Superfície de marca — a vitrine

A landing pública (`/`, servida em `grelhahub.com` e `www.grelhahub.com`) é a **primeira**
superfície do produto que não é operação nem administração. Ela precisa vender, e a tentação
é grande de importar o repertório que a §5 recusa: gradiente, vidro, ícone colorido, sombra
macia. **Nada disso entra.** A vitrine vende com o que a Chapa já tem — traço grosso, sombra
sólida, mono no dado, e a listra como ritmo.

### 8.1 A regra nova

| ID | Regra |
|----|-------|
| **CHAPA-09** | A vitrine não promete o que a API não faz |

Irmã da `CHAPA-05`. Lá, o campo que o servidor não devolve **não existe na tela**; aqui, o
recurso que o sistema não tem **não é anunciado como disponível**.

> ### Revisão de 17/08/2026 — como a regra é cumprida mudou; o que ela exige, não
>
> A primeira versão da vitrine cumpria a `CHAPA-09` **marcando** o futuro: dois blocos de
> recursos, "no ar agora" e "o que vem depois", com selo `planejado` vazado no segundo.
>
> O dono do produto removeu o segundo bloco: **cliente final não precisa do roteiro de
> engenharia**, e uma vitrine que lista oito coisas que ainda não existem convida à pergunta
> errada. A regra continua valendo, cumprida por **omissão em vez de rótulo**: a seção de
> recursos passou a listar **apenas o que está no ar**, e nada na página afirma o que não existe.
>
> **O que sobrou de tensão, e está declarado:** a seção de **planos** vende recortes de módulo
> que incluem PRO, EST, FIN e LOG — módulos com regra escrita e **sem código**. O aviso por plano
> ("inclui módulo que ainda está no roteiro") foi removido a pedido. Isso é decisão comercial
> legítima — `RN-GER-017` faz da habilitação um ato de contrato, e vender o módulo antes de
> entregá-lo é combinação entre as partes, não afirmação da página. **Mas deixa de haver aviso na
> interface**, e quem prometer prazo passa a responder por ele fora do produto.

O selo `planejado` continua existindo, e agora o seu uso é **interno**: a administração de
plataforma o usa para "sem código ainda" e para a dependência que falta (`RN-GER-020`). Por isso
ele saiu de `vitrine.css` e passou para a folha global — `/plataforma` não carrega a folha da
vitrine, e o selo estava sem estilo lá.

> Não é escrúpulo decorativo. O produto vai a banca com 151 regras catalogadas e um roteiro de
> ondas declarado; uma vitrine que anuncia estoque FEFO antes da onda 3 desmente o próprio
> documento que sustenta o projeto. **A honestidade do roteiro é argumento de venda, não
> desvantagem** — quem compra ERP já foi enganado por demonstração antes.

### 8.2 Padrões

Nomeados para poderem ser reusados na página de planos e no blog, quando existirem.

| Padrão | Classe | O que é |
|--------|--------|---------|
| Barra da vitrine | `.barra-site` | Fixa no topo. Marca à esquerda, âncoras ao centro, **`Acesse já` à direita** |
| Capa | `.capa-site` | Carvão. Título em peso, subtítulo em sans, dois botões e uma prova visual |
| Prova visual | `.capa-mostra` | A `<Comanda>` real, não desenho de comanda. O que a vitrine mostra é o produto |
| Cartão de dor | `.cartao-dor` | Grade 2×2. Título em mono, parágrafo em sans |
| Contato | `BotaoDeContato` | **Não renderiza** quando não há canal em `conteudo.ts`. Botão que abre um `mailto:` vazio faz o visitante concluir que o site está quebrado |
| Cartão numerado | `.cartao-num` | O número em mono grande e fraco, no canto. Não é ícone |
| Bloco de recurso | `.bloco-recurso` | Título e frase. **Sem selo** desde a revisão acima |
| Plano | `.plano` | Preço, lista de módulos, botão. **Exatamente um é âmbar** — `CHAPA-02` |
| Pergunta | `.pergunta` | `<details>` nativo. Acordeão próprio seria reimplementar acessibilidade de graça |
| Faixa de chamada | `.faixa-cta` | Carvão, centrada, um botão só |
| Rodapé | `.rodape-site` | Três colunas, filete acima |

### 8.3 Selo de disponibilidade

Reusa `.selo` com duas fases novas, porque disponibilidade **é** estado e `CHAPA-06` já
resolveu como estado se mostra:

| Fase | Aparência | Significa |
|------|-----------|-----------|
| `no-ar` | fundo `--ok`, texto branco | Está em produção agora |
| `planejado` | fundo transparente, traço e texto em `--muted` | Está no roteiro, com a onda declarada |

`planejado` é **vazado** de propósito: cheio, competiria de igual para igual com o que existe,
e é exatamente a confusão que a `CHAPA-09` evita.

### 8.4 Onde a listra entra

Duas vezes, e as duas com significado — `CHAPA-02` vale aqui como no painel:

- **Barra da vitrine:** a seção visível é a âmbar. Uma por vez
- **Como funciona:** três etapas, `<Grelha etapa={n} de={3} />` — a mesma régua que o cliente
  vê acompanhando o pedido dele. Reusar o componente é o argumento: a vitrine e o produto são
  a mesma coisa

### 8.5 O que a vitrine **não** faz

| Não faz | Por quê |
|---------|---------|
| Depoimento de cliente | Não há cliente. Depoimento inventado é prova social fabricada |
| Alternador de menu no celular | Um botão para revelar quatro âncoras é um clique e um estado a mais. As âncoras rolam na horizontal, como as abas do painel |
| Botão de cadastro de restaurante | Ninguém ganha conta sem contrato: a criação é da plataforma (`SPEC-GER-007` §4) |
| Número de adoção ("+300 lojas") | Mesma coisa, com aparência de dado |
| Preço fixado no código | Preço é decisão comercial do time, não do componente. A seção existe estruturada; o valor entra em [`conteudo.ts`](../frontend/src/features/site/conteudo.ts) |
| Promessa de suporte ("resposta em 1h") | Compromisso operacional que ninguém assumiu |
| Contador animado, carrossel automático | `CHAPA-08` — movimento tem massa, e carrossel tira do usuário o controle da leitura |

> **A seção "Como este produto foi construído" também saiu**, na mesma revisão. Ela mostrava as
> contagens de regras, de testes e do banco real — verdade verificável, e **argumento de engenheiro
> para engenheiro**. Quem escolhe ERP de restaurante decide por operação, não por contagem de
> teste; e a seção tinha o efeito colateral de exigir sincronia com números que mudam a cada
> entrega. O argumento continua vivo onde ele importa: no `README` e na defesa de banca.

---

## 9. Movimento — `CHAPA-08`

> **Movimento tem massa.** O objeto entra **no lugar dele**, subindo pouco e parando seco.

Até 17/08/2026 `CHAPA-08` era slogan sem medida, e slogan sem medida não recusa nada em revisão.
Esta seção fixa os três números, o que **nunca** anima, e a armadilha que deixaria a página em
branco.

### 9.1 Os tokens

```css
--mov-subida:  12px;                          /* a distância. Uma, para tudo */
--mov-tempo:   320ms;                         /* a duração */
--mov-curva:   cubic-bezier(0.16, 0.84, 0.44, 1);   /* sai rápido, encosta devagar */
--mov-passo:   70ms;                          /* atraso entre irmãos de um grupo */
--mov-teto:    280ms;                         /* atraso máximo acumulado */
```

**Por que 12 px e não 40.** A Chapa é feita de traço de 2 px e sombra sólida deslocada de 5 px.
Um objeto que percorre 40 px passa mais tempo desalinhado do que alinhado, e a sombra sólida — que
não desfoca — denuncia cada quadro. Doze pixels é pouco mais que o deslocamento da própria sombra:
o cartão parece **assentar**, não viajar.

**Por que a curva encosta devagar.** `ease-in-out` sai devagar e chega devagar: parece elástico.
A curva daqui arranca e freia no fim — é o que dá a impressão de peso, que é literalmente o nome
da regra.

**Por que existe teto no atraso.** Numa grade de seis cartões, `70ms × 6` = 420 ms só de espera: o
último cartão chega depois de o olho já ter desistido dele. Com teto de 280 ms o grupo lê como
**um** gesto escalonado, e não como uma onda.

### 9.2 O que anima

| Anima | Como |
|-------|------|
| Bloco de conteúdo entrando em vista na **vitrine** | sobe `--mov-subida` e revela |
| A **capa**, no carregamento | o mesmo, sem esperar rolagem — ela já está à vista |
| Botão sob o dedo | o deslocamento de 0,06 s que já existia (`transform` + sombra) |

### 9.3 O que **nunca** anima

| Não anima | Por quê |
|-----------|---------|
| **A barra âmbar do `<Grelha>`** | Ela é **estado** (`CHAPA-02`), não decoração. Barra que desliza lê como mudança de fase — o operador vê o pedido "andando" sozinho |
| **O `<Selo>` de status** | Mesma razão: é o estado do pedido |
| **Qualquer coisa no painel, no cardápio da gestão ou no KDS** | A operação é lida com pressa, de pé, a dois metros. Movimento ali é obstáculo, não charme |
| **Número, preço, total** | Contador que sobe é `CHAPA-08` sendo violado com aparência de dado — já estava recusado na §8.5 |
| **A barra fixa da vitrine** | Ela é referência: o que não se mexe é o que orienta |
| Escala, rotação, desfoque, pulso, brilho | Repertório de outro sistema. A Chapa move em **um** eixo e nada mais |

### 9.4 ⚠ A armadilha: `prefers-reduced-motion` desligando a revelação

A global já tem o interruptor:

```css
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after { transition: none !important; animation: none !important; }
}
```

Uma revelação escrita como *"nasce em `opacity: 0` e a transição leva a 1"* combinada com esse
interruptor produz **conteúdo invisível para sempre** — para exatamente quem pediu menos
movimento. O `!important` mata a transição; o estado inicial permanece.

**A regra, então:** todo estado inicial de revelação é **desfeito** no mesmo bloco de mídia.

```css
@media (prefers-reduced-motion: reduce) {
  [data-revela] { opacity: 1; transform: none; }
}
```

E a mesma proteção vale para **JavaScript ausente ou `IntersectionObserver` indisponível**: se o
observador não pode ser criado, o código revela tudo de imediato. Página que depende de observador
para ser legível é página que aposta na infraestrutura do navegador para entregar texto.

> **Isto não contradiz `CHAPA-10`** (*"texto nunca leva `opacity`"*). `CHAPA-10` proíbe `opacity`
> como **estado de repouso** — o contraste composto não aparece em ferramenta de verificação, e
> ninguém descobre que o subtítulo está a 10:1 em vez de 15:1. Aqui a opacidade é **transitória e
> termina em 1**: o estado de repouso é sempre opacidade cheia, inclusive no caminho de
> `prefers-reduced-motion`, que é o único em que ela poderia ficar parada.

### 9.5 O contrato de `data-revela`

Marcação e comportamento, para não haver duas implementações:

| Peça | Papel |
|------|-------|
| `data-revela` no elemento | declara "este bloco entra animado" |
| `data-revelado="true"` | posto **uma vez**, ao entrar em vista, e nunca retirado |
| `style="--mov-atraso: …"` | o escalonamento do irmão dentro de um grupo |

Três decisões dentro disso:

- **Revela uma vez.** Sem re-esconder na rolagem para cima. Bloco que pisca a cada passada
  transforma leitura em brinquedo, e quem volta ao topo está procurando algo — não assistindo
- **O atributo é escrito no DOM, não guardado em estado do React.** Trinta blocos em estado
  seriam trinta re-renderizações da vitrine inteira durante a rolagem. O conteúdo aqui é estático:
  não há nada para o React reconciliar, e `data-revela` no JSX nunca é reescrito
- **Um observador para a página, não um por bloco.** Trinta `IntersectionObserver` fazem o mesmo
  trabalho que um, trinta vezes
