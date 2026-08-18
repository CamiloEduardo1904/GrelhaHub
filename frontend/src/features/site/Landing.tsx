import { useEffect, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Comanda } from '../../components/Comanda';
import { Grelha } from '../../components/Grelha';
import type { PedidoResumo } from '../../services/tipos';
import {
  BENEFICIOS,
  CAPA,
  CHAMADA_FINAL,
  COMO_FUNCIONA,
  DORES,
  ETAPAS,
  linkDeContato,
  PERGUNTAS,
  PLANOS,
  PLANOS_CABECALHO,
  RECURSOS,
  RODAPE,
  type Plano,
  type Recurso,
} from './conteudo';
import './vitrine.css';

/**
 * A vitrine — `grelhahub.com` e `www.grelhahub.com`.
 *
 * Direção Chapa (ADR-0012), §8 do design system. Duas coisas que a página **não** faz, e
 * ambas por decisão registrada:
 *
 * - **Não inventa depoimento nem número de adoção.** Não há cliente ainda; prova social
 *   fabricada é fabricação. No lugar vai a seção de fundação, que é verificável
 * - **Não anuncia recurso que não existe como se existisse** (CHAPA-09). A seção de recursos
 *   lista apenas o que está no ar; o bloco de roteiro foi removido em 17/08/2026
 *
 * O texto vive em `conteudo.ts`. Aqui fica só estrutura.
 */

const SECOES = [
  { id: 'recursos', rotulo: 'Recursos' },
  { id: 'como-funciona', rotulo: 'Como funciona' },
  { id: 'planos', rotulo: 'Planos' },
  { id: 'perguntas', rotulo: 'Perguntas' },
] as const;

/**
 * Fora do componente de propósito: `SECOES.map(...)` dentro do render devolveria um array novo
 * a cada passada, e o efeito do observador recriaria a inscrição em loop.
 */
const IDS_DAS_SECOES = SECOES.map((secao) => secao.id);

export function Landing() {
  const secaoVisivel = useSecaoVisivel(IDS_DAS_SECOES);

  // CHAPA-08, design system §9 — os blocos sobem 12 px e param secos ao entrar em vista.
  // A barra fixa e o `<Grelha>` NÃO entram nisso: a barra é referência, e a listra é estado.
  useRevelacaoAoRolar();

  return (
    <div className="site">
      <BarraDaVitrine secaoVisivel={secaoVisivel} />

      <main>
        <Capa />

        {/* ── As dores ─────────────────────────────────────────────────────────────── */}
        <section className="faixa">
          <div className="faixa-interna">
            <Titulo sobre="O cenário" texto="Reconhece a sua sexta-feira?" revela />

            <div className="grade-dores" data-revela-grupo>
              {DORES.map((dor) => (
                <article key={dor.titulo} className="cartao-dor" data-revela>
                  <h3>{dor.titulo}</h3>
                  <p>{dor.texto}</p>
                </article>
              ))}
            </div>
          </div>
        </section>

        {/* ── Benefícios ───────────────────────────────────────────────────────────── */}
        <section className="faixa alternada">
          <div className="faixa-interna">
            <Titulo sobre="O que muda" texto="Quatro coisas param de acontecer" revela />

            <div className="grade-quatro" data-revela-grupo>
              {BENEFICIOS.map((beneficio, i) => (
                <article key={beneficio.titulo} className="cartao-num" data-revela>
                  <span className="cartao-num-n dado" aria-hidden="true">
                    {String(i + 1).padStart(2, '0')}
                  </span>
                  <h3>{beneficio.titulo}</h3>
                  <p>{beneficio.texto}</p>
                </article>
              ))}
            </div>
          </div>
        </section>

        {/* ── Recursos ─────────────────────────────────────────────────────────────── */}
        <section className="faixa" id="recursos">
          <div className="faixa-interna">
            <Titulo
              sobre="Recursos"
              texto={
                <>
                  {/* A ligadura da marca, como no ícone: "Hub" em brasa. Aqui é a brasa
                      DERIVADA (`--brasa-texto`), porque esta faixa é clara e a brasa pura daria
                      2,22:1 — abaixo do mínimo até para texto grande. */}
                  Com Grelha<span className="marca-hub">Hub</span> você terá
                </>
              }
              revela
            />

            {/* Um bloco só. O de "o que vem depois" saiu por decisão do dono do produto: cliente
                final não precisa do roteiro de engenharia. E como nada aqui é promessa futura, o
                selo de disponibilidade deixou de ter função. */}
            <div className="grade-recursos" data-revela-grupo>
              {RECURSOS.map((recurso) => (
                <BlocoRecurso key={recurso.titulo} recurso={recurso} />
              ))}
            </div>
          </div>
        </section>

        {/* ── Como funciona ────────────────────────────────────────────────────────── */}
        <section className="faixa alternada" id="como-funciona">
          <div className="faixa-interna">
            <Titulo sobre={COMO_FUNCIONA.sobre} texto={COMO_FUNCIONA.titulo} revela />

            <ol className="passos" data-revela-grupo>
              {ETAPAS.map((etapa, i) => (
                <li key={etapa.titulo} data-revela>
                  {/* A mesma régua que o cliente vê acompanhando o pedido dele. */}
                  <Grelha etapa={i} de={ETAPAS.length} alta />
                  <h3>{etapa.titulo}</h3>
                  <p>{etapa.texto}</p>
                </li>
              ))}
            </ol>
          </div>
        </section>

        {/* ── Planos ───────────────────────────────────────────────────────────────── */}
        <section className="faixa alternada" id="planos">
          <div className="faixa-interna">
            <Titulo
              sobre={PLANOS_CABECALHO.sobre}
              texto={PLANOS_CABECALHO.titulo}
              apoio={PLANOS_CABECALHO.apoio}
              revela
            />

            <div className="grade-planos" data-revela-grupo>
              {PLANOS.map((plano) => (
                <CartaoDePlano key={plano.nome} plano={plano} />
              ))}
            </div>
          </div>
        </section>

        {/* ── Perguntas ────────────────────────────────────────────────────────────── */}
        <section className="faixa" id="perguntas">
          <div className="faixa-interna estreita">
            <Titulo sobre="Perguntas" texto="O que costumam perguntar antes" revela />

            <div className="perguntas" data-revela-grupo>
              {PERGUNTAS.map((item) => (
                // `<details>` nativo: acordeão próprio seria reimplementar de graça o
                // teclado e o leitor de tela que o navegador já entrega.
                <details key={item.pergunta} className="pergunta" data-revela>
                  <summary>{item.pergunta}</summary>
                  <p>{item.resposta}</p>
                </details>
              ))}
            </div>
          </div>
        </section>

        {/* ── Chamada final ────────────────────────────────────────────────────────── */}
        <section className="faixa-cta">
          <div className="faixa-interna estreita" data-revela>
            <h2>{CHAMADA_FINAL.titulo}</h2>
            <p>{CHAMADA_FINAL.texto}</p>
            <BotaoDeContato rotulo={CHAMADA_FINAL.acao} classe="btn" />
          </div>
        </section>
      </main>

      <RodapeDaVitrine />
    </div>
  );
}

// ════════════════════════════════════════════════════════════════════════════════════
// BARRA — o "Acesse já" fica no alto, à direita
// ════════════════════════════════════════════════════════════════════════════════════

function BarraDaVitrine({ secaoVisivel }: { secaoVisivel: string | null }) {
  return (
    <header className="barra-site">
      <div className="barra-site-interna">
        <Link to="/" className="marca" aria-label="GrelhaHub — início">
          <span className="wordmark">
            Grelha<span>Hub</span>
          </span>
        </Link>

        {/* Sem alternador de menu: no celular as âncoras rolam na horizontal, que é o mesmo
            padrão das abas do painel. Um botão "Menu" para revelar quatro links é um clique a mais
            e um estado a mais para nada. */}
        <nav className="menu-site" aria-label="Seções da página">
          {SECOES.map((secao) => {
            const ativa = secaoVisivel === secao.id;

            return (
              <a key={secao.id} href={`#${secao.id}`} aria-current={ativa ? 'true' : undefined}>
                {/* CHAPA-02 — exatamente uma barra âmbar no grupo: a seção visível. */}
                <Grelha etapa={ativa ? 0 : -1} de={1} />
                {secao.rotulo}
              </a>
            );
          })}
        </nav>

        <div className="barra-site-acoes">
          <Link className="btn" to="/entrar">
            Acesse já
          </Link>
        </div>
      </div>
    </header>
  );
}

/**
 * O botão de contato — ou **nada**, quando não há canal configurado.
 *
 * Renderizar um botão que abre um `mailto:` vazio é pior do que não ter botão: o visitante clica,
 * o cliente de e-mail abre em branco, e ele conclui que o site está quebrado.
 */
function BotaoDeContato({ rotulo, classe }: { rotulo: string; classe: string }) {
  const href = linkDeContato();

  if (!href) {
    return null;
  }

  return (
    <a className={classe} href={href} target="_blank" rel="noreferrer">
      {rotulo}
    </a>
  );
}

// ════════════════════════════════════════════════════════════════════════════════════
// CAPA
// ════════════════════════════════════════════════════════════════════════════════════

function Capa() {
  return (
    <section className="capa-site">
      {/*
        A capa usa o MESMO mecanismo das seções de baixo, e não um segundo caminho para
        "animar no carregamento": ela já está à vista, então o observador dispara no primeiro
        quadro e o efeito sai de graça. Dois mecanismos para um movimento idêntico seria a
        segunda cópia — a que erra.
      */}
      <div className="capa-interna" data-revela-grupo>
        <div className="capa-texto" data-revela>
          <span className="selo" data-fase="aberto">
            {CAPA.faixa}
          </span>

          <h1>{CAPA.titulo}</h1>
          <p className="capa-sub">{CAPA.subtitulo}</p>

          <div className="capa-acoes">
            <BotaoDeContato rotulo={CAPA.acaoPrimaria} classe="btn" />
            <a className="btn calada" href="#como-funciona">
              {CAPA.acaoSecundaria}
            </a>
          </div>

          <ul className="capa-garantias">
            {CAPA.garantias.map((garantia) => (
              <li key={garantia}>{garantia}</li>
            ))}
          </ul>
        </div>

        {/* A prova visual é a Comanda REAL do painel, com os mesmos tokens e o mesmo
            componente. Desenhar uma comanda falsa aqui seria mostrar o que o produto não é. */}
        <div className="capa-mostra" aria-label="Exemplo de comanda no painel do restaurante" data-revela>
          <Comanda pedido={PEDIDO_DE_EXEMPLO} />
        </div>
      </div>
    </section>
  );
}

/** Exemplo estático — não vem da API, e por isso não usa slug nem unidade de ninguém. */
const PEDIDO_DE_EXEMPLO: PedidoResumo = {
  id: 'exemplo',

  // Exemplo estático: um GUID de fachada. Não vem da API e não pertence a unidade nenhuma.
  unidadeId: 'exemplo',

  numero: 118,
  canal: 'DeliveryProprio',
  status: 'EmProducao',
  cliente: 'Marina A.',
  telefone: '11987654321',
  total: 74.5,
  quantidadeDeItens: 3,
  criadoEm: '2026-08-17T22:41:00Z',
  dataOperacional: '2026-08-17',
  observacao: 'Entregar na portaria, apartamento 42.',
  itens: [
    { produto: 'Duplo Brasa', quantidade: 2, precoPraticado: 28.9, observacao: 'sem cebola' },
    { produto: 'Batata rústica', quantidade: 1, precoPraticado: 16.7 },
  ],
};

// ════════════════════════════════════════════════════════════════════════════════════
// PEÇAS
// ════════════════════════════════════════════════════════════════════════════════════

function Titulo({
  sobre,
  texto,
  apoio,
  claro = false,
  revela = false,
}: {
  sobre: string;
  /** `ReactNode` e não `string`: o título de Recursos carrega a ligadura da marca. */
  texto: ReactNode;
  apoio?: string;
  claro?: boolean;

  /**
   * CHAPA-08 — o título entra animado junto com a seção.
   *
   * É opção e não padrão porque o título fica **fora** do `data-revela-grupo` da grade: se
   * herdasse o escalonamento dos cartões, o rótulo da seção chegaria depois do conteúdo dela.
   */
  revela?: boolean;
}) {
  return (
    <div
      className={claro ? 'titulo-secao claro' : 'titulo-secao'}
      {...(revela ? { 'data-revela': '' } : {})}
    >
      <span className="rotulo">{sobre}</span>
      <h2>{texto}</h2>
      {apoio && <p className="titulo-apoio">{apoio}</p>}
    </div>
  );
}

function BlocoRecurso({ recurso }: { recurso: Recurso }) {
  return (
    <article className="bloco-recurso" data-revela>
      <h3>{recurso.titulo}</h3>
      <p>{recurso.descricao}</p>
    </article>
  );
}

function CartaoDePlano({ plano }: { plano: Plano }) {
  return (
    <article className={plano.indicado ? 'plano indicado' : 'plano'} data-revela>
      {/* CHAPA-02 — a barra âmbar marca o único plano indicado do grupo. */}
      <Grelha etapa={plano.indicado ? 0 : -1} de={1} alta />

      <h3>{plano.nome}</h3>
      <p className="plano-resumo">{plano.resumo}</p>

      <div className="plano-preco">
        {plano.preco === null ? (
          <>
            <strong>Sob consulta</strong>
            <span className="secundario">{plano.periodo}</span>
          </>
        ) : (
          <>
            <strong className="dado">
              {plano.preco.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
            </strong>
            <span className="secundario">{plano.periodo}</span>
          </>
        )}
      </div>

      <ul className="plano-itens">
        {plano.itens.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>

      <BotaoDeContato
        rotulo="Falar sobre este plano"
        classe={plano.indicado ? 'btn bloco' : 'btn calada bloco'}
      />
    </article>
  );
}

function RodapeDaVitrine() {
  return (
    <footer className="rodape-site">
      <div className="faixa-interna">
        <div className="rodape-colunas">
          <div>
            <span className="wordmark">
              Grelha<span>Hub</span>
            </span>
            <p className="secundario">{RODAPE.descricao}</p>
          </div>

          <div>
            <span className="rotulo">Produto</span>
            <ul>
              {SECOES.map((secao) => (
                <li key={secao.id}>
                  <a href={`#${secao.id}`}>{secao.rotulo}</a>
                </li>
              ))}
            </ul>
          </div>

          <div>
            <span className="rotulo">Acesso</span>
            <ul>
              <li>
                <Link to="/entrar">Acesse já</Link>
              </li>
            </ul>
          </div>
        </div>

        <p className="rodape-fim secundario">
          © {RODAPE.empresa} · CNPJ <span className="dado">{RODAPE.cnpj}</span> · GrelhaHub
        </p>
      </div>
    </footer>
  );
}

// ════════════════════════════════════════════════════════════════════════════════════
// Qual seção está à vista — para a barra âmbar da navegação
// ════════════════════════════════════════════════════════════════════════════════════

// ════════════════════════════════════════════════════════════════════════════════════
// REVELAÇÃO AO ROLAR — CHAPA-08, design system §9
// ════════════════════════════════════════════════════════════════════════════════════

/**
 * Revela os blocos marcados com `data-revela` quando eles entram em vista.
 *
 * **Três decisões, todas registradas no design system §9.5:**
 *
 * - **Revela uma vez** — `unobserve` na entrada, sem re-esconder na rolagem para cima. Bloco que
 *   pisca a cada passada transforma leitura em brinquedo, e quem volta ao topo está procurando
 *   algo, não assistindo
 * - **Escreve o atributo no DOM, e não em estado do React** — trinta blocos em estado seriam
 *   trinta re-renderizações da vitrine inteira durante a rolagem. O conteúdo aqui é estático:
 *   não há nada para reconciliar, e `data-revela` no JSX nunca é reescrito
 * - **Um observador para a página** — trinta observadores fazem o mesmo trabalho que um,
 *   trinta vezes
 *
 * ⚠ **Se o observador não existir, revela tudo na hora.** Página que depende de
 * `IntersectionObserver` para ser legível aposta a entrega do texto na infraestrutura do
 * navegador. É a mesma proteção que `prefers-reduced-motion` recebe na folha global (§9.4), pelo
 * mesmo motivo: o estado de repouso é sempre opacidade cheia.
 */
function useRevelacaoAoRolar() {
  useEffect(() => {
    const blocos = Array.from(document.querySelectorAll<HTMLElement>('[data-revela]'));

    if (blocos.length === 0) {
      return;
    }

    const revelar = (bloco: HTMLElement) => bloco.setAttribute('data-revelado', 'true');

    if (typeof IntersectionObserver === 'undefined') {
      blocos.forEach(revelar);
      return;
    }

    const observador = new IntersectionObserver(
      (entradas, self) => {
        entradas.forEach((entrada) => {
          if (!entrada.isIntersecting) {
            return;
          }

          revelar(entrada.target as HTMLElement);
          self.unobserve(entrada.target);
        });
      },
      // Dispara um pouco ANTES de o bloco aparecer de fato: revelar no instante exato em que a
      // borda cruza a tela faz o movimento acontecer na periferia da visão, onde ninguém o vê
      // acabar. A margem negativa embaixo pede que o bloco esteja realmente subindo.
      { rootMargin: '0px 0px -12% 0px', threshold: 0.08 },
    );

    blocos.forEach((bloco) => observador.observe(bloco));

    return () => observador.disconnect();
  }, []);
}

/**
 * `IntersectionObserver` e não cálculo de `scroll`: o segundo roda a cada pixel e trava a
 * rolagem no tablet, que é o aparelho em que o produto roda.
 */
function useSecaoVisivel(ids: readonly string[]): string | null {
  const [visivel, setVisivel] = useState<string | null>(null);

  useEffect(() => {
    const alvos = ids
      .map((id) => document.getElementById(id))
      .filter((elemento): elemento is HTMLElement => elemento !== null);

    if (alvos.length === 0) {
      return;
    }

    const observador = new IntersectionObserver(
      (entradas) => {
        const emVista = entradas
          .filter((entrada) => entrada.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);

        if (emVista.length > 0) {
          setVisivel(emVista[0].target.id);
        }
      },
      // A seção conta como visível quando o seu topo passa do terço superior da tela.
      { rootMargin: '-20% 0px -70% 0px' },
    );

    alvos.forEach((alvo) => observador.observe(alvo));

    return () => observador.disconnect();
  }, [ids]);

  return visivel;
}
