# GrelhaHub — Design System **Chapa**

| Campo | Valor |
|-------|-------|
| Versão | 1.0 |
| Decisão | [ADR-0012](adr/0012-direcao-visual-chapa.md) |
| Situação | **Base oficial** — aplicada ao app em 17/08/2026 |
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
| `--laranja-brasa` | `#FF6B2C` | Ação primária, barras já percorridas |
| `--ambar-chama` | `#FFB020` | **A barra ativa.** Uma por grupo |
| `--creme` | `#FFE3D3` | Texto sobre carvão |
| `--carvao` | `#14100C` | Chão escuro, traço, sombra |
| `--cinza-quente` | `#9C8E7E` | Secundário |
| `--papel` | `#EBE2D6` | Chão claro — neutro quente, nunca cinza puro |
| `--papel-alto` | `#F8F3EC` | Superfície sobre o papel |

**Semântico — separado do acento de marca.** Laranja é ação, não sucesso.

| Token | Claro | Escuro |
|-------|-------|--------|
| `--ok` | `#2F6B3A` | `#6EC27F` |
| `--alerta` | `#9A5B00` | `#FFB020` |
| `--erro` | `#A3271F` | `#EF6A5E` |

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

### 2.4 Toque

| Contexto | Mínimo |
|----------|--------|
| Operação (painel, cardápio, KDS) | **48 px** |
| Administrativo (cadastro, relatório) | 44 px — `RNF-010` |

---

## 3. Componentes

Três novos. O resto é token.

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

### `<Selo />`

O status. Borda de 2 px, fundo cheio, mono em caixa alta com `letter-spacing`.

| Fase | Fundo |
|------|-------|
| Aberto | `--ambar-chama` sobre carvão |
| Confirmado, Em produção, Pronto | `--laranja-brasa` sobre branco |
| Entregue, Finalizado | `--ok` sobre branco |
| Cancelado | `--erro` sobre branco |

---

## 4. As oito regras — checklist de revisão de código

| ID | Regra |
|----|-------|
| **CHAPA-01** | Sombra é sólida e deslocada. Nunca borrada |
| **CHAPA-02** | Uma barra âmbar por grupo — nunca duas |
| **CHAPA-03** | Dado em mono, prosa em sans |
| **CHAPA-04** | Alvo de toque mínimo de 48 px na operação |
| **CHAPA-05** | Sem cadeado em campo que a API não devolve |
| **CHAPA-06** | Emoji não é ícone, e muito menos estado |
| **CHAPA-07** | Comemoração tem hora marcada |
| **CHAPA-08** | Movimento tem massa |

### CHAPA-05 merece explicação

`RN-GER-012` diz que custo, CMV e margem **não são retornados pela API** para perfil sem
permissão. Então na tela **não existe** blur, cadeado nem "desbloqueie". O campo simplesmente
não está lá.

Fingir que o dado existe é mentir sobre o modelo — e um cadeado desenhado sugere que o dado
veio e foi escondido no cliente, que é exatamente o defeito que a regra existe para evitar.

### CHAPA-07 merece explicação

Confete existe em **um** lugar: o fechamento de caixa batendo certo. Uma vez por dia, quando
o número fecha. Em cada pedido salvo, vira brinquedo — e o produto perde a metade "séria".

---

## 5. O que **não** entra

Nomeado para poder ser recusado em revisão com referência, e não com "não gostei".

| Anti-padrão | Por quê |
|-------------|---------|
| Gradiente violeta→azul, ou qualquer gradiente decorativo | Não tem relação com brasa. É a assinatura do visual genérico |
| `backdrop-filter: blur` / glassmorphism | Sem materialidade; some sobre fundo claro |
| `border-radius` uniforme em tudo | Elimina hierarquia de forma |
| Sombra cinza borrada | O cartão flutua em vez de pesar |
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
- `prefers-reduced-motion` desliga toda transição
- Alvo de toque conforme §2.4
