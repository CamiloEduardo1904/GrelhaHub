import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Grelha } from '../../components/Grelha';
import { IconeLapis, IconeMais, IconeOlho } from '../../components/Icone';
import { api, mensagemDeErro } from '../../services/api';
import { apenasDigitos, cpfValido, formatarCpf } from '../../services/cpf';
import { CadastroDeOperacao } from './CadastroDeOperacao';
import { Operacao } from '../admin/Operacao';
import { PedidosDaCasa } from './PedidosDaCasa';
import {
  COM_CODIGO,
  comDependencias,
  dependentesDe,
  type Estabelecimento,
  type ModuloDoCatalogo,
} from './tipos';

/**
 * Os estabelecimentos — `RN-GER-014`, ADR-0013.
 *
 * A lista é enxuta de propósito: **nome, CNPJ, módulos, usuários, situação**. Tudo o que é de uma
 * casa só vive atrás do olho, e não empilhado embaixo da tabela — foi assim que a primeira versão
 * desta área ficou ilegível.
 */

/**
 * ⚠ **O nome da chave é o nome do que a aba MOSTRA, e isso não é preciosismo de nomenclatura.**
 *
 * A versão anterior tinha `operacao` apontando para a lista de pedidos e `painel` apontando para
 * o painel de operação, com o rótulo "Horário e endereço" no primeiro e "Painel" no segundo — três
 * nomes para duas telas, e nenhum deles batendo com o conteúdo. Quem abria "Horário e endereço"
 * recebia a lista de pedidos, e o horário e o endereço viviam escondidos dentro de
 * "Dados e situação".
 */
type Aba = 'pedidos' | 'operacao' | 'dados' | 'loja' | 'modulos';

export function Estabelecimentos() {
  const [lista, setLista] = useState<Estabelecimento[]>([]);
  const [catalogo, setCatalogo] = useState<ModuloDoCatalogo[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [vendo, setVendo] = useState<string | null>(null);
  const [cadastrando, setCadastrando] = useState(false);

  const carregar = useCallback(async () => {
    try {
      const [casas, modulos] = await Promise.all([
        api.get<Estabelecimento[]>('/v1/plataforma/estabelecimentos'),
        api.get<ModuloDoCatalogo[]>('/v1/plataforma/modulos'),
      ]);

      setLista(casas.data);
      setCatalogo(modulos.data);
      setErro(null);
    } catch (e) {
      setErro(mensagemDeErro(e));
    }
  }, []);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  /**
   * `RN-GER-019` — quais siglas são de **núcleo**, tiradas do catálogo e não de uma lista escrita
   * aqui. Cópia local divergiria do servidor no dia em que o núcleo mudasse, e a divergência
   * apareceria como contagem que não fecha — que é exatamente o defeito que isto corrige.
   */
  const nucleo = new Set(catalogo.filter((m) => m.nucleo).map((m) => m.sigla));

  const casaAberta = lista.find((c) => c.id === vendo) ?? null;

  return (
    <>
      {erro && <p className="erro">{erro}</p>}

      <div className="painel-cartao" style={{ maxWidth: 'none' }}>
        <div className="comanda-id">
          <h2 className="wordmark" style={{ fontSize: '1.1rem' }}>
            Estabelecimentos
          </h2>
          <button
            type="button"
            className={cadastrando ? 'btn calada' : 'btn'}
            onClick={() => setCadastrando((estava) => !estava)}
          >
            {!cadastrando && <IconeMais />}
            {cadastrando ? 'Cancelar' : 'Novo estabelecimento'}
          </button>
        </div>

        {cadastrando && (
          <FormularioDeEstabelecimento
            catalogo={catalogo}
            aoCriar={async () => {
              setCadastrando(false);
              await carregar();
            }}
          />
        )}
      </div>

      <div className="rolagem">
        <table>
          {/* ⚠ O código do ADR saiu do título. Jargão de documentação na legenda de uma tabela não
              comunica nada a quem está atendendo um cliente — a rastreabilidade vive no comentário
              e no catálogo, que é onde ela é lida. */}
          <caption>Todas as unidades — a plataforma não pertence a nenhuma casa</caption>
          <thead>
            <tr>
              <th>Estabelecimento</th>
              <th>CNPJ</th>
              <th>Módulos</th>
              <th className="num col-estreita">Usuários</th>
              <th className="col-estreita">Situação</th>
              <th className="col-estreita" aria-label="Ações" />
            </tr>
          </thead>
          <tbody>
            {lista.map((casa) => (
              <tr key={casa.id} className={casa.ativa ? undefined : 'apagado'}>
                <td>
                  <strong>{casa.nomeFantasia}</strong>
                  <p className="secundario dado">/c/{casa.slug}</p>
                </td>
                <td className="dado">{casa.cnpj}</td>
                <td>
                  {/* ⚠ Célula em branco não distingue "não tem módulo" de "não carregou", e as
                      duas leituras levam a ações opostas: uma é venda, a outra é suporte. */}
                  {casa.modulos.length === 0 ? (
                    <span className="secundario">Nenhum módulo</span>
                  ) : (
                    casa.modulos.map((sigla) => (
                      <span
                        key={sigla}
                        className="selo"
                        // RN-GER-019 — o núcleo aparece, mas não como coisa contratada: ele não
                        // foi vendido, ele vem sempre. Sem a distinção, a contagem da aba (que
                        // inclui GER) divergia da lista de gestão (que não o mostrava).
                        data-fase={nucleo.has(sigla) ? 'planejado' : undefined}
                        title={nucleo.has(sigla) ? 'Núcleo — entra sempre (RN-GER-019)' : undefined}
                        style={{ marginRight: '0.25rem' }}
                      >
                        {sigla}
                      </span>
                    ))
                  )}
                </td>
                <td className="num col-estreita">{casa.usuarios}</td>
                <td className="col-estreita">
                  {/* CHAPA-06 — situação é selo com borda e cor, nunca emoji. */}
                  <span className="selo" data-fase={casa.ativa ? 'fim' : 'morto'}>
                    {casa.ativa ? 'Ativa' : 'Inativa'}
                  </span>
                </td>
                <td className="col-estreita">
                  <button
                    type="button"
                    className="btn calada"
                    aria-expanded={vendo === casa.id}
                    onClick={() => setVendo(vendo === casa.id ? null : casa.id)}
                  >
                    {/* SVG inline — ADR-0012 proíbe webfont, e emoji não é ícone (CHAPA-06). */}
                    <IconeOlho />
                    {vendo === casa.id ? 'Fechar' : 'Ver'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {lista.length === 0 && !erro && (
        <div className="vazio">
          <Grelha etapa={0} de={3} alta />
          <p className="fraco">Nenhum estabelecimento cadastrado ainda.</p>
        </div>
      )}

      {casaAberta && (
        <DetalheDaCasa
          casa={casaAberta}
          catalogo={catalogo}
          aoMudar={carregar}
        />
      )}
    </>
  );
}

/**
 * Os detalhes de uma casa, em cinco abas: **pedidos, operação, dados e situação, horário e
 * endereço, módulos**.
 */
function DetalheDaCasa({
  casa,
  catalogo,
  aoMudar,
}: {
  casa: Estabelecimento;
  catalogo: ModuloDoCatalogo[];
  aoMudar: () => Promise<void>;
}) {
  const [aba, setAba] = useState<Aba>('pedidos');

  /** `RN-GER-019` — siglas de núcleo, do catálogo. Ver a nota na aba de módulos. */
  const nucleo = new Set(catalogo.filter((m) => m.nucleo).map((m) => m.sigla));

  /**
   * ⚠ **O vocabulário é o mesmo do painel do restaurante**, e de propósito: `Pedidos` é a lista
   * do dia, `Operação` é o faturamento agregado, e o horário e o endereço têm aba própria — lá
   * ela se chama "Loja", aqui é dita por extenso porque quem olha está numa casa que não é a dele.
   *
   * Suporte e restaurante conversam por telefone olhando telas diferentes: quando o operador diz
   * "vá em Operação", os dois precisam chegar no mesmo lugar.
   */
  const ABAS: { chave: Aba; nome: string }[] = [
    { chave: 'pedidos', nome: 'Pedidos' },

    // RN-GER-038 — o painel de operação da casa, visto pela plataforma. Ela alcança pelo bypass
    // de RN-GER-014, e o recorte vai na consulta: `unidadeId` diz de QUAL casa, e o servidor
    // ignora o parâmetro para quem não é plataforma (RN-GER-016).
    { chave: 'operacao', nome: 'Operação' },

    { chave: 'dados', nome: 'Dados e situação' },

    // RN-GER-036 / RN-GER-037 — deixou de ser um bloco no fim de "Dados e situação". Aba própria:
    // é o cadastro que decide a virada do dia operacional da casa, e ninguém procura isso dentro
    // de uma tela de razão social.
    { chave: 'loja', nome: 'Horário e endereço' },

    // ⚠ A contagem é de módulos CONTRATADOS, e não de `casa.modulos.length`.
    //
    // O total incluía o núcleo (`GER`), mas a lista de gestão o filtra por `!m.nucleo` — então a
    // aba dizia "Módulos · 10" e abria uma lista de 9. Quem administra o que o cliente paga
    // encontrava uma divergência de um item e não tinha como saber qual dos dois números estava
    // errado. Agora a aba conta o que é contratável, e o núcleo é dito dentro da tela.
    {
      chave: 'modulos',
      nome: `Módulos · ${casa.modulos.filter((sigla) => !nucleo.has(sigla)).length}`,
    },
  ];

  return (
    <div className="painel-cartao detalhe-casa" style={{ maxWidth: 'none' }}>
      <h3 className="wordmark" style={{ fontSize: '1.05rem' }}>
        {casa.nomeFantasia}
        {!casa.ativa && (
          <span className="selo" data-fase="morto" style={{ marginLeft: '0.6rem' }}>
            Inativa
          </span>
        )}
      </h3>

      <nav className="abas" role="tablist" aria-label={`Detalhes de ${casa.nomeFantasia}`}>
        {ABAS.map(({ chave, nome }) => (
          <button
            key={chave}
            type="button"
            role="tab"
            className="aba"
            aria-selected={aba === chave}
            onClick={() => setAba(chave)}
          >
            <Grelha etapa={aba === chave ? 0 : -1} de={1} />
            {nome}
          </button>
        ))}
      </nav>

      {/* ⚠ Uma linha por aba, na MESMA ordem de `ABAS`, e cada uma renderizando o que o rótulo
          promete. A versão anterior tinha duas delas trocadas e uma quarta tela escondida dentro
          de outra aba — e ninguém percebeu porque o roteamento estava espalhado em três blocos
          aninhados, um deles condicionado por `(aba === 'dados' || aba === 'modulos')`.

          Enumerado, nunca por negação: condição escrita como "tudo menos X" adota silenciosamente
          toda aba nova, e já foi assim que o painel entrou num bloco que não era dele. */}
      {aba === 'pedidos' && <PedidosDaCasa casa={casa} />}

      {aba === 'operacao' && <Operacao unidadeId={casa.id} />}

      {aba === 'dados' && <DadosDaCasa casa={casa} aoMudar={aoMudar} />}

      {/* RN-GER-036 / RN-GER-037 — dado do cliente alterado em nome dele, e por isso a trilha
          registra quem mexeu (RN-GER-015). */}
      {aba === 'loja' && <CadastroDeOperacao unidadeId={casa.id} />}

      {aba === 'modulos' && (
        <ModulosDaCasa casa={casa} catalogo={catalogo} aoMudar={aoMudar} />
      )}
    </div>
  );
}

function DadosDaCasa({
  casa,
  aoMudar,
}: {
  casa: Estabelecimento;
  aoMudar: () => Promise<void>;
}) {
  const [form, setForm] = useState({
    razaoSocial: casa.razaoSocial,
    nomeFantasia: casa.nomeFantasia,
    telefone: '',
  });
  const [justificativa, setJustificativa] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  async function salvar(evento: FormEvent) {
    evento.preventDefault();
    setOcupado(true);
    setErro(null);

    try {
      await api.put(`/v1/plataforma/estabelecimentos/${casa.id}`, form);
      await aoMudar();
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  async function alternarSituacao() {
    setOcupado(true);
    setErro(null);

    try {
      await api.post(`/v1/plataforma/estabelecimentos/${casa.id}/situacao`, {
        ativa: !casa.ativa,
        justificativa,
      });

      setJustificativa('');
      await aoMudar();
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  return (
    <>
      <form onSubmit={salvar} style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
        {/* RN-GER-015 — alterar o cadastro de um cliente é SUPORTE, e exige sessão. Inativar não:
            é ato comercial e carrega justificativa própria. */}
        <fieldset disabled={false}>
          <legend>Dados cadastrais</legend>

          <div className="linha">
            <label>
              Nome fantasia
              <input
                value={form.nomeFantasia}
                onChange={(e) => setForm((a) => ({ ...a, nomeFantasia: e.target.value }))}
                required
                minLength={3}
              />
            </label>

            <label>
              Razão social
              <input
                value={form.razaoSocial}
                onChange={(e) => setForm((a) => ({ ...a, razaoSocial: e.target.value }))}
                required
              />
            </label>
          </div>

          <label>
            Telefone
            <input
              value={form.telefone}
              onChange={(e) => setForm((a) => ({ ...a, telefone: e.target.value }))}
              inputMode="tel"
              placeholder="(11) 98765-4321"
            />
          </label>
        </fieldset>

        {/* RN-VEN-036 — o slug é imutável: ele circula em rede social e material impresso. O CNPJ
            identifica a pessoa jurídica do contrato, e trocá-lo é cadastrar outro cliente. */}
        <p className="nota">
          O endereço público (<code>/c/{casa.slug}</code>) e o CNPJ{' '}
          <strong>não são alteráveis</strong>: o link já circulou, e o CNPJ é o do contrato.
        </p>

        <button type="submit" className="btn" disabled={false}>
          <IconeLapis />
          Salvar dados
        </button>
      </form>

      <fieldset>
        <legend>Situação do estabelecimento</legend>

        <p className="secundario">
          {casa.ativa ? (
            <>
              Inativar <strong>impede o acesso de todos os usuários desta casa</strong>{' '}
              (<code>RN-GER-027</code>) e o cardápio público para de responder.{' '}
              <strong>Nada é apagado</strong>: reativar devolve tudo.
            </>
          ) : (
            <>
              Esta casa está inativa: ninguém dela consegue entrar. Reativar devolve o acesso —
              nada foi removido.
            </>
          )}
        </p>

        <label>
          Justificativa
          <input
            value={justificativa}
            onChange={(e) => setJustificativa(e.target.value)}
            placeholder={casa.ativa ? 'Contrato encerrado em 17/08' : 'Pagamento regularizado'}
          />
        </label>

        <button
          type="button"
          className={casa.ativa ? 'btn perigo' : 'btn'}
          disabled={ocupado || justificativa.trim().length < 10}
          onClick={alternarSituacao}
        >
          {casa.ativa ? 'Inativar estabelecimento' : 'Reativar estabelecimento'}
        </button>

        {justificativa.length > 0 && justificativa.trim().length < 10 && (
          <p className="secundario">
            Faltam {10 - justificativa.trim().length} caracteres na justificativa.
          </p>
        )}
      </fieldset>

      {erro && <p className="erro">{erro}</p>}
    </>
  );
}

function ModulosDaCasa({
  casa,
  catalogo,
  aoMudar,
}: {
  casa: Estabelecimento;
  catalogo: ModuloDoCatalogo[];
  aoMudar: () => Promise<void>;
}) {
  const [justificativa, setJustificativa] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  async function alternar(sigla: string, habilitar: boolean) {
    if (justificativa.trim().length < 10) {
      setErro('Escreva a justificativa (ao menos 10 caracteres) antes de alterar um módulo.');
      return;
    }

    setOcupado(true);
    setErro(null);

    try {
      const acao = habilitar ? 'habilitar' : 'desabilitar';

      await api.post(`/v1/plataforma/unidades/${casa.id}/modulos/${sigla}/${acao}`, {
        justificativa,
      });

      await aoMudar();
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  return (
    <>
      <label>
        Justificativa da alteração
        <input
          value={justificativa}
          onChange={(e) => setJustificativa(e.target.value)}
          placeholder="Contratou o módulo de estoque em 17/08"
        />
      </label>

      <p className="nota">
        Habilitar módulo é <strong>ato comercial</strong> e não exige sessão de suporte — mas exige
        justificativa, que fica na linha de vigência junto do que ela explica
        (<code>RN-GER-011</code>, <code>RN-GER-021</code>).
      </p>

      {/* ⚠⚠ RN-GER-019 — o NÚCLEO aparece, e antes não aparecia.
          A lista filtrava `!m.nucleo` com o argumento de que "não existe unidade sem ele", o que é
          verdade e ainda assim produzia um defeito: a aba contava 10 módulos e a lista mostrava 9.
          Quem administra o que o cliente paga via uma divergência de um item sem saber qual dos
          dois números estava errado — e a resposta ("um deles é o núcleo") não estava na tela.

          Ele entra como linha travada, dizendo o que é: incluído, não contratado, não removível. */}
      <fieldset>
        <legend>Incluído no produto</legend>

        {catalogo
          .filter((m) => m.nucleo)
          .map(({ sigla, nome }) => (
            <div key={sigla} className="linha-modulo">
              <label className="alternador">
                <input
                  type="checkbox"
                  checked
                  disabled
                  title="O núcleo entra sempre e não é contratável — RN-GER-019"
                />
                <span className="chave">{sigla}</span>
              </label>

              <span className="secundario">{nome}</span>

              <span className="selo" data-fase="fim">
                Entra sempre
              </span>
            </div>
          ))}
      </fieldset>

      <fieldset>
        <legend>Módulos contratáveis</legend>

        {catalogo
          .filter((m) => !m.nucleo)
          .map(({ sigla, nome, dependeDe }) => {
            const habilitado = casa.modulos.includes(sigla);

            // RN-GER-020 — mostrado ANTES do clique: descobrir a cadeia por recusa é o que a
            // mensagem do servidor resolve, e o que esta linha evita.
            const faltando = dependeDe.filter((d) => !casa.modulos.includes(d));
            const presos = habilitado ? dependentesDe(sigla, casa.modulos, catalogo) : [];

            const travado =
              ocupado || (!habilitado && faltando.length > 0) || presos.length > 0;

            /**
             * ⚠ O motivo de o controle estar travado, na PRÓPRIA caixa.
             *
             * Os selos ao lado já diziam a cadeia, e ainda assim a revisão de UX registrou
             * "checkbox cinza sem tooltip nem legenda": o selo fica depois do nome do módulo, e o
             * olho que encontra o controle desabilitado não chega até ele. Quem tenta clicar
             * precisa da explicação no ponto do clique.
             */
            const motivoDoTravamento = ocupado
              ? 'Aguarde a alteração anterior terminar.'
              : presos.length > 0
                ? `Não pode ser desativado: ${presos.join(', ')} ${
                    presos.length === 1 ? 'depende' : 'dependem'
                  } deste. Desative-${presos.length === 1 ? 'o' : 'os'} primeiro (RN-GER-020).`
                : faltando.length > 0
                  ? `Não pode ser ativado sozinho: exige ${faltando.join(', ')}. Ative-${
                      faltando.length === 1 ? 'o' : 'os'
                    } primeiro (RN-GER-020).`
                  : undefined;

            return (
              <div key={sigla} className="linha-modulo">
                <label className="alternador" title={motivoDoTravamento}>
                  <input
                    type="checkbox"
                    checked={habilitado}
                    disabled={travado}
                    // Um `<input disabled>` não recebe foco nem dispara `title` em leitor de tela;
                    // o rótulo acessível vai no próprio controle.
                    aria-label={motivoDoTravamento ? `${sigla} — ${motivoDoTravamento}` : sigla}
                    onChange={(e) => void alternar(sigla, e.target.checked)}
                  />
                  <span className="chave">{sigla}</span>
                </label>

                <span className="secundario">{nome}</span>

                {faltando.length > 0 && !habilitado && (
                  <span className="selo" data-fase="planejado">
                    Depende de {faltando.join(', ')}
                  </span>
                )}

                {presos.length > 0 && (
                  <span className="selo" data-fase="planejado">
                    {presos.join(', ')} dependem deste
                  </span>
                )}

                {/* CHAPA-09 — o que ainda não existe é dito, não escondido. */}
                {!COM_CODIGO.has(sigla) && (
                  <span className="selo" data-fase="planejado">
                    Sem código ainda
                  </span>
                )}
              </div>
            );
          })}
      </fieldset>

      {erro && <p className="erro">{erro}</p>}
    </>
  );
}

function FormularioDeEstabelecimento({
  catalogo,
  aoCriar,
}: {
  catalogo: ModuloDoCatalogo[];
  aoCriar: () => Promise<void>;
}) {
  const [form, setForm] = useState({
    razaoSocial: '',
    nomeFantasia: '',
    cnpj: '',
    telefone: '',
    nomeAdministrador: '',
    sobrenomeAdministrador: '',
    cpfAdministrador: '',
    email: '',
    senha: '',
  });
  const [modulos, setModulos] = useState<string[]>(['VEN']);
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  function campo(nome: keyof typeof form) {
    return {
      value: form[nome],
      onChange: (e: { target: { value: string } }) =>
        setForm((atual) => ({ ...atual, [nome]: e.target.value })),
    };
  }

  const cpfErrado =
    apenasDigitos(form.cpfAdministrador).length === 11 && !cpfValido(form.cpfAdministrador);

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);

    try {
      await api.post('/v1/plataforma/estabelecimentos', {
        ...form,
        cpfAdministrador: apenasDigitos(form.cpfAdministrador),
        modulos,
      });

      await aoCriar();
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <form onSubmit={enviar} style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
      <fieldset>
        <legend>O estabelecimento</legend>

        <div className="linha">
          <label>
            Nome fantasia
            <input {...campo('nomeFantasia')} required minLength={3} />
          </label>

          <label>
            Razão social
            <input {...campo('razaoSocial')} required />
          </label>
        </div>

        <div className="linha">
          <label>
            CNPJ
            <input {...campo('cnpj')} required inputMode="numeric" placeholder="00.000.000/0001-00" />
          </label>

          <label>
            Telefone
            <input {...campo('telefone')} inputMode="tel" />
          </label>
        </div>
      </fieldset>

      <fieldset>
        <legend>Administrador da casa</legend>

        <div className="linha">
          <label>
            Nome
            <input {...campo('nomeAdministrador')} required />
          </label>

          <label>
            Sobrenome
            <input {...campo('sobrenomeAdministrador')} required />
          </label>
        </div>

        <div className="linha">
          <label>
            CPF
            <input
              value={formatarCpf(form.cpfAdministrador)}
              onChange={(e) =>
                setForm((a) => ({ ...a, cpfAdministrador: apenasDigitos(e.target.value) }))
              }
              required
              inputMode="numeric"
              aria-invalid={cpfErrado || undefined}
            />
          </label>

          <label>
            E-mail
            <input type="email" {...campo('email')} required />
          </label>
        </div>

        <label>
          Senha inicial
          <input type="password" {...campo('senha')} required minLength={8} />
        </label>
      </fieldset>

      <fieldset>
        <legend>Módulos contratados</legend>

        {catalogo
          .filter((m) => !m.nucleo)
          .map(({ sigla, nome, dependeDe }) => (
            <label key={sigla} className="alternador">
              <input
                type="checkbox"
                checked={modulos.includes(sigla)}
                onChange={(e) =>
                  setModulos((atual) =>
                    e.target.checked
                      // RN-GER-020 — marcar EST marca PRO e VEN JUNTO, e o vendedor VÊ as caixas
                      // mudarem. Completar aqui evita a ida e volta; acontece à vista, não no envio.
                      ? [...new Set([...atual, ...comDependencias([sigla], catalogo)])]
                      : atual.filter(
                          (s) => s !== sigla && !dependentesDe(sigla, [s], catalogo).length,
                        ),
                  )
                }
              />
              <span className="chave">{sigla}</span>
              <span className="secundario">{nome}</span>

              {dependeDe.length > 0 && (
                <span className="secundario">(exige {dependeDe.join(', ')})</span>
              )}

              {!COM_CODIGO.has(sigla) && (
                <span className="selo" data-fase="planejado">
                  Sem código
                </span>
              )}
            </label>
          ))}
      </fieldset>

      {cpfErrado && <p className="erro">Este CPF não é válido.</p>}
      {erro && <p className="erro">{erro}</p>}

      <p className="nota">
        O módulo <strong>GER</strong> entra sempre — <code>RN-GER-019</code>. A casa nasce com
        perfis, parâmetros e um cardápio de exemplo.
      </p>

      <button type="submit" className="btn bloco" disabled={enviando || cpfErrado}>
        {enviando ? 'Cadastrando…' : 'Cadastrar estabelecimento'}
      </button>
    </form>
  );
}
