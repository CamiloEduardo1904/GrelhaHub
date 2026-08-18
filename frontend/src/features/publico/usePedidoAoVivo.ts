import { useCallback, useEffect, useRef, useState } from 'react';
import { api } from '../../services/api';
import { pedidoEncerrado } from '../../services/ciclo';
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
 * 1. **Para em estado encerrado, e só nele.** `RN-VEN-047`: Finalizado e Cancelado. ⚠ **Entregue
 *    não encerra** — dali ainda se vai para Finalizado
 * 2. **Suspende com a aba escondida — sem soltar a corrente.** Um celular no bolso não precisa
 *    perguntar nada, mas o relógio continua batendo. Ao voltar, consulta **na hora**
 * 3. **Nunca sobrepõe requisição.** Rede lenta somando pedidos em fila é o que transforma um
 *    polling manso em enxurrada
 * 4. **Recua quando falha.** Erro consecutivo dobra o intervalo até um teto: se a API cair, cada
 *    cliente esperando deixa de somar carga justamente quando ela é o problema
 *
 * ⚠⚠ **A propriedade que o item 2 protege é a que faltava, e é a mais difícil de ver:** entre
 * "parou de perguntar" e "o pedido parou de andar" a tela é **idêntica**. Nenhum erro, nenhuma
 * mensagem — só um estado que não muda mais. Por isso `RN-VEN-047` exige que suspender nunca seja
 * cessar: só o estado encerrado desliga esta corrente, e **quem suspende reagenda**.
 */

/**
 * `PAR-VEN-005` — 15 segundos, o valor padrão do parâmetro.
 *
 * ⚠ **Cópia do parâmetro no cliente, e a duplicação é conhecida.** O valor de verdade vive em
 * `parametro_valor` e é por unidade (`SPEC-GER-003`); esta tela é **anônima** e não tem rota que
 * leia parâmetro sem autenticação. Enquanto não houver — o lugar natural é o próprio corpo do
 * cardápio público —, a constante fica aqui, nomeando o parâmetro que ela copia. Quem mudar
 * `PAR-VEN-005` no banco precisa mudar esta linha, e é exatamente isso que a citação existe para
 * fazer aparecer no `grep`.
 *
 * O ciclo do pedido leva minutos, então o cliente não ganha nada com 1 s — e o servidor perde:
 * numa noite de 30 pedidos simultâneos, 1 s significa 30 requisições por segundo de gente só
 * esperando.
 */
const INTERVALO_MS = 15_000;

/**
 * `PAR-VEN-006` e `PAR-VEN-007` — depois de 10 consultas, o intervalo passa para 25 s.
 *
 * Dois minutos e meio na tela querem dizer que o cliente está esperando algo que demora: pedido em
 * produção, fila de sexta à noite. Espaçar dali em diante corta quase metade das requisições da
 * noite cheia sem que ninguém perceba a diferença — e é **preferível a encurtar o acompanhamento**,
 * que era como a versão anterior "resolvia" o custo (parava sozinha e deixava a tela mentindo).
 *
 * O contador é de consultas **feitas**, não de tempo decorrido: aba escondida não consulta, então
 * não conta — quem voltar depois de meia hora ainda recebe as primeiras dez no ritmo curto.
 */
const CONSULTAS_ANTES_DE_AFROUXAR = 10;
const INTERVALO_LONGO_MS = 25_000;

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

  // RN-VEN-047 — consultas FEITAS nesta tela, para saber quando afrouxar o intervalo. Ref e não
  // estado: mudar isto não pode reiniciar o efeito, senão o timer é recriado a cada resposta e o
  // intervalo real deixa de ser o intervalo.
  const consultasFeitas = useRef(0);
  const aoAtualizarRef = useRef(aoAtualizar);
  const statusAtual = useRef<StatusPedido | null>(null);

  aoAtualizarRef.current = aoAtualizar;
  statusAtual.current = pedido?.status ?? null;

  // RN-VEN-047 — encerrado é Finalizado ou Cancelado, e mais nada.
  //
  // ⚠ Aqui estava `faseDe(status)` em ['fim', 'morto'], e era um defeito silencioso: a fase 'fim'
  // agrupa **Entregue** com Finalizado — agrupamento certo para filtrar, errado para parar de
  // perguntar. O acompanhamento desligava no Entregue e a tela do cliente congelava um estado
  // antes do fim, sem erro nenhum na tela. `pedidoEncerrado` existe para essa distinção.
  const acompanhando = pedido !== null && !pedidoEncerrado(pedido.status);

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
      consultasFeitas.current += 1;

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

        // Só avisa quem chamou se o estado REALMENTE mudou: um `setPedido` a cada 15 s
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
      // RN-VEN-047 — 15 s nas primeiras dez consultas, 25 s daí em diante.
      const base =
        consultasFeitas.current < CONSULTAS_ANTES_DE_AFROUXAR
          ? INTERVALO_MS
          : INTERVALO_LONGO_MS;

      // Recuo exponencial no erro, reiniciado no primeiro sucesso. Multiplica a base VIGENTE:
      // quem já afrouxou e perdeu a rede recua a partir de 25 s, não volta para 15 s.
      const espera = Math.min(base * 2 ** falhasSeguidas.current, INTERVALO_MAXIMO_MS);

      // Um temporizador por corrente. Sem isto, `agendar` chamado por dois caminhos no mesmo
      // instante — o retorno da consulta e a volta para a aba — deixaria dois timers correndo, e
      // o intervalo real passaria a ser metade do configurado, dobrando a cada repetição.
      window.clearTimeout(timer);

      timer = window.setTimeout(() => {
        // ⚠ Aba escondida: **suspende a consulta e REAGENDA** (RN-VEN-047).
        //
        // Aqui havia um `return` seco, e era onde o acompanhamento morria: sem reagendar, a
        // corrente inteira dependia de o `visibilitychange` disparar depois. Em celular ele nem
        // sempre dispara — a aba congelada pelo navegador volta por `resume`, e a página
        // restaurada do cache de retorno (`bfcache`) volta por `pageshow`. Quando nenhum dos dois
        // veio, o resultado foi o pior possível: a tela seguiu aberta, sem erro, mostrando um
        // estado que ninguém mais estava conferindo.
        //
        // Reagendar custa um temporizador ocioso — que o navegador já limita a uma vez por minuto
        // em aba de fundo — e garante que a única coisa capaz de encerrar isto seja o estado
        // encerrado do pedido.
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

      // Consulta em voo: não dá para disparar outra (item 3), e quem está em voo reagenda no
      // `finally`. Mas o `clearTimeout` acima já apagou o temporizador — se a corrente dependesse
      // deste caminho, ela pararia aqui. Reagendar é o que fecha o buraco.
      if (emVoo.current) {
        agendar();
        return;
      }

      void consultar();
    }

    document.addEventListener('visibilitychange', aoVoltarParaAAba);

    // `pageshow` cobre a volta do cache de retorno (`bfcache`), em que a página é restaurada
    // inteira sem recarregar e sem necessariamente passar por `visibilitychange` — é o caminho do
    // "voltar" do navegador no celular, e era mais um jeito de a corrente ficar parada com a tela
    // no ar.
    window.addEventListener('pageshow', aoVoltarParaAAba);

    agendar();

    return () => {
      vivo = false;
      window.clearTimeout(timer);
      document.removeEventListener('visibilitychange', aoVoltarParaAAba);
      window.removeEventListener('pageshow', aoVoltarParaAAba);
    };
  }, [acompanhando, slug, numero, telefone, gatilho]);

  return { acompanhando, atualizadoEm, semConexao, atualizarAgora };
}
