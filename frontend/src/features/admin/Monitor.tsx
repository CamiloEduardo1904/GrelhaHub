import { useState } from 'react';
import { CapaDaCasa } from '../../components/CapaDaCasa';
import { Comanda } from '../../components/Comanda';
import { Grelha } from '../../components/Grelha';
import { api, mensagemDeErro } from '../../services/api';
import { blocoDaFila, PROXIMA_ACAO, ROTULO_DO_BLOCO, rotaDeAvanco } from '../../services/ciclo';
import type { BlocoDaFila } from '../../services/ciclo';
import type { MeuRestaurante, PedidoNaFila } from '../../services/tipos';
import { useFilaAoVivo } from './useFilaAoVivo';

/**
 * A fila de monitoramento — `RN-VEN-054` ⚠, `RN-VEN-055` ⚠⚠, `SPEC-VEN-004`.
 *
 * **A pergunta que nenhuma das outras duas telas respondia.** O painel de operação responde
 * *quanto* a casa vendeu; a lista de pedidos responde *o que entrou no dia*. Nenhuma responde **o
 * que ainda tenho para entregar** — e a lista responde pior conforme a noite avança, porque ela
 * cresce: às onze horas, os três pendentes estão espalhados entre quarenta comandas finalizadas, e
 * a única forma de achá-los é ler o selo de cada uma.
 *
 * **A capa é a mesma do cardápio — sem o endereço.** `RN-GER-026`: a logo da casa manda. O endereço
 * é para quem vai até lá; na tela de quem já está dentro dela é linha morta ocupando o topo.
 *
 * ⚠ **Sem telefone e sem observação, e não porque a tela esconde:** a API não os envia
 * (`RN-VEN-054`). É `CHAPA-05` do lado do dado — não há cadeado porque não há campo. O nome fica,
 * porque é como se chama quem vem buscar.
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
  const [erroDaAcao, setErroDaAcao] = useState<string | null>(null);

  const emAberto = fila.filter((pedido) => blocoDaFila(pedido.status) === 'aberto');
  const prontos = fila.filter((pedido) => blocoDaFila(pedido.status) === 'pronto');

  /**
   * `RN-GER-006` ⚠⚠ — quando marcar a data na comanda.
   *
   * **A tela NÃO calcula o dia operacional corrente, e não pode:** a virada é cadastrada por
   * unidade (`RN-GER-036`) e derivada no banco. Escrever esse cálculo aqui seria a segunda
   * implementação de uma regra estrutural — o defeito que `DiaOperacionalParidadeTests` existe para
   * pegar.
   *
   * O que a tela sabe responder é a pergunta que realmente importa: **há mais de um dia na fila?**
   * A marca existe porque `numero` reinicia a cada virada e a comanda nº 1 de ontem é idêntica à de
   * hoje. Com um dia só na fila não há duas iguais, e a data seria ruído em toda comanda.
   */
  const diasNaFila = new Set(fila.map((pedido) => pedido.dataOperacional));
  const marcarDia = diasNaFila.size > 1;

  async function avancar(pedido: PedidoNaFila) {
    setErroDaAcao(null);

    try {
      await api.post(`/v1/pedidos/${pedido.id}/${rotaDeAvanco(pedido.status)}`, {});

      // Consulta na hora: esperar o intervalo deixaria o cartão que acabou de ser tocado no bloco
      // errado por até 15 s, e a pessoa clicaria de novo.
      atualizarAgora();
    } catch (e) {
      setErroDaAcao(mensagemDeErro(e));
    }
  }

  return (
    <div className="monitor">
      <CapaDaCasa
        nomeFantasia={restaurante?.nomeFantasia ?? 'Fila da operação'}
        urlLogo={restaurante?.urlLogo}
        chamada="O que ainda está na casa"
      />

      <SinalAoVivo
        carregando={carregando}
        atualizadoEm={atualizadoEm}
        semConexao={semConexao}
        ocioso={fila.length === 0}
        atualizarAgora={atualizarAgora}
      />

      {/* Erro de resposta — 403, módulo desabilitado. Falha de rede não passa por aqui: ela vira
          o sinal "sem conexão" acima, e a fila exibida continua sendo a última boa. */}
      {erro && <p className="erro">{erro}</p>}
      {erroDaAcao && <p className="erro">{erroDaAcao}</p>}

      <BlocoDeFila
        bloco="aberto"
        pedidos={emAberto}
        marcarDia={marcarDia}
        vazio="Nada em preparo agora."
        aoAvancar={avancar}
      />

      <BlocoDeFila
        bloco="pronto"
        pedidos={prontos}
        marcarDia={marcarDia}
        vazio="Nada esperando para sair."
        aoAvancar={avancar}
      />
    </div>
  );
}

/**
 * Um bloco da fila — `RN-VEN-054`.
 *
 * ⚠ **O bloco vazio NÃO some.** "Nada pronto" é informação para quem está esperando na frente do
 * balcão, e um bloco que desaparece obriga o olho a reconstruir a tela toda vez que o último pedido
 * sai dela.
 */
function BlocoDeFila({
  bloco,
  pedidos,
  marcarDia,
  vazio,
  aoAvancar,
}: {
  bloco: BlocoDaFila;
  pedidos: PedidoNaFila[];
  marcarDia: boolean;
  vazio: string;
  aoAvancar: (pedido: PedidoNaFila) => Promise<void>;
}) {
  return (
    <section className="bloco-de-fila">
      <header className="divisor-dia">
        {/* CHAPA-02 — uma barra âmbar por grupo, e aqui ela é o bloco PRONTO. Não é "o primeiro":
            é o que exige ação de alguém agora. No divisor de dia operacional o âmbar marca o dia
            mais recente porque lá a pergunta é outra; a barra segue a pergunta da tela. */}
        <Grelha etapa={bloco === 'pronto' ? 0 : -1} de={1} />

        <h3>
          <span className="divisor-titulo">{ROTULO_DO_BLOCO[bloco]}</span>
        </h3>

        <span className="divisor-resumo dado">
          {pedidos.length} pedido{pedidos.length === 1 ? '' : 's'}
        </span>
      </header>

      {pedidos.length === 0 ? (
        <p className="fila-vazia secundario">{vazio}</p>
      ) : (
        <div className="grade">
          {pedidos.map((pedido) => (
            <Comanda
              key={pedido.id}
              pedido={pedido}
              diaOperacional={marcarDia ? pedido.dataOperacional : undefined}
              acoes={
                PROXIMA_ACAO[pedido.status] && (
                  <button type="button" className="btn" onClick={() => void aoAvancar(pedido)}>
                    {PROXIMA_ACAO[pedido.status]}
                  </button>
                )
              }
            />
          ))}
        </div>
      )}
    </section>
  );
}

/**
 * O sinal de que a tela está viva — `RN-VEN-055`.
 *
 * **`CHAPA-06`** — o sinal é a listra âmbar, nunca bolinha pulsando nem emoji. **`CHAPA-08`** —
 * nada pisca: numa tela que fica aberta o serviço inteiro, um indicador que pulsa a cada segundo é
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
        <span className="rotulo fila-cadencia">Fila vazia · conferindo a cada minuto</span>
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
