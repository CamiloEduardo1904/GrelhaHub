import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlternadorDeTema } from '../../components/AlternadorDeTema';
import { Comanda } from '../../components/Comanda';
import { Dialogo } from '../../components/Dialogo';
import { EnvioDeImagem } from '../../components/EnvioDeImagem';
import { IconeImpressora } from '../../components/Icone';
import { QRCodeSVG } from 'qrcode.react';
import { Grelha } from '../../components/Grelha';
import { api, mensagemDeErro } from '../../services/api';
import {
  chegaColapsado,
  grupoDaLista,
  GRUPOS_DA_LISTA,
  PROXIMA_ACAO,
  ROTULO_DO_GRUPO,
  rotaDeAvanco,
} from '../../services/ciclo';
import type { GrupoDaLista } from '../../services/ciclo';
import type { CategoriaInterna, MeuRestaurante, PedidoResumo } from '../../services/tipos';
import { CadastroDeOperacao } from '../plataforma/CadastroDeOperacao';
import { Cardapio } from './Cardapio';
import { encerrarSessao, lerSessao, pode } from './sessao';
import { comoConsulta, iso, periodoDeHoje, type Periodo } from '../../services/periodo';
import { FiltroDePeriodo } from './FiltroDePeriodo';
import { Monitor } from './Monitor';
import { Operacao } from './Operacao';
import { Usuarios } from './Usuarios';

type Aba = 'pedidos' | 'cardapio' | 'link' | 'equipe' | 'loja' | 'operacao' | 'monitor';

/**
 * A aba de equipe só existe para quem consegue ver usuário (`RN-GER-010`). Esconder é
 * conveniência — a rota é protegida no pipeline da API de qualquer forma.
 */
const ABAS: { chave: Aba; nome: string; permissao?: string }[] = [
  { chave: 'pedidos', nome: 'Pedidos' },
  { chave: 'cardapio', nome: 'Cardápio' },
  { chave: 'equipe', nome: 'Equipe', permissao: 'usuario.visualizar' },
  { chave: 'link', nome: 'Link público' },

  // RN-GER-036 / RN-GER-037 — o cadastro da própria casa. A aba só aparece para quem tem
  // `unidade.visualizar`: aba que abre em 403 é pior do que aba ausente.
  { chave: 'loja', nome: 'Loja', permissao: 'unidade.visualizar' },

  // RN-GER-038 — o painel de operacao tem permissao PROPRIA, separada de pedido.visualizar:
  // o atendente ve o pedido da mesa 4 e nao ve o faturamento da semana.
  { chave: 'operacao', nome: 'Operação', permissao: 'operacao.visualizar' },

  // RN-VEN-054 — a fila do que ainda está na casa. ⚠ Permissão é `pedido.visualizar`, e NÃO
  // `operacao.visualizar`: a fila não agrega nada, é um recorte da mesma lista com menos campos.
  // Quem prepara o sanduíche precisa dela; quem prepara o sanduíche não vê faturamento.
  { chave: 'monitor', nome: 'Monitor', permissao: 'pedido.visualizar' },
];

export function Painel() {
  const navegar = useNavigate();
  const sessao = lerSessao();

  const [aba, setAba] = useState<Aba>('pedidos');
  const [restaurante, setRestaurante] = useState<MeuRestaurante | null>(null);
  const [pedidos, setPedidos] = useState<PedidoResumo[]>([]);
  const [cardapio, setCardapio] = useState<CategoriaInterna[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  /**
   * `RN-GER-006` ⚠⚠ — o recorte da lista é de **dia operacional**, e abre no dia corrente.
   *
   * O filtro já existia na API desde a fatia vertical; o que faltava era a tela. Quem operava só
   * enxergava hoje: para conferir o pedido de ontem à noite — que é o caso mais comum de ligação
   * de cliente — não havia caminho nenhum.
   */
  const [periodo, setPeriodo] = useState<Periodo>(() => periodoDeHoje());

  const carregar = useCallback(async () => {
    try {
      const [meu, listaPedidos, listaCardapio] = await Promise.all([
        api.get<MeuRestaurante>('/v1/restaurante/meu'),
        api.get<PedidoResumo[]>(`/v1/pedidos?${comoConsulta(periodo)}`),
        api.get<CategoriaInterna[]>('/v1/cardapio'),
      ]);
      setRestaurante(meu.data);
      setPedidos(listaPedidos.data);
      setCardapio(listaCardapio.data);
      setErro(null);
    } catch (e) {
      setErro(mensagemDeErro(e));
    }
  }, [periodo]);

  useEffect(() => {
    // Carga imediata — ao montar, ao trocar o período e ao VOLTAR para uma aba. Dado de quinze
    // segundos atrás na tela de quem acabou de chegar é o mesmo problema com outro nome.
    void carregar();

    // ⚠⚠ RN-VEN-055 — a CORRENTE de polling só existe na aba que a usa.
    //
    // Aqui havia um `setInterval` incondicional: ele consultava a cada 15 s **com a aba de
    // cardápio à frente, com a janela escondida e com o monitor aberto**. Abrir a fila de
    // monitoramento passou a SOMAR uma corrente em vez de trocá-la — duas telas perguntando a
    // mesma coisa ao mesmo tempo, e nenhuma das duas sabendo da outra.
    //
    // O `document.hidden` dentro do callback é o segundo lado: aba de fundo não precisa perguntar
    // nada. ⚠ Aqui um `return` seco é seguro, e em `useFilaAoVivo` NÃO seria — `setInterval`
    // continua disparando sozinho, então pular um ciclo não solta a corrente; lá a corrente é uma
    // cadeia de `setTimeout`, e quem suspende precisa reagendar.
    if (aba !== 'pedidos') {
      return;
    }

    const timer = setInterval(() => {
      if (!document.hidden) {
        void carregar();
      }
    }, 15_000);

    return () => clearInterval(timer);
  }, [aba, carregar]);

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

        {/* Design system §2.1 — escuro é a cozinha, claro é o escritório. Fica ANTES de Sair,
            no fim da barra: é a coisa que se mexe uma vez e não se procura de novo. */}
        <AlternadorDeTema />

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
          <>
            <FiltroDePeriodo periodo={periodo} aoMudar={setPeriodo} />
            <Pedidos pedidos={pedidos} aoAgir={agir} />
          </>
        )}

        {/* RN-GER-038 — a aba só existe para quem tem a permissão, e a rota recusa de todo jeito. */}
        {aba === 'operacao' && <Operacao />}

        {/* RN-VEN-054 / RN-VEN-055 — a fila do que ainda está na casa, com corrente própria.
            `ativo` sai da condição real e não de um `true` literal: hoje o componente é
            desmontado ao sair da aba, e no dia em que alguém o mantiver montado o polling continua
            suspenso pelo motivo certo. */}
        {aba === 'monitor' && <Monitor restaurante={restaurante} ativo={aba === 'monitor'} />}

        {aba === 'cardapio' && <Cardapio categorias={cardapio} aoMudar={carregar} />}

        {aba === 'equipe' && <Usuarios />}

        {aba === 'link' && restaurante && (
          <LinkPublico restaurante={restaurante} aoMudar={carregar} />
        )}

        {/* RN-GER-036 — o restaurante cadastra o próprio horário. Só existe porque a permissão
            `unidade.gerenciar` foi semeada nos perfis por migration de dados: entregar a tela
            antes dela daria um botão que responde 403. */}
        {aba === 'loja' && <CadastroDeOperacao />}
      </main>
    </div>
  );
}

/**
 * Os pedidos, em blocos de **dia operacional** — `RN-GER-006` ⚠⚠.
 *
 * ⚠ Ordem decrescente: o dia mais recente primeiro, e dentro dele o número maior primeiro. Quem
 * abre o painel durante o serviço quer o pedido que acabou de entrar, não o da semana passada.
 */
function agruparPorDiaOperacional(
  pedidos: PedidoResumo[],
): { dia: string; doDia: PedidoResumo[] }[] {
  const porDia = new Map<string, PedidoResumo[]>();

  for (const pedido of pedidos) {
    const dia = pedido.dataOperacional;
    porDia.set(dia, [...(porDia.get(dia) ?? []), pedido]);
  }

  return [...porDia.entries()]
    .sort(([a], [b]) => b.localeCompare(a))
    .map(([dia, doDia]) => ({
      dia,
      doDia: [...doDia].sort((a, b) => b.numero - a.numero),
    }));
}

/**
 * "Hoje", "Ontem", ou o dia da semana.
 *
 * ⚠ Compara com o dia do CALENDÁRIO de quem está olhando, e não com o dia operacional corrente da
 * casa — que o cliente não conhece. Entre a meia-noite e a virada, "hoje" no relógio da pessoa
 * ainda é o dia operacional de ontem, e é por isso que o rótulo nunca aparece sozinho: a data
 * completa vem ao lado, em mono, e é ela que manda.
 */
function rotuloDoDia(dia: string): string {
  const hoje = new Date();
  const ontem = new Date();
  ontem.setDate(ontem.getDate() - 1);

  if (dia === iso(hoje)) return 'Hoje';
  if (dia === iso(ontem)) return 'Ontem';

  const [ano, mes, data] = dia.split('-').map(Number);
  const quando = new Date(ano, mes - 1, data);

  return quando.toLocaleDateString('pt-BR', { weekday: 'long' });
}

/**
 * A lista de pedidos — `RN-VEN-057` ⚠, `RN-GER-006` ⚠⚠.
 *
 * **Duas hierarquias, e as duas são obrigatórias por motivos diferentes.**
 *
 * `RN-GER-006` manda agrupar por **dia operacional**: `numero` reinicia a cada virada, e sem o
 * divisor a comanda #1 de hoje e a #1 de ontem aparecem idênticas.
 *
 * `RN-VEN-057` manda separar por **grupo** dentro do dia: às onze da noite, o que ainda falta
 * preparar dividia espaço com dezenas de comandas inteiras que ninguém vai reler, e a rolagem era
 * toda delas.
 *
 * ⚠ `restaurante` saiu das propriedades: o contador vinha de `pedidosDoDia`, que é do dia corrente.
 * Com a lista podendo trazer vários dias, esse número passou a contradizer o que estava na tela —
 * cada bloco conta o seu, e é o único jeito de o contador não mentir.
 */
function Pedidos({
  pedidos,
  aoAgir,
}: {
  pedidos: PedidoResumo[];
  aoAgir: (id: string, caminho: string, corpo?: unknown) => Promise<void>;
}) {
  // O pedido em cancelamento. `null` = diálogo fechado — e o estado guarda o PEDIDO, não um
  // booleano, porque o diálogo mostra o número dele no título.
  const [cancelando, setCancelando] = useState<PedidoResumo | null>(null);

  /** `RN-VEN-057` — o grupo em foco. `todos` mostra os três, separados por divisor. */
  const [foco, setFoco] = useState<GrupoDaLista | 'todos'>('todos');

  /**
   * `RN-VEN-057` ⚠⚠ — os pedidos cujo estado de abertura está **invertido** em relação ao padrão.
   *
   * **Um conjunto e não dois, e isso é o que faz o clique valer nos dois sentidos.** O padrão vem do
   * estado do pedido (`chegaColapsado`): concluído e cancelado chegam fechados, o que está na casa
   * chega aberto. Guardar "os que o usuário abriu" exigiria um segundo conjunto para "os que o
   * usuário fechou", e o segundo sempre é o que alguém esquece.
   *
   * ⚠ O efeito colateral é o certo: um pedido que **anda** enquanto a tela está aberta reassume o
   * padrão do estado novo. O pedido que acabou de ser entregue fecha sozinho na próxima consulta —
   * a menos que quem estava olhando o tenha aberto de propósito.
   */
  const [alternados, setAlternados] = useState<ReadonlySet<string>>(new Set());

  const sessao = lerSessao();
  const podeCancelar = pode(sessao, 'pedido.cancelar');

  function alternar(id: string) {
    setAlternados((atual) => {
      const proximo = new Set(atual);
      if (!proximo.delete(id)) proximo.add(id);

      return proximo;
    });
  }

  // ⚠ As contagens dos filtros vêm do PERÍODO INTEIRO, e as dos blocos, do que está exibido —
  // `RN-VEN-057`. Contador de filtro que respeitasse o filtro ativo mostraria o total do grupo
  // selecionado e zero nos outros, que é a informação inútil: quem olha o chip quer saber o que
  // encontraria clicando nele.
  const totalDoGrupo = (grupo: GrupoDaLista) =>
    pedidos.filter((pedido) => grupoDaLista(pedido.status) === grupo).length;

  const visiveis =
    foco === 'todos' ? pedidos : pedidos.filter((pedido) => grupoDaLista(pedido.status) === foco);

  if (pedidos.length === 0) {
    return (
      <div className="vazio">
        <Grelha etapa={-1} de={6} alta />
        <p>Nenhum pedido no período escolhido.</p>
        <p className="secundario">
          Abra o link público do seu cardápio em outra aba e faça um pedido para vê-lo
          aparecer aqui.
        </p>
      </div>
    );
  }

  return (
    <>
      {/* RN-VEN-057 — o filtro. CHAPA-02: exatamente um chip é o ativo, e é o âmbar. */}
      <div className="chips filtro-de-grupo" role="group" aria-label="Filtrar por grupo">
        <button
          type="button"
          className="chip"
          aria-pressed={foco === 'todos'}
          onClick={() => setFoco('todos')}
        >
          <Grelha etapa={foco === 'todos' ? 0 : -1} de={1} />
          Todos
          <span className="dado">{pedidos.length}</span>
        </button>

        {GRUPOS_DA_LISTA.map((grupo) => (
          <button
            key={grupo}
            type="button"
            className="chip"
            aria-pressed={foco === grupo}
            onClick={() => setFoco(grupo)}
          >
            <Grelha etapa={foco === grupo ? 0 : -1} de={1} />
            {ROTULO_DO_GRUPO[grupo]}
            <span className="dado">{totalDoGrupo(grupo)}</span>
          </button>
        ))}
      </div>

      {/* ⚠ O filtro pode não achar nada, e o vazio precisa dizer QUE FILTRO está ativo. Um "nenhum
          pedido" seco aqui contradiz o chip que acabou de anunciar o total do período — e é assim
          que alguém conclui que o sistema perdeu os pedidos. */}
      {visiveis.length === 0 ? (
        <div className="vazio">
          <Grelha etapa={-1} de={6} alta />
          <p>
            Nenhum pedido em <strong>{ROTULO_DO_GRUPO[foco as GrupoDaLista]}</strong> no período.
          </p>
          <button type="button" className="btn calada" onClick={() => setFoco('todos')}>
            Ver todos os {pedidos.length}
          </button>
        </div>
      ) : (
        /* ⚠⚠ AGRUPADO POR DIA OPERACIONAL, e o agrupamento é obrigatório — não é organização.
           `numero` é único por (unidade, dia operacional): ele REINICIA a cada virada. Numa lista
           de vários dias, a comanda #1 de hoje e a #1 de ontem apareciam lado a lado, idênticas,
           sem nada dizendo que eram pedidos diferentes — o operador não tinha como saber qual
           estava lendo. Foi o que o filtro de período revelou no dia em que entrou. */
        agruparPorDiaOperacional(visiveis).map(({ dia, doDia }, indice) => (
          <section key={dia} className="dia-de-pedidos">
            <header className="divisor-dia">
              {/* CHAPA-02 — uma barra âmbar por grupo: o dia mais recente da lista. */}
              <Grelha etapa={indice === 0 ? 0 : -1} de={1} />

              <h3>
                <span className="divisor-titulo">{rotuloDoDia(dia)}</span>
                <span className="dado divisor-data">{dia}</span>
              </h3>

              <span className="divisor-resumo dado">
                {doDia.length} pedido{doDia.length === 1 ? '' : 's'}
              </span>
            </header>

            {/* RN-VEN-057 — dentro do dia, os grupos. Com um grupo em foco o subdivisor não
                aparece: o chip já disse o que está na tela, e repetir viraria moldura vazia. */}
            {(foco === 'todos' ? GRUPOS_DA_LISTA : [foco]).map((grupo) => {
              const doGrupo = doDia.filter((pedido) => grupoDaLista(pedido.status) === grupo);

              if (doGrupo.length === 0) {
                return null;
              }

              return (
                <div key={grupo} className="grupo-de-pedidos" data-grupo={grupo}>
                  {foco === 'todos' && (
                    <p className="divisor-grupo">
                      <span className="rotulo">{ROTULO_DO_GRUPO[grupo]}</span>
                      <span className="dado">{doGrupo.length}</span>
                    </p>
                  )}

                  <div className="grade">
                    {doGrupo.map((pedido) => (
                      <Comanda
                        key={pedido.id}
                        pedido={pedido}
                        colapsada={chegaColapsado(pedido.status) !== alternados.has(pedido.id)}
                        aoAlternar={() => alternar(pedido.id)}
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

                            {/* RN-GER-002 — cancelamento exige motivo; a permissão é
                                `pedido.cancelar`. Esconder aqui é usabilidade; o servidor recusa de
                                todo jeito. */}
                            {podeCancelar &&
                              pedido.status !== 'Cancelado' &&
                              pedido.status !== 'Finalizado' && (
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
                </div>
              );
            })}
          </section>
        ))
      )}

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
          // A logo é o que o CLIENTE vê no topo do cardápio público (RN-GER-026), e é a única
          // imagem da casa. Conferi-la num selo de 72 px é conferir que ela existe, não que ficou
          // boa.
          grande
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

      <CartazDoCardapio
        link={link}
        nomeFantasia={restaurante.nomeFantasia}
        urlLogo={restaurante.urlLogo}
      />

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

/**
 * O QR do cardápio e o cartaz para imprimir.
 *
 * **Imprime pelo navegador, sem biblioteca de PDF.** O diálogo de impressão de qualquer navegador
 * tem "Salvar como PDF", então baixar e imprimir são a mesma ação — e uma biblioteca de PDF
 * custaria centenas de kB no pacote para gerar um cartaz com três elementos. O que faz isto
 * funcionar é o `@media print`: fora dele, esconder o resto da tela é problema do CSS, não do JS.
 *
 * O QR aponta para o **mesmo link** exibido acima; ele não é gerado a partir de outra string, e é
 * por isso que `link` chega por propriedade em vez de ser remontado aqui — dois lugares montando a
 * URL divergem no dia em que o domínio mudar, e o cartaz impresso é o que menos perdoa: ele já
 * está na parede.
 */
function CartazDoCardapio({
  link,
  nomeFantasia,
  urlLogo,
}: {
  link: string;
  nomeFantasia: string;
  urlLogo?: string | null;
}) {
  return (
    <div className="qr-bloco">
      <div className="qr-moldura">
        <QRCodeSVG
          value={link}
          size={168}
          // Preto no branco, sem cor de marca: contraste é o que o leitor de QR precisa, e um
          // código âmbar sobre carvão é bonito na tela e falha na câmera de celular antigo.
          bgColor="#ffffff"
          fgColor="#161210"
          // 'M' aguenta ~15% de dano — respingo de gordura, dobra do papel, fita adesiva.
          level="M"
          marginSize={2}
        />
      </div>

      <p className="secundario">Aponte a câmera para abrir o cardápio.</p>

      <button type="button" className="btn calada" onClick={() => window.print()}>
        <IconeImpressora />
        Imprimir cartaz
      </button>

      {/* O cartaz. Fica fora da tela e só existe na impressão — montá-lo em outra janela com
          `window.open` esbarraria no bloqueador de pop-up justamente de quem clicou no botão. */}
      <div className="cartaz-impressao" aria-hidden="true">
        {/* A faixa de marca: carvão com as listras âmbar. É o que tira o cartaz do "branco
            largado" sem gastar um cartucho por folha — tinta onde ela significa algo. */}
        <div className="cartaz-faixa">
          <span className="cartaz-listras">
            {[0, 1, 2, 3, 4].map((barra) => (
              <i key={barra} className={barra === 1 ? 'quente' : undefined} />
            ))}
          </span>

          <span className="cartaz-marca">
            Grelha<span>Hub</span>
          </span>
        </div>

        <div className="cartaz-corpo">
          {urlLogo && <img className="cartaz-logo" src={urlLogo} alt="" />}

          <strong className="cartaz-nome">{nomeFantasia}</strong>

          <p className="cartaz-chamada">Cardápio digital</p>

          {/* A moldura do QR repete a sombra sólida deslocada da interface — no papel ela vira
              um deslocamento em carvão, que é o mesmo gesto sem depender de cor de fundo. */}
          <div className="cartaz-qr">
            <QRCodeSVG
              value={link}
              size={300}
              bgColor="#ffffff"
              fgColor="#161210"
              level="M"
              marginSize={2}
            />
          </div>

          <p className="cartaz-instrucao">Aponte a câmera do celular</p>

          <code className="cartaz-link">{link}</code>
        </div>
      </div>
    </div>
  );
}
