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
