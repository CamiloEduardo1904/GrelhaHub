import { useCallback, useEffect, useRef, useState } from 'react';
import { CapaDaCasa } from '../../components/CapaDaCasa';
import { Grelha } from '../../components/Grelha';
import { blocoDaFila, ROTULO_DO_BLOCO } from '../../services/ciclo';
import type { BlocoDaFila } from '../../services/ciclo';
import type { MeuRestaurante, PedidoNaFila } from '../../services/tipos';
import { useFilaAoVivo } from './useFilaAoVivo';

/**
 * O painel de senha — `RN-VEN-054` ⚠⚠, `RN-VEN-055` ⚠⚠, `RN-VEN-056`, `SPEC-VEN-004`.
 *
 * **Duas colunas lado a lado, e dentro delas só NÚMERO.**
 *
 * ⚠⚠ **A versão de 19/08/2026 pela manhã desenhava a comanda inteira aqui, e estava errada de
 * escala.** Sessenta pedidos davam quinze linhas de cartão — cerca de seis telas de rolagem —, com
 * a coluna **pronto** embaixo de todas elas. Numa tela cuja única função é dizer "o seu saiu", a
 * informação de maior urgência era a mais difícil de alcançar. A regra foi revisada antes deste
 * código, e o enunciado dela carrega o porquê.
 *
 * **O que sobrou é o que a parede precisa:** o número, e a coluna em que ele está.
 */
export function Monitor({
  restaurante,
  ativo,
}: {
  restaurante: MeuRestaurante | null;

  /**
   * `RN-VEN-055` ⚠ — `false` suspende a corrente. É o painel dizendo "outra aba está à frente",
   * e é metade do balanceamento: sem isto, abrir o monitor **somava** um polling em vez de trocá-lo.
   */
  ativo: boolean;
}) {
  const { fila, carregando, atualizadoEm, semConexao, erro, atualizarAgora } = useFilaAoVivo(ativo);

  const mural = useRef<HTMLDivElement>(null);
  const { emTelaCheia, alternarTelaCheia, telaCheiaDisponivel } = useTelaCheia(mural);

  const preparando = fila.filter((pedido) => blocoDaFila(pedido.status) === 'aberto');
  const prontos = fila.filter((pedido) => blocoDaFila(pedido.status) === 'pronto');

  /**
   * `RN-GER-006` ⚠⚠ — quando o número precisa carregar a data.
   *
   * **A tela NÃO calcula o dia operacional corrente, e não pode:** a virada é cadastrada por
   * unidade (`RN-GER-036`) e derivada no banco. Escrever esse cálculo aqui seria a segunda
   * implementação de uma regra estrutural — o defeito que `DiaOperacionalParidadeTests` existe para
   * pegar.
   *
   * O que a tela sabe responder é a pergunta que importa: **há mais de um dia na fila?** `numero`
   * reinicia a cada virada, e num painel de senha o #1 de ontem e o #1 de hoje são **o mesmo
   * algarismo na parede** — sem nome, sem item, sem nada distinguindo. Com um dia só, a data seria
   * ruído em todo número.
   */
  const dias = new Set(fila.map((pedido) => pedido.dataOperacional));
  const diaMaisNovo = [...dias].sort().at(-1);
  const marcarDia = dias.size > 1;

  return (
    <div className={emTelaCheia ? 'monitor mural' : 'monitor'} ref={mural}>
      <header className="mural-topo">
        <CapaDaCasa
          nomeFantasia={restaurante?.nomeFantasia ?? 'Fila da operação'}
          urlLogo={restaurante?.urlLogo}
          // ⚠ `null` — e não `undefined`. A assinatura da casa é da VITRINE, para quem chega de
          // fora; na parede da cozinha ela ocupa a linha que os números querem. `undefined` cairia
          // no texto padrão, que é justamente o que se está tirando.
          chamada={null}
        />

        <div className="mural-controles">
          <SinalAoVivo
            carregando={carregando}
            atualizadoEm={atualizadoEm}
            semConexao={semConexao}
            ocioso={fila.length === 0}
            atualizarAgora={atualizarAgora}
          />

          {/* RN-VEN-056 — o botão some quando o navegador não tem a API. Botão que não faz nada é
              pior que botão ausente: quem clica conclui que o sistema está quebrado. */}
          {telaCheiaDisponivel && (
            <button type="button" className="btn calada" onClick={() => void alternarTelaCheia()}>
              {emTelaCheia ? 'Sair da tela cheia' : 'Tela cheia'}
            </button>
          )}
        </div>
      </header>

      {erro && <p className="erro">{erro}</p>}

      {/* A divisão VERTICAL — RN-VEN-054. Lado a lado, e não empilhadas: o olho de quem espera
          percorre a coluna "pronto" inteira sem rolar, e é isso que a tela existe para permitir. */}
      <div className="mural-colunas">
        <ColunaDaFila
          bloco="aberto"
          pedidos={preparando}
          marcarDia={marcarDia}
          diaMaisNovo={diaMaisNovo}
        />

        <ColunaDaFila
          bloco="pronto"
          pedidos={prontos}
          marcarDia={marcarDia}
          diaMaisNovo={diaMaisNovo}
        />
      </div>
    </div>
  );
}

/**
 * Uma coluna do painel — `RN-VEN-054`.
 *
 * ⚠ **A coluna vazia NÃO some.** "Nada pronto" é informação para quem está esperando na frente do
 * balcão, e uma coluna que desaparece faz a outra saltar para o meio da tela — a parede inteira se
 * reorganiza toda vez que o último pedido sai dela.
 */
function ColunaDaFila({
  bloco,
  pedidos,
  marcarDia,
  diaMaisNovo,
}: {
  bloco: BlocoDaFila;
  pedidos: PedidoNaFila[];
  marcarDia: boolean;
  diaMaisNovo?: string;
}) {
  return (
    <section className="mural-coluna" data-bloco={bloco} data-lotacao={lotacaoDe(pedidos.length)}>
      <header className="mural-coluna-topo">
        {/* CHAPA-02 — uma barra âmbar por grupo, e aqui ela é a coluna PRONTO. Não é "a primeira":
            é a que exige ação de alguém agora. */}
        <Grelha etapa={bloco === 'pronto' ? 0 : -1} de={1} />

        <h3>{bloco === 'aberto' ? 'Preparando' : ROTULO_DO_BLOCO[bloco]}</h3>

        <span className="mural-conta dado">{pedidos.length}</span>
      </header>

      {pedidos.length === 0 ? (
        <p className="mural-vazio">—</p>
      ) : (
        <ol className="mural-numeros">
          {pedidos.map((pedido) => (
            <li key={pedido.id}>
              {/* CHAPA-03 — o número do pedido é o único lugar do produto que leva `--peso`, e é
                  o mesmo tratamento de `.numero-pedido` na comanda: aqui ele só cresce. */}
              <span className="mural-numero">{pedido.numero}</span>

              {/* RN-GER-006 ⚠⚠ — a data que impede dois "#1" de se confundirem na parede. Só no
                  que NÃO é do dia mais novo da fila, e só quando há mais de um dia nela. */}
              {marcarDia && pedido.dataOperacional !== diaMaisNovo && (
                <span className="mural-dia dado">{pedido.dataOperacional}</span>
              )}
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

/**
 * ⚠⚠ **O degrau do número acompanha a LOTAÇÃO da coluna, e não é enfeite: sem isto o painel
 * esconde número exatamente quando mais gente está esperando.**
 *
 * Em tela cheia, com o número no degrau grande, cabem cerca de vinte por coluna. No pico de
 * sessenta, os de baixo saíam da área visível — e a coluna rola, o que numa parede significa
 * *número que ninguém vai ler*, porque não há mão para rolar a parede.
 *
 * O CSS não sabe contar filhos para dimensionar, então quem conta é o componente. Três degraus, e
 * não uma fórmula contínua: degrau nomeado é escolha; largura calculada por item é o valor
 * fracionário que `CHAPA-13` recusa.
 */
function lotacaoDe(quantos: number): 'baixa' | 'media' | 'alta' {
  if (quantos <= 12) return 'baixa';
  if (quantos <= 30) return 'media';

  return 'alta';
}

/**
 * `RN-VEN-056` — tela cheia, e a tela do dispositivo sem apagar.
 *
 * ⚠⚠ **O bloqueio de suspensão não é acessório: sem ele o modo não cumpre o que promete.** O painel
 * funciona por dez minutos e o monitor apaga sozinho — e é o tipo de falha que ninguém reporta,
 * porque parece problema do computador, não do sistema.
 *
 * ⚠ **Mas ele é oportunista, nunca condição.** Navegador sem `wakeLock` entra em tela cheia do mesmo
 * jeito, e a casa resolve pela configuração de energia do sistema. Recusar a tela cheia por falta de
 * uma API acessória seria trocar o problema de quem tem pelo problema de todos.
 */
function useTelaCheia(alvo: React.RefObject<HTMLElement | null>) {
  const [emTelaCheia, setEmTelaCheia] = useState(false);
  const trava = useRef<WakeLockSentinel | null>(null);

  const telaCheiaDisponivel =
    typeof document !== 'undefined' && document.fullscreenEnabled === true;

  const segurarATela = useCallback(async () => {
    if (!('wakeLock' in navigator)) {
      return;
    }

    try {
      trava.current = await navigator.wakeLock.request('screen');
    } catch {
      // Recusa do navegador — bateria fraca, política do sistema. Silencioso de propósito: a tela
      // cheia já está de pé, e um alerta aqui só teria como ação "não faça nada".
    }
  }, []);

  const alternarTelaCheia = useCallback(async () => {
    if (document.fullscreenElement) {
      await document.exitFullscreen();
      return;
    }

    await alvo.current?.requestFullscreen();
  }, [alvo]);

  useEffect(() => {
    function aoMudar() {
      const ligado = document.fullscreenElement === alvo.current;
      setEmTelaCheia(ligado);

      if (ligado) {
        void segurarATela();
      } else {
        void trava.current?.release();
        trava.current = null;
      }
    }

    // ⚠ A trava CAI sozinha quando a aba é escondida — é o contrato da API, e não um defeito.
    // Sem esta reaquisição, minimizar a janela uma vez desliga o bloqueio para sempre e o monitor
    // volta a apagar, com a tela cheia ainda no ar dizendo que está tudo certo.
    function aoVoltar() {
      if (!document.hidden && document.fullscreenElement === alvo.current) {
        void segurarATela();
      }
    }

    document.addEventListener('fullscreenchange', aoMudar);
    document.addEventListener('visibilitychange', aoVoltar);

    return () => {
      document.removeEventListener('fullscreenchange', aoMudar);
      document.removeEventListener('visibilitychange', aoVoltar);
      void trava.current?.release();
      trava.current = null;
    };
  }, [alvo, segurarATela]);

  return { emTelaCheia, alternarTelaCheia, telaCheiaDisponivel };
}

/**
 * O sinal de que a tela está viva — `RN-VEN-055`.
 *
 * **`CHAPA-06`** — o sinal é a listra âmbar, nunca bolinha pulsando nem emoji. **`CHAPA-08`** —
 * nada pisca: numa tela que fica ligada o serviço inteiro, um indicador que pulsa a cada segundo é
 * ruído permanente.
 *
 * ⚠ **A tela DIZ que afrouxou.** Com a fila vazia a cadência passa a um minuto, e sem esta linha a
 * diferença entre "está mais devagar de propósito" e "parou de perguntar" seria invisível — que é
 * a mesma armadilha que `RN-VEN-047` documenta do outro lado do produto.
 */
function SinalAoVivo({
  carregando,
  atualizadoEm,
  semConexao,
  ocioso,
  atualizarAgora,
}: {
  carregando: boolean;
  atualizadoEm: Date | null;
  semConexao: boolean;
  ocioso: boolean;
  atualizarAgora: () => void;
}) {
  return (
    <div className="ao-vivo">
      <span className="ao-vivo-marca">
        <Grelha etapa={semConexao ? -1 : 0} de={1} morta={semConexao} />
        <span className="rotulo">
          {semConexao ? 'Sem conexão' : carregando ? 'Carregando' : 'Ao vivo'}
        </span>
      </span>

      {!semConexao && ocioso && !carregando && (
        <span className="rotulo fila-cadencia">Fila vazia · a cada minuto</span>
      )}

      {atualizadoEm && !semConexao && (
        <span className="secundario dado">{atualizadoEm.toLocaleTimeString('pt-BR')}</span>
      )}

      {semConexao && (
        <button type="button" className="btn calada" onClick={atualizarAgora}>
          Tentar agora
        </button>
      )}
    </div>
  );
}
