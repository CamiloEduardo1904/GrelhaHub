import type { ReactNode } from 'react';
import { moeda, valor } from '../services/api';
import { CICLO, etapaDe } from '../services/ciclo';
import type { PedidoResumo } from '../services/tipos';
import { Grelha } from './Grelha';
import { Selo } from './Selo';

/**
 * O cartão de pedido, desenhado como comanda impressa (ADR-0012).
 *
 * O furo de tracionador no topo faz o objeto ser lido como "impresso" antes de
 * o usuário ler qualquer texto. O número domina porque precisa ser lido do
 * outro lado da cozinha.
 */
export function Comanda({ pedido, acoes }: { pedido: PedidoResumo; acoes?: ReactNode }) {
  const cancelado = pedido.status === 'Cancelado';

  return (
    <article className="comanda">
      <div className="comanda-picote" aria-hidden="true" />

      <header className="comanda-cab">
        <Grelha etapa={etapaDe(pedido.status)} de={CICLO.length} morta={cancelado} />
        <span className="rotulo">{rotuloDoCanal(pedido.canal)}</span>
      </header>

      <div className="comanda-corpo">
        <div className="comanda-id">
          <span className="numero-pedido">{pedido.numero}</span>
          <Selo status={pedido.status} />
        </div>

        {pedido.cliente && (
          <p className="secundario">
            {pedido.cliente}
            {pedido.telefone && <span className="dado"> · {formatarTelefone(pedido.telefone)}</span>}
          </p>
        )}

        <ul className="comanda-itens">
          {pedido.itens.map((item, i) => (
            <li key={i}>
              <span>
                <span className="qtd">{item.quantidade}×</span> {item.produto}
                {item.observacao && <span className="obs">{item.observacao}</span>}
              </span>
              <span>{valor.format(item.precoPraticado * item.quantidade)}</span>
            </li>
          ))}
        </ul>

        {pedido.observacao && <p className="nota">{pedido.observacao}</p>}

        <div className="comanda-soma">
          <span className="rotulo">Total</span>
          <span className="valor">{moeda.format(pedido.total)}</span>
        </div>

        {acoes && <div className="comanda-acoes">{acoes}</div>}
      </div>
    </article>
  );
}

/** RN-VEN-001 — os quatro canais de venda, em português. */
function rotuloDoCanal(canal: string): string {
  const mapa: Record<string, string> = {
    Salao: 'Salão',
    Balcao: 'Balcão',
    DeliveryProprio: 'Delivery próprio',
    Marketplace: 'Marketplace',
  };
  return mapa[canal] ?? canal;
}

/** O back guarda só dígitos (RN-CRM-001); a máscara é da apresentação. */
function formatarTelefone(digitos: string): string {
  if (digitos.length === 11) {
    return `(${digitos.slice(0, 2)}) ${digitos.slice(2, 7)}-${digitos.slice(7)}`;
  }
  if (digitos.length === 10) {
    return `(${digitos.slice(0, 2)}) ${digitos.slice(2, 6)}-${digitos.slice(6)}`;
  }
  return digitos;
}
