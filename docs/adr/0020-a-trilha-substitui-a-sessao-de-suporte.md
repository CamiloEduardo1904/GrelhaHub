# ADR-0020 — A trilha substitui a sessão de suporte

| Campo | Valor |
|-------|-------|
| Situação | **Aceita** |
| Data | 18/08/2026 |
| Decide sobre | `RN-GER-015` — escrita de plataforma sobre dado do cliente |
| **Emenda** | [ADR-0013](0013-acesso-de-plataforma.md) — que criou a sessão |
| Relacionada | [ADR-0017](0017-tres-bancos-aplicacao-arquivo-e-log.md), [ADR-0009](0009-onde-as-regras-de-negocio-vivem.md) |

---

## Contexto

O [ADR-0013](0013-acesso-de-plataforma.md) criou a **sessão de suporte**: para alterar dado de um
cliente, o usuário de plataforma precisava abrir uma sessão declarando o motivo, e a escrita era
recusada sem ela. A intenção era boa — *alterar o dado de alguém sem dizer por quê não deveria ser
possível*.

Na prática, quatro meses depois:

1. **O portão é aberto por quem ele deveria conter.** Quem quer escrever abre a sessão, escreve e
   segue. O motivo digitado apressadamente para destravar o botão não é finalidade declarada — é
   pedágio.
2. **Ele criou um beco.** A tela mostrava o formulário desabilitado e o operador não sabia por quê;
   foi um dos gates que subiram sem porta na interface, e está registrado como tal na §8 da
   `SPEC-GER-007`.
3. **Ele contaminou tudo o que encosta nele.** Todo teste que escreve como plataforma abre uma
   sessão antes; a tela carrega o estado da sessão; o pipeline tem um *behavior* só para isso.
4. **O registro do que aconteceu nunca dependeu dele.** A trilha de auditoria já grava usuário,
   instante, operação, valores anteriores e novos — para **toda** gravação, de plataforma ou não.

E, com o [ADR-0017](0017-tres-bancos-aplicacao-arquivo-e-log.md), a trilha ganha banco próprio,
retenção própria e imutabilidade imposta por gatilho. Ela passa a ser o controle forte; a sessão
continuou sendo o fraco.

## Decisão

> **A sessão de suporte é removida. O controle passa a ser a trilha: toda operação de plataforma
> sobre dado de cliente é registrada, sem exceção e sem portão.**

`RN-GER-015` é **revisada**, não apagada. O que ela garantia — *nenhuma escrita de plataforma sem
rastro* — continua valendo, e com o mesmo mecanismo de sempre. O que sai é a **pré-condição**.

### O que se perde, e é preciso dizer

⚠ **A finalidade declarada.** A sessão capturava um `motivo` *antes* da operação; a trilha registra
o *quê*, o *quem* e o *quando* — não o *porquê*.

A troca é consciente: um motivo digitado para destravar um botão tem valor probatório baixo, e o
custo dele era alto. Onde o porquê **importa de verdade** — desativar um estabelecimento, redefinir
a senha de um usuário —, a justificativa **continua obrigatória no próprio comando** e vai para a
coluna `justificativa` da trilha. É a mesma doutrina de `RN-VEN-018`: cancelar exige motivo porque
é o cancelamento que precisa dele, não porque existe um portão genérico antes.

## Consequências

### Positivas

- **Some um gate sem porta na interface** — o quarto da lista da `SPEC-GER-007` §8.
- **Some um *behavior* do pipeline**, uma entidade, uma tabela, um controller, três casos de uso e
  um componente de tela.
- **Os testes deixam de abrir sessão para escrever** — cenário mais curto e mais parecido com o que
  o usuário faz.
- **Cobertura sobe por remoção**: `SessaoDeSuporteController` estava em 33%,
  `ListarSessoesDeSuporteHandler` e `EncerrarSessaoDeSuporteHandler` em **0%**. Código sem teste que
  deixa de existir é a única forma de dívida que se paga apagando.

### Negativas

- **Não há mais pré-declaração de finalidade.** Registrado acima.
- **A tabela `sessao_suporte` é removida**, e com ela o histórico de sessões abertas. Não há cliente
  em produção; havendo, a migration teria de arquivá-la em vez de descartá-la.
- **`RN-GER-015` muda de forma**, e toda referência a ela nos documentos precisa acompanhar — é o
  tipo de mudança que deixa resíduo se não for varrida de uma vez.

## Alternativas descartadas

| Alternativa | Por que não |
|-------------|-------------|
| **Manter a sessão e dar-lhe uma porta na interface** | Resolveria o beco e não o problema: o portão continua sendo aberto por quem ele deveria conter |
| **Exigir justificativa em toda escrita de plataforma** | É a sessão de novo, com outro nome e mais fricção por operação |
| **Sessão automática, aberta no login de plataforma** | Um registro que nunca recusa nada é ruído com aparência de controle — pior do que não ter |

## Quando reabrir

Se um contrato ou uma exigência regulatória pedir **finalidade declarada antes do acesso** — e aí a
conversa é sobre consentimento e escopo, não sobre um botão que destrava um formulário.
