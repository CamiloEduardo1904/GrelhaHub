import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Grelha } from '../../components/Grelha';
import { CapaDaCasa } from './CapaDaCasa';
import { api, mensagemDeErro, moeda, valor } from '../../services/api';
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
 * Página anônima: nenhum token é enviado. O recorte vem do slug da URL,
 * resolvido no servidor. Os DTOs públicos não têm custo, CMV nem margem
 * (RN-GER-012, CHAPA-05) — não há cadeado porque não há campo.
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
      if (!existente) return delta > 0 ? [...atual, { produto, quantidade: delta }] : atual;

      const quantidade = existente.quantidade + delta;

      // RN-VEN-011 — quantidade sempre maior que zero: zerar remove o item em
      // vez de mandar zero para o servidor.
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
    return (
      <div className="tela-centro">
        <Grelha etapa={2} de={5} alta />
      </div>
    );
  }

  if (!cardapio) {
    return (
      <div className="tela-centro">
        <div className="placa form">
          <h1 className="wordmark">Restaurante não encontrado</h1>
          <p className="secundario">{erro ?? 'Confira o endereço do cardápio.'}</p>
        </div>
      </div>
    );
  }

  if (criado) {
    return (
      <div className="tela-centro">
        <div className="placa form" style={{ textAlign: 'center' }}>
          <p className="tagline">Pedido enviado</p>
          <p className="numero-grande">{criado.numero}</p>
          <p>
            Enviado para <strong>{cardapio.nomeFantasia}</strong> e aguardando confirmação do
            restaurante.
          </p>
          <p className="secundario dado">Total {moeda.format(criado.total)}</p>
          <a className="btn bloco" href={`/c/${slug}/acompanhar?numero=${criado.numero}`}>
            Acompanhar pedido
          </a>
          <button type="button" className="btn calada bloco" onClick={() => setCriado(null)}>
            Fazer outro pedido
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="publico">
      <CapaDaCasa
        nomeFantasia={cardapio.nomeFantasia}
        urlLogo={cardapio.urlLogo}
        endereco={cardapio.endereco}
        aberta={cardapio.aberta}
        horarios={cardapio.horarios}
      />

      <div className="vitrine">
        <section>
          {cardapio.categorias.length === 0 && (
            <p className="vazio">Este cardápio ainda não tem itens disponíveis.</p>
          )}

          {cardapio.categorias.map((categoria) => (
            <div key={categoria.id} className="grupo">
              <h2 className="grupo-titulo">{categoria.nome}</h2>

              <ul className="pratos">
                {categoria.produtos.map((produto) => {
                  const quantidade = quantidadeDe(produto.id);

                  return (
                    <li key={produto.id} className="prato">
                      {/* RN-VEN-042 — a foto do lanche. A maioria dos produtos não tem, e a
                          linha não muda de altura por causa disso. */}
                      {/* ADR-0015 — se o arquivo faltar, a linha fica SEM foto em vez de mostrar
                          o ícone de imagem quebrada do navegador. Aqui quem olha é o cliente
                          escolhendo o que comer: foto quebrada faz o restaurante parecer
                          abandonado. */}
                      {produto.urlImagem && (
                        <img
                          className="prato-foto"
                          src={produto.urlImagem}
                          alt={produto.nome}
                          onError={(e) => {
                            e.currentTarget.style.display = 'none';
                          }}
                        />
                      )}

                      <div className="prato-info">
                        <strong>{produto.nome}</strong>
                        {produto.descricao && <p className="descricao">{produto.descricao}</p>}
                        <span className="prato-preco">{moeda.format(produto.preco)}</span>
                      </div>

                      <div className="contador">
                        {quantidade > 0 && (
                          <>
                            <button
                              type="button"
                              onClick={() => alterar(produto, -1)}
                              aria-label={`Remover um ${produto.nome}`}
                            >
                              −
                            </button>
                            <span className="n">{quantidade}</span>
                          </>
                        )}
                        <button
                          type="button"
                          onClick={() => alterar(produto, 1)}
                          aria-label={`Adicionar um ${produto.nome}`}
                        >
                          +
                        </button>
                      </div>
                    </li>
                  );
                })}
              </ul>
            </div>
          ))}
        </section>

        <aside className="carrinho">
          <h2>Seu pedido</h2>

          {carrinho.length === 0 ? (
            <p className="secundario">Escolha os itens ao lado.</p>
          ) : (
            <>
              <ul className="carrinho-itens">
                {carrinho.map((item) => (
                  <li key={item.produto.id}>
                    <span>
                      <span className="fraco">{item.quantidade}×</span> {item.produto.nome}
                    </span>
                    <span>{valor.format(item.produto.preco * item.quantidade)}</span>
                  </li>
                ))}
              </ul>

              <p className="total">
                <span className="rotulo">Total</span>
                <span className="valor">{moeda.format(total)}</span>
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
                  inputMode="tel"
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
                className="btn bloco"
                onClick={() => void enviarPedido()}
                disabled={enviando || !cliente.nome || !cliente.telefone}
              >
                {enviando ? 'Enviando…' : 'Enviar pedido'}
              </button>

              <p className="nota">
                O restaurante ainda precisa confirmar — você recebe o número de
                acompanhamento.
              </p>
            </>
          )}

          <a className="link-discreto" href={`/c/${slug}/acompanhar`}>
            Já fiz um pedido
          </a>
        </aside>
      </div>
    </div>
  );
}
