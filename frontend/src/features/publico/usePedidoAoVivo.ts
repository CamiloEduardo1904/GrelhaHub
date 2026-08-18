import { useCallback, useEffect, useRef, useState } from 'react';
import { api } from '../../services/api';
import { faseDe } from '../../services/ciclo';
import type { StatusPedido } from '../../services/tipos';

/**
 * Mantém o pedido do cliente atualizado enquanto ele olha a tela — `RN-VEN-018`.
 *
 * **Por que polling, e não SSE ou WebSocket.** ADR-0006 — uma VPS, Nginx na frente. Conexão
 * persistente exigiria configuração de proxy, teto de conexões simultâneas e um caminho de
 * reconexão próprio, para entregar segundos de diferença em um fluxo que leva minutos. Polling é
 * uma requisição `GET` na mesma rota que a tela já usa, e a rota já é pública e anônima
 * (`RN-VEN-037`).
 *
 * As quatro coisas que fazem isto ser educado com o servidor e com o celular do cliente:
 *
 * 1. **Para em estado terminal.** Entregue, Finalizado e Cancelado não mudam mais — continuar
 *    perguntando seria bater no servidor para sempre, e a aba fica aberta esquecida
 * 2. **Pausa com a aba escondida.** Um celular no bolso não precisa perguntar nada. Ao voltar,
 *    consulta **na hora**, em vez de esperar o próximo intervalo
 * 3. **Nunca sobrepõe requisição.** Rede lenta somando pedidos em fila é o que transforma um
 *    polling manso em enxurrada
 * 4. **Recua quando falha.** Erro consecutivo dobra o intervalo até um teto: se a API cair, cada
 *    cliente esperando deixa de somar carga justamente quando ela é o problema
 */

/**
 * 10 segundos. O ciclo do pedido leva minutos, então o cliente não ganha nada com 1 s — e o
 * servidor perde: numa noite de 30 pedidos simultâneos, 1 s significa 30 requisições por segundo
 * de gente só esperando.
 */
const INTERVALO_MS = 10_000;

/** Teto do recuo. Um minuto entre tentativas ainda recupera sozinho quando a API volta. */
const INTERVALO_MAXIMO_MS = 60_000;

export interface PedidoPublico {
  numero: number;
  status: StatusPedido;
  total: number;
  criadoEm: string;
  itens: { produto: string; quantidade: number; precoPraticado: number; total: number }[];
}

export interface PedidoAoVivo {
  /** `true` enquanto o polling está de pé — isto é, o pedido ainda pode mudar. */
  acompanhando: boolean;

  /** Instante da última resposta bem-sucedida. Nulo antes da primeira. */
  atualizadoEm: Date | null;

  /**
   * `true` quando a última tentativa falhou. **O pedido exibido continua sendo o último bom** —
   * apagar a tela por causa de uma falha de rede tiraria do cliente a informação que ele já tinha.
   */
  semConexao: boolean;

  /** Força uma consulta agora, sem esperar o intervalo. */
  atualizarAgora: () => void;
}

/**
 * @param slug Identificador público do restaurante (`RN-VEN-036`).
 * @param numero Número do pedido.
 * @param telefone Vai junto do número de propósito: sem ele, alguém listaria pedidos alheios só
 *   variando o número numa rota anônima. É o mesmo par que a busca usa.
 * @param pedido O pedido já carregado pela busca; `null` desliga o acompanhamento.
 * @param aoAtualizar Recebe o pedido novo. Chamado **apenas** quando algo mudou.
 */
export function usePedidoAoVivo(
  slug: string,
  numero: string,
  telefone: string,
  pedido: PedidoPublico | null,
  aoAtualizar: (novo: PedidoPublico) => void,
): PedidoAoVivo {
  const [atualizadoEm, setAtualizadoEm] = useState<Date | null>(null);
  const [semConexao, setSemConexao] = useState(false);
  const [gatilho, setGatilho] = useState(0);

  // Refs e não estado: mudar qualquer um destes NÃO deve reiniciar o efeito, senão o timer é
  // recriado a cada resposta e o intervalo real deixa de ser o intervalo.
  const emVoo = useRef(false);
  const falhasSeguidas = useRef(0);
  const aoAtualizarRef = useRef(aoAtualizar);
  const statusAtual = useRef<StatusPedido | null>(null);

  aoAtualizarRef.current = aoAtualizar;
  statusAtual.current = pedido?.status ?? null;

  // Estado terminal: `faseDe` devolve 'fim' para Entregue e Finalizado, 'morto' para Cancelado.
  // Reusar essa função em vez de listar os estados aqui é o que impede esta tela de divergir do
  // ciclo quando ele mudar (RN-VEN-018).
  const terminal = pedido !== null && ['fim', 'morto'].includes(faseDe(pedido.status));
  const acompanhando = pedido !== null && !terminal;

  const atualizarAgora = useCallback(() => setGatilho((n) => n + 1), []);

  useEffect(() => {
    if (!acompanhando) {
      return;
    }

    let vivo = true;
    let timer: number | undefined;

    async function consultar() {
      // Uma requisição por vez. Sem isto, rede lenta empilha pedidos e o polling manso vira
      // enxurrada justamente no cliente que está com sinal ruim.
      if (emVoo.current) {
        return;
      }

      emVoo.current = true;

      try {
        const { data } = await api.get<PedidoPublico>(`/publico/${slug}/pedidos/${numero}`, {
          params: { telefone },
        });

        if (!vivo) {
          return;
        }

        falhasSeguidas.current = 0;
        setSemConexao(false);
        setAtualizadoEm(new Date());

        // Só avisa quem chamou se o estado REALMENTE mudou: um `setPedido` a cada 10 s
        // re-renderizaria a árvore inteira para nada.
        if (data.status !== statusAtual.current) {
          aoAtualizarRef.current(data);
        }
      } catch {
        if (!vivo) {
          return;
        }

        // Silencioso de propósito: o pedido exibido continua sendo o último bom, e a tela mostra
        // um aviso discreto. Trocar a comanda por uma mensagem de erro tiraria do cliente a
        // informação que ele já tinha.
        falhasSeguidas.current += 1;
        setSemConexao(true);
      } finally {
        emVoo.current = false;

        if (vivo) {
          agendar();
        }
      }
    }

    function agendar() {
      // Recuo exponencial no erro, reiniciado no primeiro sucesso.
      const espera = Math.min(
        INTERVALO_MS * 2 ** falhasSeguidas.current,
        INTERVALO_MAXIMO_MS,
      );

      timer = window.setTimeout(() => {
        // Aba escondida: não consulta e não reagenda. Quem retoma é o `visibilitychange`, que
        // consulta na hora — o cliente que volta para a aba quer ver agora, não em 10 s.
        if (document.hidden) {
          return;
        }

        void consultar();
      }, espera);
    }

    function aoVoltarParaAAba() {
      if (!document.hidden) {
        window.clearTimeout(timer);
        void consultar();
      }
    }

    document.addEventListener('visibilitychange', aoVoltarParaAAba);
    agendar();

    return () => {
      vivo = false;
      window.clearTimeout(timer);
      document.removeEventListener('visibilitychange', aoVoltarParaAAba);
    };
  }, [acompanhando, slug, numero, telefone, gatilho]);

  return { acompanhando, atualizadoEm, semConexao, atualizarAgora };
}
