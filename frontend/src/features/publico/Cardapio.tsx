import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { api, mensagemDeErro, moeda } from '../../services/api';
import type { CardapioPublico, ProdutoPublico } from '../../services/tipos';

interface ItemDoCarrinho {
  produto: ProdutoPublico;
  quantidade: number;
}

interface PedidoCriado {
  numero: number;
  total: number;
  status: string;
}

/**
 * Cardápio público — RN-VEN-036, RN-VEN-037.
 *
 * Página anônima: nenhum token é enviado. O recorte de dados vem do slug da URL, resolvido
 * no servidor. Os DTOs públicos não têm custo, CMV nem margem (RN-GER-012).
 */
export function Cardapio() {
  const { slug = '' } = useParams();

  const [cardapio, setCardapio] = useState<CardapioPublico | null>(null);
  const [carrinho, setCarrinho] = useState<ItemDoCarrinho[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [criado, setCriado] = useState<PedidoCriado | null>(null);

  const [cliente, setCliente] = useState({ nome: '', telefone: '', observacao: '' });

  useEffect(() => {
    void (async () => {
      try {
        const { data } = await api.get<CardapioPublico>(`/publico/${slug}/cardapio`);
        setCardapio(data);
      } catch (e) {
        setErro(mensagemDeErro(e));
      } finally {
        setCarregando(false);
      }
    })();
  }, [slug]);

  const total = useMemo(
    () => carrinho.reduce((soma, item) => soma + item.produto.preco * item.quantidade, 0),
    [carrinho],
  );

  function alterar(produto: ProdutoPublico, delta: number) {
    setCarrinho((atual) => {
      const existente = atual.find((i) => i.produto.id === produto.id);

      if (!existente) {
        return delta > 0 ? [...atual, { produto, quantidade: delta }] : atual;
      }

      const quantidade = existente.quantidade + delta;

      // RN-VEN-011 — quantidade sempre maior que zero: zerar remove o item em vez de
      // mandar zero para o servidor.
      return quantidade <= 0
        ? atual.filter((i) => i.produto.id !== produto.id)
        : atual.map((i) => (i.produto.id === produto.id ? { ...i, quantidade } : i));
    });
  }

  function quantidadeDe(produtoId: string) {
    return carrinho.find((i) => i.produto.id === produtoId)?.quantidade ?? 0;
  }

  async function enviarPedido() {
    setErro(null);
    setEnviando(true);

    try {
      const { data } = await api.post<PedidoCriado>(`/publico/${slug}/pedidos`, {
        nomeCliente: cliente.nome,
        telefone: cliente.telefone,
        observacao: cliente.observacao || null,
        itens: carrinho.map((i) => ({
          produtoId: i.produto.id,
          quantidade: i.quantidade,
          observacao: null,
        })),
      });

      setCriado(data);
      setCarrinho([]);
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  if (carregando) {
    return <div className="tela-centro publico">Carregando cardápio…</div>;
  }

  if (!cardapio) {
    return (
      <div className="tela-centro publico">
        <div className="cartao centro">
          <h1>Restaurante não encontrado</h1>
          <p className="secundario">{erro ?? 'Confira o endereço do cardápio.'}</p>
        </div>
      </div>
    );
  }

  if (criado) {
    return (
      <div className="tela-centro publico">
        <div className="cartao centro">
          <p className="tagline">PEDIDO ENVIADO</p>
          <h1 className="numero-grande">#{criado.numero}</h1>
          <p>
            Seu pedido foi enviado para <strong>{cardapio.nomeFantasia}</strong> e está
            aguardando confirmação do restaurante.
          </p>
          <p className="secundario">
            Total: {moeda.format(criado.total)} · Guarde o número {criado.numero} para
            acompanhar.
          </p>
          <a className="botao-link" href={`/c/${slug}/acompanhar?numero=${criado.numero}`}>
            Acompanhar pedido
          </a>
          <button type="button" className="secundario-btn" onClick={() => setCriado(null)}>
            Fazer outro pedido
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="publico">
      <header className="capa">
        <p className="tagline">CARDÁPIO ONLINE</p>
        <h1>{cardapio.nomeFantasia}</h1>
        {cardapio.telefone && <p className="secundario">{cardapio.telefone}</p>}
      </header>

      <main className="publico-conteudo">
        <section className="itens-cardapio">
          {cardapio.categorias.length === 0 && (
            <p className="vazio">Este cardápio ainda não tem itens disponíveis.</p>
          )}

          {cardapio.categorias.map((categoria) => (
            <section key={categoria.id}>
              <h2>{categoria.nome}</h2>

              <ul className="produtos">
                {categoria.produtos.map((produto) => {
                  const quantidade = quantidadeDe(produto.id);

                  return (
                    <li key={produto.id} className="produto">
                      <div>
                        <strong>{produto.nome}</strong>
                        {produto.descricao && <p className="secundario">{produto.descricao}</p>}
                        <span className="preco">{moeda.format(produto.preco)}</span>
                      </div>

                      <div className="contador">
                        {quantidade > 0 && (
                          <>
                            <button type="button" onClick={() => alterar(produto, -1)}>
                              −
                            </button>
                            <span>{quantidade}</span>
                          </>
                        )}
                        <button type="button" onClick={() => alterar(produto, 1)}>
                          +
                        </button>
                      </div>
                    </li>
                  );
                })}
              </ul>
            </section>
          ))}
        </section>

        <aside className="carrinho">
          <h2>Seu pedido</h2>

          {carrinho.length === 0 ? (
            <p className="secundario">Escolha os itens ao lado.</p>
          ) : (
            <>
              <ul className="itens">
                {carrinho.map((item) => (
                  <li key={item.produto.id}>
                    <span>
                      {item.quantidade}× {item.produto.nome}
                    </span>
                    <span>{moeda.format(item.produto.preco * item.quantidade)}</span>
                  </li>
                ))}
              </ul>

              <p className="total">
                <span>Total</span>
                <strong>{moeda.format(total)}</strong>
              </p>

              <label>
                Seu nome
                <input
                  value={cliente.nome}
                  onChange={(e) => setCliente({ ...cliente, nome: e.target.value })}
                  required
                />
              </label>

              <label>
                Telefone com DDD
                <input
                  value={cliente.telefone}
                  onChange={(e) => setCliente({ ...cliente, telefone: e.target.value })}
                  placeholder="(11) 98765-4321"
                  required
                />
              </label>

              <label>
                Observação
                <textarea
                  value={cliente.observacao}
                  onChange={(e) => setCliente({ ...cliente, observacao: e.target.value })}
                  rows={2}
                  placeholder="Ponto da carne, sem cebola…"
                />
              </label>

              {erro && <p className="erro">{erro}</p>}

              <button
                type="button"
                onClick={() => void enviarPedido()}
                disabled={enviando || !cliente.nome || !cliente.telefone}
              >
                {enviando ? 'Enviando…' : 'Enviar pedido'}
              </button>

              <p className="nota">
                O restaurante ainda precisa confirmar o pedido — você receberá o número de
                acompanhamento.
              </p>
            </>
          )}

          <a className="link-discreto" href={`/c/${slug}/acompanhar`}>
            Já fiz um pedido, quero acompanhar
          </a>
        </aside>
      </main>
    </div>
  );
}
