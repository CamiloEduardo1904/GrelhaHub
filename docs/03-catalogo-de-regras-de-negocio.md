# GrelhaHub — Catálogo Consolidado de Regras de Negócio

| Campo | Valor |
|-------|-------|
| Versão | **2.1** |
| Data | Agosto/2026 |
| Situação | Em validação — **168 regras**: 146 em hipótese, 22 decididas pelo time |
| Fontes consolidadas | *Catálogo de Regras v0.1* (117 regras, P3 — Análise de Negócios) + *Documento de Levantamento v1.0* (17 regras complementares) + 10 regras descobertas na implementação (`IMP`) + 22 decisões de produto do time (`DEC`) |
| Auditoria do cruzamento | [`07-cruzamento-catalogo-x-levantamento.md`](07-cruzamento-catalogo-x-levantamento.md) |
| Papel no projeto | **Fonte de verdade.** Toda spec de implementação transcreve regras daqui. Nenhuma regra de negócio nasce no código |

---

> ## Este documento é a base do SDD
>
> O GrelhaHub é desenvolvido em **Spec-Driven Development** com **Document First**: a regra
> é escrita e validada antes da spec, a spec antes do código, e o código não introduz
> comportamento que não esteja aqui.
>
> **Não implemente a partir deste documento diretamente.** O caminho é
> `regra → spec → código`, descrito em [`06-guia-de-specs.md`](06-guia-de-specs.md).
>
> **Este catálogo é vivo.** Cada implementação o realimenta: uma regra descoberta durante o
> desenvolvimento é escrita aqui **antes** de ser codificada, e a spec correspondente é
> registrada na coluna de rastreabilidade. Um catálogo que não muda durante a implementação
> é um catálogo que parou de ser lido.

---

## Sumário

1. [Estado do catálogo](#1-estado-do-catálogo)
2. [Como ler este documento](#2-como-ler-este-documento)
3. [Ciclo de vida de uma regra](#3-ciclo-de-vida-de-uma-regra)
4. [Resumo por módulo](#4-resumo-por-módulo)
5. [GER — Gerais e transversais](#ger--gerais-e-transversais)
6. [VEN — Vendas e Comercial](#ven--vendas-e-comercial)
7. [PRO — Produção](#pro--produção)
8. [EST — Estoque](#est--estoque)
9. [COM — Compras](#com--compras)
10. [FIN — Financeiro](#fin--financeiro)
11. [CRM — Relacionamento com o cliente](#crm--relacionamento-com-o-cliente)
12. [LOG — Logística e Frota](#log--logística-e-frota)
13. [RH — Recursos Humanos](#rh--recursos-humanos)
14. [FIS — Fiscal](#fis--fiscal)
15. [As sete regras estruturais](#15-as-sete-regras-estruturais)
16. [Rastreabilidade regra → spec](#16-rastreabilidade-regra--spec)
17. [Histórico de versões](#17-histórico-de-versões)

---

## 1. Estado do catálogo

Das 168 regras, **146 estão em estado de hipótese** — 134 herdadas das fontes e 12 descobertas
durante a implementação (origem `IMP`) — e as outras 19 são decisões de produto, estado `DEC`.
As herdadas foram derivadas do
conhecimento do domínio (operação de hamburgueria) e da estrutura dos dez módulos de ERP
exigidos no trabalho — **e não de entrevista com cliente real**.

Esse é o ponto de partida correto, mas é apenas o ponto de partida. Um catálogo inteiramente
em hipótese ainda não passou pelo teste que importa: a confirmação de quem opera o negócio.

**Uso pretendido:** levar este catálogo à primeira entrevista como roteiro de validação,
percorrendo regra a regra os blocos críticos. Cada hipótese que se converte em regra
validada — ou rejeitada — é levantamento de requisitos efetivo. **Uma rejeição vale tanto
quanto uma confirmação** como evidência de método.

A ordem de prioridade para a entrevista está em
[`05-fila-de-validacao-com-cliente.md`](05-fila-de-validacao-com-cliente.md).

---

## 2. Como ler este documento

### Tipo da regra

| | |
|---|---|
| **D** | **Definição** — estabelece o significado de um termo |
| **F** | **Fato / estrutura** — afirma uma relação que existe |
| **R** | **Restrição** — limita o que pode ser verdade |
| **C** | **Cálculo** — deriva um valor a partir de outros |
| **I** | **Inferência** — conclui uma classificação a partir de dados |
| **A** | **Ação** — dispara um efeito quando algo acontece |

### Estado de validação

| | |
|---|---|
| **HIP** | Hipótese — ainda não confirmada com o cliente |
| **VAL** | Validada — confirmada em entrevista |
| **DEC** | Decidida pelo time — decisão de produto, não hipótese sobre a operação |
| **CNF** | Conflito — respostas divergentes entre fontes |
| **ESC** | Fora de escopo — real, mas não será implementada |
| **REJ** | Rejeitada — não corresponde à operação real |

> **`DEC` não é o mesmo que `VAL`.** `VAL` afirma *como a operação funciona* e só a
> entrevista pode confirmar. `DEC` afirma *como o sistema vai se comportar* — é decisão de
> produto, tomada pelo time, e não depende de entrevista. Uma regra `DEC` pode conviver com
> uma resposta contrária do cliente: se o operador disser que prefere sem alerta, a decisão
> de produto continua valendo até que o time a mude.

Toda mudança de estado registra a data e a fonte da validação no
[histórico de versões](#17-histórico-de-versões).

### Mecanismo de implementação previsto

| | |
|---|---|
| **MOD** | Estrutura do modelo — a existência da entidade, do relacionamento ou da cardinalidade já expressa a regra |
| **NN** | `NOT NULL` |
| **UQ** | `UNIQUE` |
| **FK** | Chave estrangeira, com `ON DELETE` correspondente |
| **CK** | `CHECK` constraint |
| **EX** | `EXCLUDE` constraint — exclusão por sobreposição |
| **ENU** | Domínio fechado: `ENUM` ou tabela de domínio |
| **GEN** | Coluna gerada — `GENERATED ALWAYS AS` |
| **TRG** | Trigger ou função no banco |
| **VW** | View ou view materializada |
| **APP** | Camada de aplicação (.NET) |
| **PAR** | Depende de um parâmetro configurável do sistema |

**Ordem de preferência:** `MOD → NN/UQ/FK/CK/EX/ENU → GEN → TRG → VW → APP`.
A camada de aplicação é o último recurso, não o primeiro: o que está sendo avaliado é o
modelo. A convivência entre mecanismo de banco e de aplicação está decidida em
[ADR-0009](adr/0009-onde-as-regras-de-negocio-vivem.md).

### Escopo no MVP

| | |
|---|---|
| **N** | **Núcleo** — modelado *e* implementado até a Sprint 9 |
| **M** | **Modelo** — modelado no banco; software apenas cadastro e consulta, sem automação |
| **2** | **Fase 2** — modelado; implementação após o MVP |

### Origem

| | |
|---|---|
| *(sem marca)* | Catálogo de Regras v0.1 |
| **LEV** | Contribuição do Documento de Levantamento, incorporada no cruzamento |
| **CRZ** | Regra criada durante o cruzamento, para resolver conflito entre as fontes |
| **DEC** | Decisão de produto tomada pelo time, registrada como regra |
| **IMP** | Regra descoberta durante a implementação |

### Marcador de criticidade

Regras assinaladas com **⚠** são **estruturais**: se estiverem erradas, o modelo de dados
nasce errado e a correção posterior custa retrabalho de projeto, não de código.
**⚠⚠** indica a regra de maior impacto do catálogo.

---

## 3. Ciclo de vida de uma regra

```
        escrita            entrevista          spec              implementação
   ┌──────────────┐   ┌─────────────────┐  ┌──────────┐    ┌────────────────────┐
   │  HIP         │──►│  VAL / REJ /    │─►│ SPEC-xxx │───►│ código + teste que  │
   │  hipótese    │   │  CNF / ESC      │  │ escrita  │    │ prova a regra       │
   └──────────────┘   └─────────────────┘  └──────────┘    └────────────────────┘
          ▲                                                            │
          │              nova regra descoberta na implementação        │
          └────────────────────────────────────────────────────────────┘
```

**Regras do processo — não negociáveis:**

1. Só se implementa regra que está neste catálogo. Se durante a implementação aparecer um
   comportamento necessário que não está aqui, **a implementação para**, a regra é escrita
   aqui, e só então o código continua.
2. Regra em estado `HIP` pode ser implementada, mas o teste correspondente deve citar o ID
   da regra — para que uma futura rejeição em entrevista encontre o código que a assume.
3. Regra que muda de estado para `REJ` gera issue de remoção do código que a implementava.
4. A coluna **Spec** da [rastreabilidade](#16-rastreabilidade-regra--spec) é preenchida
   quando a spec é escrita, não quando o código é entregue.

---

## 4. Resumo por módulo

| Módulo | Regras | Prioridade de validação | Escopo predominante |
|--------|-------:|-------------------------|---------------------|
| **GER** — Gerais e transversais | 31 | **ALTA** — RN-GER-006 é estrutural | Núcleo |
| **VEN** — Vendas e Comercial | 46 | **MÁXIMA** | Núcleo |
| **PRO** — Produção | 13 | **MÁXIMA** | Núcleo |
| **EST** — Estoque | 23 | **MÁXIMA** | Núcleo |
| **COM** — Compras | 9 | ALTA | Modelo |
| **FIN** — Financeiro | 16 | MÉDIA | Núcleo |
| **CRM** — Relacionamento com o cliente | 9 | MÉDIA | Modelo |
| **LOG** — Logística e Frota | 9 | MÉDIA | Núcleo |
| **RH** — Recursos Humanos | 6 | BAIXA | Modelo |
| **FIS** — Fiscal | 6 | BAIXA | **Fase 2 — por último** |
| **Total** | **168** | | |

> **Ordem de ataque decidida (17/08/2026).** O módulo **FIS** fica **por último**: é o único
> cujo valor depende de homologação externa (SEFAZ, certificado digital), e nada nos outros
> nove módulos depende dele. **RH permanece no MVP em nível Modelo** — colaborador, cargo,
> escala e apontamento, sem folha de pagamento. `RN-LOG-005` (entregador é colaborador
> ativo) depende de RH, o que já o torna necessário.

### Onde a entrevista precisa ir fundo

Os três módulos de prioridade máxima — **Vendas, Produção e Estoque** — formam a cadeia
`venda → ficha técnica → baixa de insumo`. É nela que o modelo ganha ou perde.

---

## GER — Gerais e transversais

**22 regras**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-GER-001 | Todo registro transacional guarda a data e hora de criação e o usuário responsável pela operação. | R | todas transacionais | NN MOD APP | HIP | N |
| RN-GER-002 | Nenhum registro transacional é excluído fisicamente. O desfazimento é registrado como cancelamento, com motivo e responsável. | R | todas transacionais | MOD APP | HIP | N |
| RN-GER-003 | Valores monetários são expressos em reais, com duas casas decimais. | R | todas com valor | CK | HIP | N |
| RN-GER-004 | Quantidades de insumo admitem até quatro casas decimais, para permitir gramas e mililitros. | R | `movimento_estoque`, `ficha_tecnica_item` | CK | HIP | N |
| RN-GER-005 | Datas e horas são armazenadas com fuso horário; a operação ocorre em `America/Sao_Paulo`. | R | todas com data/hora | MOD | HIP | N |
| **RN-GER-006** ⚠⚠ | **O dia operacional não coincide com o dia civil: começa às 04h00 e termina às 03h59 do dia seguinte. Uma venda registrada à 01h30 de domingo pertence ao dia operacional de sábado.** | D C | `pedido`, `caixa_diario`, `dim_tempo` | GEN VW PAR | HIP | N |
| RN-GER-007 | Dados pessoais de cliente só são armazenados mediante consentimento registrado, com data e finalidade. | R | `cliente`, `consentimento` | MOD NN | HIP | N |
| RN-GER-008 | O cliente pode solicitar a anonimização dos seus dados. O histórico transacional é preservado, desvinculado da identificação pessoal. | A | `cliente`, `pedido` | APP TRG | HIP | M |
| RN-GER-009 ⚠ | A operação ocorre em uma única unidade, mas todo registro transacional é vinculado a uma unidade para permitir expansão futura. **Única exceção ao recorte: o usuário de plataforma (`RN-GER-014`).** | F | todas transacionais | FK | HIP | N |
| RN-GER-010 | Todo usuário do sistema pertence a um perfil, e o perfil determina quais operações ele pode realizar. | F | `usuario`, `perfil`, `permissao` | MOD APP | HIP | N |
| RN-GER-011 `LEV` | Operações sensíveis — cancelamento, desconto, ajuste de estoque, fechamento de caixa — registram usuário, data/hora, operação e os valores anterior e posterior. | A | `log_auditoria` | TRG APP | HIP | N |
| RN-GER-012 `LEV` | Custo, CMV e margem são visíveis apenas a perfis de gerência. A restrição é de dado: a API não retorna o campo para perfil sem permissão, em vez de ocultá-lo na tela. | R | `produto`, `ficha_tecnica`, `movimento_estoque` | APP VW | HIP | N |
| RN-GER-013 `IMP` | Documentos e telefones são armazenados **apenas com dígitos**, sem máscara de formatação. A máscara é apresentação. Sem isso, o mesmo telefone digitado como `(11) 98765-4321` e `11987654321` geraria dois clientes e `RN-CRM-001` deixaria de valer. | R | `unidade`, `cliente`, `colaborador`, `fornecedor` | CK APP | HIP | N |

### Acesso de plataforma — suporte ao restaurante

> Decisão de produto do time, 17/08/2026. O GrelhaHub é vendido a restaurantes, e quem o vende
> precisa dar suporte sem pedir a senha do cliente.

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-GER-014 `DEC` ⚠⚠ | Existe um **usuário de plataforma** (suporte do GrelhaHub) que **não pertence a nenhuma unidade** e **enxerga todas**. Para ele, e só para ele, o recorte por unidade de `RN-GER-009` não se aplica — é o que permite suporte e relatório agregado entre restaurantes. Ele não é usuário do restaurante: é de quem opera o produto. | F R | `usuario`, `unidade` | APP | **DEC** | N |
| RN-GER-015 `DEC` ⚠ | **Toda leitura** feita por usuário de plataforma sobre dado de unidade é registrada, com usuário, instante e operação. **Toda escrita** exige, além do registro, uma **sessão de suporte aberta com motivo**. Ler para diagnosticar é rotina de suporte; alterar o dado de um cliente sem dizer por quê, não. | R A | `sessao_suporte`, `log_auditoria`, `usuario` | APP MOD NN | **DEC** | N |
| RN-GER-016 `DEC` | Usuário de plataforma é criado **apenas** por outro usuário de plataforma ou pela carga inicial. O administrador do restaurante não pode criá-lo, alterá-lo, nem vê-lo em listagem alguma. | R | `usuario` | APP VW | **DEC** | N |
| RN-GER-030 `IMP` | **A plataforma administra os seus próprios usuários.** Um usuário de plataforma **cria outro** (é o caminho que `RN-GER-016` já prevê e que não existia) e **vê a lista** deles — sem a lista, o usuário criado desapareceria da tela, porque `RN-GER-016` o esconde de toda listagem de restaurante. O criado nasce **sem unidade, sem perfil e sem CPF** (`RN-GER-014`, `RN-GER-023`) e com **troca de senha obrigatória no primeiro acesso** (`RF-SEG-005`): a senha inicial foi escolhida por outra pessoa. **Não exige sessão de suporte** (`RN-GER-015`) — nenhum dado de cliente é tocado, e a sessão é aberta *para uma unidade*: exigi-la aqui não tornaria a operação mais controlada, tornaria-a impossível de executar. Mesma linha comercial × suporte de `RN-GER-029`. | F R | `usuario` | APP | HIP | N |
| RN-GER-031 `IMP` ⚠ | **A instalação nunca fica sem acesso de suporte.** Usuário de plataforma é **inativado**, nunca excluído (`RN-GER-002`), por outro usuário de plataforma; **ninguém inativa a si mesmo**, e o sistema **recusa inativar o último** usuário de plataforma ativo. Reativar devolve o acesso sem restaurar nada, porque nada foi removido. Sem esta regra um clique deixaria o produto sem suporte **e sem caminho de volta**: o acesso de plataforma não se recria pela interface — só pela carga inicial, que por sua vez não sobrescreve usuário existente (`RN-GER-016`). ⚠ *Não é hipótese distante: em 17/08/2026 esta instalação passou a existir sem nenhum usuário de plataforma, por variável de ambiente não repassada, e a recuperação exigiu acesso ao servidor.* | R | `usuario` | APP | HIP | N |

### Modularidade comercial

> Decisão de produto do time, 17/08/2026. Os dez módulos são vendidos separadamente: o
> restaurante contrata o que usa, e o que não contratou não aparece nem responde.

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-GER-017 `DEC` ⚠ | Cada unidade tem um conjunto de **módulos habilitados**. Operação que pertença a módulo não habilitado é **recusada**, ainda que o perfil do usuário tenha a permissão — módulo não contratado não é questão de permissão. | R | `modulo`, `unidade_modulo` | MOD APP | **DEC** | N |
| RN-GER-018 `DEC` | Desabilitar módulo **não apaga dado** (`RN-GER-002`): o dado transacional permanece e volta a ficar acessível se o módulo for reabilitado. Desabilitar é interromper o acesso, nunca o histórico. | R | `unidade_modulo` | MOD | **DEC** | N |
| RN-GER-019 `DEC` | O módulo **GER** é núcleo e está sempre habilitado. Não existe unidade operando sem autenticação, parâmetro e auditoria. | R | `modulo`, `unidade_modulo` | CK APP | **DEC** | N |
| RN-GER-020 `DEC` | Módulo só é habilitado se **as suas dependências** estiverem habilitadas, e a desabilitação de um módulo do qual outro depende é **recusada** enquanto o dependente estiver habilitado. Vender VEN sem GER, ou EST sem PRO, produziria um sistema que aceita o pedido e não sabe o que baixar. | R | `modulo`, `modulo_dependencia`, `unidade_modulo` | MOD APP | **DEC** | N |
| RN-GER-021 `DEC` | A habilitação de um módulo tem **vigência** (`[inicio, fim)`), pelo mesmo desenho de `parametro_valor`. É o que permite responder "o que esta unidade tinha contratado em março" — pergunta de cobrança, não de operação. | F | `unidade_modulo` | MOD EX | **DEC** | N |
| RN-GER-022 `DEC` | Existe o perfil **Operador**, de **visualização simplista**: ele consulta o que precisa para trabalhar e não altera nada. ⚠ **Conjunto de permissões provisório** — o time decidiu que o perfil existe, não ainda o que exatamente ele vê. Semeado com o mínimo defensável (consulta de pedido e de cardápio) até ser especificado; errar para menos gera pedido de acesso, errar para mais entrega capacidade que ninguém autorizou. | F | `perfil`, `permissao` | MOD APP | **DEC** | N |

### Identificação da pessoa e delegação de acesso

> Decisão de produto do time, 17/08/2026. O acesso deixa de ser "um nome e um e-mail": a
> pessoa é identificada, e quem cria usuário não pode criar alguém mais poderoso que si mesmo.

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-GER-023 `DEC` | Todo usuário é identificado por **nome**, **sobrenome** e **CPF**. O CPF é **único em todo o sistema**, guardado apenas com os 11 dígitos (`RN-GER-013`) e validado pelos **dois dígitos verificadores** antes de ser aceito. Sequência repetida (`111.111.111-11`) é recusada, ainda que passe no cálculo. **Exceção declarada:** o usuário de plataforma (`RN-GER-014`) não tem CPF — ele não é pessoa física de nenhum restaurante. | R F | `usuario` | UQ CK APP | **DEC** | S |
| RN-GER-024 `DEC` ⚠⚠ | **Ninguém concede acesso que não possui.** A criação de usuário é limitada por uma **matriz de delegação**: cada perfil declara quais perfis ele pode atribuir. O **Administrador** atribui qualquer perfil; o **Gerente** atribui apenas **Operador, Atendente, Cozinha e Entregador**. Sem esta regra, `usuario.gerenciar` no Gerente seria escalação de privilégio: ele criaria um Administrador e passaria a ser um. | R | `perfil`, `usuario` | APP | **DEC** | S |
| RN-GER-025 `DEC` | O **Gerente** administra os usuários da **própria unidade** — cria, desativa e redefine senha —, limitado por `RN-GER-024`. O vínculo com a unidade continua sendo do sistema (`RN-GER-009`): o gerente não escolhe a unidade do usuário que cria. | R F | `usuario`, `perfil_permissao` | APP | **DEC** | S |
| RN-GER-026 `DEC` | A unidade pode ter uma **logomarca**, exibida no cardápio público (`RN-VEN-037`). Vale o mesmo domínio de arquivo de `RN-VEN-042`: JPEG ou PNG, até 5 MB, formato conferido pelo **conteúdo** do arquivo. Unidade sem logo exibe o nome fantasia — a ausência é estado válido, não erro. | F | `unidade` | APP CK | **DEC** | S |
| RN-GER-027 `DEC` ⚠ | **Unidade inativa não autentica ninguém.** ⚠ *A checagem já existia em `Autenticar` desde a `SPEC-GER-004`, sem regra que a enunciasse — e sem operação que a alcançasse (ver `RN-GER-029`).* Nenhum usuário vinculado a uma unidade inativa consegue entrar, qualquer que seja o perfil. A mensagem ao usuário é **genérica e idêntica** à de credencial inválida — ela não diz que a loja foi desativada —, acompanhada de um **código de suporte** que identifica o motivo para quem atende. Dizer "a sua loja está desativada" na tela de login informaria a situação comercial de um cliente a quem só tem o e-mail dele. | R | `unidade`, `usuario` | APP | **DEC** | S |
| RN-GER-028 `DEC` | Quem administra usuários (`usuario.gerenciar`) **altera o cadastro** — nome, sobrenome, CPF e e-mail — e **redefine a senha** dos usuários da sua unidade, limitado pela matriz de delegação (`RN-GER-024`). Senha redefinida por outra pessoa **reativa a troca obrigatória** no próximo acesso (`RF-SEG-005`): a senha que um terceiro conhece não é senha. Alteração de CPF e de e-mail continua sujeita à unicidade global (`RN-GER-023`, `SPEC-GER-004`). | A R | `usuario` | APP UQ | **DEC** | S |
| RN-GER-029 `DEC` | **Inativar unidade é ato da plataforma**, com justificativa obrigatória, e **não apaga dado** (`RN-GER-002`): o cardápio público para de responder, os usuários param de autenticar (`RN-GER-027`) e todo o histórico permanece. Reativar devolve o acesso sem restaurar nada, porque nada foi removido. | A R | `unidade` | APP | **DEC** | S |

> ### ⚠⚠ RN-GER-006 — a regra mais subestimada do catálogo
>
> Se o modelo usar `date_trunc('day', dh_criacao)`, o faturamento de sábado à noite cai em
> domingo, o fechamento de caixa não bate e o relatório de melhor dia da semana fica errado
> em definitivo. **Precisa estar decidida antes do modelo lógico.**
>
> Decisão registrada em [ADR-0011](adr/0011-dia-operacional.md): coluna gerada
> `data_operacional` em toda tabela com data de evento comercial, derivada de
> `(dh_evento - PAR-GER-001)::date`, com índice — nunca calculada na consulta.

---

## VEN — Vendas e Comercial

**41 regras** · prioridade de validação **MÁXIMA**

### Estrutura do pedido

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-001 | Todo pedido pertence a exatamente um canal de venda: salão, balcão, delivery próprio ou marketplace. | F | `pedido`, `canal_venda` | FK NN | HIP | N |
| RN-VEN-002 | Todo pedido tem ao menos um item. Um pedido sem itens não pode ser confirmado. | R | `pedido`, `item_pedido` | TRG APP | HIP | N |
| RN-VEN-003 | Todo item de pedido refere-se a exatamente um produto do cardápio. | F | `item_pedido`, `produto` | FK NN | HIP | N |
| RN-VEN-004 | Pedido do canal salão está vinculado a uma comanda; pedido de balcão, delivery e marketplace não usa comanda. | F | `pedido`, `comanda` | MOD CK | HIP | N |
| RN-VEN-005 | Uma comanda está vinculada a zero ou uma mesa. Comanda de balcão não tem mesa. | F | `comanda`, `mesa` | FK | HIP | N |
| RN-VEN-006 ⚠ | Uma mesa tem no máximo uma comanda aberta por vez. | R | `comanda`, `mesa` | EX | HIP | N |
| RN-VEN-007 | Uma comanda pode ser transferida de uma mesa para outra, e duas comandas podem ser unificadas quando os clientes juntam mesas. | A | `comanda`, `mesa` | APP TRG | HIP | N |

### Preço e valores

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-008 ⚠ | O preço praticado em um item de pedido é registrado no momento da venda e não muda se o preço de tabela do produto for alterado depois. | R | `item_pedido`, `preco_produto` | MOD NN | HIP | N |
| RN-VEN-009 | O valor total de um item é a quantidade multiplicada pelo preço praticado. | C | `item_pedido` | GEN | HIP | N |
| RN-VEN-010 ⚠ | Um produto tem um preço de tabela vigente por período; a alteração de preço cria uma nova vigência e preserva a anterior. | F | `produto`, `preco_produto` | MOD CK | HIP | N |
| RN-VEN-011 | A quantidade de um item de pedido é sempre maior que zero. | R | `item_pedido` | CK | HIP | N |
| RN-VEN-012 | O preço praticado pode ser inferior ao preço de tabela mediante desconto, que exige registro de motivo. | R | `item_pedido` | CK APP | HIP | N |
| RN-VEN-013 | Desconto acima do limite parametrizado (`PAR-VEN-001`) exige autorização de um usuário com perfil de gerência, registrada no pedido. | R A | `pedido`, `autorizacao` | APP PAR | HIP | N |
| RN-VEN-014 | Sobre o consumo no salão incide taxa de serviço opcional (`PAR-VEN-002`), que não incide sobre delivery nem sobre a taxa de entrega. | C R | `comanda`, `pedido` | GEN PAR | HIP | N |
| RN-VEN-039 `IMP` | O valor total de um pedido é a soma dos valores totais dos seus itens, antes de desconto (`RN-VEN-012`), taxa de serviço (`RN-VEN-014`) e taxa de entrega (`RN-LOG-003`). | C | `pedido`, `item_pedido` | GEN APP | HIP | N |

### Composição do item

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-015 | Um item de pedido pode ter adicionais e remoções — por exemplo, mais bacon ou sem cebola. | F | `item_pedido`, `item_pedido_ajuste` | MOD | HIP | N |
| RN-VEN-016 ⚠ | Adicional e remoção alteram tanto o preço do item quanto o consumo de insumos daquele item. | C A | `item_pedido_ajuste`, `ficha_tecnica` | TRG APP | HIP | N |
| RN-VEN-017 | Um combo é um produto que agrupa outros produtos e tem preço próprio, distinto da soma dos componentes. | F | `produto`, `produto_composicao` | MOD | HIP | N |

### Ciclo de vida e disponibilidade

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-018 ⚠ | O pedido percorre os estados: **Aberto → Confirmado → Em produção → Pronto → Entregue → Finalizado**. De qualquer estado anterior a Finalizado pode ir para **Cancelado**. | F R | `pedido`, `status_pedido` | ENU TRG | HIP | N |
| RN-VEN-019 | Um pedido Finalizado ou Cancelado não retorna a nenhum estado anterior. | R | `pedido` | TRG | HIP | N |
| RN-VEN-040 `IMP` | Um pedido aceita inclusão, alteração e remoção de itens apenas nos estados **Aberto** e **Confirmado**. A partir de **Em produção** a composição está congelada: o insumo já foi baixado e o item já está sendo preparado. | R | `pedido`, `item_pedido` | TRG APP | HIP | N |
| RN-VEN-041 `DEC` ⚠ | O **administrador da unidade** pode **excluir logicamente** um pedido, com motivo obrigatório. A exclusão lógica **não apaga o registro** (`RN-GER-002`): marca-o, guarda quem excluiu, quando e por quê, e o retira de toda consulta e apuração. **É distinta de cancelamento:** cancelar é fato de negócio — o cliente desistiu, e isso é métrica; excluir é reconhecer que o pedido **nunca deveria ter existido** (lançamento em duplicidade, teste, erro de operação), e por isso ele não pode poluir a taxa de cancelamento. | A R | `pedido` | MOD APP VW | **DEC** | N |
| RN-VEN-020 | Um produto inativo não pode ser lançado em um novo pedido, mas permanece nos pedidos históricos. | R | `produto`, `item_pedido` | APP FK | HIP | N |
| RN-VEN-021 | Um produto só pode ser lançado se houver saldo de todos os insumos da sua ficha técnica vigente na quantidade requerida. **O comportamento na falta de saldo é parametrizável por produto (`PAR-VEN-003`): bloquear, avisar ou permitir.** | R A | `produto`, `ficha_tecnica`, `lote` | VW APP PAR | HIP | N |
| RN-VEN-035 `DEC` ⚠ | **A falta de saldo de qualquer insumo da ficha técnica sempre gera alerta visível na tela de lançamento**, identificando o insumo e a quantidade faltante. O alerta é exibido nos três modos de `PAR-VEN-003` — o parâmetro decide se o lançamento é recusado, exige confirmação ou apenas informa, **nunca se o operador é avisado**. | R A | `produto`, `ficha_tecnica`, `lote` | APP VW | **DEC** | N |
| RN-VEN-022 | Um produto disponível apenas em determinada faixa de horário não pode ser lançado fora dela. | R | `produto`, `disponibilidade_produto` | APP PAR | HIP | 2 |

### Pagamento

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-023 | A conta de uma comanda pode ser dividida em múltiplos pagamentos, com formas diferentes. | F | `comanda`, `pagamento` | MOD | HIP | N |
| RN-VEN-024 | Uma comanda só é fechada quando a soma dos pagamentos iguala o seu valor total. | R | `comanda`, `pagamento` | TRG | HIP | N |
| RN-VEN-025 | Cada forma de pagamento tem taxa de administração e prazo de recebimento próprios. | F | `forma_pagamento` | MOD | HIP | N |
| RN-VEN-026 | Pedido de marketplace já entra no sistema como pago; o valor a receber é líquido da comissão do canal. | F C | `pedido`, `canal_venda` | MOD TRG | HIP | M |

### Complementos incorporados no cruzamento

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-027 `LEV` ⚠ | Os ajustes de um produto organizam-se em **grupos de modificadores** com seleção mínima e máxima. Quando o mínimo é maior ou igual a 1, a seleção é obrigatória para lançar o item. | R F | `grupo_modificador`, `opcao_modificador`, `produto_grupo_modificador` | MOD CK APP | HIP | N |
| RN-VEN-028 `LEV` | O valor de um item é `(preço praticado + soma dos ajustes selecionados) × quantidade`. Um ajuste pode ter acréscimo positivo, zero ou negativo. | C | `item_pedido`, `item_pedido_ajuste` | GEN | HIP | N |
| RN-VEN-029 `LEV` | O valor total de um pedido nunca é negativo, ainda que desconto e cupom somados excedam o consumo. | R | `pedido` | CK | HIP | N |
| RN-VEN-030 `LEV` | Em pagamento em espécie, o valor recebido pode exceder o total do pedido; a diferença é registrada como troco e não integra a receita. | C R | `pagamento` | GEN CK | HIP | N |
| RN-VEN-031 `LEV` | Comanda fechada não aceita novos itens. A reabertura exige perfil de gerência e fica registrada. | R | `comanda` | TRG APP | HIP | N |
| RN-VEN-032 `LEV` | O preço de tabela vigente de um produto é sempre maior que zero. | R | `preco_produto` | CK | HIP | N |
| RN-VEN-033 `LEV` | A transferência de comanda exige que a mesa de destino esteja livre. | R | `comanda`, `mesa` | TRG | HIP | N |
| RN-VEN-034 `LEV` | Na divisão de conta por número de pessoas, a diferença de arredondamento é somada à primeira parcela, de modo que a soma das partes iguale exatamente o total. | C | `comanda`, `pagamento` | APP | HIP | N |

### Cardápio público

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-036 `DEC` | Cada unidade possui um **identificador público** (`slug`) único em todo o sistema, imutável após a criação, que compõe o endereço do seu cardápio online. | F R | `unidade` | UQ CK | **DEC** | N |
| RN-VEN-037 `DEC` | O cardápio público é acessível **sem autenticação** e expõe apenas produtos **ativos e disponíveis** da unidade do `slug`. Não expõe custo, CMV, margem (`RN-GER-012`) nem qualquer dado de outra unidade. | R | `unidade`, `produto`, `categoria` | APP VW | **DEC** | N |
| RN-VEN-038 `DEC` | Pedido originado do cardápio público entra no canal **delivery próprio**, no estado **Aberto**, e exige nome e telefone do cliente (`RN-CRM-001`, `RN-CRM-003`). Ele **não é confirmado automaticamente** — a confirmação é ato do restaurante, e é ela que dispara baixa de estoque e ordem de produção (`RN-VEN-018`). | R A | `pedido`, `cliente`, `canal_venda` | ENU APP | **DEC** | N |
| RN-VEN-042 `DEC` | O produto pode ter **uma imagem**, exibida no cardápio público. Aceita **JPEG e PNG**, até **5 MB**; qualquer outro formato é recusado. **O formato é conferido pelo conteúdo do arquivo, não pela extensão nem pelo `Content-Type`** — os dois são informados por quem envia, e quem envia o cardápio público é o restaurante, mas quem envia um arquivo renomeado é um atacante. Produto sem imagem é estado válido. | F R | `produto` | APP CK | **DEC** | S |

### Cadastro do cardápio

> As três regras abaixo nasceram ao escrever a `SPEC-VEN-003`, e **duas delas já estavam
> implementadas sem regra que as enunciasse** — `uq_categoria_nome`, `uq_produto_nome` e
> `ck_produto_preco` existem no banco desde a migration `Fundacao`. Comportamento sem regra é o
> inverso do defeito habitual e igualmente grave: ninguém sabe se é decisão ou acidente, e a
> primeira entrevista não tem o que validar.

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-VEN-043 `IMP` | A **categoria** organiza o cardápio: tem nome **único na unidade** e uma **ordem de exibição**, que é a ordem em que o cliente vê os grupos. **Categoria inativa esconde os seus produtos do cardápio público sem inativá-los** — o produto continua cadastrado e volta junto quando a categoria voltar. É a vitrine que muda, não o cadastro. | F R | `categoria`, `produto` | UQ APP | **HIP** | S |
| RN-VEN-044 `IMP` ⚠ | O **produto** tem nome único na unidade e preço maior que zero (`RN-VEN-032`). Ele carrega **dois estados independentes**, e confundi-los é o erro previsível: **`ativo`** é cadastral — o produto saiu do cardápio e não volta nesta temporada (`RN-VEN-020`); **`disponível`** é do turno — "acabou hoje", e amanhã volta. Inativar exige decisão de quem cadastra; marcar como indisponível é operação de quem está no salão. | F R | `produto` | UQ CK APP | **HIP** | S |
| RN-VEN-045 `IMP` | **Categoria e produto não são excluídos, apenas inativados.** Produto excluído levaria consigo o item de pedidos históricos (`RN-VEN-020`), e categoria excluída deixaria produto órfão. `RN-GER-002` não cobre estes dois: ele fala de registro **transacional**, e cadastro é outra coisa — por isso a regra é própria. | R | `categoria`, `produto`, `item_pedido` | FK APP | **HIP** | S |
| RN-VEN-046 `IMP` | Cada categoria aceita no máximo **`PAR-VEN-004` produtos ativos** — 50 por padrão, ajustável por unidade. O limite conta **apenas os ativos**: como `RN-VEN-045` proíbe exclusão, contar o histórico faria a categoria entupir para sempre a cada produto retirado. Recusar não é proteção de banco — é a leitura do cliente: cardápio com centenas de itens numa seção não é cardápio, é catálogo, e quem escolhe desiste antes. | F R | `produto`, `categoria` | APP PAR | **HIP** | S |

---

## PRO — Produção

**13 regras** · prioridade de validação **MÁXIMA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-PRO-001 ⚠ | Todo produto vendável tem exatamente uma ficha técnica vigente em cada data. | R F | `produto`, `ficha_tecnica` | EX TRG | HIP | N |
| RN-PRO-002 ⚠ | A ficha técnica é versionada. Criar uma nova versão encerra a vigência da anterior e não altera o custo dos pedidos já realizados. | R | `ficha_tecnica` | MOD CK | HIP | N |
| RN-PRO-003 | A ficha técnica relaciona um produto aos insumos consumidos, com quantidade e unidade de medida. | F | `ficha_tecnica_item`, `insumo` | MOD | HIP | N |
| **RN-PRO-004** ⚠⚠ | **Uma ficha técnica pode conter uma sub-receita — um produto intermediário que possui ficha técnica própria e é estocado, como o molho da casa.** | F | `ficha_tecnica_item`, `produto` | MOD FK | HIP | N |
| RN-PRO-005 ⚠ | Cada insumo na ficha técnica tem percentual de perda de preparo, que representa o descarte inerente ao processamento — casca, aparas. | F C | `ficha_tecnica_item` | CK | HIP | N |
| RN-PRO-006 | A quantidade bruta consumida de um insumo é a quantidade líquida da ficha dividida por `(1 − percentual de perda)`. | C | `ficha_tecnica_item` | GEN VW | HIP | N |
| RN-PRO-007 | A ficha técnica declara um rendimento: quantas porções são produzidas por execução. | F | `ficha_tecnica` | CK | HIP | N |
| RN-PRO-008 | A confirmação de um pedido gera uma ordem de produção para cada item que exija preparo. | A | `pedido`, `ordem_producao` | TRG APP | HIP | N |
| RN-PRO-009 | Cada ordem de produção é roteada para uma estação de trabalho: chapa, fritadeira, montagem ou bebidas. | F | `ordem_producao`, `estacao` | FK | HIP | N |
| RN-PRO-010 | Cada etapa de preparo registra o instante de início e de conclusão, permitindo apurar o tempo efetivo. | F C | `etapa_preparo` | MOD GEN | HIP | N |
| RN-PRO-011 | Um item só é marcado como Pronto quando todas as suas etapas de preparo estiverem concluídas. | R | `ordem_producao`, `etapa_preparo` | TRG | HIP | N |
| RN-PRO-012 | A produção antecipada de sub-receita ocorre sem pedido vinculado e gera entrada de estoque do produto intermediário. | A F | `ordem_producao`, `movimento_estoque` | TRG | HIP | N |
| RN-PRO-013 `LEV` | O roteamento da ordem de produção é determinado pela categoria do produto, que define a estação padrão, admitindo sobreposição por produto. | F | `categoria`, `produto`, `estacao` | FK MOD | HIP | N |

> ### ⚠⚠ RN-PRO-004 — a regra que mais quebra modelos ingênuos
>
> Uma ficha técnica que só aponta para insumo não representa o molho da casa, que é
> **produto e insumo ao mesmo tempo**. A modelagem precisa admitir recursividade.
> Descobrir isso na Sprint 7 custa uma sprint inteira; descobrir agora custa uma conversa.
>
> Implicação para o modelo: `ficha_tecnica_item` referencia um **item estocável**, que pode
> ser `insumo` ou `produto` — o que exige ou herança/especialização, ou uma entidade
> unificadora. A decisão vai para a Sprint 3 & 4, e precisa de proteção contra ciclo
> (uma ficha não pode conter a si mesma, direta ou indiretamente).

---

## EST — Estoque

**20 regras** · prioridade de validação **MÁXIMA**

### Princípio estrutural

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-EST-001 ⚠ | O saldo de um insumo nunca é um valor armazenado e editável. É sempre derivado da soma algébrica dos seus movimentos. | C R | `movimento_estoque` | VW MOD | HIP | N |
| RN-EST-002 | Todo movimento de estoque tem um tipo: entrada por compra, saída por venda, saída por produção, entrada por produção, perda, ajuste de inventário, transferência ou devolução. | F | `movimento_estoque`, `tipo_movimento` | ENU NN | HIP | N |
| RN-EST-003 | Todo movimento de estoque registra quem o realizou e quando. | R | `movimento_estoque` | NN | HIP | N |

### Lote e validade

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-EST-004 ⚠ | Todo movimento de estoque refere-se a um lote específico do insumo. | F | `movimento_estoque`, `lote` | FK NN | HIP | N |
| RN-EST-005 | Insumo perecível exige lote com data de validade informada. Insumo não perecível admite lote sem validade. | R | `lote`, `insumo` | CK | HIP | N |
| RN-EST-006 | A data de validade de um lote é posterior à sua data de entrada. | R | `lote` | CK | HIP | N |
| RN-EST-007 ⚠ | A saída de estoque consome prioritariamente o lote de menor data de validade entre os disponíveis — critério **FEFO**. | A | `lote`, `movimento_estoque` | TRG VW | HIP | N |
| RN-EST-008 | Não se aceita entrada de insumo cujo prazo restante de validade seja inferior ao mínimo parametrizado (`PAR-EST-001`). | R | `lote`, `recebimento` | APP PAR | HIP | M |

### Saldo e custo

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-EST-009 | O saldo de um lote nunca é negativo. | R | `movimento_estoque` | TRG | HIP | N |
| RN-EST-010 | Toda entrada de estoque registra o custo unitário de aquisição do lote. | R | `lote`, `movimento_estoque` | NN CK | HIP | N |
| RN-EST-011 ⚠ | O custo de uma saída é o custo do lote efetivamente consumido — método do **custo específico por lote**. | C | `movimento_estoque`, `lote` | TRG | HIP | N |
| RN-EST-012 ⚠ | Um insumo pode ser comprado em uma unidade de medida e consumido em outra; o fator de conversão é definido por insumo. | F C | `insumo`, `unidade_medida`, `conversao_unidade` | MOD GEN | HIP | N |

### Operações

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-EST-013 | Toda perda registra um motivo obrigatório: vencimento, quebra, queima, degustação, furto ou devolução ao fornecedor. | R | `movimento_estoque`, `motivo_perda` | ENU NN | HIP | N |
| RN-EST-014 | A contagem de inventário gera movimento de ajuste correspondente à diferença apurada, com justificativa obrigatória. | A R | `inventario`, `movimento_estoque` | TRG NN | HIP | N |
| RN-EST-015 | Uma transferência entre locais de estoque gera dois movimentos — saída na origem e entrada no destino — na mesma transação. | A R | `movimento_estoque`, `local_estoque` | TRG | HIP | M |
| RN-EST-016 | Cada insumo tem estoque mínimo e ponto de pedido definidos, usados para disparar a sugestão de compra. | F | `insumo` | MOD CK | HIP | N |
| RN-EST-017 | Embalagens e descartáveis são insumos e integram a ficha técnica dos produtos vendidos no canal delivery. | F | `insumo`, `ficha_tecnica_item` | MOD | HIP | N |
| RN-EST-018 ⚠ | Item cancelado **após** entrar em produção **não** devolve insumo ao estoque; gera perda com motivo de cancelamento. | A R | `item_pedido`, `movimento_estoque` | TRG | HIP | N |
| RN-EST-019 | Item cancelado **antes** de entrar em produção não gera nenhum movimento de estoque. | R | `item_pedido`, `movimento_estoque` | TRG | HIP | N |
| RN-EST-020 `LEV` | Insumo com movimento registrado não pode ser excluído, apenas inativado. | R | `insumo`, `movimento_estoque` | TRG APP | HIP | N |

### Gatilho da baixa pela venda

> **Estas três regras fecham uma lacuna do catálogo, não uma decisão nova.** `RN-VEN-018` — uma das
> sete estruturais — determina que **a baixa de estoque dispara na Confirmação do pedido**. Nenhuma
> regra do módulo EST enunciava esse gatilho do lado do estoque, e implementar a baixa sem ele
> seria fazer a regra nascer no código. A lacuna foi encontrada na auditoria de rastreabilidade de
> 17/08/2026.

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-EST-021 `IMP` ⚠⚠ | A **confirmação** do pedido (`RN-VEN-018`) gera os movimentos de **saída por venda** dos insumos de todos os seus itens. Para cada item, a ficha técnica do produto é explodida **recursivamente** até os insumos folha (`RN-PRO-004`), e a quantidade de cada insumo é a da ficha multiplicada pela quantidade do item (`RN-VEN-011`), convertida quando a unidade de consumo difere da de compra (`RN-EST-012`). Cada movimento refere-se a **um lote** (`RN-EST-004`), escolhido por **FEFO** (`RN-EST-007`), e carrega o custo do lote consumido (`RN-EST-011`). **Os movimentos e a mudança de estado são gravados na mesma transação:** pedido confirmado sem baixa, ou baixa sem pedido confirmado, deixariam o saldo mentindo — e o saldo é derivado dos movimentos (`RN-EST-001`), então não há como corrigi-lo por fora. | A R | `pedido`, `item_pedido`, `ficha_tecnica`, `movimento_estoque`, `lote` | TRG APP | **HIP** | S |
| RN-EST-022 `IMP` | **Nenhum item gera baixa mais de uma vez.** O conjunto de movimentos de saída por venda é único por item de pedido: reentrada no mesmo estado, repetição de requisição ou execução concorrente da confirmação não duplicam consumo. Sem isto, duas confirmações do mesmo pedido consomem o insumo duas vezes e o saldo passa a acusar falta do que está na geladeira. | R | `item_pedido`, `movimento_estoque` | UQ APP | **HIP** | S |
| RN-EST-023 `IMP` ⚠ | Item cancelado ou excluído **depois da confirmação e antes de entrar em produção** gera movimento de **devolução** (`RN-EST-002`) da quantidade consumida, **ao mesmo lote de origem** — devolver a outro lote quebraria o custo específico (`RN-EST-011`) e a ordem FEFO. **⚠ Esta regra revisa `RN-EST-019`,** cujo texto ("não gera nenhum movimento") só é verdadeiro se a baixa ocorresse na produção, e não na confirmação como `RN-VEN-018` determina. Ver o conflito **C10** em §17. | A R | `item_pedido`, `movimento_estoque`, `lote` | TRG APP | **HIP** | S |

> #### O que estas regras deliberadamente **não** decidem
>
> **Saldo insuficiente na confirmação.** Quem decide é `RN-VEN-021` com `PAR-VEN-003`, e
> `RN-VEN-035` garante que o operador é avisado nos três modos. Mas resta uma ambiguidade real:
> `RN-EST-009` diz que **o saldo de um lote nunca é negativo**, então o modo *permitir* de
> `PAR-VEN-003` não pode significar saldo negativo. Ele significa confirmar sem baixar, baixar só
> o que existe, ou baixar contra lote a ser recebido? **Não está decidido, e inventar aqui seria
> exatamente o erro que este catálogo existe para evitar** — está na fila de validação.
>
> **Produto sem ficha técnica.** `RN-PRO-001` trata do custo; o efeito sobre a baixa (não consome
> nada? recusa a confirmação?) depende dele e entra com a onda 2.

> ### RN-EST-001 e o desempenho da consulta de saldo
>
> Saldo derivado é o princípio; agregar todo o histórico a cada tela é inviável.
> A conciliação está em [ADR-0008](adr/0008-estoque-como-livro-razao.md): o saldo materializa
> em **view materializada** por `(unidade, insumo, lote)`, atualizada na transação do
> movimento. **Nunca uma coluna editável** — a distinção não é de desempenho, é de
> autoridade: a view é derivada e recomputável; uma coluna editável é uma segunda fonte de
> verdade que pode divergir sem que ninguém perceba.

---

## COM — Compras

**9 regras** · prioridade de validação **ALTA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-COM-001 | Todo pedido de compra é dirigido a exatamente um fornecedor. | F | `pedido_compra`, `fornecedor` | FK NN | HIP | M |
| RN-COM-002 | Cada fornecedor tem valor de pedido mínimo e prazo de entrega em dias. | F | `fornecedor` | MOD | HIP | M |
| RN-COM-003 | Um pedido de compra abaixo do mínimo do fornecedor não pode ser emitido. | R | `pedido_compra`, `fornecedor` | TRG APP | HIP | M |
| RN-COM-004 | Um mesmo insumo pode ser fornecido por vários fornecedores, com preço e código próprios de cada um. | F | `insumo_fornecedor` | MOD UQ | HIP | M |
| RN-COM-005 | Compras acima do valor parametrizado (`PAR-COM-001`) exigem cotação de ao menos dois fornecedores. | R | `cotacao`, `pedido_compra` | APP PAR | HIP | M |
| RN-COM-006 | O recebimento de um pedido de compra pode ser parcial; a quantidade não recebida permanece pendente. | F | `recebimento`, `pedido_compra_item` | MOD CK | HIP | M |
| RN-COM-007 | Divergência de preço ou quantidade entre o pedido e o recebimento gera ocorrência que exige aprovação para prosseguir. | A R | `recebimento`, `ocorrencia_recebimento` | APP | HIP | M |
| RN-COM-008 ⚠ | O recebimento gera simultaneamente a entrada de estoque e a conta a pagar ao fornecedor. | A | `recebimento`, `movimento_estoque`, `conta_pagar` | TRG | HIP | N |
| RN-COM-009 | A sugestão de compra de um insumo é a demanda prevista para o horizonte de reposição, convertida pela ficha técnica, menos o saldo disponível, mais o estoque de segurança (`PAR-EST-002`), arredondada para o múltiplo de compra do fornecedor. | C A | `insumo`, `ficha_tecnica`, `previsao_demanda` | VW PAR | HIP | 2 |

---

## FIN — Financeiro

**16 regras** · prioridade de validação **MÉDIA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-FIN-001 | Todo pedido finalizado gera lançamento de conta a receber, conforme a forma de pagamento utilizada. | A | `pedido`, `conta_receber` | TRG | HIP | N |
| RN-FIN-002 | O prazo de recebimento depende da forma de pagamento: dinheiro e PIX no mesmo dia, débito no dia seguinte, crédito conforme prazo do adquirente. | C F | `forma_pagamento`, `conta_receber` | MOD PAR | HIP | N |
| RN-FIN-003 ⚠ | A taxa da forma de pagamento é reconhecida como despesa no momento da **venda**, não no do recebimento. | A C | `pagamento`, `lancamento_financeiro` | TRG | HIP | N |
| RN-FIN-004 | A comissão do canal marketplace é despesa vinculada ao pedido e deduzida do valor a receber. | C | `pedido`, `canal_venda` | GEN TRG | HIP | M |
| RN-FIN-005 | Todo lançamento financeiro é classificado em uma conta do plano de contas e em um centro de custo. | R | `lancamento_financeiro`, `plano_de_contas`, `centro_de_custo` | FK NN | HIP | N |
| RN-FIN-006 | Todo recebimento e todo pagamento estão vinculados a uma conta bancária ou ao caixa da loja. | R | `lancamento_financeiro`, `conta_bancaria` | FK NN | HIP | N |
| RN-FIN-007 | O caixa é aberto com valor inicial informado e fechado com conferência; a diferença entre o esperado e o contado é registrada como sobra ou quebra de caixa. | A C | `caixa_diario` | GEN TRG | HIP | N |
| RN-FIN-008 | Sangria e suprimento são movimentações registradas no caixa aberto, com responsável e motivo. | F R | `movimento_caixa` | MOD NN | HIP | N |
| RN-FIN-009 | Um caixa não pode ser reaberto após o fechamento. | R | `caixa_diario` | TRG | HIP | N |
| RN-FIN-010 | Toda conta a pagar tem data de vencimento; o pagamento após o vencimento registra os acréscimos de juros e multa separadamente do principal. | F R | `conta_pagar` | MOD CK | HIP | M |
| RN-FIN-011 | Despesas fixas são rateadas entre centros de custo segundo critério definido por despesa. | C | `lancamento_financeiro`, `rateio` | VW | HIP | M |
| RN-FIN-012 ⚠ | O resultado gerencial (DRE) é sempre derivado dos lançamentos; nenhum valor de resultado é digitado diretamente. | C R | `lancamento_financeiro` | VW | HIP | N |
| RN-FIN-013 `LEV` | Uma sangria não pode exceder o saldo em espécie disponível no caixa. | R | `movimento_caixa` | TRG | HIP | N |
| RN-FIN-014 `LEV` | Existe no máximo um caixa aberto por operador por vez. | R | `caixa_diario` | EX | HIP | N |
| RN-FIN-015 `LEV` | O valor esperado no fechamento é `valor inicial + recebimentos em espécie + suprimentos − sangrias`. | C | `caixa_diario`, `movimento_caixa` | GEN VW | HIP | N |
| RN-FIN-016 `LEV` | Cada forma de pagamento declara se movimenta espécie. Apenas as que movimentam integram o saldo do caixa; as demais são registradas sem vínculo de caixa. | F R | `forma_pagamento`, `movimento_caixa` | MOD CK | HIP | N |

---

## CRM — Relacionamento com o cliente

**9 regras** · prioridade de validação **MÉDIA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-CRM-001 | O cliente é identificado unicamente pelo número de telefone. | R D | `cliente` | UQ | HIP | N |
| RN-CRM-002 | Um cliente pode ter vários endereços cadastrados, dos quais exatamente um é o principal. | F R | `cliente`, `endereco_cliente` | EX MOD | HIP | N |
| RN-CRM-003 | Pedido do canal delivery exige cliente identificado; pedido de salão e balcão admite cliente anônimo. | R | `pedido`, `cliente` | CK | HIP | N |
| RN-CRM-004 | Um cupom tem período de validade, tipo de benefício (valor fixo ou percentual), valor mínimo de pedido, limite total de usos e limite de usos por cliente. | F R | `cupom` | MOD CK | HIP | M |
| RN-CRM-005 | Cupons não são acumuláveis: um pedido admite no máximo um cupom. | R | `pedido`, `uso_cupom` | UQ | HIP | M |
| RN-CRM-006 | Todo uso de cupom é registrado com o pedido e o cliente, permitindo medir a efetividade da campanha. | F | `uso_cupom`, `campanha` | MOD | HIP | M |
| RN-CRM-007 | Um cliente é classificado nos segmentos RFM — Campeão, Fiel, Em risco, Hibernando, Perdido — a partir da recência, frequência e valor dos seus pedidos. | I | `cliente`, `pedido` | VW | HIP | N |
| RN-CRM-008 | Um cliente é considerado em risco quando o tempo desde o seu último pedido supera `PAR-CRM-001` vez o seu intervalo médio histórico entre pedidos. | I | `cliente`, `pedido` | VW PAR | HIP | N |
| RN-CRM-009 | A avaliação de um pedido é dada em nota de 1 a 5 e está vinculada a exatamente um pedido entregue. | F R | `avaliacao`, `pedido` | CK FK | HIP | 2 |

> **RN-CRM-007 e RN-CRM-008 são a matéria-prima do BI da Sprint 7 & 8.** São regras de
> *inferência* — derivam classificação de dados existentes, sem entrada manual. Marcadas
> como Núcleo por isso, ainda que o módulo CRM seja predominantemente Modelo.

---

## LOG — Logística e Frota

**9 regras** · prioridade de validação **MÉDIA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-LOG-001 | Todo pedido de delivery gera exatamente uma entrega. | F | `pedido`, `entrega` | FK UQ | HIP | N |
| RN-LOG-002 | Uma rota agrupa uma ou mais entregas atribuídas a um mesmo entregador em um mesmo trajeto. | F | `rota`, `entrega` | MOD | HIP | M |
| RN-LOG-003 | Cada zona de entrega define a taxa cobrada do cliente e o tempo estimado de entrega. | F | `zona_entrega` | MOD | HIP | N |
| RN-LOG-004 | Endereço fora de qualquer zona atendida não permite a confirmação do pedido de delivery. | R | `endereco_cliente`, `zona_entrega` | APP VW | HIP | N |
| RN-LOG-005 | O entregador é um colaborador ativo, associado a um veículo durante o turno. | F R | `entregador`, `colaborador`, `veiculo` | FK CK | HIP | M |
| RN-LOG-006 | A entrega percorre os estados: Aguardando → Em rota → Entregue, ou Não entregue com motivo obrigatório. | F R | `entrega`, `status_entrega` | ENU TRG | HIP | N |
| RN-LOG-007 ⚠ | A taxa de entrega cobrada do cliente e o valor pago ao entregador são grandezas distintas e registradas separadamente. | F | `entrega` | MOD | HIP | N |
| RN-LOG-008 | Todo evento relevante da entrega — saída, chegada, tentativa frustrada — é registrado com data e hora. | F | `evento_entrega` | MOD | HIP | M |
| RN-LOG-009 `LEV` | A taxa de entrega vigente da zona é congelada no pedido no momento da criação; alteração posterior na tabela de zonas não afeta pedidos já emitidos. | R | `pedido`, `zona_entrega` | MOD NN | HIP | N |

---

## RH — Recursos Humanos

**6 regras** · prioridade de validação **BAIXA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-RH-001 | Todo colaborador ocupa um cargo, e o cargo determina o custo por hora usado nas apurações gerenciais. | F | `colaborador`, `cargo` | FK | HIP | M |
| RN-RH-002 | A escala define os turnos previstos de cada colaborador por dia. | F | `escala`, `turno` | MOD | HIP | M |
| RN-RH-003 | Um mesmo colaborador não pode ter dois turnos com horários sobrepostos. | R | `escala` | EX | HIP | M |
| RN-RH-004 | O apontamento registra os horários efetivos de entrada e saída, que podem divergir do turno previsto. | F | `apontamento_ponto` | MOD | HIP | M |
| RN-RH-005 | O tempo trabalhado além da jornada prevista é classificado como hora extra, com adicional conforme o dia. | C I | `apontamento_ponto`, `evento_folha` | VW PAR | HIP | M |
| RN-RH-006 | Colaborador inativo não pode ser escalado nem registrar operação no sistema. | R | `colaborador` | APP CK | HIP | N |

---

## FIS — Fiscal

**6 regras** · prioridade de validação **BAIXA**

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
> ### ⏸ Módulo adiado — é o último da fila
>
> **Decisão de 17/08/2026:** FIS fica por último. Duas razões:
>
> 1. É o único módulo cujo valor depende de **homologação externa** — SEFAZ, certificado
>    digital A1, contingência. O esforço não é de modelagem, é de integração e burocracia.
> 2. **Nada nos outros nove módulos depende dele.** `RN-FIS-001` é disparada pelo pedido
>    finalizado, não o contrário. Adiar FIS não bloqueia nenhuma outra spec.
>
> As seis regras permanecem catalogadas e a modelagem já reserva o espaço — em especial
> `RN-FIS-006` (retrato congelado do emitente e do destinatário), que é um requisito de
> modelo, não de integração, e seria caro acrescentar depois.

| ID | Regra | Tipo | Entidades | Impl. | Estado | MVP |
|----|-------|:----:|-----------|-------|:------:|:---:|
| RN-FIS-001 | Todo pedido finalizado gera um documento fiscal. | A | `pedido`, `documento_fiscal` | TRG | HIP | 2 |
| RN-FIS-002 | O documento fiscal é imutável. Correção só é possível por cancelamento seguido de emissão de novo documento. | R | `documento_fiscal` | TRG APP | HIP | 2 |
| RN-FIS-003 | O cancelamento de documento fiscal só é admitido dentro do prazo legal a partir da emissão (`PAR-FIS-001`). | R | `documento_fiscal` | CK PAR | HIP | 2 |
| RN-FIS-004 | Todo produto vendido possui classificação NCM, e toda operação possui um CFOP correspondente. | F R | `produto`, `documento_fiscal`, `ncm`, `cfop` | FK NN | HIP | 2 |
| RN-FIS-005 | A empresa é optante pelo Simples Nacional; a alíquota efetiva depende da faixa de faturamento acumulado dos últimos doze meses. | C F | `regime_tributario`, `aliquota` | VW PAR | HIP | 2 |
| RN-FIS-006 | O documento fiscal congela um retrato dos dados do emitente e do destinatário no momento da emissão. | R | `documento_fiscal` | MOD | HIP | 2 |

> **Limite de escopo declarado.** As regras FIS descrevem a **estrutura** do documento
> fiscal no modelo de dados. A **emissão** efetiva de NFC-e ou SAT está fora do MVP. O
> modelo é projetado para acoplar a emissão sem redesenho. Pergunta 20 da fila de validação.

---

## 15. As sete regras estruturais

Se estiverem erradas, o modelo de dados nasce errado. **Nenhuma spec de modelagem começa
antes que estas sete estejam resolvidas.**

| Regra | O que trava se estiver errada |
|-------|-------------------------------|
| **RN-GER-006** ⚠⚠ — dia operacional | Faturamento por dia, fechamento de caixa e toda a dimensão tempo do BI |
| **RN-PRO-004** ⚠⚠ — sub-receita recursiva | A modelagem da ficha técnica inteira. Exige item estocável polimórfico e proteção contra ciclo |
| **RN-EST-001** — saldo derivado | O modelo de estoque inteiro; define se há coluna de saldo ou view |
| **RN-EST-004** + **RN-EST-011** — lote obrigatório e custo por lote | Toda a apuração de custo e CMV; sem lote não há FEFO nem validade |
| **RN-VEN-008** + **RN-VEN-010** — congelamento de preço e vigência | A reconstituição histórica de qualquer venda passada |
| **RN-VEN-018** — ciclo de vida do pedido | O gatilho de baixa de estoque, a ordem de produção e o momento do faturamento |
| **RN-GER-009** — vínculo a unidade | Toda tabela transacional, toda chave única composta e todo índice |

---

## 16. Rastreabilidade regra → spec

Preenchida à medida que as specs são escritas. Uma regra sem spec é uma regra não
implementada; uma spec sem regra é comportamento não autorizado.

| Regra | Spec | Situação | Teste que a prova |
|-------|------|----------|-------------------|
| `RN-GER-001` | [SPEC-GER-005](specs/SPEC-GER-005-trilha-de-auditoria.md) | **Entregue** — autor e instante preenchidos pelo interceptor e imutáveis depois. `criado_por` é **nulável**: pedido público e cadastro de restaurante não têm autor | `AuditoriaTests` ✔ CA-1, CA-2 |
| `RN-GER-002` | [SPEC-GER-001](specs/SPEC-GER-001-unidade-e-convencoes-do-modelo.md) · [005](specs/SPEC-GER-005-trilha-de-auditoria.md) | **Entregue nas duas camadas** — `ON DELETE RESTRICT` no banco (76 FKs) e o interceptor recusando `Deleted` inclusive sem dependente | `ConvencoesDoModeloTests` ✔ CA-6; `AuditoriaTests` ✔ CA-5; `PedidoTests` ✔ |
| `RN-GER-003` | [SPEC-GER-001](specs/SPEC-GER-001-unidade-e-convencoes-do-modelo.md) | **Entregue** | `DinheiroTests` ✔ 8 casos; `ConvencoesDoModeloTests` ✔ CA-3 (`numeric(12,2)` no catálogo do banco) |
| `RN-GER-004` | [SPEC-GER-001](specs/SPEC-GER-001-unidade-e-convencoes-do-modelo.md) | **Parcial** — tipo e guarda entregues; falta a primeira coluna de quantidade (onda 2) | `QuantidadeTests` ✔ 6 casos; `ConvencoesDoModeloTests` ✔ modelo recusa `decimal` cru |
| `RN-GER-005` | [SPEC-GER-001](specs/SPEC-GER-001-unidade-e-convencoes-do-modelo.md) | **Entregue** | `UnidadeTests` ✔; `ConvencoesDoModeloTests` ✔ CA-5 (`timestamptz`, UTC) |
| `RN-GER-006` ⚠⚠ | [SPEC-GER-002](specs/SPEC-GER-002-dia-operacional.md) | **Entregue** — coluna gerada, `dim_tempo` e paridade | `DiaOperacionalTests` ✔ 4; `DiaOperacionalParidadeTests` ✔ 4; `DimensaoTempoTests` ✔ 11 |
| `RN-GER-009` ⚠ | [SPEC-GER-001](specs/SPEC-GER-001-unidade-e-convencoes-do-modelo.md) | **Entregue** — filtro global + interceptor | `ConvencoesDoModeloTests` ✔ CA-1, CA-2, e a fiação do interceptor na API |
| `RN-GER-010` | [SPEC-GER-004](specs/SPEC-GER-004-usuarios-perfis-e-permissoes.md) | **Entregue** — permissão granular, gate no pipeline, declaração obrigatória | `AcessoTests` ✔ CA-1, CA-2 |
| `RN-GER-011` | [SPEC-GER-005](specs/SPEC-GER-005-trilha-de-auditoria.md) · [003](specs/SPEC-GER-003-parametros-com-vigencia.md) | **Entregue** — `log_auditoria` grava só o que mudou, na mesma transação, e é imutável por gatilho **e** por interceptor. Auditadas hoje: `pedido`, `parametro_valor`, `unidade_modulo`, `usuario` | `AuditoriaTests` ✔ CA-3, CA-4, CA-7 (aplicação **e** banco), CA-6 do `SPEC-GER-003`; `ParametrosTests` ✔ |
| `RN-GER-012` | [SPEC-GER-004](specs/SPEC-GER-004-usuarios-perfis-e-permissoes.md) · [005](specs/SPEC-GER-005-trilha-de-auditoria.md) | **Parcial** — CA-3 fechado (nenhuma das 7 leituras tem chave de custo) e a trilha exige `auditoria.consultar`, já que ela guarda valores anteriores de campo sensível; CA-4 espera o campo, que nasce na onda 2 | `UsuariosESuporteTests` ✔ CA-3, varredura no corpo bruto; `AuditoriaTests` ✔ CA-8 |
| `RN-RH-006` | [SPEC-GER-004](specs/SPEC-GER-004-usuarios-perfis-e-permissoes.md) | **Parcial** — CA-5 fechado (usuário inativo não autentica); CA-6 espera a tabela `colaborador`, módulo RH | `UsuariosESuporteTests` ✔ CA-5 |
| `RN-GER-013` `IMP` | [SPEC-VEN-001](specs/SPEC-VEN-001-cardapio-publico-e-pedido.md) | **Código entregue** | `UnidadeTests` ✔ 1 caso |
| `RN-VEN-035` `DEC` | *(onda 4 — lançar item)* | A escrever | — |
| `RN-EST-021` a `023` `IMP` ⚠⚠ | *(onda 3 — estoque)* | **Regra escrita, código não** — o gatilho da baixa existe no catálogo; a implementação espera o módulo EST | — |
| `RN-GER-014` `DEC` ⚠⚠ · `RN-GER-016` | [SPEC-GER-004](specs/SPEC-GER-004-usuarios-perfis-e-permissoes.md) | **Entregue** — plataforma vê todas as unidades (ADR-0013) e é invisível ao restaurante | `AcessoTests` ✔ CA-9 — **e que o usuário comum continua isolado**; `UsuariosESuporteTests` ✔ CA-11 |
| `RN-GER-030` `IMP` · `RN-GER-031` `IMP` ⚠ | [SPEC-GER-007](specs/SPEC-GER-007-pessoas-e-delegacao-de-acesso.md) | **Entregue** — a plataforma cria, lista e inativa usuário de plataforma; o último ativo não pode ser inativado | `UsuariosDePlataformaTests` ✔ CA-13 a CA-18 |
| `RN-GER-015` `DEC` ⚠ | [SPEC-GER-004](specs/SPEC-GER-004-usuarios-perfis-e-permissoes.md) · [005](specs/SPEC-GER-005-trilha-de-auditoria.md) | **Entregue** — `sessao_suporte` com motivo, escrita de plataforma bloqueada sem ela, e cada operação do usuário de plataforma agora fica na trilha com o seu `usuario_id` | `UsuariosESuporteTests` ✔ CA-10; `AuditoriaTests` ✔ CA-1 |
| `RN-GER-017` a `021` `DEC` | [SPEC-GER-006](specs/SPEC-GER-006-modulos-por-unidade.md) · [007](specs/SPEC-GER-007-pessoas-e-delegacao-de-acesso.md) | **Entregue** — módulo por unidade, com vigência e dependências. `RN-GER-020` é imposta nos **dois** caminhos (alterar e criar) por `FechoDeDependencia`, com fecho **transitivo** | `ModulosTests` ✔ 7 casos; `DependenciaDeModulosTests` ✔ 9; `FechoDeDependenciaTests` ✔ 7 de contrato |
| `RN-GER-022` `DEC` | [SPEC-GER-004](specs/SPEC-GER-004-usuarios-perfis-e-permissoes.md) | **Entregue** — perfil Operador semeado; conjunto de permissões **provisório** | `AcessoTests` ✔ perfis conferidos contra o catálogo |
| `RN-VEN-041` `DEC` ⚠ | [SPEC-GER-004](specs/SPEC-GER-004-usuarios-perfis-e-permissoes.md) | **Entregue** — exclusão lógica com filtro global | `AcessoTests` ✔ 2 casos |
| `RN-GER-023` `DEC` | [SPEC-GER-007](specs/SPEC-GER-007-pessoas-e-delegacao-de-acesso.md) | **Entregue** — value object `Cpf` com verificador, recusa de sequência repetida e unicidade global | `CpfTests` ✔ 23 casos; `DelegacaoTests` ✔ CA-1 a CA-4 |
| `RN-GER-024` `DEC` ⚠⚠ | [SPEC-GER-007](specs/SPEC-GER-007-pessoas-e-delegacao-de-acesso.md) | **Entregue** — matriz de delegação imposta no servidor + validação na partida. Nasceu de um defeito de escalação de privilégio | `DelegacaoTests` ✔ CA-6, CA-7, CA-8 |
| `RN-GER-025` `DEC` | [SPEC-GER-007](specs/SPEC-GER-007-pessoas-e-delegacao-de-acesso.md) | **Entregue** — Gerente com `usuario.gerenciar`, limitado pela matriz; tela na aba Equipe | `DelegacaoTests` ✔ CA-5, CA-9 |
| `RN-GER-026` `DEC` | [SPEC-VEN-002](specs/SPEC-VEN-002-imagem-de-produto-e-logo.md) | **Entregue** — logo da unidade no cardápio público | `ImagemTests` ✔ CA-6, CA-10 |
| `RN-VEN-042` `DEC` | [SPEC-VEN-002](specs/SPEC-VEN-002-imagem-de-produto-e-logo.md) | **Entregue** — JPEG/PNG até 5 MB, **formato conferido pela assinatura** do arquivo | `ImagemTests` ✔ 15 casos, incluindo CA-4 (extensão e `Content-Type` mentirosos) |
| `RN-VEN-043` `IMP` | [SPEC-VEN-003](specs/SPEC-VEN-003-cadastro-do-cardapio.md) | **Entregue** — categoria com nome único na unidade e ordem; **categoria inativa esconde os produtos sem inativá-los**, e a gestão continua vendo a categoria escondida (era um beco: sem isso não havia como reativá-la) | `CadastroDoCardapioTests` ✔ CA-1 a CA-7 |
| `RN-VEN-044` `IMP` ⚠ | [SPEC-VEN-003](specs/SPEC-VEN-003-cadastro-do-cardapio.md) | **Entregue** — nome único, preço positivo, e **`ativo` (cadastral) separado de `disponível` (do turno)**, com rota própria para cada um | `CadastroDoCardapioTests` ✔ CA-8 a CA-11 — CA-11 é o que prova a independência |
| `RN-VEN-045` `IMP` | [SPEC-VEN-003](specs/SPEC-VEN-003-cadastro-do-cardapio.md) | **Entregue** — **não existe rota de exclusão**, e a ausência é a implementação. Inativar preserva o item de pedido histórico | `CadastroDoCardapioTests` ✔ CA-13 |
| `RN-VEN-046` `IMP` | [SPEC-VEN-003](specs/SPEC-VEN-003-cadastro-do-cardapio.md) | **Entregue** — teto de `PAR-VEN-004` produtos **ativos** por categoria (50 por padrão), imposto nos **dois** caminhos de entrada por `TetoDeProdutos`: criar/mover e **reativar** | `CadastroDoCardapioTests` ✔ CA-17 a CA-21 — CA-20 é o contorno pela reativação |
| **10 regras `PAR`** | [SPEC-GER-003](specs/SPEC-GER-003-parametros-com-vigencia.md) | **Habilitadas** — os 10 parâmetros existem, com vigência e leitura na data do evento. `PAR-VEN-004` é o primeiro a ser **consumido por regra de cadastro** (`RN-VEN-046`) | `ParametrosTests` ✔ 11 casos, incluindo CA-7 (documento × código × banco) |

### Fatia vertical — `SPEC-VEN-001`

Regras alcançadas pela fatia vertical antecipada. **Implementação parcial por desenho:** a
[§8 da spec](specs/SPEC-VEN-001-cardapio-publico-e-pedido.md#8-fora-de-escopo--a-dívida-declarada)
declara o que cada uma ainda deve. `⧗` marca a regra que depende de teste de integração
contra PostgreSQL real para ser considerada provada (ADR-0009).

| Regra | Situação | Teste que a prova |
|-------|----------|-------------------|
| `RN-VEN-036` `DEC` | Código entregue | `UnidadeTests` ✔ 3 casos |
| `RN-VEN-037` `DEC` | Código entregue | **`CardapioPublicoTests`** ✔ CA-1, CA-2; **`AcompanhamentoPublicoTests`** ✔ telefone exigido, sem vazamento de dado interno |
| `RN-VEN-038` `DEC` | Código entregue | `PedidoTests` ✔; **`PedidoPublicoTests`** ✔ CA-4 |
| `RN-VEN-001` | Código entregue — `ENUM NOT NULL` | **`PedidoPublicoTests`** ✔ canal conferido no banco |
| `RN-VEN-002` | Código entregue | `PedidoTests` ✔ 1 caso |
| `RN-VEN-003` | Código entregue — FK `NOT NULL` | ⧗ garantida por estrutura; produto inexistente é barrado antes pela `RN-VEN-020` |
| `RN-VEN-008` ⚠ | Código entregue | `PedidoTests` ✔; **`PedidoPublicoTests`** ✔ CA-3 |
| `RN-VEN-009` | Código entregue — total do item | **`PedidoPublicoTests`** ✔ CA-8 |
| `RN-VEN-011` | Código entregue | `PedidoTests` ✔ 1 caso |
| `RN-VEN-018` ⚠ | **Parcial** — ciclo completo e refletido na consulta do cliente; os gatilhos de estoque e produção não existem (regra escrita: `RN-EST-021` a `023`) | `PedidoTests` ✔ 2 casos; **`AcompanhamentoPublicoTests`** ✔ o ciclo inteiro visto pelo cliente |
| `RN-VEN-019` | Código entregue | `PedidoTests` ✔ 2 casos; **`PedidoPublicoTests`** ✔ CA-5 |
| `RN-VEN-020` | Código entregue | **`CardapioPublicoTests`** ✔ CA-7, 2 casos |
| `RN-VEN-032` | Código entregue — `CHECK (preco > 0)` | ⧗ |
| `RN-VEN-039` `IMP` | Código entregue | `PedidoTests` ✔; **`PedidoPublicoTests`** ✔ CA-8 |
| `RN-VEN-040` `IMP` | Código entregue | `PedidoTests` ✔; **`PedidoPublicoTests`** ✔ CA-9, agregado relido do banco |
| `RN-GER-002` | **Entregue** — cancelamento com motivo, e o cancelamento do pedido entra na trilha classificado como `Cancelamento`, não como alteração | `PedidoTests` ✔; **`PedidoPublicoTests`** ✔; **`AuditoriaTests`** ✔ CA-3 |
| `RN-GER-003` | Código entregue — value object `Dinheiro` (`SPEC-GER-001`) | **`DinheiroTests`** ✔; **`ConvencoesDoModeloTests`** ✔ CA-3 |
| `RN-GER-005` | Código entregue | `UnidadeTests` ✔ 1 caso |
| `RN-GER-006` ⚠⚠ | **Entregue** — a fatia deixou só a coluna gerada em `pedido`; `dim_tempo` entrou com a `SPEC-GER-002` | `DiaOperacionalTests` ✔ 4 casos; **`DiaOperacionalParidadeTests`** ✔ 4 casos, incluindo a paridade C# × PostgreSQL nas 96 amostras |
| `RN-GER-009` ⚠ | Código entregue — filtro global **e** interceptor (`SPEC-GER-001`) | **`CardapioPublicoTests`** ✔ isolamento; **`ConvencoesDoModeloTests`** ✔ CA-1, CA-2 |
| `RN-GER-010` | **Entregue** pela `SPEC-GER-004` — o `enum` de perfil foi substituído por `perfil` + `perfil_permissao`, com migração de dados | `AcessoTests` ✔ CA-1, CA-2; `AuditoriaTests` ✔ CA-8 (atendente recusado) |
| `RN-GER-012` | Código entregue — ausência do campo na projeção pública | **`CardapioPublicoTests`** ✔ CA-1, no corpo bruto |
| `RN-CRM-001` | Código entregue — `UNIQUE (unidade_id, telefone)` | **`PedidoPublicoTests`** ✔ três formatações, um cliente |
| `RN-CRM-003` | Código entregue — validator | ⧗ |

**Cobertura:** 44 das 159 regras têm spec escrita. **40 têm o ID citado no `DisplayName` de um
teste** — 195 testes no total: 65 de domínio, 7 de aplicação e 123 de integração contra PostgreSQL real
(Testcontainers), como o ADR-0009 exige.

> Duas dessas 35 (`RN-EST-001`, `RN-EST-012`) são citadas por `QuantidadeTests`, que prova a
> **precisão que elas exigem** — não a regra, que nasce na onda 3. O número mede rastreabilidade,
> não implementação; a coluna "Situação" é quem diz o que está pronto.

**A onda 0 está entregue.** `SPEC-GER-001` a `007`, mais a fatia vertical `SPEC-VEN-001` e a
`SPEC-VEN-002` (imagem), antecipada da onda 1 porque o cardápio público já existe.
Restam **dois critérios bloqueados por dependência de onda**, ambos declarados na sua spec:

| Critério | Espera | Onda |
|----------|--------|------|
| `SPEC-GER-004` CA-4 — gerente vê custo | o campo de custo, que nasce com a ficha técnica | 2 |
| `SPEC-GER-004` CA-6 — colaborador inativo não autentica | a tabela `colaborador` | 10 (RH) |

O CA-6 de `SPEC-GER-003` **foi fechado** pela `SPEC-GER-005`.

> ⚠ **Falta uma regra `EST` que a onda 3 precisa escrever.** `RN-VEN-018` diz que a baixa de
> estoque dispara na Confirmação do pedido, mas **nenhuma regra do módulo EST enuncia esse
> gatilho** do lado do estoque. Sem ela, a implementação da baixa nasceria no código — o que a
> regra inegociável proíbe.

> **O padrão de vigência já está disponível para reuso.** `preco_produto` (`RN-VEN-010`) e
> `ficha_tecnica` (`RN-PRO-002`) herdam de `parametro_valor` o intervalo semiaberto
> `[inicio, fim)` e a `EXCLUDE USING gist` contra sobreposição — com teste provando as duas
> metades: vigência que começa onde a outra termina é aceita; sobreposta é recusada mesmo por
> `INSERT` direto no banco.

> **O ADR-0009 se pagou aqui.** O CA-9 de `SPEC-VEN-001` — item acrescentado a pedido relido
> do banco — expôs um defeito que nenhum teste de domínio veria: o EF Core emitia `UPDATE` no
> lugar de `INSERT` porque o modelo não declarava que a chave é gerada pela aplicação. O
> código parecia certo e o teste de unidade passava. Detalhe na §10 da spec.

As 6 que seguem sem teste não estão sem teste por esquecimento: `RN-VEN-003` e `RN-VEN-032`
são garantidas por estrutura e o caminho da aplicação as barra antes; `RN-GER-003` espera os
value objects de `SPEC-GER-001`; `RN-GER-010`, `RN-CRM-003` e `RN-RH-006` precisam de
superfície que a fatia não tem — usuário sem gerência, cadastro de cliente e inativação de
usuário entram com `SPEC-GER-004`.

> **Renome concluído — 17/08/2026.** Os IDs mal citados no código e nos testes foram
> corrigidos: `RN-VEN-012` → `RN-VEN-039`, `RN-VEN-010` → `RN-VEN-040`,
> `RN-VEN-014` → `RN-GER-002` e `RN-GER-003` → `RN-GER-013`. As citações que já estavam
> corretas foram preservadas — `Produto.cs` cita `RN-VEN-010` porque de fato se refere à
> vigência de preço, e as três de `RN-GER-003` são de fato sobre casa decimal de moeda.
> `grep` por cada ID novo encontra a implementação **e** o teste.
>
> **Lacuna aberta pela mesma auditoria.** O código citava `RN-EST-040`, que não existe — o
> módulo EST vai até `RN-EST-020`. A citação foi corrigida para `RN-VEN-018` e `RN-PRO-008`,
> que existem e cobrem o gatilho, mas **nenhuma regra do EST enuncia explicitamente que a
> confirmação do pedido gera a saída por venda dos insumos da ficha técnica**. `RN-EST-002`
> apenas admite o tipo de movimento; `RN-PRO-008` cobre a ordem de produção, não a baixa.
> A regra precisa ser escrita quando a onda 3 chegar — e antes do código que a implementa.

**Convenção de ID de spec:** `SPEC-<MOD>-<nnn>-<slug>` — por exemplo
`SPEC-VEN-001-abrir-pedido`. Cada spec declara no cabeçalho as regras que implementa, e
esta tabela é o índice inverso. Ver [`06-guia-de-specs.md`](06-guia-de-specs.md).

---

## 17. Histórico de versões

| Versão | Data | Alteração |
|--------|------|-----------|
| 0.1 | 15/08/2026 | Catálogo inicial — 117 regras, todas em estado de hipótese. *(P3 — Análise de Negócios)* |
| 1.0 | 17/08/2026 | **Consolidação com o Documento de Levantamento.** 17 regras complementares incorporadas (`LEV`); 9 conflitos resolvidos com decisão registrada; escopo ampliado para 10 módulos; coluna **MVP** acrescentada; RN-VEN-021 parametrizada (`PAR-VEN-003`) para resolver o conflito de saldo negativo. Total: **134 regras**. Auditoria em [`07-cruzamento`](07-cruzamento-catalogo-x-levantamento.md) |
| **1.1** | **17/08/2026** | **Decisões de produto do time.** (a) **RN-VEN-035** criada — alerta de falta de insumo é obrigatório na tela, em qualquer modo de `PAR-VEN-003`; primeira regra em estado `DEC`. (b) `PAR-VEN-003` redefinido: os três modos passam a ser *bloquear / avisar / permitir*, todos com alerta, diferindo apenas na fricção. (c) Módulo **FIS** movido para **Fase 2 — por último**; **RH mantido** no MVP em nível Modelo. Estados `DEC` e origens `DEC`/`IMP` acrescentados à legenda. Total: **135 regras** |
| **1.2** | **17/08/2026** | **Cardápio público.** Três decisões de produto criadas para viabilizar a fatia vertical (`SPEC-VEN-001`), todas em estado `DEC`: **RN-VEN-036** (identificador público imutável), **RN-VEN-037** (cardápio anônimo, sem custo nem dado de outra unidade) e **RN-VEN-038** (pedido público nasce Aberto no canal delivery próprio, sem confirmação automática). Seção *Cardápio público* acrescentada ao módulo VEN. Total: **138 regras** |
| **1.3** | **17/08/2026** | **Correção de rastreabilidade após a fatia vertical.** Auditoria dos IDs citados no código revelou três comportamentos implementados sem regra no catálogo — e citando, por aproximação, o ID de outra regra. Criadas com origem `IMP`: **RN-VEN-039** (total do pedido é a soma dos itens; o código citava `RN-VEN-012`, que é desconto), **RN-VEN-040** (composição congelada a partir de Em produção; citava `RN-VEN-010`, que é vigência de preço) e **RN-GER-013** (documento e telefone sem máscara; citava `RN-GER-003`, que é casa decimal de moeda). O cancelamento com motivo, que citava `RN-VEN-014` — taxa de serviço —, passa a citar **RN-GER-002**, que já o previa. §16 reorganizada com a cobertura real da fatia vertical. Total: **141 regras** |

| **1.4** | **17/08/2026** | **Plataforma e modularidade comercial.** Oito regras `DEC` criadas para viabilizar a operação como produto: **RN-GER-014** (usuário de plataforma, que enxerga todas as unidades), **RN-GER-015** (sessão de suporte com motivo, exigida antes de qualquer escrita de plataforma), **RN-GER-016** (a plataforma é invisível ao restaurante) e **RN-GER-017** a **RN-GER-021** (módulo contratado por unidade, com vigência, dependência entre módulos e recusa na partida quando uma operação não declara o módulo a que pertence). Registradas em [ADR-0013](adr/0013-acesso-de-plataforma.md) e [ADR-0014](adr/0014-modularidade-comercial.md); o ADR-0013 **revisa** o invariante de isolamento do ADR-0005. Total: **149 regras** |
| **1.5** | **17/08/2026** | **Perfis e exclusão lógica.** **RN-GER-022** (perfil Operador — visualização simplista; o time decidiu que o perfil existe, **não** ainda o conjunto exato de permissões, que é semeado com o mínimo defensável) e **RN-VEN-041** (o administrador do restaurante pode excluir logicamente um pedido lançado por engano; separada de `pedido.cancelar` porque cancelar é fato de negócio e entra na métrica, enquanto excluir tira da apuração). Total: **151 regras** |
| **1.6** | **17/08/2026** | **Onda 0 entregue — rastreabilidade fechada.** Nenhuma regra nova. §16 reescrita com o teste que prova cada regra: `RN-GER-001`, `RN-GER-002`, `RN-GER-011` e `RN-GER-015` passam a **Entregue** pela `SPEC-GER-005`, e o CA-6 da `SPEC-GER-003` fecha. Registrados os **dois** critérios que permanecem abertos por dependência de onda (custo → onda 2; colaborador → onda 10) e **a lacuna do módulo EST**: `RN-VEN-018` diz que a baixa de estoque dispara na Confirmação, mas nenhuma regra `EST` enuncia esse gatilho do lado do estoque — a onda 3 precisa escrevê-la antes de implementar. Total: **151 regras** |

| **1.7** | **17/08/2026** | **Identificação da pessoa, delegação de acesso e imagem.** Cinco regras `DEC`. **RN-GER-023** — usuário passa a ter nome, sobrenome e **CPF** único, validado por dígito verificador, com o usuário de plataforma como exceção declarada. **RN-GER-024** ⚠⚠ — **matriz de delegação de perfil**: a regra nasceu de um defeito de segurança encontrado ao especificar "o gerente cadastra operadores" — dar `usuario.gerenciar` ao Gerente sem limitar o perfil que ele atribui é escalação de privilégio, porque ele criaria um Administrador e passaria a ser um. O time escolheu **lista fixa** (Gerente → Operador, Atendente, Cozinha, Entregador) em vez da regra geral de contenção de permissões, ciente de que perfil novo exige revisitar a matriz; o risco foi neutralizado por **validação na partida**, que recusa o boot se algum perfil do catálogo não estiver declarado na matriz. **RN-GER-025** — o Gerente administra os usuários da própria unidade. **RN-GER-026** e **RN-VEN-042** — logo da unidade e imagem do produto: JPEG ou PNG até 5 MB, com o formato conferido pelo **conteúdo** do arquivo e não pela extensão. Total: **156 regras** |

| **1.8** | **17/08/2026** | **O gatilho da baixa de estoque.** Três regras `IMP`, fechando uma lacuna e não criando decisão: `RN-VEN-018` (estrutural) determina que a baixa dispara na **Confirmação**, e **nenhuma regra do módulo EST enunciava esse gatilho do lado do estoque** — implementar a baixa sem ela seria fazer a regra nascer no código. **RN-EST-021** — a confirmação gera a saída por venda, com a ficha explodida recursivamente, lote por FEFO, custo do lote consumido e **na mesma transação** da mudança de estado. **RN-EST-022** — a baixa é **idempotente**: nenhum item consome duas vezes. **RN-EST-023** ⚠ — cancelamento entre Confirmado e Em produção gera **devolução ao lote de origem**, e **revisa RN-EST-019**: o texto dela ("não gera nenhum movimento") pressupunha baixa na produção, o que contradiz `RN-VEN-018`. Conflito **C10** registrado abaixo e na fila de validação. Total: **159 regras** |

### Conflito C10 — descoberto em 17/08/2026

| Campo | Conteúdo |
|-------|----------|
| Regras em conflito | `RN-VEN-018` (estrutural) × `RN-EST-019` |
| O conflito | `RN-VEN-018` dispara a baixa na **Confirmação**. `RN-EST-019` diz que item cancelado **antes da produção** "não gera nenhum movimento de estoque" — o que só é verdade se a baixa ocorresse **na produção**. Se ela ocorre na confirmação, existe consumo a desfazer, e não desfazê-lo deixaria o insumo baixado para um pedido que não vai existir |
| Como foi encontrado | Auditoria de rastreabilidade: ao escrever o gatilho que faltava, as duas regras não puderam ser verdadeiras ao mesmo tempo |
| Resolução provisória | **`RN-VEN-018` prevalece** — é uma das sete estruturais, e a confirmação é o momento em que o restaurante assume o pedido. `RN-EST-023` passa a cobrir a janela, com **devolução**. `RN-EST-019` fica restrita ao cancelamento de pedido **ainda Aberto**, em que de fato não houve baixa |
| Por que não é decisão fechada | A alternativa — baixar na produção — é operacionalmente defensável e mudaria `RN-VEN-018`, uma estrutural. **Só a entrevista decide.** Está na fila |

| **1.9** | **17/08/2026** | **Administração de loja e de pessoa.** Três regras `DEC`, todas nascidas de pedido de produto para as telas de administração — e nenhuma delas é de tela. **RN-GER-027** ⚠ — unidade inativa **não autentica ninguém**, com mensagem genérica e **código de suporte**. ⚠ **O comportamento já estava implementado** — `Autenticar` confere `usuario.Unidade.Ativa` desde a `SPEC-GER-004` — e **nenhuma regra o enunciava**: era comportamento sem regra, o inverso do defeito habitual. Pior: **não existia operação para inativar a unidade** (`Unidade.Ativa` tinha `private set` e nenhum método), então a checagem era **inalcançável na prática**. A regra passa a enunciar o que existe, e `RN-GER-029` cria a operação que faltava. A mensagem é idêntica à de credencial inválida de propósito — dizer "a sua loja está desativada" contaria a situação comercial de um cliente a quem só tem o e-mail dele. **RN-GER-028** — quem administra altera cadastro e **redefine senha** dos usuários da sua unidade, e a senha redefinida por terceiro **reativa** a troca obrigatória (`RF-SEG-005`); a §8 de `SPEC-GER-007` tinha posto os dois fora de escopo. **RN-GER-029** — inativar unidade é ato de plataforma com justificativa e **não apaga dado**. Total: **162 regras** |

| **2.0** | **17/08/2026** | **Cadastro do cardápio — e duas regras que já estavam no banco sem estar no catálogo.** `RN-VEN-043` (categoria: nome único na unidade, ordem de exibição, e **categoria inativa esconde os produtos sem inativá-los**), `RN-VEN-044` ⚠ (produto: nome único, preço positivo, e a distinção entre **`ativo`** — cadastral, o produto saiu — e **`disponível`** — do turno, "acabou hoje"; confundir os dois é o erro previsível), `RN-VEN-045` (categoria e produto **não** são excluídos: `RN-GER-002` fala de registro transacional, e cadastro é outra coisa). ⚠ **`uq_categoria_nome`, `uq_produto_nome` e `ck_produto_preco` existiam desde a migration `Fundacao`** sem regra que os enunciasse — o inverso do defeito habitual, e igualmente grave: ninguém sabia se era decisão ou acidente, e a entrevista não tinha o que validar. Total: **165 regras** |

| **2.1** | **17/08/2026** | **Teto de produtos por categoria.** `RN-VEN-046` (`IMP`) — no máximo **`PAR-VEN-004`** produtos **ativos** por categoria, 50 por padrão, ajustável por unidade pela tabela `parametro_valor`. Pedida pelo dono do produto como "flag de 50 por categoria, ajustável via banco": virou **parâmetro** e não coluna, porque é valor configurável da operação e `parametro_valor` já entrega vigência, recorte por unidade e trilha (`SPEC-GER-003`) — coluna nova daria só o número. ⚠ **Conta apenas os ativos**: `RN-VEN-045` proíbe exclusão, então contar o histórico entupiria a categoria para sempre. Total: **166 regras** |

| **2.2** | **18/08/2026** | **A plataforma passa a administrar os seus próprios usuários** — e a regra nasceu de uma falha de operação, não de um pedido de tela. `RN-GER-016` já dizia que usuário de plataforma é criado "por outro usuário de plataforma ou pela carga inicial", e **só a carga inicial existia**: quando esta instalação subiu sem nenhum usuário de plataforma — variável de ambiente não repassada pelo compose —, não havia caminho pela interface, e a recuperação exigiu acesso ao servidor. **RN-GER-030** (`IMP`) — criar e listar usuário de plataforma, sem unidade, sem perfil e sem CPF, com troca de senha obrigatória; **sem** sessão de suporte, pela mesma linha comercial × suporte de `RN-GER-029` (a sessão é aberta *para uma unidade*, e aqui não há unidade nenhuma). **RN-GER-031** (`IMP`) ⚠ — inativação por outro usuário de plataforma, nunca de si mesmo, e **recusa de inativar o último ativo**: sem isso um clique reproduziria de propósito o estado que custou um dia de diagnóstico. Total: **168 regras** |

### Regras revogadas na consolidação

| Origem | Regra revogada | Substituída por | Motivo |
|--------|----------------|-----------------|--------|
| Levantamento RN-042 | Custo médio ponderado | RN-EST-011 | Domínio de perecíveis exige custo por lote — conflito C1 |
| Levantamento RN-015 | Cancelamento devolve estoque | RN-EST-018, RN-EST-019 | O lanche pronto não volta à geladeira — conflito C2 |
| Levantamento RN-046 | Saldo pode ser negativo | RN-EST-009 + RN-VEN-021 | Substituído por bloqueio parametrizável — conflito C3 |
| Levantamento RN-048 | Produto sem ficha técnica com custo zero | RN-PRO-001 | Custo zero contamina o CMV silenciosamente — conflito C6 |

---

*Documento mantido pelo time do GrelhaHub. Toda mudança de estado de uma regra registra a
data e a fonte da validação. Regra nova descoberta em implementação entra aqui antes de
entrar no código.*

> **As 151 regras seguem em `HIP` ou `DEC`; nenhuma foi validada com o cliente.** Implementar
> uma regra não muda o seu estado — `Entregue` na §16 diz que o código a cumpre, não que ela
> esteja certa. A fila está em
> [`05-fila-de-validacao-com-cliente.md`](05-fila-de-validacao-com-cliente.md).
