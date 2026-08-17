import { faseDe, ROTULO_STATUS } from '../services/ciclo';
import type { StatusPedido } from '../services/tipos';

/**
 * O status do pedido — RN-VEN-018.
 *
 * CHAPA-06 — status é selo com borda e cor, nunca emoji: emoji renderiza
 * diferente em cada sistema e não tem contraste garantido.
 */
export function Selo({ status }: { status: StatusPedido }) {
  return (
    <span className="selo" data-fase={faseDe(status)}>
      {ROTULO_STATUS[status]}
    </span>
  );
}
