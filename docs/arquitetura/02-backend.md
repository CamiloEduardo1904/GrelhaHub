# 02 — Backend: o que acontece entre o clique e o banco

> **Para o time:** a Parte 1 explica sem jargão o caminho de um pedido. A Parte 2 mostra as pastas
> e os nomes reais, para quem vai mexer no código.

---

## Parte 1 — Para entender sem ser técnico

### O caminho de um pedido, do clique ao banco

```mermaid
flowchart TB
    A["Cliente clica<br/>'Fazer pedido'"] --> B["Porta de entrada<br/><small>recebe e traduz</small>"]
    B --> C{"Você é quem diz ser?"}
    C -->|não| X1["401 — entre de novo"]
    C -->|sim| D{"Sua casa contratou<br/>este módulo?"}
    D -->|não| X2["403 — módulo não contratado"]
    D -->|sim| E{"Seu perfil<br/>permite isto?"}
    E -->|não| X3["403 — falta a permissão"]
    E -->|sim| F{"Os dados fazem<br/>sentido?"}
    F -->|não| X4["422 — em português,<br/>dizendo qual campo"]
    F -->|sim| G["Regra de negócio<br/><small>o cálculo de verdade</small>"]
    G --> H["Banco<br/><small>confere de novo, à sua maneira</small>"]
    H --> I["Trilha de auditoria<br/><small>registra quem fez</small>"]
```

**As quatro perguntas antes da regra** acontecem sempre, na mesma ordem, para toda operação. Não é
o programador que lembra de escrevê-las: elas estão no caminho, e uma operação nova passa por elas
sem que ninguém precise fazer nada.

### Por que existem quatro camadas

Imagine uma cozinha. O **salão** recebe o pedido e fala com o cliente. A **praça** decide o que
fazer com ele. A **despensa** guarda os ingredientes. E existe a **receita**, que não muda por
causa de quem está no salão nem de onde vieram os ingredientes.

```mermaid
flowchart LR
    API["🚪 API<br/><small>o salão</small>"] --> APP["🧑‍🍳 Aplicação<br/><small>a praça</small>"]
    APP --> DOM["📖 Domínio<br/><small>a receita</small>"]
    APP --> INF["📦 Infraestrutura<br/><small>a despensa</small>"]
    INF --> DOM
```

| Camada | O que faz | O que ela **não** conhece |
|--------|-----------|---------------------------|
| **API** | Recebe a requisição HTTP e devolve a resposta | Não sabe calcular nada |
| **Aplicação** | Orquestra: valida, chama a regra, grava | Não sabe o que é HTTP |
| **Domínio** | As regras de negócio e os conceitos | **Não conhece banco nem framework nenhum** |
| **Infraestrutura** | Banco, e-mail, arquivos | Não decide regra |

> ⚠ **O domínio não tem nenhuma dependência externa** — nem do banco, nem do framework de
> mensagens. Isso não é purismo: é o que permite testar a regra do dia operacional sem subir nada,
> e o que impede uma troca de tecnologia de arrastar a regra junto.

### As duas travas que sempre aparecem

**Módulo** e **permissão** respondem perguntas diferentes, e as duas precisam passar:

- *Módulo* — **a casa comprou isso?** Se o restaurante não contratou Estoque, a tela de estoque não
  existe para ele.
- *Permissão* — **esta pessoa pode?** Dentro de uma casa que tem Vendas, o atendente vê o pedido e
  não vê o faturamento.

### Onde cada regra é imposta — a decisão que define o projeto

O sistema valida em **duas camadas de propósito**:

> O banco garante que o dado nunca fica inválido.
> A aplicação garante que o usuário nunca chega até lá.

A ordem de preferência é: **modelagem → restrição do banco → coluna calculada → gatilho → visão →
aplicação**. A aplicação é o **último** recurso — o que a disciplina avalia é o modelo.

**Mas a mensagem de erro vem sempre da aplicação.** Erro de restrição do banco que chega ao usuário
é defeito: ninguém deve ler `ERROR: duplicate key value violates unique constraint`.

### Dinheiro e quantidade não são o mesmo tipo

Um número solto não distingue **R$ 12,00** de **12 gramas**, e o computador não impede somar os
dois. Pior: quantidade tratada com a precisão do dinheiro grava `0,1563` como `0,16` **sem erro
nenhum** — e o desvio entra no custo com um número plausível.

| Conceito | Precisão | Onde se usa |
|----------|----------|-------------|
| **Dinheiro** | 2 casas | Preço, total, taxa |
| **Quantidade** | 4 casas | Insumo, que se **mede** |
| **Contagem** | inteiro | Dois lanches, não 2,0000 lanches |

E há uma verificação que **recusa o sistema na partida** se alguém guardar dinheiro num tipo solto,
dizendo qual campo corrigir.

---

## Parte 2 — A estrutura real

### As pastas

```
backend/src/
├── GrelhaHub.Domain/          as regras e os conceitos — sem dependências
│   ├── Common/                Dinheiro, Quantidade, CPF, CNPJ, Dia operacional
│   ├── Gerais/                Unidade, Usuário, Perfil, Permissão, Módulo, Parâmetro
│   ├── Vendas/                Pedido, Item, Status
│   └── Cardapio/              Categoria, Produto
│
├── GrelhaHub.Application/     um caso de uso por pasta
│   ├── Common/                interfaces, exceções, os atributos de módulo e permissão
│   └── Features/<Módulo>/<Caso>/
│         Command ou Query  +  Handler  +  Validator
│
├── GrelhaHub.Infrastructure/  o mundo externo
│   ├── Persistence/           mapeamento, migrations, interceptadores
│   ├── Email/                 fila e envio
│   └── Arquivos/              imagens
│
└── GrelhaHub.Api/             controllers, autenticação, injeção de dependência
```

### Um caso de uso, por dentro

Cada operação é uma pasta com três arquivos que se leem em ordem:

| Arquivo | Responde |
|---------|----------|
| **Command / Query** | *o que se pede* — os dados de entrada, e os atributos que declaram módulo e permissão |
| **Validator** | *isto faz sentido?* — formato, obrigatoriedade, faixa |
| **Handler** | *o que acontece* — a regra, e só ela |

Exemplo real, o painel de operação:

```
Features/Vendas/ResumoDaOperacao/ResumoDaOperacao.cs
   [ExigeModulo(Vendas)] [ExigePermissao("operacao.visualizar")]
   ResumoDaOperacaoQuery(De, Ate, UnidadeId)
   ResumoDaOperacaoHandler  →  soma o faturamento, monta a série, calcula o ticket
```

### O que acontece sozinho, em toda operação

Três peças interceptam **toda** gravação, sem nenhum caso de uso precisar chamá-las:

| Peça | O que faz |
|------|-----------|
| **Filtro de unidade** | Acrescenta `unidade_id` em toda consulta. Um restaurante não vê o do outro |
| **Interceptador de unidade** | Preenche `unidade_id` no registro que nasce |
| **Interceptador de auditoria** | Escreve a trilha: quem, o quê, antes e depois |

> ⚠ **A ordem importa.** O de unidade roda **antes** do de auditoria: invertido, a trilha
> registraria a unidade vazia do registro que acabou de nascer.

### Como um erro vira mensagem

| Situação | Resposta | O que o usuário lê |
|----------|----------|--------------------|
| Dados inválidos | `422` | A mensagem do validador, em português |
| Regra de negócio recusou | `422` | A mensagem **e o número da regra** — é o que liga o erro ao catálogo |
| Sem permissão | `403` | Qual permissão falta |
| Módulo não contratado | `403` | Qual módulo |
| Sessão expirada | `401` | Volta para a tela de entrada |

> ⚠ **`401` só quando a sessão morreu.** Senha atual incorreta na troca de senha é `422`, não
> `401` — quem já está autenticado não tem falta de autenticação; o que falhou foi o valor
> digitado. Confundir os dois expulsava o usuário da tela antes de ele ler a mensagem.

### Segurança, em quatro linhas

- Senha nunca é guardada — só o **hash**, irreversível.
- Link de acesso (convite, "esqueci minha senha") é **de uso único**, com validade curta, guardado
  **só como hash**.
- Ninguém concede acesso que não possui: uma **matriz de delegação** recusa, e o sistema **não
  sobe** se um perfil novo não declarar o que delega.
- O endereço do link do e-mail vem de **configuração**, nunca do cabeçalho da requisição — é o furo
  clássico da troca de senha.

### Testes

| Tipo | Quantidade | O que provam |
|------|-----------:|--------------|
| Domínio | 76 | As regras isoladas: dia operacional, dinheiro, CPF |
| Aplicação | 13 | Fecho de dependência entre módulos, modelo do e-mail |
| Integração | 200 | **PostgreSQL de verdade**, subindo num contêiner, com a API real |

**Nada de banco em memória.** O provider em memória não executa restrição, gatilho nem coluna
calculada — daria falsa confiança exatamente nas regras que mais importam.

O número da regra vai no nome do teste:

```csharp
[Fact(DisplayName = "RN-VEN-049 — faturamento conta só FINALIZADO; em andamento vai em contador separado")]
```

> Procurar por `RN-VEN-049` no repositório devolve a regra, o código e o teste. É o que torna a
> rastreabilidade real em vez de declarada.

### O teste que compara as duas camadas

Quando a mesma regra existe no banco **e** no código, o teste que importa é o de **paridade**: ele
compara o cálculo do dia operacional feito em C# com o da coluna calculada do banco, em 96 amostras
do dia. Duas implementações da mesma regra divergem em silêncio — este teste transforma a
divergência em falha.
