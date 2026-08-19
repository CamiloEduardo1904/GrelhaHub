import { useCallback, useEffect, useState } from 'react';
import { Grelha } from '../../components/Grelha';
import { api, mensagemDeErro, moeda, valor } from '../../services/api';
import { comoConsulta, invertido, periodoDeAtras, type Periodo } from '../../services/periodo';
import { FiltroDePeriodo } from './FiltroDePeriodo';

/**
 * O painel de operação — `RN-VEN-049` ⚠⚠, `RN-VEN-050`, `RN-VEN-051`, `RN-GER-038`.
 *
 * **Faturamento é só o que foi FINALIZADO.** O que ainda está no salão aparece em contador
 * separado, com rótulo próprio: contar o que ainda pode ser cancelado dá um número que encolhe
 * sozinho depois, e número que anda para trás sem ninguém mexer destrói a confiança no painel
 * inteiro.
 *
 * ⚠ **O gráfico é desenhado com div e altura em porcentagem, e a ausência de biblioteca é
 * decisão.** ADR-0012 recusa
 * webfont pelo mesmo motivo que aqui recusa gráfico de terceiro: o painel roda no tablet da
 * cozinha e não pode depender de 200 kB baixando no meio do serviço. Toda biblioteca de gráfico
 * traz junto o repertório visual que a §5 do design system enumera para recusar — gradiente,
 * sombra macia, cantos redondos, animação de entrada. Barra é retângulo; retângulo a Chapa desenha.
 */

interface DiaDaOperacao {
  dia: string;
  faturamento: number;
  pedidos: number;
}

interface ProdutoVendido {
  produto: string;
  quantidade: number;
  total: number;
}

interface Resumo {
  de: string;
  ate: string;
  faturamento: number;
  pedidosFinalizados: number;
  pedidosEmAndamento: number;
  valorEmAndamento: number;
  pedidosCancelados: number;
  ticketMedio: number | null;
  serie: DiaDaOperacao[];
  maisVendidos: ProdutoVendido[];
}

export function Operacao({ unidadeId }: { unidadeId?: string }) {
  // Abre em sete dias e não em "hoje": um painel de faturamento que abre num ponto só não
  // responde a pergunta que levou alguém a abri-lo, que é sempre comparativa.
  const [periodo, setPeriodo] = useState<Periodo>(() => periodoDeAtras(6));
  const [resumo, setResumo] = useState<Resumo | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  const carregar = useCallback(async () => {
    if (invertido(periodo)) {
      return;
    }

    setCarregando(true);

    try {
      const unidade = unidadeId ? `&unidadeId=${unidadeId}` : '';

      const { data } = await api.get<Resumo>(
        `/v1/pedidos/resumo?${comoConsulta(periodo)}${unidade}`,
      );

      setResumo(data);
      setErro(null);
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setCarregando(false);
    }
  }, [periodo, unidadeId]);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  return (
    <div className="operacao-painel">
      <FiltroDePeriodo periodo={periodo} aoMudar={setPeriodo} ocupado={carregando} />

      {erro && <p className="erro">{erro}</p>}

      {resumo && (
        <>
          <div className="indicadores">
            <Indicador
              rotulo="Faturamento"
              valor={moeda.format(resumo.faturamento)}
              nota={`${resumo.pedidosFinalizados} pedido${resumo.pedidosFinalizados === 1 ? '' : 's'} finalizado${resumo.pedidosFinalizados === 1 ? '' : 's'}`}
              destaque
            />

            <Indicador
              rotulo="Ticket médio"
              // RN-VEN-051 — sem venda não há ticket. O traço diz "não houve"; um R$ 0,00 diria
              // "o ticket foi zero", e são leituras diferentes para quem compara semanas.
              valor={resumo.ticketMedio === null ? '—' : moeda.format(resumo.ticketMedio)}
              nota={resumo.ticketMedio === null ? 'nenhuma venda no período' : 'por pedido finalizado'}
            />

            <Indicador
              rotulo="No salão"
              valor={String(resumo.pedidosEmAndamento)}
              // ⚠ RN-VEN-049 — o rótulo é o que impede a leitura errada: este valor NÃO está no
              // faturamento acima, e a tela precisa dizer isso onde o número aparece.
              nota={`${moeda.format(resumo.valorEmAndamento)} fora do faturamento`}
            />

            <Indicador
              rotulo="Cancelados"
              valor={String(resumo.pedidosCancelados)}
              nota="não entram no faturamento"
            />
          </div>

          <GraficoDeFaturamento serie={resumo.serie} />

          <MaisVendidos produtos={resumo.maisVendidos} />
        </>
      )}
    </div>
  );
}

/**
 * Um número e o que ele significa.
 *
 * CHAPA-03 — o número é dado: mono, tabular. O rótulo e a nota são metadado, e vão em `--muted`
 * sem culpa: `CHAPA-10` restringe `--muted` a rótulo e metadado, que é exatamente o que são.
 */
function Indicador({
  rotulo,
  valor: conteudo,
  nota,
  destaque = false,
}: {
  rotulo: string;
  valor: string;
  nota: string;
  destaque?: boolean;
}) {
  return (
    <div className={destaque ? 'indicador destaque' : 'indicador'}>
      <span className="indicador-rotulo">{rotulo}</span>
      <strong className="indicador-valor">{conteudo}</strong>
      <span className="indicador-nota">{nota}</span>
    </div>
  );
}

/**
 * O faturamento por dia operacional.
 *
 * ⚠ **A barra mais alta é a âmbar** — `CHAPA-02`, uma por grupo. Não é enfeite: num gráfico de
 * sete a trinta barras iguais, o melhor dia é a única pergunta que o olho faz primeiro, e marcá-lo
 * com a cor da régua do produto responde antes da leitura do eixo.
 *
 * ⚠ `RN-VEN-050` — dia sem pedido vem do servidor com zero e é desenhado com altura mínima. Pular
 * o dia faria o olho ler continuidade onde houve fechamento.
 */
function GraficoDeFaturamento({ serie }: { serie: DiaDaOperacao[] }) {
  const teto = Math.max(...serie.map((d) => d.faturamento), 0);
  const melhor = teto > 0 ? serie.findIndex((d) => d.faturamento === teto) : -1;

  if (serie.length === 0) {
    return null;
  }

  return (
    <section className="grafico placa">
      <header className="grafico-topo">
        <h3>Faturamento por dia operacional</h3>
        <span className="dado">{moeda.format(serie.reduce((s, d) => s + d.faturamento, 0))}</span>
      </header>

      {teto === 0 ? (
        <div className="vazio">
          <Grelha etapa={-1} de={6} alta />
          <p>Nenhum pedido finalizado no período.</p>
          <p className="secundario">
            O faturamento conta apenas pedidos finalizados. Pedidos no salão aparecem no contador
            acima.
          </p>
        </div>
      ) : (
        /* `role="img"` com um rótulo que diz o total: leitor de tela não percorre barra. A tabela
           abaixo do gráfico é a versão navegável do mesmo dado — e não uma segunda fonte. */
        <div className="grafico-barras" role="img" aria-label={`Faturamento de ${serie.length} dias operacionais`}>
          {serie.map((dia, indice) => {
            const altura = teto === 0 ? 0 : (dia.faturamento / teto) * 100;

            return (
              <div className="barra-dia" key={dia.dia}>
                <span className="barra-valor">
                  {dia.faturamento > 0 ? valor.format(dia.faturamento) : ''}
                </span>

                <div className="barra-trilho">
                  <div
                    className={indice === melhor ? 'barra melhor' : 'barra'}
                    /* Altura mínima de 2px: barra de zero precisa existir como marca do dia,
                       senão o dia sem venda vira um buraco e o olho fecha a série sem ele. */
                    style={{ height: `max(2px, ${altura}%)` }}
                  />
                </div>

                <span className="barra-rotulo">{diaCurto(dia.dia)}</span>
              </div>
            );
          })}
        </div>
      )}

      {teto > 0 && (
        <div className="rolagem">
          <table>
            <caption>O mesmo dado, dia a dia</caption>
            <thead>
              <tr>
                <th>Dia operacional</th>
                <th className="num">Pedidos</th>
                <th className="num">Faturamento</th>
              </tr>
            </thead>
            <tbody>
              {serie.map((dia) => (
                <tr key={dia.dia}>
                  <td className="dado">{dia.dia}</td>
                  <td className="num">{dia.pedidos}</td>
                  <td className="num">{moeda.format(dia.faturamento)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function MaisVendidos({ produtos }: { produtos: ProdutoVendido[] }) {
  if (produtos.length === 0) {
    return null;
  }

  const teto = Math.max(...produtos.map((p) => p.quantidade));

  return (
    <section className="mais-vendidos placa">
      <header className="grafico-topo">
        <h3>Mais vendidos no período</h3>
        <span className="dado">{produtos.length} produtos</span>
      </header>

      {/* ⚠ O critério, dito. A revisão de UX leu esta lista como "ordenada de forma ambígua",
          citando "Batata frita (2× · R$ 36,00) acima de X-Burger (2× · R$ 56,00)". A ordem estava
          certa e era determinística — empate em quantidade, desempate alfabético —, e nada na tela
          dizia isso. Ranking cujo critério não está escrito é ranking em que ninguém confia.

          ⚠⚠ **Não há alternador "por faturamento" aqui, e a ausência é decisão.** O servidor
          devolve os oito primeiros POR QUANTIDADE (`Take` depois do `OrderByDescending`).
          Reordenar essa lista por valor no cliente mostraria "os oito mais vendidos, dispostos por
          faturamento" — que não é "os oito que mais faturaram", e a diferença é invisível para
          quem olha. Alternar de verdade exige o critério ir na consulta. */}
      <p className="secundario">
        Por <strong>quantidade vendida</strong>; a barra mede a quantidade. Empate desempata pelo
        nome.
      </p>

      <ul className="ranking">
        {produtos.map((produto, indice) => (
          <li key={produto.produto}>
            {/* CHAPA-02 — o primeiro é o âmbar. Um por grupo. */}
            <span className="ranking-marca">
              <Grelha etapa={indice === 0 ? 0 : -1} de={1} />
            </span>

            <span className="ranking-nome">{produto.produto}</span>

            <span
              className="ranking-trilho"
              role="img"
              aria-label={`${produto.quantidade} de ${teto} do mais vendido`}
            >
              <span
                className={indice === 0 ? 'barra melhor' : 'barra'}
                style={{ width: `${(produto.quantidade / teto) * 100}%` }}
              />
            </span>

            <span className="ranking-qtd dado">{produto.quantidade}×</span>
            <span className="ranking-total dado">{moeda.format(produto.total)}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}

/** `2026-08-18` → `18/08`. O ano não cabe e não muda dentro de uma série de 30 dias. */
function diaCurto(iso: string): string {
  const partes = iso.split('-');

  return partes.length === 3 ? `${partes[2]}/${partes[1]}` : iso;
}
