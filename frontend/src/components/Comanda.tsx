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
/**
 * ⚠ **A propriedade `diaOperacional` foi REMOVIDA em 19/08/2026, e a remoção é intencional.** Ela
 * existiu por meia manhã, para a fila de monitoramento marcar a comanda de dia anterior. Com a
 * revisão de `RN-VEN-054` a fila deixou de desenhar comanda — virou painel de senha —, e a marca
 * de dia foi para lá (`.mural-dia`). Manter a propriedade sem chamador seria deixar uma porta
 * aberta para alguém reabrir a discussão sem reabrir a regra.
 *
 * Na lista de pedidos quem resolve a ambiguidade de `numero` é o **divisor de dia**, que agrupa.
 */
export function Comanda({
  pedido,
  acoes,
  colapsada = false,
  aoAlternar,
}: {
  pedido: PedidoResumo;
  acoes?: ReactNode;

  /**
   * `RN-VEN-057` ⚠ — a comanda **fechada**: número, canal, estado e total, numa linha só.
   *
   * ⚠⚠ **Colapsar não é esconder, e a diferença é a regra.** O pedido continua na lista, contado no
   * grupo dele, e um clique devolve a comanda inteira — inclusive a observação do cliente
   * (`RN-VEN-052`), que nunca é truncada. Nada é removido da tela; o que muda é quanto de cada
   * pedido está aberto ao mesmo tempo.
   *
   * Quem decide o estado inicial é `chegaColapsado`, pelo estado do pedido: concluído e cancelado
   * chegam fechados, porque era a rolagem deles que enterrava o que ainda falta preparar.
   */
  colapsada?: boolean;

  /** Sem isto o cartão não é clicável — é o caso de quem só exibe, como a plataforma. */
  aoAlternar?: () => void;
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

  /**
   * `RN-VEN-057` — a linha fechada.
   *
   * ⚠ É um `<button>` de verdade, com `aria-expanded`: teclado e leitor de tela ganham o mesmo
   * caminho do mouse. Uma `div` com `onClick` deixaria o pedido inalcançável por `Tab`, e numa tela
   * de operação isso não é detalhe — é o operador com a mão ocupada perdendo o acesso.
   *
   * ⚠ O **total** entra aqui, e o cliente não. Fechada, a comanda responde "qual pedido é este e em
   * que pé está"; nome e telefone são o detalhe que o clique abre. `RN-GER-039` — dado pessoal não
   * fica à vista sem necessidade.
   */
  if (colapsada) {
    return (
      <article className="comanda comanda-fechada">
        <button
          type="button"
          className="comanda-abre"
          aria-expanded={false}
          onClick={aoAlternar}
        >
          <Grelha etapa={etapaDe(pedido.status)} de={CICLO.length} morta={cancelado} />

          <span className="numero-pedido">{pedido.numero}</span>

          <span className="comanda-fechada-canal rotulo">{canalCurto(pedido.canal)}</span>

          <Selo status={pedido.status} />

          <span className="comanda-fechada-total dado">{moeda.format(pedido.total)}</span>
        </button>
      </article>
    );
  }

  return (
    <article className="comanda">
      <div className="comanda-picote" aria-hidden="true" />

      {/* ⚠ O mesmo alternador, no sentido inverso — e é o que impede o colapso de ser via de mão
          única. Sem ele, quem abrisse um pedido concluído para conferir ficaria com ele aberto até
          recarregar a página, e a tela voltaria ao problema que a regra resolveu.

          Quando não há `aoAlternar` (a plataforma só exibe), o cabeçalho volta a ser cabeçalho: um
          `<button>` que não faz nada é pior que texto. */}
      {aoAlternar ? (
        <button
          type="button"
          className="comanda-cab comanda-abre"
          aria-expanded={true}
          onClick={aoAlternar}
        >
          <Grelha etapa={etapaDe(pedido.status)} de={CICLO.length} morta={cancelado} />
          <span className="rotulo">{rotuloDoCanal(pedido.canal)}</span>
        </button>
      ) : (
        <header className="comanda-cab">
          <Grelha etapa={etapaDe(pedido.status)} de={CICLO.length} morta={cancelado} />
          <span className="rotulo">{rotuloDoCanal(pedido.canal)}</span>
        </header>
      )}

      <div className="comanda-corpo">
        <div className="comanda-id">
          <span className="numero-pedido">{pedido.numero}</span>

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

        {/* RN-VEN-058 ⚠⚠ — por que, quando e por quem. `RN-GER-002` já exigia o motivo; o
            instante e o responsável entraram em 19/08/2026, e até então o próprio comentário do
            domínio prometia "com motivo e responsável" sem gravar o responsável.

            ⚠ Cada linha aparece só se existir. O pedido cancelado ANTES da regra não tem
            responsável, e a alternativa a omitir a linha seria escrever "sistema" — que é afirmar
            algo falso sobre quem cancelou. */}
        {pedido.motivoCancelamento && (
          <div className="comanda-cancelamento">
            <p className="comanda-cancelamento-motivo">
              <span className="rotulo">Motivo do cancelamento</span>
              <span className="comanda-obs-texto">{pedido.motivoCancelamento}</span>
            </p>

            {(pedido.canceladoEm || pedido.canceladoPor) && (
              <p className="comanda-cancelamento-quem">
                {pedido.canceladoEm && (
                  <span className="dado">{dataEHora(pedido.canceladoEm)}</span>
                )}
                {pedido.canceladoPor && <span>{pedido.canceladoPor}</span>}
              </p>
            )}
          </div>
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

/**
 * `RN-VEN-058` — o instante do cancelamento, com hora.
 *
 * ⚠ **Com hora, e não só a data.** "Cancelado em 19/08" não distingue o que aconteceu na abertura
 * do que aconteceu no fecho — e cancelamento é o registro que alguém relê justamente para
 * reconstruir a sequência de um turno.
 *
 * Fuso do navegador, como todo instante exibido: o servidor guarda em UTC (`RN-GER-005`) e quem lê
 * está na casa.
 */
function dataEHora(iso: string): string {
  const quando = new Date(iso);

  if (Number.isNaN(quando.getTime())) {
    return '';
  }

  return quando.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * `RN-VEN-001` — o canal na linha FECHADA, curto.
 *
 * ⚠ **"Delivery próprio" não cabe, e cortado com reticências ele vira "D."** — que não é rótulo,
 * é ruído ocupando o lugar de um. Na comanda fechada sobram ~60 px entre o número e o selo, e a
 * escolha real é entre um nome curto e nenhum nome.
 *
 * **"Delivery" e não "Delivery próprio" porque o outro delivery é `Marketplace`**, que tem nome
 * próprio na mesma lista: encurtar não cria ambiguidade nenhuma aqui. Na comanda aberta o nome
 * inteiro continua, porque lá ele cabe.
 */
function canalCurto(canal: string): string {
  const mapa: Record<string, string> = {
    Salao: 'Salão',
    Balcao: 'Balcão',
    DeliveryProprio: 'Delivery',
    Marketplace: 'Market',
  };

  return mapa[canal] ?? canal;
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
