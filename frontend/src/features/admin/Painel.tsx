import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, mensagemDeErro, moeda } from '../../services/api';
import type {
  CategoriaInterna,
  MeuRestaurante,
  PedidoResumo,
  StatusPedido,
} from '../../services/tipos';
import { ehGerencia, encerrarSessao, lerSessao } from './sessao';

type Aba = 'pedidos' | 'cardapio' | 'link';

/** RN-VEN-018 — rótulos do ciclo de vida, na ordem em que acontecem. */
const ROTULO_STATUS: Record<StatusPedido, string> = {
  Aberto: 'Aberto',
  Confirmado: 'Confirmado',
  EmProducao: 'Em produção',
  Pronto: 'Pronto',
  Entregue: 'Entregue',
  Finalizado: 'Finalizado',
  Cancelado: 'Cancelado',
};

const PROXIMA_ACAO: Partial<Record<StatusPedido, string>> = {
  Aberto: 'Confirmar',
  Confirmado: 'Iniciar produção',
  EmProducao: 'Marcar pronto',
  Pronto: 'Marcar entregue',
  Entregue: 'Finalizar',
};

export function Painel() {
  const navegar = useNavigate();
  const sessao = lerSessao();

  const [aba, setAba] = useState<Aba>('pedidos');
  const [restaurante, setRestaurante] = useState<MeuRestaurante | null>(null);
  const [pedidos, setPedidos] = useState<PedidoResumo[]>([]);
  const [cardapio, setCardapio] = useState<CategoriaInterna[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  const carregar = useCallback(async () => {
    try {
      const [meu, listaPedidos, listaCardapio] = await Promise.all([
        api.get<MeuRestaurante>('/v1/restaurante/meu'),
        api.get<PedidoResumo[]>('/v1/pedidos'),
        api.get<CategoriaInterna[]>('/v1/cardapio'),
      ]);
      setRestaurante(meu.data);
      setPedidos(listaPedidos.data);
      setCardapio(listaCardapio.data);
      setErro(null);
    } catch (e) {
      setErro(mensagemDeErro(e));
    }
  }, []);

  useEffect(() => {
    void carregar();

    // O painel de pedidos é uma tela de operação: fica aberta o serviço inteiro e precisa
    // mostrar pedido novo sem alguém apertar F5. Polling simples resolve nesta fatia;
    // tempo real (SSE ou WebSocket) entra com o KDS, na onda 7.
    const timer = setInterval(() => void carregar(), 15_000);
    return () => clearInterval(timer);
  }, [carregar]);

  async function acao(id: string, caminho: string, corpo?: unknown) {
    try {
      await api.post(`/v1/pedidos/${id}/${caminho}`, corpo ?? {});
      await carregar();
    } catch (e) {
      setErro(mensagemDeErro(e));
    }
  }

  function sair() {
    encerrarSessao();
    navegar('/entrar');
  }

  const linkCompleto = restaurante ? `${location.origin}/c/${restaurante.slug}` : '';

  return (
    <div className="painel">
      <header className="barra">
        <div className="marca-compacta">
          Grelha<span className="destaque">Hub</span>
        </div>
        <div className="barra-info">
          <strong>{restaurante?.nomeFantasia ?? '…'}</strong>
          <span className="secundario">
            {sessao?.nome} · {sessao?.perfil}
          </span>
        </div>
        <button className="secundario-btn" onClick={sair} type="button">
          Sair
        </button>
      </header>

      <nav className="abas">
        {(['pedidos', 'cardapio', 'link'] as Aba[]).map((chave) => (
          <button
            key={chave}
            type="button"
            className={aba === chave ? 'aba ativa' : 'aba'}
            onClick={() => setAba(chave)}
          >
            {chave === 'pedidos' && `Pedidos (${pedidos.length})`}
            {chave === 'cardapio' && 'Cardápio'}
            {chave === 'link' && 'Link público'}
          </button>
        ))}
      </nav>

      {erro && <p className="erro faixa">{erro}</p>}

      <main className="conteudo">
        {aba === 'pedidos' && (
          <Pedidos pedidos={pedidos} restaurante={restaurante} aoAgir={acao} />
        )}

        {aba === 'cardapio' && <Cardapio categorias={cardapio} aoMudar={carregar} />}

        {aba === 'link' && restaurante && (
          <LinkPublico restaurante={restaurante} link={linkCompleto} />
        )}
      </main>
    </div>
  );
}

function Pedidos({
  pedidos,
  restaurante,
  aoAgir,
}: {
  pedidos: PedidoResumo[];
  restaurante: MeuRestaurante | null;
  aoAgir: (id: string, caminho: string, corpo?: unknown) => Promise<void>;
}) {
  const sessao = lerSessao();

  if (pedidos.length === 0) {
    return (
      <div className="vazio">
        <p>Nenhum pedido no dia operacional de hoje.</p>
        <p className="secundario">
          Abra o link público do seu cardápio em outra aba e faça um pedido para ver ele
          aparecer aqui.
        </p>
      </div>
    );
  }

  return (
    <>
      <p className="secundario resumo-dia">
        {/* RN-GER-006 ⚠⚠ — o dia operacional vai das 04h00 às 03h59 do dia seguinte. */}
        Dia operacional <strong>{pedidos[0]?.dataOperacional}</strong> ·{' '}
        {restaurante?.pedidosDoDia ?? pedidos.length} pedido(s)
      </p>

      <div className="grade">
        {pedidos.map((pedido) => (
          <article key={pedido.id} className={`cartao pedido status-${pedido.status}`}>
            <header>
              <span className="numero">#{pedido.numero}</span>
              <span className={`etiqueta ${pedido.status}`}>{ROTULO_STATUS[pedido.status]}</span>
            </header>

            <p className="cliente">
              {pedido.cliente ?? 'Cliente não identificado'}
              {pedido.telefone && <span className="secundario"> · {pedido.telefone}</span>}
            </p>

            <ul className="itens">
              {pedido.itens.map((item, i) => (
                <li key={i}>
                  <span>
                    {item.quantidade}× {item.produto}
                  </span>
                  <span>{moeda.format(item.precoPraticado * item.quantidade)}</span>
                </li>
              ))}
            </ul>

            {pedido.observacao && <p className="observacao">“{pedido.observacao}”</p>}

            <footer>
              <strong>{moeda.format(pedido.total)}</strong>

              <div className="acoes">
                {PROXIMA_ACAO[pedido.status] && (
                  <button
                    type="button"
                    onClick={() =>
                      aoAgir(pedido.id, pedido.status === 'Aberto' ? 'confirmar' : 'avancar')
                    }
                  >
                    {PROXIMA_ACAO[pedido.status]}
                  </button>
                )}

                {/* RN-VEN-014 — cancelamento é de gerência e exige motivo.
                    Esconder o botão aqui é usabilidade; o servidor recusa de todo jeito. */}
                {ehGerencia(sessao) &&
                  pedido.status !== 'Cancelado' &&
                  pedido.status !== 'Finalizado' && (
                    <button
                      type="button"
                      className="perigo"
                      onClick={() => {
                        const motivo = prompt('Motivo do cancelamento:');
                        if (motivo) void aoAgir(pedido.id, 'cancelar', { motivo });
                      }}
                    >
                      Cancelar
                    </button>
                  )}
              </div>
            </footer>
          </article>
        ))}
      </div>
    </>
  );
}

function Cardapio({
  categorias,
  aoMudar,
}: {
  categorias: CategoriaInterna[];
  aoMudar: () => Promise<void>;
}) {
  async function alternar(produtoId: string, disponivel: boolean) {
    await api.patch(`/v1/cardapio/produtos/${produtoId}/disponibilidade`, { disponivel });
    await aoMudar();
  }

  return (
    <div className="lista">
      {categorias.map((categoria) => (
        <section key={categoria.id} className="cartao">
          <h2>{categoria.nome}</h2>

          <table className="tabela">
            <thead>
              <tr>
                <th>Produto</th>
                <th>Preço</th>
                <th>No cardápio</th>
              </tr>
            </thead>
            <tbody>
              {categoria.produtos.map((produto) => (
                <tr key={produto.id} className={produto.disponivel ? '' : 'apagado'}>
                  <td>
                    <strong>{produto.nome}</strong>
                    {produto.descricao && <p className="secundario">{produto.descricao}</p>}
                  </td>
                  <td>{moeda.format(produto.preco)}</td>
                  <td>
                    <label className="alternador">
                      <input
                        type="checkbox"
                        checked={produto.disponivel}
                        onChange={(e) => void alternar(produto.id, e.target.checked)}
                      />
                      <span>{produto.disponivel ? 'Sim' : 'Acabou'}</span>
                    </label>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      ))}
    </div>
  );
}

function LinkPublico({ restaurante, link }: { restaurante: MeuRestaurante; link: string }) {
  const [copiado, setCopiado] = useState(false);

  return (
    <div className="cartao centro">
      <h2>Cardápio online</h2>
      <p className="secundario">
        Este é o endereço que você divulga para os clientes. Ele é fixo e não muda.
      </p>

      <div className="link-box">
        <code>{link}</code>
        <button
          type="button"
          onClick={async () => {
            await navigator.clipboard.writeText(link);
            setCopiado(true);
            setTimeout(() => setCopiado(false), 2000);
          }}
        >
          {copiado ? 'Copiado!' : 'Copiar'}
        </button>
      </div>

      <a className="botao-link" href={`/c/${restaurante.slug}`} target="_blank" rel="noreferrer">
        Abrir cardápio
      </a>

      <dl className="dados">
        <div>
          <dt>Produtos ativos</dt>
          <dd>{restaurante.totalDeProdutos}</dd>
        </div>
        <div>
          <dt>Pedidos hoje</dt>
          <dd>{restaurante.pedidosDoDia}</dd>
        </div>
        <div>
          <dt>CNPJ</dt>
          <dd>{restaurante.cnpj}</dd>
        </div>
      </dl>
    </div>
  );
}
