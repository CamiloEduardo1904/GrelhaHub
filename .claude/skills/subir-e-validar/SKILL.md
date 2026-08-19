---
name: subir-e-validar
description: >-
  Sobe a pilha real do GrelhaHub — PostgreSQL em contêiner, os três bancos migrados, a API em :5000
  e o Vite em :5173 —, semeia um cenário pelas ROTAS da API e dirige um Chrome de verdade para
  conferir a tela, medir cadência de polling e tirar captura. Use quando a mudança é de INTERFACE ou
  de COMPORTAMENTO DE CLIENTE: tela nova, aba nova, polling, estado vazio, celular, `CHAPA-*`.
  Acione em "roda o app", "sobe a pilha", "valida na tela", "abre no navegador", "screenshot",
  "confere a tela", "mede o polling", "prova que funciona".
---

# Subir e validar o GrelhaHub na tela

> ## Por que esta skill existe
>
> Em 19/08/2026 a fila de monitoramento (`RN-VEN-054`) passou por **tudo**: `dotnet build` com zero
> avisos, 356 testes verdes, `tsc` limpo, `eslint` limpo, `vite build` limpo. Aberta no navegador,
> **subiu vazia** — com a API devolvendo seis pedidos na mesma requisição.
>
> A causa valia para as **duas** correntes de polling do produto, e uma delas estava no ar desde o
> dia anterior afirmando no catálogo o contrário do que fazia (`RN-VEN-047`).
>
> **O frontend deste projeto não tem runner de teste.** Enquanto não tiver, esta skill é o único
> lugar onde comportamento de cliente é verificado. Ela não substitui o runner — ela mede o tamanho
> da dívida a cada uso.

---

## 1. Quando usar, e quando não

| Situação | Esta skill? |
|----------|-------------|
| Tela nova, aba nova, componente novo | **Sim** |
| Polling, intervalo, estado "ao vivo", suspensão por aba | **Sim, obrigatoriamente** — é o que nenhum gate vê |
| Estado vazio, erro na tela, caminho de volta de operação reversível | **Sim** |
| Celular, alvo de toque (`CHAPA-04`), rolagem horizontal | **Sim** |
| Regra que o banco impõe (constraint, coluna gerada, trigger) | Não — é `dotnet test`, com PostgreSQL real |
| Contrato de rota, autorização, recorte de unidade | Não — é o harness de integração |
| Mudança só de documentação | Não |

---

## 2. Subir

```bash
bash .claude/skills/subir-e-validar/scripts/subir.sh
```

O script faz, nesta ordem, e para na primeira falha:

1. Contêiner `postgres:16-alpine` em **:5433** com `grelhahub/grelhahub` — a porta e as credenciais
   que `appsettings.Development.json` já espera.
2. Cria `grelhahub_log` e `grelhahub_arquivo`. **ADR-0017 e ADR-0018 — três bancos.**
3. `dotnet run -- --migrate --semear`, que migra os **três** e cria o usuário de plataforma.
4. API em background na :5000, Vite na :5173, e espera as duas responderem.

> ### ⚠ Três armadilhas que já custaram tempo aqui
>
> - **A flag é `--migrate`, em inglês, não `--migrar`.** Escrita errada, ela é ignorada em silêncio
>   e o processo **sobe como servidor**. A API responde `/api/health`, parece tudo certo, e os bancos
>   de log e de arquivo continuam **vazios** — a trilha falha em toda operação e o primeiro upload dá
>   500.
> - **O startup de Development migra só o banco principal** (`Program.cs`, bloco
>   `IsDevelopment`). Os outros dois só vêm por `--migrate`.
> - **Sem os três bancos, o defeito não aparece onde a causa está:** a API sobe, a operação grava na
>   outbox, e o worker falha do outro lado.

---

## 3. Semear o cenário

```bash
bash .claude/skills/subir-e-validar/scripts/semear.sh
```

Cria um restaurante (`brasa-da-esquina`, admin `dona@brasa.local` / `senha-de-demo-1234`), um
segundo sem pedido nenhum (`casa-vazia`, `vazia@brasa.local`) e um cenário completo de pedidos:

- um em cada estado **vivo** — `Aberto`, `Confirmado`, `Em produção`, `Pronto`
- um `Entregue`, um `Finalizado` e um `Cancelado` — os que **saem** da fila
- 14 finalizados espalhados pelos últimos 28 dias, para o painel de operação ter série longa
- **um pedido vivo de dia operacional anterior**, que é o caso de `RN-GER-006` na fila

> ### ⚠⚠ Tudo entra pelas ROTAS da API. A única exceção é envelhecer.
>
> É a mesma doutrina do harness de integração: dado que entra por caminho que o sistema não tem
> escapa de validator, de autorização e do vínculo de unidade.
>
> A exceção é `dh_criacao`. `Pedido.CriadoEm` não tem setter — e está certo que não tenha —, então
> envelhecer um pedido é `UPDATE pedido SET dh_criacao = ...` por SQL, e o PostgreSQL **recalcula**
> `data_operacional` porque a coluna é `GENERATED ALWAYS AS ... STORED`. É o mesmo caminho que
> `PedidoPublicoTests` usa.

> ### ⚠ Dois detalhes que fazem o semeador parecer quebrado
>
> - **Nada de acento no corpo do `curl`.** O Git Bash do Windows manda o byte em cp1252 e a API
>   recusa o JSON. `Rogerio`, não `Rogério` — em dado de demonstração isso não custa nada.
> - **`RN-CRM-001` — o cliente é casado pelo TELEFONE.** Repetir o número faz todos os pedidos
>   apontarem para a mesma linha de cliente, e **o nome dela vira o do último pedido**. Seis comandas
>   com nomes diferentes viram seis "Historico 14". Por isso o semeador dá um telefone por pedido.

---

## 4. Dirigir o navegador

Não há `chromium-cli` nem Playwright neste repositório, e **não é preciso baixar navegador**:
`playwright-core` dirige o Chrome que já está instalado no Windows.

```bash
cd .claude/skills/subir-e-validar/driver
npm install                 # só playwright-core, ~1 pacote, sem baixar navegador
node exemplos/monitor.mjs    # a validação da fila de monitoramento, ponta a ponta
```

`navegador.mjs` dá o mínimo: `abrir()`, `entrar()`, `aba()`, `foto()`, `contador()` para cadência,
as sondas de `CHAPA-02` e `CHAPA-04`, e a coleta de erro de console, de `pageerror` e de requisição
que falhou. Os exemplos são validações **reais**, e cada um sai com **código 1** quando algo quebra:

| Exemplo | O que prova |
|---------|-------------|
| `monitor.mjs` | Cadência de polling (`RN-VEN-055`) — 15 s com movimento, 60 s ocioso, suspensão por aba |
| `mural.mjs` | O painel de senha no pico (`RN-VEN-054`, `RN-VEN-056`) — rolagem, colunas, tela cheia |
| `lista.mjs` | Os grupos da lista (`RN-VEN-057`) — colapso, filtro, e quanto ele economiza |
| `operacao.mjs` | A série de 30 dias (`RN-VEN-050`) — nenhuma barra some |

Copie o mais próximo do que você mudou.

> ### ⚠⚠ `PICO` — meça com sessenta, não com seis
>
> ```bash
> PICO=54 bash .claude/skills/subir-e-validar/scripts/semear.sh
> ```
>
> Com 6 pedidos **qualquer desenho de fila parece certo**. Foi com 60 que a comanda completa no
> painel se revelou errada (6 telas de rolagem) e que o agrupamento da lista ganhou número (8,4
> telas → 4,3).
>
> ⚠ E a **proporção** importa mais que o número: a primeira versão do `PICO` deixava quase tudo
> vivo, e com 59 dos 62 na casa o agrupamento não tinha o que economizar. Ele semeia uma noite
> real — ~60% concluídos, ~30% na casa, ~10% cancelados.

> ### ⚠ Caminho do navegador com barra normal
>
> `'C:/Program Files/Google/Chrome/Application/chrome.exe'`. A barra invertida escapada sobrevive no
> arquivo e **não** sobrevive a um heredoc do Bash — o erro sai como
> `executable doesn't exist at C:Program FilesGoogleChrome...`, com as barras comidas. Escreva o
> script com a ferramenta de arquivo, nunca por `cat <<EOF`.

### Olhe a captura

`fullPage: true` para conferir composição, e viewport fixo para conferir **o que cabe sem rolar** —
a fila do monitor precisa aparecer no tablet sem rolagem. Uma captura em branco é falha de subida,
não sucesso.

---

## 5. O que medir, e não só olhar

Captura prova composição. **Comportamento pede número.** O que a validação de 19/08 mediu, e o que
cada medida provou:

| Medida | Como | O que ela pega |
|--------|------|----------------|
| Cadência com movimento | contar `response` de `/pedidos/fila` por 40 s | intervalo real ≠ intervalo configurado |
| Cadência ociosa | logar no `casa-vazia` e observar 150 s | `PAR-VEN-008`/`009` — 15 s, 15 s, 15 s, **60 s** |
| Suspensão por aba | trocar de aba e contar por 35 s | corrente que continua perguntando escondida |
| **Corrente concorrente** | contar `/v1/pedidos` **enquanto** o monitor está à frente | duas telas somando polling em vez de trocá-lo |
| Ação reconsulta | clicar no avanço e contar | cartão que fica no bloco errado por 15 s |
| `CHAPA-02` | contar `.grelha i.ativa` **no grupo certo** | duas barras âmbar no mesmo grupo |
| `CHAPA-04` | `getBoundingClientRect().height` dos botões | alvo abaixo de 48 px na operação |
| Dado que não devia estar no fio | `innerText` da página e corpo cru da resposta | telefone e observação onde `RN-VEN-054` os proíbe |

> ### ⚠ Escope o seletor no grupo, senão a medida mente
>
> `CHAPA-02` foi medida primeiro com `header .grelha i.ativa` dentro do bloco — e deu **duas** barras
> âmbar, uma violação que não existia: o seletor pegava também o `<header>` de cada `<Comanda>`, que
> tem a régua própria dela. `.bloco-de-fila > header` é o grupo. **Sonda mal escopada acusa defeito
> falso, e perseguir defeito falso é como se perde a tarde.**

---

## 6. Derrubar

```bash
bash .claude/skills/subir-e-validar/scripts/derrubar.sh
```

Mata a API e o Vite e **remove o contêiner** — inclusive o volume, porque dado de demonstração que
sobrevive à sessão seguinte é dado que alguém vai confundir com dado real.

⚠ As tarefas de background da API e do Vite terminam com **código 1** quando você as mata. É
esperado; não é falha.

---

## 7. O que registrar depois

O que a validação encontrar **é achado de projeto**, não nota de sessão:

1. **Defeito** → corrija, e registre onde a regra vive: linha do §16 do catálogo, §5 ou §8 da spec, e
   o CLAUDE.md quando for armadilha para quem vier depois. Se a rastreabilidade afirmava um
   comportamento que não era verdade, **é a linha dela que precisa mudar** — foi o caso de
   `RN-VEN-047`.
2. **Comportamento sem regra** → pare, e escreva a regra primeiro. A skill não muda essa ordem.
3. **Medida** → vá para a spec, com número. "Cadência conferida" não vale nada; "consultas aos 15 s,
   30 s, 45 s e 105 s" vale.
4. **Nada** → diga isso, e diga o que foi conferido. Validação silenciosa é indistinguível de
   validação não feita.

---

## 8. A lição que gerou esta skill

> **Guarda de concorrência nunca vai num `useRef`.**
>
> `ref` sobrevive à remontagem do efeito; o efeito, não. A corrente nova encontra a trava da velha
> ligada, sai sem agendar nada, e a velha não reagenda por já estar morta. `let` dentro do efeito.
>
> O sintoma é o pior que existe numa tela ao vivo: **nada.** Sem erro, sem mensagem, sem console —
> só um estado que não muda mais. É indistinguível de "o pedido parou de andar", e é exatamente por
> isso que `RN-VEN-047` exige que **suspender nunca seja cessar**.
