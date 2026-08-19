import { useCallback, useEffect, useState } from 'react';
import { CampoDeData } from '../../components/CampoDeData';
import { Grelha } from '../../components/Grelha';
import { diasAtras, hojeIso } from '../../services/datas';
import { Selo } from '../../components/Selo';
import { api, mensagemDeErro, moeda } from '../../services/api';
import { faseDe, ROTULO_STATUS } from '../../services/ciclo';
import type { PedidoResumo, StatusPedido } from '../../services/tipos';
import type { Estabelecimento } from './tipos';

/**
 * Os **pedidos** de uma casa, vistos pela plataforma — por dia operacional e por fase do ciclo.
 *
 * ⚠ Chamava-se `OperacaoDaCasa`, e o nome era metade do defeito: com "Operação" já sendo o
 * painel de faturamento (`RN-GER-038`), dois componentes disputavam a mesma palavra e a aba que
 * apontava para este vinha rotulada "Horário e endereço". Nome que não diz o que a tela mostra
 * sobrevive à revisão porque ninguém abre o arquivo para conferir.
 *
 * ⚠⚠ **`RN-GER-006` manda aqui, e é a regra mais fácil de errar numa tela de filtro.** O intervalo
 * é de **dia operacional** — o que vai de uma virada à seguinte, e não de data de calendário. "De 10 a 12" são três
 * dias operacionais, e não 72 horas corridas: o pedido da 01h30 do dia 13 pertence ao dia 12, que é
 * como o restaurante conta a noite dele.
 *
 * Por isso o rótulo do campo diz **"dia operacional"** em vez de "data": quem filtra precisa saber
 * que a madrugada não pertence ao dia do calendário. Um filtro que dissesse só "data" produziria um
 * número diferente do painel do restaurante, e os dois estariam "certos".
 */

/**
 * As fases, com os estados de `RN-VEN-018` que cada uma agrupa.
 *
 * A filtragem por fase é **no cliente**, e é decisão: a API filtra por status **exato**, e mapear
 * fase → conjunto de estados no servidor duplicaria em C# o que `faseDe` já define. Volume de um
 * intervalo de dias de uma casa é pequeno; se um dia doer, o filtro sobe para a rota.
 */
const FASES = [
  { chave: 'todas', nome: 'Todos' },
  { chave: 'aberto', nome: 'Aberto' },
  { chave: 'andamento', nome: 'Em andamento' },
  { chave: 'fim', nome: 'Finalizados' },
  { chave: 'morto', nome: 'Cancelados' },
] as const;

type Fase = (typeof FASES)[number]['chave'];

/**
 * Os atalhos de período — **em dias operacionais**, como todo o resto desta tela.
 *
 * "7 dias" é `hoje - 6` até hoje, e não `hoje - 7`: sete dias operacionais **contando o de hoje**,
 * que é como o dono da casa conta a semana. Começar em `-7` devolveria oito dias e o número não
 * bateria com o que ele diz em voz alta.
 */
const ATALHOS = [
  { chave: 'hoje', nome: 'Hoje', calcular: () => [hojeIso(), hojeIso()] as const },
  { chave: 'ontem', nome: 'Ontem', calcular: () => [diasAtras(1), diasAtras(1)] as const },
  { chave: '7', nome: '7 dias', calcular: () => [diasAtras(6), hojeIso()] as const },
  { chave: '30', nome: '30 dias', calcular: () => [diasAtras(29), hojeIso()] as const },
] as const;

export function PedidosDaCasa({ casa }: { casa: Estabelecimento }) {
  const [de, setDe] = useState(hojeIso);
  const [ate, setAte] = useState(hojeIso);
  const [fase, setFase] = useState<Fase>('todas');

  const [pedidos, setPedidos] = useState<PedidoResumo[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  const carregar = useCallback(async () => {
    setCarregando(true);

    try {
      const { data } = await api.get<PedidoResumo[]>('/v1/pedidos', {
        params: { unidadeId: casa.id, de, ate },
      });

      setPedidos(data);
      setErro(null);
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setCarregando(false);
    }
  }, [casa.id, de, ate]);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  const visiveis =
    fase === 'todas' ? pedidos : pedidos.filter((p) => faseDe(p.status) === fase);

  const total = visiveis.reduce((soma, p) => soma + p.total, 0);

  return (
    <div className="operacao">
      <div className="operacao-filtros">
        <CampoDeData rotulo="Do dia operacional" valor={de} aoMudar={setDe} />
        <CampoDeData rotulo="Até" valor={ate} aoMudar={setAte} />

        {/* Os intervalos que a operação pede de verdade. É o que torna a digitação um caminho de
            exceção, e não o caminho principal — e é por isso que o campo não precisa de
            calendário. CHAPA-02: uma barra âmbar no grupo, o atalho em vigor. */}
        <nav className="chips atalhos-de-data" aria-label="Períodos">
          {ATALHOS.map(({ chave, nome, calcular }) => {
            const [inicio, fim] = calcular();
            const vigente = de === inicio && ate === fim;

            return (
              <button
                key={chave}
                type="button"
                className="chip"
                aria-pressed={vigente}
                onClick={() => {
                  setDe(inicio);
                  setAte(fim);
                }}
              >
                <Grelha etapa={vigente ? 0 : -1} de={1} />
                {nome}
              </button>
            );
          })}
        </nav>
      </div>

      {/* ⚠⚠ RN-GER-036 — a frase dizia "das 04h00 às 03h59", e isso deixou de ser verdade em
          18/08/2026: a virada passou a sair do horário cadastrado de CADA casa. Numa tela da
          plataforma, que olha a casa dos outros, afirmar a virada errada é pior que não afirmar
          nada — o suporte confere um número com o dono e os dois discordam sem saber por quê.

          O texto agora diz a PROPRIEDADE, que vale para toda casa, e manda para onde o número
          real está: a aba "Horário e endereço", que exibe a virada derivada. */}
      <p className="secundario">
        O dia operacional vai da <strong>virada cadastrada desta casa</strong> até um minuto antes
        dela no dia seguinte: o pedido da madrugada pertence ao dia anterior. A virada em vigor
        está em <strong>Horário e endereço</strong>.
      </p>

      <nav className="chips" aria-label="Filtrar por fase do pedido">
        {FASES.map(({ chave, nome }) => (
          <button
            key={chave}
            type="button"
            className="chip"
            aria-pressed={fase === chave}
            onClick={() => setFase(chave)}
          >
            {/* CHAPA-02 — uma barra âmbar no grupo: a fase escolhida. */}
            <Grelha etapa={fase === chave ? 0 : -1} de={1} />
            {nome}
            {chave !== 'todas' && (
              <span className="dado">
                {' '}
                {pedidos.filter((p) => faseDe(p.status) === chave).length}
              </span>
            )}
          </button>
        ))}
      </nav>

      {erro && <p className="erro">{erro}</p>}

      <div className="rolagem">
        <table>
          <caption>
            {visiveis.length} pedido{visiveis.length === 1 ? '' : 's'} · total{' '}
            <span className="dado">{moeda.format(total)}</span>
          </caption>
          <thead>
            <tr>
              <th className="num col-estreita">Nº</th>
              <th className="col-estreita">Dia oper.</th>
              <th>Cliente</th>
              <th>Canal</th>
              <th>Estado</th>
              <th className="num col-estreita">Itens</th>
              <th className="num col-estreita">Total</th>
            </tr>
          </thead>
          <tbody>
            {visiveis.map((p) => (
              <tr key={p.id}>
                <td className="num col-estreita">{p.numero}</td>
                <td className="dado col-estreita">
                  {new Date(`${p.dataOperacional}T12:00:00`).toLocaleDateString('pt-BR')}
                </td>
                <td>{p.cliente ?? '—'}</td>
                <td className="secundario">{p.canal}</td>
                <td>
                  <Selo status={p.status} />
                </td>
                <td className="num col-estreita">{p.quantidadeDeItens}</td>
                <td className="num col-estreita">{moeda.format(p.total)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {!carregando && visiveis.length === 0 && !erro && (
        <div className="vazio">
          <Grelha etapa={0} de={3} alta />
          <p className="fraco">
            {pedidos.length === 0
              ? 'Nenhum pedido no período.'
              : `Nenhum pedido ${rotuloDaFase(fase)} no período.`}
          </p>
        </div>
      )}
    </div>
  );
}

function rotuloDaFase(fase: Fase): string {
  if (fase === 'aberto') return `em ${ROTULO_STATUS.Aberto.toLowerCase()}`;
  if (fase === 'andamento') return 'em andamento';
  if (fase === 'fim') return 'finalizado';
  if (fase === 'morto') return 'cancelado';
  return '';
}

export type { StatusPedido };
