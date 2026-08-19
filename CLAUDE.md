# GrelhaHub — instruções para agentes

ERP para restaurantes e deliveries (nicho hamburgueria). Trabalho acadêmico de 9 sprints,
que precisa ser **defensável em banca**: decisão sem justificativa registrada é decisão
errada, mesmo quando o código funciona.

> **Existe um agente dedicado a este projeto:** `grelhahub-engineer`
> (`.claude/agents/grelhahub-engineer.md`). Use-o para implementar, escrever specs, modelar
> o banco ou responder dúvidas de contexto — ele carrega o catálogo de regras e os ADRs.
>
> **E existe uma skill para validar tela:** `subir-e-validar`
> (`.claude/skills/subir-e-validar/`). Ela sobe PostgreSQL, os três bancos, a API e o Vite, semeia
> um cenário pelas rotas da API e dirige o Chrome instalado. ⚠⚠ **Os quatro gates não olham a
> tela** — foi assim que a fila de monitoramento passou em 356 testes e subiu vazia no navegador.

---

## A regra inegociável

> **Nenhuma regra de negócio nasce no código.**

O projeto é conduzido em **Document First** e **Spec-Driven Development**:

```
catálogo de regras  →  spec  →  código + teste que cita a regra
```

Se aparecer um comportamento necessário que não está no catálogo:
**pare o código**, escreva a regra em `docs/03-catalogo-de-regras-de-negocio.md` com estado
`HIP` e origem `IMP`, acrescente-a ao cabeçalho da spec, e só então continue.
Não implemente "provisoriamente".

> **O modo de falha real, observado neste repositório em 17/08/2026:** ninguém inventa uma
> regra do nada. O que acontece é **citar o ID mais parecido** — o cancelamento com motivo
> citou `RN-VEN-014`, que é taxa de serviço; o total do pedido citou `RN-VEN-012`, que é
> desconto. O código fica plausível, o comentário fica plausível, e a rastreabilidade morre
> em silêncio: um `grep` pelo ID devolve o teste errado. **Se a regra não descreve exatamente
> o comportamento, ela não é a regra** — leia o enunciado inteiro antes de citar o ID, nunca
> se baseie na lembrança do número.

---

## Leia antes de agir

| Arquivo | Para quê |
|---------|----------|
| `docs/README.md` | Mapa e estado atual |
| `docs/03-catalogo-de-regras-de-negocio.md` | **Fonte de verdade** — 188 regras |
| `docs/06-guia-de-specs.md` | Formato da spec, ondas de implementação, definição de pronto |
| `docs/adr/README.md` | 21 decisões (a 0021 é **proposta**) e 4 em aberto |
| `docs/02-glossario.md` | Vocabulário controlado — use os termos exatos |
| `docs/04-parametros-do-sistema.md` | Valores configuráveis |
| `docs/08-design-system.md` | **Base visual obrigatória** — leia antes de qualquer tela |
| `docs/arquitetura/` | Modelo de dados com diagrama, backend e frontend — extraído do modelo real |
| `.claude/skills/subir-e-validar/SKILL.md` | **Subir a pilha real e validar a tela** — obrigatório em mudança de interface |

**Nunca responda de memória sobre uma regra** — `grep` pelo ID. As regras mudam de estado a
cada entrevista.

---

## As sete regras estruturais

Errar aqui custa retrabalho de projeto, não de código.

| Regra | O que exige |
|-------|-------------|
| `RN-GER-006` ⚠⚠ | Dia operacional começa na **virada cadastrada da unidade** (`RN-GER-036`) — não é mais 04h00 fixo. **Nunca agrupe por data de criação** — use a coluna gerada `data_operacional`, que lê `hora_virada_operacional` e `fuso_operacional` **da própria linha do pedido** |
| `RN-PRO-004` ⚠⚠ | Ficha técnica recursiva: o molho da casa é produto **e** insumo. Exige proteção contra ciclo |
| `RN-EST-001` | Saldo **nunca** é coluna editável — é soma dos movimentos, materializada em view |
| `RN-EST-004` + `RN-EST-011` | Todo movimento referencia um **lote**; custo é o do lote consumido, consumo **FEFO**. Nunca custo médio |
| `RN-VEN-008` + `RN-VEN-010` | Preço praticado é copiado na venda; preço de tabela tem vigência |
| `RN-VEN-018` | `Aberto → Confirmado → Em produção → Pronto → Entregue → Finalizado`. Baixa de estoque e ordem de produção disparam na **Confirmação** |
| `RN-GER-009` | `unidade_id` em toda tabela transacional, **primeira coluna** de toda chave única e índice |

### Armadilhas

- Cancelamento **não** devolve estoque se o item já entrou em produção — gera **perda**
  (`RN-EST-018`). Antes da produção, não gera movimento (`RN-EST-019`)
- Nenhum literal de negócio no código nem em `CHECK`. `10%` é `PAR-VEN-002`.
  `CHECK` valida domínio (`entre 0 e 100`), nunca política (`<= 10`)
- **Dependência entre módulos vive em `FechoDeDependencia`, e só lá** (`RN-GER-020`): quem
  habilitar módulo por um caminho novo chama essa função. O fecho é **transitivo**, e o núcleo
  conta como presente
- **Ninguém concede acesso que não possui** (`RN-GER-024` ⚠⚠): criar usuário passa pela
  `MatrizDeDelegacao`, e `ValidadorDeDelegacao` **recusa a partida** se um perfil novo não
  declarar o que delega. Perfil novo em `CatalogoDeAcesso` exige linha na matriz
- **Arquivo do usuário: o formato vem da assinatura, nunca da extensão** (`RN-VEN-042`). E o
  nome no disco é gerado pelo sistema — o nome recebido nunca toca o sistema de arquivos
- Quantidade de insumo: **4** casas decimais. Moeda: 2
- Custo, CMV e margem **não são retornados pela API** sem permissão (`RN-GER-012`)
- Nenhum registro transacional é excluído fisicamente (`RN-GER-002`)
- O termo é `unidade`, não `estabelecimento`; `estacao`, não `praça`

---

## Convenções

### Backend

```
backend/src/
├── GrelhaHub.Domain/          sem dependências — nem EF Core, nem MediatR
├── GrelhaHub.Application/     Features/<Modulo>/<Caso>/ com Command|Query + Handler + Validator
├── GrelhaHub.Infrastructure/  EF Core, migrations, PostgreSQL
└── GrelhaHub.Api/             controllers, auth, DI
```

- Domínio em **português** (`Pedido`, `FichaTecnica`, `MovimentoEstoque`); termos de
  framework em inglês (`Handler`, `Command`)
- Banco em `snake_case`, PK `uuid`, `timestamptz` em UTC, `numeric` para dinheiro —
  nunca `float`
- Versões de pacote **só** em `backend/Directory.Packages.props` (CPM)
- Release ativa `TreatWarningsAsErrors` — build de Release limpo é obrigatório

### Frontend — direção Chapa

Base visual oficial desde 17/08/2026 (ADR-0012). Detalhe em `docs/08-design-system.md`.

**A regra central:** o kit de marca diz que *"a barra da ligadura sempre em cor de destaque
diferente das demais"*. Na interface isso vira: **em qualquer grupo, exatamente uma barra é
âmbar — a ativa**. Estado do pedido, aba, etapa, seleção.

**Cartoon por fora, sem graça por dentro.** Personalidade na moldura; dentro do dado, nada.

- Sombra **sólida deslocada** (`5px 5px 0` em carvão). Raio de desfoque é plástico
- Dado em **mono** com `tabular-nums`; prosa em sans; peso só no número do pedido e no total
- **Prosa usa a cor do texto, nunca `--muted`** (`CHAPA-10`) — `--muted` é rótulo e metadado. E
  texto **nunca** leva `opacity`: o contraste composto não aparece em ferramenta de verificação
- **Quem declara `background` declara `color`** (`CHAPA-11`): sem isso a superfície herda a cor
  da seção que a contém — foi assim que a comanda da capa ficou creme sobre creme
- **O rótulo da aba nomeia o que a aba mostra** (`CHAPA-12`): e a exigência é de forma —
  **uma linha por aba, na mesma ordem da lista**, condição enumerada e nunca por negação. Na área
  de estabelecimentos, "Horário e endereço" carregava a lista de pedidos porque o roteamento vivia
  em três blocos aninhados. ⚠ **Tela trocada não dá erro:** ela carrega e parece funcionar
- **Tamanho de fonte e espaçamento só por token, e nunca em `em`** (`CHAPA-13`): `--texto-2xs` a
  `--texto-3xl`, `--esp-1` a `--esp-16`. ⚠ **O valor fracionário é o sintoma:** `17,28px` na
  vitrine é `1.08em` dentro de `1.2em`, e ninguém escolheu aquele degrau. `rem` não compõe.
  ⚠ **A vitrine está convertida (21 degraus → 8); painel e cardápio, não** — a varredura da folha
  global é decisão pendente, e §2.3 do design system diz por quê
- Alvo de toque **48 px** na operação (acima dos 44 do `RNF-010`)
- Sem webfont, sem Inter/Geist, sem gradiente decorativo, sem glassmorphism
- Emoji não é ícone nem estado — status é selo com borda e cor
- **A vitrine não anuncia o que não existe** (`CHAPA-09`): ela lista **apenas o que está no ar**.
  Sem depoimento inventado, sem número de adoção, sem preço nem contato fixados no código — o
  texto vive em `frontend/src/features/site/conteudo.ts`, e o botão de contato **não renderiza**
  enquanto não houver canal configurado
- **Não existe cadastro anônimo de restaurante** (`SPEC-GER-007` §4): ninguém ganha conta sem
  contrato. Quem cria estabelecimento é o usuário de plataforma, e o **harness de teste cria por
  essa rota** — não reintroduza um atalho anônimo para facilitar teste
- **Sem cadeado em campo que a API não devolve** (`RN-GER-012`, `CHAPA-05`): se o perfil não
  vê o custo, não há blur nem "desbloqueie" — o campo não existe

Componente novo entra no design system **antes** de entrar no código, mesma regra do catálogo.

### Testes

O ID da regra vai no `DisplayName`. É isso que torna a rastreabilidade real:

```csharp
[Fact(DisplayName = "RN-EST-018 — item cancelado após produção gera perda, não devolução")]
```

Teste de integração usa **PostgreSQL real** (Testcontainers). O provider in-memory do
EF Core não executa constraint, trigger nem coluna gerada — daria falsa confiança
justamente nas regras que mais importam (ADR-0009). O harness está pronto: veja
"O harness de integração" em Estado atual. **Precisa de Docker rodando.**

Quando a regra existe nas duas camadas — C# e banco —, o teste que importa é o de
**paridade**: `DiaOperacionalParidadeTests` compara `DiaOperacional.De()` com a coluna gerada
nas 96 amostras do dia. Duas implementações da mesma regra divergem em silêncio; é esse teste
que transforma a divergência em falha.

### Onde cada regra é imposta — ADR-0009

O projeto usa **as duas camadas**, deliberadamente:

> O banco garante que o dado nunca fica inválido. A aplicação garante que o usuário nunca
> chega até lá.

Ordem de preferência: `MOD → constraints → GEN → TRG → VW → APP`. A aplicação é o último
recurso — o que a disciplina avalia é o modelo. Mas a **mensagem de erro ao usuário vem
sempre da aplicação**: erro de constraint que vaza para o usuário é defeito da aplicação.

---

## Comandos

```bash
# Backend
cd backend
dotnet build -c Release      # Release trata warning como erro
dotnet test -c Release       # integração sobe PostgreSQL efêmero: exige Docker rodando

# Cobertura — a régua honesta (18/08/2026: 90,6% de linha, 70,7% de ramo)
dotnet test -c Release --settings cobertura.runsettings --results-directory ./TestResults
reportgenerator "-reports:TestResults/**/coverage.cobertura.xml" "-targetdir:TestResults/relatorio" "-reporttypes:TextSummary" \n  "-classfilters:-Microsoft.AspNetCore.OpenApi.Generated*"   # sem isto o Api mede 54,9% em vez de 80,9%

# Frontend
cd frontend
npm ci                       # sem isto, o `npm run` abaixo sai 0 sem rodar nada
npm run lint && npm run build

# Validação em TELA — o gate que os quatro acima não cobrem
bash .claude/skills/subir-e-validar/scripts/subir.sh     # Postgres + 3 bancos + API + Vite
bash .claude/skills/subir-e-validar/scripts/semear.sh    # cenário pelas ROTAS da API
cd .claude/skills/subir-e-validar/driver && npm install && node exemplos/monitor.mjs
bash .claude/skills/subir-e-validar/scripts/derrubar.sh
```

⚠ **`--migrate`, em inglês.** Escrita como `--migrar` a flag é ignorada em SILÊNCIO e o processo sobe
como servidor: a API responde `/api/health`, e os bancos de log e de arquivo ficam vazios — a trilha
falha em toda operação e o primeiro upload dá 500. O startup de Development migra **só** o principal.

---

## Estado atual

Sprint 1 & 2 — documentação entregue. **188 regras catalogadas: 162 em `HIP`, 26 em `DEC`;
nenhuma validada com cliente.** A **onda 0 está entregue** — `SPEC-GER-001` a `007`. Da **onda 1**, o cadastro do cardápio
está entregue (`SPEC-VEN-003`). Em 18/08/2026 a `SPEC-GER-007` ganhou a **equipe da plataforma** (`RN-GER-030`, `RN-GER-031`) e a `SPEC-GER-008` entregou a **recuperação de acesso por e-mail** (`RN-GER-032` a `035`, ADR-0016): link de uso único, convite na criação de usuário e "esqueci minha senha" — o que fecha o beco de quem perdia a senha do acesso de plataforma.

Restam **dois critérios abertos por dependência de onda**, ambos declarados na sua spec:
`SPEC-GER-004` CA-4 (gerente vê custo → espera a ficha técnica, onda 2) e CA-6 (colaborador
inativo → espera o módulo RH, onda 10).

**Existe uma fatia vertical funcionando** — `SPEC-VEN-001`, fora da ordem das ondas:
cadastro de restaurante → cardápio público em `/c/:slug` → pedido → painel do restaurante.
Backend, frontend, migration `Fundacao` e pipeline de deploy. 26 regras com código, e
**63 regras têm o ID citado no `DisplayName` de um teste** ao todo. A dívida de cada simplificação está na §8 da spec — **um pedido
que não baixa estoque hoje não significa que a regra mudou**, significa que o estoque ainda
não existe.

Em 18/08/2026 entrou o **painel de operação** (`RN-GER-038`, `RN-VEN-049` a `051`): faturamento por
dia operacional, ticket médio, o que está no salão e mais vendidos, com filtro de período — que
ganhou tela também na lista de pedidos, onde o backend já suportava `de`/`ate` desde a fatia
vertical. ⚠ **A lista é agrupada por dia operacional, e o agrupamento é obrigatório:** `numero` é
único por (unidade, dia operacional) e REINICIA a cada virada — sem o divisor, a comanda #1 de hoje
e a #1 de ontem aparecem lado a lado, idênticas. Foi o que o filtro revelou no dia em que entrou.

Em 19/08/2026 entraram `RN-VEN-052` e `RN-VEN-053` — **os dois tetos de texto livre**. A observação
do cliente tem **80 caracteres** e a comanda passa a **rotulá-la** (`Observação do cliente`) em vez
de exibi-la solta em `--muted`: o campo já era gravado e já era exibido, e mesmo assim o único
lugar em que o cliente fala com a cozinha lia-se como sobra de tela — `Acompanhar.tsx` já aplicava
`CHAPA-10` a esse mesmo dado, e a tela de quem prepara o sanduíche não. A descrição do produto tem
**200**, inteira na vitrine e resumida a duas linhas na gestão. ⚠⚠ **Os dois tetos vivem na tela e
no validator, NÃO na coluna** — `pedido.observacao` e `produto.descricao` continuam `varchar(500)`,
com comentário na configuração do EF dizendo por quê: encurtar a coluna reclassificaria dado já
gravado, o oposto do que `RN-GER-036` decidiu. Quem for "corrigir" a divergência, leia a regra
antes. O número mora em três lugares — catálogo, constante de domínio e `services/limites.ts` —, e
mexer num sem os outros produz o pior defeito para o usuário: campo que aceita digitar e recusa ao
salvar.

⚠⚠ **19/08/2026 — uma revisão externa de design e UX das quatro superfícies**, em dois documentos
(vitrine/cardápio/painel, e um anexo de plataforma). O que ela achou de mais grave **não estava em
nenhum teste**, porque nada disso quebra: **`/painel` aberto por usuário de plataforma montava o
painel de loja inteiro em cima de "Restaurante não encontrado."** — falha em cima, interface
funcionando embaixo. O desvio agora vive em `ExigeSessao` com `area="loja" | "plataforma"`, as duas
direções no mesmo lugar. **No celular, o carrinho do cardápio público não era fixo**: adicionar item
não mudava nada no campo de visão, e fechar o pedido exigia rolar o cardápio inteiro — atrito que
não deixa rastro, porque ninguém reclama, as pessoas só desistem. Entraram também: **`RN-GER-039`**
(CPF mascarado em listagem, com a dívida da API declarada), **tempo decorrido na comanda** — que
vale mais que o número do pedido num produto cujo argumento é "a cozinha vê a fila" —, faixa de
categorias, atalhos de semana no horário, reenvio de convite para "senha pendente", e as duas
regras novas do design system (`CHAPA-12`, `CHAPA-13`).

⚠ **Três sugestões da revisão foram RECUSADAS, e o motivo é catálogo, não preguiça.** "CTA final sem
botão" é a `CHAPA-09` funcionando — `BotaoDeContato` existe e não renderiza porque `CONTATO` está
`null`, e canal inventado numa vitrine pública é pior que canal nenhum (o beco real foi fechado com
o destino que **existe**: entrar). "Preço a partir de R$ X" fixaria política em código. E "permitir
montar pedido com a loja fechada, para enviar na abertura" é **pedido agendado**, que `RN-VEN-048`
não prevê — o que a regra pedia e faltava era recusar o **envio** na tela, não só no servidor.


⚠⚠ **19/08/2026 — DUAS LACUNAS DE SEGURANÇA ABERTAS, achadas ao avaliar o ASP.NET Identity**
([ADR-0021](docs/adr/0021-identity-do-aspnet-core.md), **proposta, aguarda decisão**):

1. **Não há bloqueio por tentativa no login.** `RN-GER-027` protege contra **enumeração de contas**
   (a resposta é igual para senha errada e conta inexistente) e **não** contra força bruta. Senha de
   8 caracteres sem limite de tentativas é atacável.
2. **O JWT sobrevive à troca de senha** — não há *security stamp* nem revogação, e o token vale
   até 8 h (`Jwt:ExpiracaoEmMinutos`). ⚠ Isto já estava documentado abaixo, em `RF-SEG-005`, como
   detalhe de usabilidade: *"trocar a senha NÃO derruba a pendência do token que já está na mão"*. O
   contorno da tela é de usabilidade; **quem teve a senha vazada troca a senha e o atacante segue
   dentro por oito horas.**

⚠ **O `PasswordHasher` do Identity JÁ é usado** (`RNF-005`, PBKDF2 com salt) — a pasta se chama
`Infrastructure/Identity/`. O que a ADR-0021 recusa é `AspNetUsers`/`AspNetRoles`/`UserManager`, com
sete motivos; o principal é que **papel do Identity é global e o perfil do GrelhaHub é por unidade**
(`RN-GER-009` exige `unidade_id` como primeira coluna de toda chave única).

⚠⚠ **19/08/2026 — a FILA DE MONITORAMENTO** (`RN-VEN-054`, `RN-VEN-055`, `SPEC-VEN-004`). Aba
**Monitor**, ao lado de Operação: o que ainda está na casa, em dois blocos — **em aberto** (Aberto,
Confirmado, Em produção) e **pronto**. Sai da fila quem chega a Entregue, Finalizado ou Cancelado.
As três telas de pedido respondem três perguntas diferentes, e é isso que impede a redundância:
**Pedidos** diz o que entrou no dia, **Operação** diz quanto a casa vendeu, **Monitor** diz o que
falta entregar — e a lista responde essa última cada vez pior conforme a noite avança, porque ela
cresce.

- ⚠⚠ **A fila NÃO tem recorte de período, e isso é a regra.** O pedido aberto às 23h50 que
  atravessou a virada continua para ser entregue: ele sai da fila por **estado**, nunca por data.
  Com mais de um dia operacional na fila, a comanda ganha a **data** — `numero` reinicia a cada
  virada, e a nº 1 de ontem é idêntica à de hoje. ⚠ A tela **não calcula** dia operacional (isso é
  `RN-GER-006`, e mora no banco): ela só pergunta se há mais de um dia na resposta.
- ⚠⚠ **A cadência sai do MOVIMENTO, não do relógio da tela** (`RN-VEN-055`): 15 s (`PAR-VEN-005`)
  com fila; 60 s (`PAR-VEN-008`) depois de 4 ciclos (`PAR-VEN-009`) com a fila **vazia**; e volta ao
  ritmo curto no primeiro pedido. Afrouxar por tempo em tela — que é o que `RN-VEN-047` faz do lado
  do cliente, e com razão — seria o erro exato aqui: **a tela aberta há mais tempo é a da noite
  cheia**. O que esta tela não tem é estado terminal: ela fica ligada o serviço inteiro.
- ⚠⚠ **A lista de pedidos consultava a cada 15 s com QUALQUER aba à frente e com a janela
  escondida.** Abrir o monitor passou a **somar** uma corrente em vez de trocá-la. Agora o
  `setInterval` do `Painel` só existe na aba `pedidos` e pula o ciclo com `document.hidden`. ⚠ Ali
  um `return` seco é seguro porque `setInterval` continua disparando; em `useFilaAoVivo` a corrente
  é uma cadeia de `setTimeout`, e **quem suspende reagenda** — trocar um pelo outro mata a tela em
  silêncio.
- ⚠ **A fila não devolve telefone nem observação, e não é a tela que esconde:** os campos não
  existem em `PedidoNaFilaDto`. `CHAPA-05` do lado do servidor. O **nome do cliente fica** — é como
  se chama quem vem buscar. ⚠⚠ A observação é instrução de preparo (`RN-VEN-052`) e continua
  **inteira na comanda** da aba Pedidos: **a fila não substitui a comanda de preparo**, e a
  `SPEC-VEN-004` §8 declara isso como a pergunta aberta da próxima entrevista (item 23 da fila de
  validação). Se a cozinha adotar a fila como comanda, a observação volta e a regra muda com ela.
- `CapaDaCasa` saiu de `features/publico/` para `components/` — três chamadores agora, e componente
  compartilhado que mora dentro de uma área é o começo de uma cópia.

⚠⚠ **A trava de "uma requisição por vez" NUNCA vai num `useRef`.** `ref` sobrevive à remontagem do
efeito; o efeito, não. A corrente nova encontrava a trava da VELHA ligada, saía sem agendar nada, e a
velha não reagendava por já estar morta — **tela no ar, sem erro, e nada mais perguntando**. Vale
`let emVoo` dentro do efeito, e mais nada. ⚠ O defeito estava nas **duas** correntes: nasceu com
`useFilaAoVivo` e já existia em `usePedidoAoVivo` desde 18/08, alcançável por "Tentar agora" com uma
requisição em voo — ou seja, `RN-VEN-047` afirmava "suspender nunca solta a corrente" enquanto o
código fazia o contrário. **Nada disso aparecia em `tsc`, em `eslint` nem nos 356 testes:** foi um
navegador de verdade, com a pilha de pé, que mostrou a fila vazia na tela e cheia na API. É a dívida
do runner de frontend cobrando juros — ver `SPEC-VEN-004` §5.

⚠ **No painel de operação, o ranking subiu para acima do gráfico**, e a série de 30 dias ganhou
régua: rótulo de data **rareia**, só o melhor dia mostra o valor, e a tabela dia a dia virou uma
**dobra** fechada com teto de altura. ⚠⚠ **Dobrar não é truncar** — cortar em "os sete primeiros
dias" esconderia dia sem dizer que escondeu, que é o defeito de leitura que `RN-VEN-050` existe para
impedir. Nenhuma barra some.

⚠⚠ **18/08/2026 — quatro mudanças estruturais no mesmo dia**, todas com ADR:

- **ADR-0017 — três bancos.** `grelhahub` (negócio), `grelhahub_log` (trilha + exceções) e
  `grelhahub_arquivo` (binários). A trilha atravessa por **outbox**: ela é gravada na transação da
  operação e copiada por um worker. ⚠ **A trilha chega com segundos de atraso** — teste que a
  consulta precisa chamar `Ambiente.DrenarOutboxAsync()` antes, e o helper usa o MESMO código do
  worker.
- **ADR-0018 — a imagem virou binário no banco**, substituindo a ADR-0015. Não há mais volume de
  disco: os bytes estão em `arquivo_binario` (banco de arquivo) e o metadado em `arquivo` (banco da
  aplicação), com o **mesmo id**. ⚠⚠ **A ordem de gravação é a decisão:** bytes primeiro, metadado
  depois — metadado sem bytes é imagem quebrada que o CLIENTE vê; bytes sem metadado é invisível e
  varrível. Quando não há transação, falhe do lado que não mente para o usuário.
- **ADR-0019 — CQRS de verdade, e CONCLUÍDO.** A leitura tem `ILeituraDb` (sem `SaveChanges`,
  modelos sem chave, 17 visões do banco). `SeparacaoDeCqrsTests` recusa a build se um handler
  cruzar os lados. **16 de 16 consultas convertidas**; a lista-catraca ficou no teste, vazia, de
  propósito.
- **ADR-0020 — a sessão de suporte foi REMOVIDA.** `RN-GER-015` foi revisada: o controle é a
  trilha, não o portão. Não existe mais `sessao_suporte`, nem behavior, nem tela.

⚠ **Cobertura: 90,8% de linha, 71,4% de ramo** — medida com `cobertura.runsettings` E o filtro de
classe do relatório. Sem os dois, o número mente: ver o aviso acima.

⚠⚠ **Quando a LEITURA precisa de uma regra, extraia a função — não a traduza para SQL.** A conversão
do ADR-0019 esbarrou em `RN-VEN-048` ("a casa está aberta agora?") e `RN-GER-037` (endereço em uma
linha), que viviam dentro de `Unidade` e que o handler convertido não alcança mais. Reescrevê-las na
visão criaria a segunda implementação que diverge em silêncio — o defeito que
`DiaOperacionalParidadeTests` existe para pegar. Estão em `ApresentacaoDaCasa`, chamada pelos dois
lados: **um código, dois chamadores, nenhuma paridade a manter.**

⚠ **A imagem tem DUAS rotas de leitura, e a escolha não é indiferente.**
`GET /api/v1/arquivos/{id}` devolve os bytes com `Cache-Control: immutable` — é o que `<img src>`
usa na vitrine. `/base64` devolve o data URI, para a miniatura do painel. **Base64 infla um terço e
não entra no cache de imagem**: num cardápio de vinte produtos no celular do cliente isso aparece
antes do primeiro preço. Nenhuma listagem embute imagem — elas devolvem a referência.

A arquitetura ganhou documento próprio em [`docs/arquitetura/`](docs/arquitetura/) — modelo de
dados com diagrama e dicionário completo, backend e frontend, **extraídos do modelo real** e
escritos em dois níveis (apresentável ao time, e detalhado para quem mexe).

### O que está verde e o que não está

| Gate | Estado |
|------|--------|
| `dotnet build -c Release` | ✔ 0 avisos |
| `dotnet test -c Release` | ✔ **361 testes** — 110 de domínio + 18 de aplicação + 233 de integração |
| Testes de integração | ✔ PostgreSQL real via Testcontainers (`postgres:16-alpine`). **Exige Docker rodando** |
| `npm run lint && npm run build` | ✔ limpo — `npm ci` primeiro (204 pacotes, 0 vulnerabilidades) |
| **Validação em tela** | ✔ 19/08/2026 — fila, cadência, celular e `CHAPA-02`/`04` conferidos em Chrome real. ⚠⚠ **Não é gate automático:** roda à mão, pela skill `subir-e-validar` |
| ⚠⚠ Runner de frontend | ✖ **não existe.** Dois defeitos de polling passaram pelos quatro gates acima e só um navegador os viu |

> ⚠⚠ **A cobertura que o projeto citava — 95,4% — estava inflada.** As migrations do EF Core são
> milhares de linhas GERADAS e contavam como cobertas só porque a suíte cria o banco na partida.
> Sem elas, e sem as 208 linhas do gerador de OpenAPI da Microsoft, o número real é **90,8%**. E há duas exclusões que apagam medição em SILÊNCIO, ambas
> documentadas em `backend/cobertura.runsettings`: `CompilerGeneratedAttribute` derruba todo código
> `async` (2.249 linhas contra 4.106), e `Exclude`/`ExcludeByAttribute` fizeram o assembly
> **Infrastructure sumir do relatório** — não com nota baixa: ausente, com a média subindo porque a
> camada saiu da conta. **Confira sempre se os quatro assemblies aparecem no resumo.**

> ⚠ **`npm run` mascara ausência de dependência.** Sem `node_modules`, `npm run lint` imprime
> "eslint não é reconhecido" e ainda assim **sai com código 0** — o gate parece verde e não
> rodou nada. Confira o código de saída do `eslint`/`tsc`, não o do `npm run`.

### `RF-SEG-005` — a troca no primeiro acesso, e o detalhe que engana

`/trocar-senha` existe, e `ExigeSessao` manda para lá quando a pendência está aberta.

⚠ **Trocar a senha NÃO derruba a pendência do token que já está na mão:** o claim
`trocar_senha` viaja nele. Depois do `POST /v1/usuarios/minha-senha`, a tela **reautentica** com a
senha nova para receber um token limpo — e é por isso que o e-mail vem na resposta do login. Quem
mexer nesse fluxo e só navegar para o painel vai ver "Troque a sua senha" de novo, com a senha já
trocada.

> **Este foi o terceiro gate que subiu sem porta na interface** — os outros dois: sessão de suporte
> e dependência de módulo. A pergunta de revisão está na definição de pronto do guia de specs:
> **se esta regra recusar a operação, para onde o usuário vai?**
>
> **O quarto apareceu na `SPEC-VEN-003`, e é o primeiro que o item de revisão pegou** em vez de um
> usuário travado: `ListarCardapioInterno` filtrava `c.Ativa`, então **categoria inativada
> desaparecia da tela de gestão e não havia como reativá-la** — os produtos ficavam presos num
> grupo invisível. Beco **sem** mensagem de erro é mais difícil de ver do que beco com mensagem:
> percorra o caminho de volta de toda operação reversível, não só o de ida.

### O harness de integração

`tests/GrelhaHub.Integration.Tests/Infraestrutura/` — contêiner PostgreSQL + a API real, sem
nenhum dublê. Um contêiner por assembly; cada teste limpa o banco antes de rodar, com a lista
de tabelas descoberta no catálogo do PostgreSQL (não escrita a mão, senão as ondas seguintes
deixam resíduo silencioso).

- Herde de `TesteDeIntegracao` e marque a classe com `[Collection(ColecaoDeIntegracao.Nome)]`
- Monte o cenário pelas **rotas da API** (`CadastrarRestauranteAsync`, `CriarProdutoAsync`),
  não por `INSERT`: dado que entra por caminho que o sistema não tem escapa de validator,
  de autorização e do vínculo de unidade
- Use `Contexto(restaurante)` só para **conferir** o que foi gravado
- Para verificar isolamento entre unidades, use token ou `slug` — nunca `ContextoFixo`, que
  provaria apenas que o teste soube preencher um campo

> ⚠ **`url_imagem` do produto guarda uma REFERÊNCIA, não um caminho de disco.** Vale
> `/api/v1/arquivos/{id}` desde a ADR-0018. As gravadas antes valem `/arquivos/…` e continuam
> servidas pelo Nginx, somente leitura — a coluna é texto justamente para os dois formatos
> conviverem sem migração obrigatória de dado. O frontend pergunta o formato antes de escolher o
> caminho (`services/imagens.ts`).

**Próximo trabalho de código:** o resto da **onda 1 — cadastro**: **preço com vigência**
(`RN-VEN-010`), **insumo**, **unidade de medida e conversão**. Já entregues: a onda 0 inteira —
inclusive `SPEC-GER-007` (CPF, delegação de perfil e telas de administração) —, `SPEC-VEN-002`
(imagem de produto e logo) e `SPEC-VEN-003` (**cadastro do cardápio**: categoria e produto, com
`ativo` cadastral separado de `disponível` do turno). O índice está em
[`docs/06-guia-de-specs.md`](docs/06-guia-de-specs.md#8-índice-de-specs).

⚠ **`ativo` e `disponível` no produto não são o mesmo campo com dois nomes** (`RN-VEN-044`). O
efeito no cliente é idêntico — o produto não aparece —, e é por isso que fundi-los é tentador. São
duas rotas separadas de propósito: `PATCH .../situacao` (cadastral) e `PATCH .../disponibilidade`
(do turno).

⚠⚠ **A onda 3 começa com um conflito aberto, não com código.** `RN-EST-021` a `023` já enunciam o
gatilho da baixa — confirmação, ficha explodida, FEFO, mesma transação, idempotência e devolução
na janela antes da produção. Mas `RN-EST-023` **revisa** `RN-EST-019`, e o **conflito C10** está
aberto: se a baixa dispara na Confirmação (`RN-VEN-018`, estrutural) ou na Produção **só a
entrevista decide** — perguntas 8 e 9 da fila. E o modo *permitir* de `PAR-VEN-003` não é
implementável enquanto ninguém disser o que ele significa, já que `RN-EST-009` proíbe saldo
negativo (pergunta 10b). **Não implemente a baixa antes dessas três respostas.**

### Dinheiro e quantidade nunca são `decimal` — `SPEC-GER-001` §4

> **Um `decimal` solto não distingue R$ 12,00 de 12 gramas**, e o compilador não impede somar
> os dois. Pior: quantidade tratada com a precisão do dinheiro grava `0,1563` como `0,16`
> **sem erro nenhum**, e o desvio entra direto no CMV com um número plausível.

- **`Dinheiro`** — `numeric(12,2)`, `RN-GER-003`. Construção só por `Dinheiro.De(valor)`;
  arredonda `AwayFromZero`, que é o arredondamento comercial e não o padrão bancário do .NET
- **`Quantidade`** — `numeric(14,4)`, `RN-GER-004`. Para insumo, que se **mede**
- **Contagem de produto vendido é `int`** — dois lanches, não 2,0000 lanches (`RN-VEN-011`)
- **Não há convenção para `decimal` cru, de propósito.** `GarantirAusenciaDeDecimalCru`
  **recusa o modelo na partida** e diz qual propriedade corrigir
- Os DTOs de API seguem em `decimal`: value object é conceito de domínio, não formato de fio

⚠ **Nunca compare `Guid` com `Guid?` dentro de árvore de expressão.** `p.Id != produtoIgnorado`
com `produtoIgnorado` nulo é `p.id <> NULL` em SQL — que filtra **todas** as linhas e devolve
contagem zero. O EF Core compensa gerando `(p.id <> @p OR @p IS NULL)`, mas **a consulta compilada é
reaproveitada**, e o resultado passa a depender de qual chamada compilou primeiro. O sintoma é o pior
que existe: a classe de teste passa sozinha e falha com a suíte inteira (`SPEC-VEN-003` §11.7). Use
`Where` condicional: `if (id is Guid x) consulta = consulta.Where(p => p.Id != x);`

⚠ **O EF Core não traduz acesso a membro de propriedade com conversor de valor.**
`p.Preco.Valor` não vira SQL, e `Sum(i => i.PrecoPraticado * i.Quantidade)` não compila para
consulta. Filtre e ordene no banco; monte o DTO em memória. Para agregação de verdade, use
view (mecanismo `VW` do catálogo), não `Sum` sobre value object.

Enquanto `RN-GER-006` e `RN-PRO-004` estiverem em `HIP`, qualquer modelagem da ficha técnica
ou da dimensão tempo é provisória. Diga isso ao pedir ou receber trabalho nessas áreas — não
recuse, mas registre que a decisão pode virar retrabalho.

### Decisões do time — 17/08/2026

- **FIS é o último módulo** (onda 11). Nada depende dele, e o valor exige homologação
  externa. As regras seguem catalogadas e a modelagem reserva o espaço
- **RH permanece no MVP**, em nível Modelo — `RN-LOG-005` depende dele
- **`RN-VEN-035`** — falta de insumo **sempre** gera alerta na tela, nos três modos de
  `PAR-VEN-003`. O parâmetro decide a fricção (recusar, exigir confirmação, informar),
  **nunca se o operador é avisado**

---

## Idioma

Documentação, domínio e mensagens em **português do Brasil**.
