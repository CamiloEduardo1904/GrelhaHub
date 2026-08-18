import { useCallback, useState, type FormEvent } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { Grelha } from '../../components/Grelha';
import { Selo } from '../../components/Selo';
import { api, mensagemDeErro, moeda, valor } from '../../services/api';
import { CICLO, etapaDe, ROTULO_STATUS } from '../../services/ciclo';
import { usePedidoAoVivo, type PedidoPublico } from './usePedidoAoVivo';

export function Acompanhar() {
  const { slug = '' } = useParams();
  const [params] = useSearchParams();

  const [numero, setNumero] = useState(params.get('numero') ?? '');
  const [telefone, setTelefone] = useState('');
  const [pedido, setPedido] = useState<PedidoPublico | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [buscando, setBuscando] = useState(false);

  // O pedido se atualiza sozinho enquanto o cliente olha a tela. O hook só liga depois da busca:
  // antes dela não há número nem telefone conferidos, e a rota é anônima (RN-VEN-037).
  const aoAtualizar = useCallback((novo: PedidoPublico) => setPedido(novo), []);

  const { acompanhando, atualizadoEm, semConexao, atualizarAgora } = usePedidoAoVivo(
    slug,
    numero,
    telefone,
    pedido,
    aoAtualizar,
  );

  async function buscar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setBuscando(true);

    try {
      // O telefone vai junto do número de propósito: sem ele, alguém listaria
      // pedidos alheios só variando o número numa rota anônima.
      const { data } = await api.get<PedidoPublico>(`/publico/${slug}/pedidos/${numero}`, {
        params: { telefone },
      });
      setPedido(data);
    } catch (e) {
      setPedido(null);
      setErro(mensagemDeErro(e));
    } finally {
      setBuscando(false);
    }
  }

  const etapa = pedido ? etapaDe(pedido.status) : -1;
  const cancelado = pedido?.status === 'Cancelado';

  return (
    <div className="tela-centro">
      <div className="placa form">
        <header>
          <Grelha etapa={2} de={5} alta />
          <p className="tagline">Acompanhar pedido</p>
        </header>

        <form onSubmit={buscar} style={{ display: 'grid', gap: '0.75rem' }}>
          <div className="linha">
            <label>
              Número
              <input
                value={numero}
                onChange={(e) => setNumero(e.target.value)}
                inputMode="numeric"
                required
              />
            </label>

            <label>
              Seu telefone
              <input
                value={telefone}
                onChange={(e) => setTelefone(e.target.value)}
                placeholder="(11) 98765-4321"
                inputMode="tel"
                required
              />
            </label>
          </div>

          <button type="submit" className="btn bloco" disabled={buscando}>
            {buscando ? 'Buscando…' : 'Buscar'}
          </button>
        </form>

        {erro && <p className="erro">{erro}</p>}

        {pedido && (
          <section style={{ display: 'grid', gap: '0.9rem' }}>
            <div className="comanda-id">
              <span className="numero-pedido">{pedido.numero}</span>
              <Selo status={pedido.status} />
            </div>

            {cancelado ? (
              <p className="erro">Este pedido foi cancelado pelo restaurante.</p>
            ) : (
              <ol className="regua">
                {CICLO.map((estado, indice) => (
                  <li key={estado} className={indice === etapa ? 'atual' : ''}>
                    {/* CHAPA-02 — a barra âmbar marca onde o pedido está agora. */}
                    <Grelha etapa={indice <= etapa ? 0 : -1} de={1} />
                    {ROTULO_STATUS[estado]}
                  </li>
                ))}
              </ol>
            )}

            <ul className="comanda-itens">
              {pedido.itens.map((item, i) => (
                <li key={i}>
                  <span>
                    <span className="qtd">{item.quantidade}×</span> {item.produto}
                  </span>
                  <span>{valor.format(item.total)}</span>
                </li>
              ))}
            </ul>

            <p className="total">
              <span className="rotulo">Total</span>
              <span className="valor">{moeda.format(pedido.total)}</span>
            </p>

            <EstadoAoVivo
              acompanhando={acompanhando}
              atualizadoEm={atualizadoEm}
              semConexao={semConexao}
              atualizarAgora={atualizarAgora}
            />
          </section>
        )}

        <a className="link-discreto" href={`/c/${slug}`}>
          Voltar ao cardápio
        </a>
      </div>
    </div>
  );
}

/**
 * O rodapé de acompanhamento.
 *
 * **CHAPA-06** — nada de emoji nem de bolinha pulsando: o sinal de que a tela está viva é a
 * listra âmbar, que é o mecanismo que o design system já tem. **CHAPA-07** — o pedido ficar
 * Pronto não ganha comemoração; confete é do fechamento de caixa, uma vez por dia.
 */
function EstadoAoVivo({
  acompanhando,
  atualizadoEm,
  semConexao,
  atualizarAgora,
}: {
  acompanhando: boolean;
  atualizadoEm: Date | null;
  semConexao: boolean;
  atualizarAgora: () => void;
}) {
  if (!acompanhando) {
    // Estado terminal. Dizer que parou é melhor do que a tela parecer congelada: o cliente que vê
    // "Entregue" sem mais nada fica olhando esperando algo acontecer.
    return (
      <p className="ao-vivo encerrado">
        <span className="rotulo">Acompanhamento encerrado</span>
      </p>
    );
  }

  return (
    <div className="ao-vivo">
      <span className="ao-vivo-marca">
        {/* CHAPA-02 — uma barra âmbar. Aqui ela marca "esta tela está acompanhando". */}
        <Grelha etapa={semConexao ? -1 : 0} de={1} morta={semConexao} />
        <span className="rotulo">
          {semConexao ? 'Sem conexão' : 'Acompanhando'}
        </span>
      </span>

      {atualizadoEm && !semConexao && (
        <span className="secundario dado">
          {atualizadoEm.toLocaleTimeString('pt-BR')}
        </span>
      )}

      {semConexao && (
        <button type="button" className="btn calada" onClick={atualizarAgora}>
          Tentar agora
        </button>
      )}
    </div>
  );
}
