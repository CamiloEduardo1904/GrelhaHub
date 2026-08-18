import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Comanda } from '../../components/Comanda';
import { Dialogo } from '../../components/Dialogo';
import { EnvioDeImagem } from '../../components/EnvioDeImagem';
import { Grelha } from '../../components/Grelha';
import { api, mensagemDeErro } from '../../services/api';
import { PROXIMA_ACAO, rotaDeAvanco } from '../../services/ciclo';
import type { CategoriaInterna, MeuRestaurante, PedidoResumo } from '../../services/tipos';
import { Cardapio } from './Cardapio';
import { encerrarSessao, lerSessao, pode } from './sessao';
import { Usuarios } from './Usuarios';

type Aba = 'pedidos' | 'cardapio' | 'link' | 'equipe';

/**
 * A aba de equipe só existe para quem consegue ver usuário (`RN-GER-010`). Esconder é
 * conveniência — a rota é protegida no pipeline da API de qualquer forma.
 */
const ABAS: { chave: Aba; nome: string; permissao?: string }[] = [
  { chave: 'pedidos', nome: 'Pedidos' },
  { chave: 'cardapio', nome: 'Cardápio' },
  { chave: 'equipe', nome: 'Equipe', permissao: 'usuario.visualizar' },
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
        {ABAS
          // RN-GER-010 — a aba que o usuário não pode abrir não aparece. Conveniência: a rota
          // continua protegida no pipeline da API.
          .filter(({ permissao }) => !permissao || pode(sessao, permissao))
          .map(({ chave, nome }) => (
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

        {aba === 'equipe' && <Usuarios />}

        {aba === 'link' && restaurante && (
          <LinkPublico restaurante={restaurante} aoMudar={carregar} />
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
  // O pedido em cancelamento. `null` = diálogo fechado — e o estado guarda o PEDIDO, não um
  // booleano, porque o diálogo mostra o número dele no título.
  const [cancelando, setCancelando] = useState<PedidoResumo | null>(null);

  const sessao = lerSessao();
  const podeCancelar = pode(sessao, 'pedido.cancelar');

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

                {/* RN-GER-002 — cancelamento exige motivo; a permissão é `pedido.cancelar`.
                    Esconder aqui é usabilidade; o servidor recusa de todo jeito. */}
                {podeCancelar && pedido.status !== 'Cancelado' && pedido.status !== 'Finalizado' && (
                  <button
                    type="button"
                    className="btn calada perigo"
                    onClick={() => setCancelando(pedido)}
                  >
                    Cancelar
                  </button>
                )}
              </>
            }
          />
        ))}
      </div>

      {cancelando && (
        <Dialogo
          titulo={`Cancelar o pedido nº ${cancelando.numero}?`}
          descricao={
            // RN-EST-018 / RN-EST-019 são de estoque e ainda não valem (onda 3), mas o motivo já
            // entra na trilha hoje (SPEC-GER-005) — e é lido depois por gente.
            'O pedido não é apagado: ele fica registrado como cancelado, com o motivo, ' +
            'quem cancelou e quando.'
          }
          motivo={{
            rotulo: 'Motivo do cancelamento',
            minimo: 3,
            exemplo: 'Cliente desistiu na porta',
          }}
          rotuloConfirmar="Cancelar pedido"
          perigo
          aoConfirmar={async (motivo) => {
            await aoAgir(cancelando.id, 'cancelar', { motivo });
            setCancelando(null);
          }}
          aoFechar={() => setCancelando(null)}
        />
      )}
    </>
  );
}

function LinkPublico({
  restaurante,
  aoMudar,
}: {
  restaurante: MeuRestaurante;
  aoMudar: () => Promise<void>;
}) {
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

      <fieldset>
        <legend>Logomarca</legend>

        {/* RN-GER-026 — exibida no topo do cardápio público. Sem logo, aparece o nome fantasia:
            a ausência é estado válido, não erro a corrigir com imagem genérica. */}
        <EnvioDeImagem
          rota="/v1/unidade/logo"
          urlAtual={restaurante.urlLogo}
          rotulo={`Logo de ${restaurante.nomeFantasia}`}
          aoEnviar={aoMudar}
        />

        <p className="secundario">JPEG ou PNG, até 5 MB.</p>
      </fieldset>

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
