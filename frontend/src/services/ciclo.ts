import type { StatusPedido } from './tipos';

/**
 * RN-VEN-018 — a ordem real do ciclo de vida.
 * `Cancelado` fica de fora: não é etapa, é saída.
 */
export const CICLO: StatusPedido[] = [
  'Aberto',
  'Confirmado',
  'EmProducao',
  'Pronto',
  'Entregue',
  'Finalizado',
];

export const ROTULO_STATUS: Record<StatusPedido, string> = {
  Aberto: 'Aberto',
  Confirmado: 'Confirmado',
  EmProducao: 'Em produção',
  Pronto: 'Pronto',
  Entregue: 'Entregue',
  Finalizado: 'Finalizado',
  Cancelado: 'Cancelado',
};

/** O que o botão de avanço faz a partir de cada estado. */
export const PROXIMA_ACAO: Partial<Record<StatusPedido, string>> = {
  Aberto: 'Confirmar',
  Confirmado: 'Produzir',
  EmProducao: 'Pronto',
  Pronto: 'Entregar',
  Entregue: 'Finalizar',
};

export type Fase = 'aberto' | 'andamento' | 'fim' | 'morto';

export function faseDe(status: StatusPedido): Fase {
  if (status === 'Cancelado') return 'morto';
  if (status === 'Aberto') return 'aberto';
  if (status === 'Entregue' || status === 'Finalizado') return 'fim';
  return 'andamento';
}

/**
 * `RN-VEN-047` — o pedido **acabou de andar**: não há transição saindo daqui (`RN-VEN-018`).
 *
 * ⚠ **Não é a mesma coisa que `faseDe(...) === 'fim'`, e a diferença já custou uma tela errada.**
 * A fase `fim` agrupa **Entregue e Finalizado** para efeito de *filtro* — quem filtra "finalizados"
 * quer os dois. Mas de **Entregue ainda se sai**: falta o Finalizar. O acompanhamento do cliente
 * usava a fase como critério de parada e desligava no Entregue; a tela congelava num estado que
 * ainda ia mudar, sem nada indicando que tinha parado de perguntar.
 *
 * Duas perguntas parecidas, respostas diferentes. Por isso são duas funções.
 */
export function pedidoEncerrado(status: StatusPedido): boolean {
  return status === 'Finalizado' || status === 'Cancelado';
}

/** Índice do estado na régua. −1 para Cancelado, que não tem posição. */
export function etapaDe(status: StatusPedido): number {
  return CICLO.indexOf(status);
}

/**
 * Confirmar é o único ponto do ciclo com endpoint próprio: é ele que dispara
 * baixa de estoque e ordem de produção (RN-VEN-018).
 */
export function rotaDeAvanco(status: StatusPedido): 'confirmar' | 'avancar' {
  return status === 'Aberto' ? 'confirmar' : 'avancar';
}

/** `RN-VEN-054` — os dois blocos da fila de monitoramento. */
export type BlocoDaFila = 'aberto' | 'pronto';

/**
 * `RN-VEN-054` — em que bloco da fila este pedido aparece.
 *
 * **Dois blocos e não seis, porque a pergunta da fila é binária:** falta preparar, ou está
 * esperando para sair. `Aberto`, `Confirmado` e `Em produção` são três respostas para a mesma
 * pergunta — o pedido ainda está sendo feito —, e o estado exato continua legível no `<Selo>` da
 * comanda. Quem quer o detalhe tem o detalhe; quem quer a fila tem a fila.
 *
 * ⚠ **Esta função NÃO decide quem entra na fila, e a distinção importa.** Quem entra é o servidor
 * (`GET /v1/pedidos/fila`), e é o único lugar onde essa lista existe. Repeti-la aqui criaria a
 * segunda implementação que diverge em silêncio — o defeito que `DiaOperacionalParidadeTests`
 * existe para pegar. Aqui só se responde "onde desenho o que já veio".
 */
export function blocoDaFila(status: StatusPedido): BlocoDaFila {
  return status === 'Pronto' ? 'pronto' : 'aberto';
}

/** O rótulo de cada bloco. `RN-VEN-054` — o rótulo nomeia o que o bloco mostra (`CHAPA-12`). */
export const ROTULO_DO_BLOCO: Record<BlocoDaFila, string> = {
  aberto: 'Em aberto',
  pronto: 'Pronto',
};

/** `RN-VEN-057` — os três grupos da lista de pedidos. */
export type GrupoDaLista = 'casa' | 'concluido' | 'cancelado';

/**
 * `RN-VEN-057` — em que grupo da lista este pedido cai.
 *
 * **Três, e não seis:** a pergunta da lista é "ainda preciso fazer algo com isto?". `Aberto` a
 * `Pronto` respondem *sim*; `Entregue` e `Finalizado`, *não*; `Cancelado` é a terceira resposta, e
 * merece grupo próprio porque **não é conclusão** — não entra no faturamento (`RN-VEN-049`) e é o
 * que alguém procura quando confere a taxa de cancelamento do dia.
 *
 * ⚠ **Não é `faseDe` com outro nome.** `faseDe` tem quatro valores e serve à `<Grelha>` e ao
 * `<Selo>`, onde `Pronto` é `andamento` e `Entregue` é `fim`. Aqui `Pronto` é **casa** — o pedido
 * está na mão de alguém —, e essa é a distinção que o agrupamento existe para fazer. Duas
 * perguntas parecidas, respostas diferentes; por isso são duas funções, como
 * `pedidoEncerrado` e `faseDe` já são.
 */
export function grupoDaLista(status: StatusPedido): GrupoDaLista {
  if (status === 'Cancelado') return 'cancelado';
  if (status === 'Entregue' || status === 'Finalizado') return 'concluido';

  return 'casa';
}

/**
 * O rótulo de cada grupo — `CHAPA-12`: o rótulo nomeia o que o grupo mostra.
 *
 * ⚠ "Na casa" e não "Em aberto": `Aberto` é um dos estados **dentro** do grupo, e usar o nome de um
 * estado para nomear o conjunto faria o operador ler "só os que ninguém confirmou ainda".
 */
export const ROTULO_DO_GRUPO: Record<GrupoDaLista, string> = {
  casa: 'Na casa',
  concluido: 'Concluídos',
  cancelado: 'Cancelados',
};

/** A ordem em que os grupos aparecem: o que exige ação primeiro. */
export const GRUPOS_DA_LISTA: GrupoDaLista[] = ['casa', 'concluido', 'cancelado'];

/**
 * `RN-VEN-057` ⚠ — o pedido chega **colapsado**?
 *
 * Concluído e cancelado, sim: são o histórico do dia, e era a rolagem deles que enterrava o que
 * ainda falta preparar. O que está na casa chega aberto — é a rotina de quem está olhando.
 *
 * ⚠ **Isto é só o estado INICIAL.** O clique manda nos dois sentidos, e é o que torna "colapsar"
 * diferente de "esconder".
 */
export function chegaColapsado(status: StatusPedido): boolean {
  return grupoDaLista(status) !== 'casa';
}
