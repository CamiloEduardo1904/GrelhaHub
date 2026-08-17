import { useState, type FormEvent } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { api, mensagemDeErro, moeda } from '../../services/api';

interface PedidoPublico {
  numero: number;
  status: string;
  total: number;
  criadoEm: string;
  itens: { produto: string; quantidade: number; precoPraticado: number; total: number }[];
}

/** RN-VEN-018 — as etapas que o cliente enxerga. Finalizado e Cancelado ficam de fora da régua. */
const ETAPAS = ['Aberto', 'Confirmado', 'EmProducao', 'Pronto', 'Entregue'];

const ROTULO: Record<string, string> = {
  Aberto: 'Aguardando o restaurante',
  Confirmado: 'Pedido confirmado',
  EmProducao: 'Em produção',
  Pronto: 'Pronto',
  Entregue: 'Entregue',
  Finalizado: 'Finalizado',
  Cancelado: 'Cancelado',
};

export function Acompanhar() {
  const { slug = '' } = useParams();
  const [params] = useSearchParams();

  const [numero, setNumero] = useState(params.get('numero') ?? '');
  const [telefone, setTelefone] = useState('');
  const [pedido, setPedido] = useState<PedidoPublico | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [buscando, setBuscando] = useState(false);

  async function buscar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setBuscando(true);

    try {
      // O telefone vai junto do número de propósito: sem ele, alguém listaria os pedidos
      // alheios só variando o número numa rota anônima.
      const { data } = await api.get<PedidoPublico>(
        `/publico/${slug}/pedidos/${numero}`,
        { params: { telefone } },
      );
      setPedido(data);
    } catch (e) {
      setPedido(null);
      setErro(mensagemDeErro(e));
    } finally {
      setBuscando(false);
    }
  }

  const etapaAtual = pedido ? ETAPAS.indexOf(pedido.status) : -1;

  return (
    <div className="tela-centro publico">
      <div className="cartao form">
        <header className="marca">
          <p className="tagline">ACOMPANHAR PEDIDO</p>
        </header>

        <form onSubmit={buscar}>
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
                required
              />
            </label>
          </div>

          <button type="submit" disabled={buscando}>
            {buscando ? 'Buscando…' : 'Buscar'}
          </button>
        </form>

        {erro && <p className="erro">{erro}</p>}

        {pedido && (
          <section className="acompanhamento">
            <h2>Pedido #{pedido.numero}</h2>

            {pedido.status === 'Cancelado' ? (
              <p className="erro">Este pedido foi cancelado pelo restaurante.</p>
            ) : (
              <ol className="regua">
                {ETAPAS.map((etapa, indice) => (
                  <li
                    key={etapa}
                    className={
                      indice < etapaAtual ? 'feito' : indice === etapaAtual ? 'atual' : ''
                    }
                  >
                    {ROTULO[etapa]}
                  </li>
                ))}
              </ol>
            )}

            <ul className="itens">
              {pedido.itens.map((item, i) => (
                <li key={i}>
                  <span>
                    {item.quantidade}× {item.produto}
                  </span>
                  <span>{moeda.format(item.total)}</span>
                </li>
              ))}
            </ul>

            <p className="total">
              <span>Total</span>
              <strong>{moeda.format(pedido.total)}</strong>
            </p>
          </section>
        )}

        <a className="link-discreto" href={`/c/${slug}`}>
          Voltar ao cardápio
        </a>
      </div>
    </div>
  );
}
