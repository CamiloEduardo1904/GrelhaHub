import type { ReactNode } from 'react';
import { moeda, valor } from '../services/api';
import { tempoDecorrido } from '../services/datas';
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
export function Comanda({
  pedido,
  acoes,
  diaOperacional,
}: {
  pedido: PedidoResumo;
  acoes?: ReactNode;

  /**
   * `RN-GER-006` ⚠⚠ — o dia operacional do pedido, quando a tela precisa distingui-lo.
   *
   * **Só a fila de monitoramento passa isto, e só quando há mais de um dia nela** (`RN-VEN-054`):
   * `numero` é único por (unidade, dia operacional) e REINICIA a cada virada, então a comanda nº 1
   * de ontem é idêntica à nº 1 de hoje. Na lista de pedidos quem resolve isso é o divisor de dia,
   * que agrupa; a fila é FIFO e não agrupa, então a marca vai na própria comanda.
   *
   * ⚠ `undefined` some com a marca. Exibi-la sempre seria ruído: com um dia só na tela não há duas
   * comandas iguais para distinguir.
   */
  diaOperacional?: string;
}) {
  const cancelado = pedido.status === 'Cancelado';

  /**
   * Os dois estados em que o tempo de espera deixa de significar algo — `RN-VEN-018`.
   *
   * São os mesmos de `RN-VEN-047`, onde o acompanhamento do cliente cessa, e pela mesma razão:
   * dali o pedido não anda mais. ⚠ `Entregue` **não** está na lista — de lá ainda se vai para
   * Finalizado, e enquanto isso alguém ainda está esperando o fecho.
   */
  const encerrado = cancelado || pedido.status === 'Finalizado';

  const espera = tempoDecorrido(pedido.criadoEm);

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

          {/* RN-GER-006 — a data que desempata dois números iguais. Dado: mono, tabular. */}
          {diaOperacional && <span className="comanda-dia dado">{diaOperacional}</span>}

          <span className="comanda-estado">
            <Selo status={pedido.status} />

            {/* ⚠ O dado que faltava, e que vale mais que o número: quanto tempo esta pessoa está
                esperando. O painel não dizia isso em lugar nenhum — nem no cartão, nem na lista —
                num produto cujo argumento é "a cozinha vê a fila".

                Não aparece em pedido encerrado: "há 3 dias" num pedido finalizado é ruído, e a
                urgência que o rótulo comunica não existe mais. */}
            {espera && !encerrado && <span className="comanda-espera dado">{espera}</span>}
          </span>
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

                {/* RN-VEN-052 — a observação do item, quando existir, também é instrução de
                    preparo: rótulo em cor de metadado, texto na cor do texto (CHAPA-10). */}
                {item.observacao && (
                  <span className="obs">
                    <span className="obs-rotulo">obs</span> {item.observacao}
                  </span>
                )}
              </span>
              <span>{valor.format(item.precoPraticado * item.quantidade)}</span>
            </li>
          ))}
        </ul>

        {/* RN-VEN-052 ⚠ — o único campo em que o CLIENTE fala com a cozinha, e ele precisa dizer
            o que é. Antes vinha como `.nota`: filete âmbar, texto em --muted, nenhum rótulo — e
            entre os itens e o total isso lê como sobra de tela.

            Duas correções no mesmo bloco. O rótulo, que é metadado e pode ser discreto; e a
            prosa em --text, que a CHAPA-10 já exigia — a tela do cliente (Acompanhar) fazia
            certo, a de quem prepara o sanduíche não.

            ⚠ Nunca truncada. O teto de 80 caracteres existe justamente para caber inteira: o
            corte esconderia a parte que ninguém sabe estar faltando. */}
        {pedido.observacao && (
          <p className="comanda-obs">
            <span className="rotulo">Observação do cliente</span>
            <span className="comanda-obs-texto">{pedido.observacao}</span>
          </p>
        )}

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
