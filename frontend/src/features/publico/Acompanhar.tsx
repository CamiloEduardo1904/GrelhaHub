import { useState, type FormEvent } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { Grelha } from '../../components/Grelha';
import { Selo } from '../../components/Selo';
import { api, mensagemDeErro, moeda, valor } from '../../services/api';
import { CICLO, etapaDe, ROTULO_STATUS } from '../../services/ciclo';
import type { StatusPedido } from '../../services/tipos';

interface PedidoPublico {
  numero: number;
  status: StatusPedido;
  total: number;
  criadoEm: string;
  itens: { produto: string; quantidade: number; precoPraticado: number; total: number }[];
}

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
          </section>
        )}

        <a className="link-discreto" href={`/c/${slug}`}>
          Voltar ao cardápio
        </a>
      </div>
    </div>
  );
}
