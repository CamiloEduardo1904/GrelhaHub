import { useCallback, useEffect, useRef, useState } from 'react';
import { api, mensagemDeErro } from '../../services/api';
import type { PedidoNaFila } from '../../services/tipos';

/**
 * Mantém a fila de monitoramento atualizada enquanto alguém a está olhando — `RN-VEN-055` ⚠⚠.
 *
 * **Irmão de `usePedidoAoVivo`, e não uma cópia dele.** A disciplina é a mesma — polling em vez de
 * conexão persistente (ADR-0006, uma VPS com Nginx na frente), uma requisição por vez, suspender
 * nunca é cessar, recuo exponencial na falha. O que muda é **o que faz a cadência afrouxar**, e a
 * diferença é toda a regra:
 *
 * | | Acompanhamento do cliente (`RN-VEN-047`) | Fila da casa (`RN-VEN-055`) |
 * |---|---|---|
 * | Tem fim? | Sim — Finalizado ou Cancelado | **Não.** Fica aberta o serviço inteiro |
 * | Afrouxa por quê? | Tempo na tela (10 consultas) | **Ausência de movimento** (fila vazia) |
 *
 * ⚠⚠ **Afrouxar por tempo de tela aqui seria o erro exato.** A tela que está aberta há mais tempo é
 * a da noite cheia — justamente quando a cozinha precisa do ritmo curto. O que autoriza espaçar não
 * é o relógio, é a fila vazia: casa fechada com o monitor esquecido ligado não tem nada a perguntar,
 * e é esse o caso que consome a madrugada inteira de requisições.
 *
 * ⚠ **`ativo` é o outro lado do balanceamento.** Enquanto outra aba do painel está à frente, a fila
 * não pergunta nada — porque a aba que está à frente já tem a corrente dela. Sem isto, abrir o
 * monitor **somava** um polling em vez de trocá-lo.
 */

/**
 * `PAR-VEN-005` — 15 segundos, o mesmo do acompanhamento do cliente.
 *
 * **É o mesmo parâmetro de propósito, e não um número novo com o mesmo valor:** a pergunta é
 * idêntica ("o estado do pedido mudou?"), e um `PAR` novo com 15 s ao lado de outro `PAR` com 15 s
 * seria duas verdades para uma decisão só. Ver §1.1 de `docs/04-parametros-do-sistema.md`.
 */
const INTERVALO_MS = 15_000;

/** `PAR-VEN-008` — a cadência da fila **ociosa**. Um minuto: não há nada acontecendo. */
const INTERVALO_OCIOSO_MS = 60_000;

/**
 * `PAR-VEN-009` — quantos ciclos com a fila **vazia** antes de afrouxar.
 *
 * Quatro, e não um: entre dois pedidos numa terça devagar existe um minuto de fila vazia que **não**
 * é a casa fechada. Afrouxar no primeiro vazio faria o pedido seguinte aparecer com até um minuto
 * de atraso na tela de quem está de pé esperando por ele.
 */
const CICLOS_VAZIOS_ANTES_DE_OCIAR = 4;

/**
 * Teto do recuo na falha.
 *
 * ⚠ **Dois minutos, e não os 60 s de `usePedidoAoVivo`.** A base aqui já pode ser 60 s: um teto de
 * 60 s significaria "a fila ociosa não recua nada quando a API cai" — o recuo existiria no código e
 * não no comportamento.
 */
const INTERVALO_MAXIMO_MS = 120_000;

export interface FilaAoVivo {
  fila: PedidoNaFila[];

  /** `true` só antes da primeira resposta. Depois dela a tela nunca mais fica em branco. */
  carregando: boolean;

  /** Instante da última resposta boa. Nulo antes da primeira. */
  atualizadoEm: Date | null;

  /**
   * `true` quando a última tentativa falhou. ⚠ **A fila exibida continua sendo a última boa** —
   * trocar a fila por uma mensagem de erro tiraria da cozinha a informação que ela já tinha.
   */
  semConexao: boolean;

  /** A mensagem do servidor, quando houver uma que não seja de rede. */
  erro: string | null;

  /** Consulta agora, sem esperar o intervalo. É o que a tela chama depois de avançar um pedido. */
  atualizarAgora: () => void;
}

/**
 * @param ativo `false` suspende a corrente inteira — usado quando outra aba do painel está à frente.
 */
export function useFilaAoVivo(ativo: boolean): FilaAoVivo {
  const [fila, setFila] = useState<PedidoNaFila[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [atualizadoEm, setAtualizadoEm] = useState<Date | null>(null);
  const [semConexao, setSemConexao] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [gatilho, setGatilho] = useState(0);

  // Refs e não estado: mudar qualquer um destes NÃO pode reiniciar o efeito, senão o timer é
  // recriado a cada resposta e o intervalo real deixa de ser o intervalo.
  //
  // ⚠ `emVoo` NÃO está aqui, e a ausência é a correção de 19/08/2026 — ver o `let` dentro do
  // efeito. Guarda de concorrência que sobrevive à remontagem estrangula a corrente seguinte.
  const falhasSeguidas = useRef(0);
  const ciclosVazios = useRef(0);

  /**
   * A assinatura da fila — ids e estados, na ordem. É o que decide se vale trocar o array.
   *
   * ⚠ Isto **não** substitui o re-render: `atualizadoEm` muda a cada resposta boa, e é ele que faz
   * o tempo de espera da comanda andar. A assinatura preserva a **identidade do array** quando nada
   * mudou, que é o que evita a árvore de comandas inteira remontar a cada 15 s.
   */
  const assinatura = useRef('');

  const atualizarAgora = useCallback(() => setGatilho((n) => n + 1), []);

  useEffect(() => {
    if (!ativo) {
      return;
    }

    let vivo = true;
    let timer: number | undefined;

    /**
     * ⚠⚠ **Uma requisição por vez — POR CORRENTE, e é aqui que a versão anterior morria.**
     *
     * Isto era um `useRef`, e o `ref` **sobrevive à remontagem**. O efeito, não. Quando o efeito
     * refaz — troca de dependência, `atualizarAgora`, ou o duplo `mount` que o `StrictMode` faz em
     * desenvolvimento —, a corrente nova encontrava o `emVoo` da corrente VELHA ainda ligado, saía
     * pelo `return` seco e **não agendava nada**. A requisição velha então terminava, via o seu
     * `vivo` já `false`, e também não reagendava.
     *
     * Resultado: **a tela no ar, sem erro nenhum, e nada mais perguntando.** É exatamente o modo de
     * falha que `RN-VEN-047` enuncia — *suspender não é cessar* —, entrando pela porta que o próprio
     * guarda-corpo abriu.
     *
     * `let` dentro do efeito faz o guarda valer para **esta** corrente e nenhuma outra. Uma
     * remontagem com requisição velha em voo custa uma requisição sobreposta, uma vez; a resposta
     * dela é descartada pelo `vivo`. Corrente morta custa a tela inteira, para sempre.
     */
    let emVoo = false;

    async function consultar() {
      // Uma requisição por vez. Sem isto, rede lenta empilha pedidos e o polling manso vira
      // enxurrada — no tablet da cozinha, que é onde ela dói.
      if (emVoo) {
        return;
      }

      emVoo = true;

      try {
        const { data } = await api.get<PedidoNaFila[]>('/v1/pedidos/fila');

        if (!vivo) {
          return;
        }

        falhasSeguidas.current = 0;

        // RN-VEN-055 — o contador é de ciclos com a fila VAZIA. Qualquer pedido o zera, e é por
        // isso que a volta ao ritmo curto é imediata: não há degrau de saída.
        ciclosVazios.current = data.length === 0 ? ciclosVazios.current + 1 : 0;

        const nova = data.map((p) => `${p.id}:${p.status}`).join('|');

        if (nova !== assinatura.current) {
          assinatura.current = nova;
          setFila(data);
        }

        setSemConexao(false);
        setErro(null);
        setAtualizadoEm(new Date());
        setCarregando(false);
      } catch (e) {
        if (!vivo) {
          return;
        }

        falhasSeguidas.current += 1;
        setSemConexao(true);
        setCarregando(false);

        // 403 e 404 não são falha de rede: são resposta, e a tela precisa dizer o que aconteceu em
        // vez de ficar com um aviso de conexão para sempre.
        setErro(falhasSeguidas.current === 1 ? mensagemDeErro(e) : null);
      } finally {
        emVoo = false;

        if (vivo) {
          agendar();
        }
      }
    }

    function agendar() {
      // RN-VEN-055 — a cadência sai do MOVIMENTO, não do relógio da tela.
      const base =
        ciclosVazios.current >= CICLOS_VAZIOS_ANTES_DE_OCIAR ? INTERVALO_OCIOSO_MS : INTERVALO_MS;

      // Recuo exponencial no erro, reiniciado no primeiro sucesso. Multiplica a base VIGENTE.
      const espera = Math.min(base * 2 ** falhasSeguidas.current, INTERVALO_MAXIMO_MS);

      // Um temporizador por corrente. Sem isto, `agendar` chamado por dois caminhos no mesmo
      // instante deixaria dois timers correndo, e o intervalo real passaria a ser metade do
      // configurado, dobrando a cada repetição.
      window.clearTimeout(timer);

      timer = window.setTimeout(() => {
        // ⚠ Aba escondida: **suspende a consulta e REAGENDA** (`RN-VEN-055`, e a lição de
        // `RN-VEN-047`). Um `return` seco aqui deixaria a corrente dependendo de o
        // `visibilitychange` disparar depois — e em tablet ele nem sempre dispara. O resultado
        // seria o pior possível: a fila no ar, sem erro, mostrando um estado que ninguém mais
        // estava conferindo.
        if (document.hidden) {
          agendar();
          return;
        }

        void consultar();
      }, espera);
    }

    function aoVoltarParaAAba() {
      if (document.hidden) {
        return;
      }

      window.clearTimeout(timer);

      // Consulta em voo: não dá para disparar outra, e quem está em voo reagenda no `finally`. Mas
      // o `clearTimeout` acima já apagou o temporizador — reagendar é o que fecha o buraco.
      if (emVoo) {
        agendar();
        return;
      }

      void consultar();
    }

    document.addEventListener('visibilitychange', aoVoltarParaAAba);

    // `pageshow` cobre a volta do cache de retorno (`bfcache`), em que a página é restaurada
    // inteira sem recarregar e sem necessariamente passar por `visibilitychange`.
    window.addEventListener('pageshow', aoVoltarParaAAba);

    // A primeira consulta é imediata: quem abre a aba não espera 15 s para ver a fila.
    void consultar();

    return () => {
      vivo = false;
      window.clearTimeout(timer);
      document.removeEventListener('visibilitychange', aoVoltarParaAAba);
      window.removeEventListener('pageshow', aoVoltarParaAAba);
    };
  }, [ativo, gatilho]);

  return { fila, carregando, atualizadoEm, semConexao, erro, atualizarAgora };
}
