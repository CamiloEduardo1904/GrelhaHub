import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Comanda } from '../../components/Comanda';
import { Grelha } from '../../components/Grelha';
import { api, mensagemDeErro, moeda } from '../../services/api';
import { PROXIMA_ACAO, rotaDeAvanco } from '../../services/ciclo';
import type { CategoriaInterna, MeuRestaurante, PedidoResumo } from '../../services/tipos';
import { ehGerencia, encerrarSessao, lerSessao } from './sessao';

type Aba = 'pedidos' | 'cardapio' | 'link';

const ABAS: { chave: Aba; nome: string }[] = [
  { chave: 'pedidos', nome: 'Pedidos' },
  { chave: 'cardapio', nome: 'Cardápio' },
  { chave: 'link', nome: 'Link público' },
];

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

    // O painel fica aberto o serviço inteiro e precisa mostrar pedido novo sem
    // alguém apertar F5. Polling resolve nesta fatia; tempo real entra com o KDS.
    const timer = setInterval(() => void carregar(), 15_000);
    return () => clearInterval(timer);
  }, [carregar]);

  async function agir(id: string, caminho: string, corpo?: unknown) {
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

  return (
    <div className="painel">
      <header className="barra-app">
        <span className="wordmark">
          Grelha<span>Hub</span>
        </span>

        <span className="quem">
          <strong>{restaurante?.nomeFantasia ?? '…'}</strong>
          {sessao?.nome} · {sessao?.perfil}
        </span>

        <button className="btn" onClick={sair} type="button">
          Sair
        </button>
      </header>

      <nav className="abas" role="tablist" aria-label="Seções do painel">
        {ABAS.map(({ chave, nome }) => (
          <button
            key={chave}
            type="button"
            role="tab"
            className="aba"
            aria-selected={aba === chave}
            onClick={() => setAba(chave)}
          >
            {/* CHAPA-02 — uma barra âmbar por grupo: a aba ativa. */}
            <Grelha etapa={aba === chave ? 0 : -1} de={1} />
            {nome}
            {chave === 'pedidos' && pedidos.length > 0 && ` · ${pedidos.length}`}
          </button>
        ))}
      </nav>

      <main className="conteudo">
        {erro && <p className="erro">{erro}</p>}

        {aba === 'pedidos' && (
          <Pedidos pedidos={pedidos} restaurante={restaurante} aoAgir={agir} />
        )}

        {aba === 'cardapio' && <Cardapio categorias={cardapio} aoMudar={carregar} />}

        {aba === 'link' && restaurante && <LinkPublico restaurante={restaurante} />}
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
  const gerencia = ehGerencia(lerSessao());

  if (pedidos.length === 0) {
    return (
      <div className="vazio">
        <Grelha etapa={-1} de={6} alta />
        <p>Nenhum pedido no dia operacional de hoje.</p>
        <p className="secundario">
          Abra o link público do seu cardápio em outra aba e faça um pedido para vê-lo
          aparecer aqui.
        </p>
      </div>
    );
  }

  return (
    <>
      <p className="secundario">
        {/* RN-GER-006 — o dia operacional vai das 04h00 às 03h59 do dia seguinte. */}
        Dia operacional <strong className="dado">{pedidos[0]?.dataOperacional}</strong> ·{' '}
        {restaurante?.pedidosDoDia ?? pedidos.length} pedido(s)
      </p>

      <div className="grade">
        {pedidos.map((pedido) => (
          <Comanda
            key={pedido.id}
            pedido={pedido}
            acoes={
              <>
                {PROXIMA_ACAO[pedido.status] && (
                  <button
                    type="button"
                    className="btn"
                    onClick={() => void aoAgir(pedido.id, rotaDeAvanco(pedido.status))}
                  >
                    {PROXIMA_ACAO[pedido.status]}
                  </button>
                )}

                {/* RN-VEN-014 — cancelamento é de gerência e exige motivo.
                    Esconder aqui é usabilidade; o servidor recusa de todo jeito. */}
                {gerencia && pedido.status !== 'Cancelado' && pedido.status !== 'Finalizado' && (
                  <button
                    type="button"
                    className="btn calada perigo"
                    onClick={() => {
                      const motivo = prompt('Motivo do cancelamento:');
                      if (motivo) void aoAgir(pedido.id, 'cancelar', { motivo });
                    }}
                  >
                    Cancelar
                  </button>
                )}
              </>
            }
          />
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
    <>
      {categorias.map((categoria) => (
        <section key={categoria.id} className="placa rolagem">
          <table>
            <caption>{categoria.nome}</caption>
            <thead>
              <tr>
                <th scope="col">Produto</th>
                <th scope="col" style={{ textAlign: 'right' }}>
                  Preço
                </th>
                <th scope="col">No cardápio</th>
              </tr>
            </thead>
            <tbody>
              {/* Cartoon para aqui. Dado é tabulação, contraste e nenhuma surpresa.
                  Sem coluna de custo: RN-GER-012 — a API não devolve o campo. */}
              {categoria.produtos.map((produto) => (
                <tr key={produto.id} className={produto.disponivel ? '' : 'apagado'}>
                  <td>
                    <strong>{produto.nome}</strong>
                    {produto.descricao && <p className="secundario">{produto.descricao}</p>}
                  </td>
                  <td className="num">{moeda.format(produto.preco)}</td>
                  <td>
                    <label className="alternador">
                      <input
                        type="checkbox"
                        checked={produto.disponivel}
                        onChange={(e) => void alternar(produto.id, e.target.checked)}
                      />
                      {produto.disponivel ? 'Sim' : 'Acabou'}
                    </label>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      ))}
    </>
  );
}

function LinkPublico({ restaurante }: { restaurante: MeuRestaurante }) {
  const [copiado, setCopiado] = useState(false);
  const link = `${location.origin}/c/${restaurante.slug}`;

  return (
    <div className="painel-cartao">
      <div>
        <h2 className="wordmark">Cardápio online</h2>
        <p className="secundario">
          Este é o endereço que você divulga. Ele é fixo e não muda.
        </p>
      </div>

      <div className="link-box">
        <code>{link}</code>
        <button
          type="button"
          className="btn"
          onClick={async () => {
            await navigator.clipboard.writeText(link);
            setCopiado(true);
            setTimeout(() => setCopiado(false), 2000);
          }}
        >
          {copiado ? 'Copiado' : 'Copiar'}
        </button>
      </div>

      <a className="btn calada" href={`/c/${restaurante.slug}`} target="_blank" rel="noreferrer">
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
      </dl>
    </div>
  );
}
