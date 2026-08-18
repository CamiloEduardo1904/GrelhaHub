import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Grelha } from '../../components/Grelha';
import { IconeChave, IconeDesligar, IconeLapis, IconeMais } from '../../components/Icone';
import { api, mensagemDeErro } from '../../services/api';
import { apenasDigitos, cpfValido, formatarCpf } from '../../services/cpf';
import type { UsuarioDaUnidade } from '../../services/tipos';
import { SessaoDeSuporteDaCasa } from './SessaoDeSuporteDaCasa';
import { sessaoVigente, type Estabelecimento, type SessaoDeSuporte } from './tipos';

/**
 * A tela dedicada de usuários — `RN-GER-028`.
 *
 * **A unidade é um campo do formulário, obrigatório como qualquer outro.** Foi o que permitiu tirar
 * o cadastro de usuário de dentro do painel de cada casa: antes ele só existia lá porque a unidade
 * vinha do contexto da tela, e o resultado era tudo empilhado num cartão só.
 *
 * O filtro por loja usa `?unidadeId=` — o mesmo parâmetro que só a plataforma pode informar
 * (`RN-GER-016`: para o restaurante ele é ignorado).
 */

interface PerfilDaUnidade {
  id: string;
  nome: string;
  descricao: string;
  delegavel: boolean;
}

export function UsuariosDaPlataforma() {
  const [casas, setCasas] = useState<Estabelecimento[]>([]);
  const [sessoes, setSessoes] = useState<SessaoDeSuporte[]>([]);
  const [filtro, setFiltro] = useState('');
  const [usuarios, setUsuarios] = useState<UsuarioDaUnidade[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [criando, setCriando] = useState(false);
  const [editando, setEditando] = useState<string | null>(null);

  const carregarCasas = useCallback(async () => {
    try {
      const [lista, abertas] = await Promise.all([
        api.get<Estabelecimento[]>('/v1/plataforma/estabelecimentos'),
        api.get<SessaoDeSuporte[]>('/v1/plataforma/sessoes-de-suporte'),
      ]);

      setCasas(lista.data);
      setSessoes(abertas.data);
    } catch (e) {
      setErro(mensagemDeErro(e));
    }
  }, []);

  const carregarUsuarios = useCallback(async () => {
    try {
      // Sem filtro, a base inteira (ADR-0013). Com filtro, uma casa. Cada linha traz `unidadeId`,
      // que é o que torna a lista sem recorte legível.
      const { data } = await api.get<UsuarioDaUnidade[]>('/v1/usuarios', {
        params: filtro ? { unidadeId: filtro } : undefined,
      });

      setUsuarios(data);
      setErro(null);
    } catch (e) {
      setErro(mensagemDeErro(e));
    }
  }, [filtro]);

  useEffect(() => {
    void carregarCasas();
  }, [carregarCasas]);

  useEffect(() => {
    void carregarUsuarios();
  }, [carregarUsuarios]);

  const nomePorUnidade = new Map(casas.map((c) => [c.id, c.nomeFantasia]));

  async function recarregar() {
    await Promise.all([carregarUsuarios(), carregarCasas()]);
  }

  return (
    <>
      {erro && <p className="erro">{erro}</p>}

      <div className="painel-cartao" style={{ maxWidth: 'none' }}>
        <div className="comanda-id">
          <h2 className="wordmark" style={{ fontSize: '1.1rem' }}>
            Usuários
          </h2>
          <button
            type="button"
            className={criando ? 'btn calada' : 'btn'}
            onClick={() => setCriando((estava) => !estava)}
          >
            {!criando && <IconeMais />}
            {criando ? 'Cancelar' : 'Novo usuário'}
          </button>
        </div>

        <label>
          Estabelecimento
          <select value={filtro} onChange={(e) => setFiltro(e.target.value)}>
            <option value="">Todos os estabelecimentos</option>
            {casas.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nomeFantasia}
                {c.ativa ? '' : ' (inativa)'}
              </option>
            ))}
          </select>
        </label>

        {criando && (
          <FormularioDeUsuario
            casas={casas}
            sessoes={sessoes}
            unidadeInicial={filtro}
            aoCriar={async () => {
              setCriando(false);
              await recarregar();
            }}
            aoMudarSessao={carregarCasas}
          />
        )}
      </div>

      <div className="rolagem">
        <table>
          <caption>
            {usuarios.length} usuário{usuarios.length === 1 ? '' : 's'}
            {filtro ? ` em ${nomePorUnidade.get(filtro) ?? '—'}` : ' na base'}
          </caption>
          <thead>
            <tr>
              {/* Sem filtro, saber de quem é cada linha é o que faz a lista servir. */}
              {!filtro && <th>Estabelecimento</th>}
              <th>Nome</th>
              <th>CPF</th>
              <th>E-mail</th>
              <th>Perfil</th>
              <th className="col-estreita">Situação</th>
              <th className="col-estreita" aria-label="Ações" />
            </tr>
          </thead>
          <tbody>
            {usuarios.map((u) => (
              <tr key={u.id} className={u.ativo ? undefined : 'apagado'}>
                {!filtro && (
                  <td className="secundario">
                    {u.unidadeId ? (nomePorUnidade.get(u.unidadeId) ?? '—') : '—'}
                  </td>
                )}
                <td>
                  <strong>
                    {u.nome}
                    {u.sobrenome ? ` ${u.sobrenome}` : ''}
                  </strong>
                </td>
                <td className="dado">{u.cpf ?? '—'}</td>
                <td className="dado">{u.email}</td>
                <td>
                  <span className="chave">{u.perfil}</span>
                </td>
                <td className="col-estreita">
                  <span className="selo" data-fase={situacao(u)}>
                    {rotuloDaSituacao(u)}
                  </span>
                </td>
                <td className="col-estreita">
                  <button
                    type="button"
                    className="btn calada"
                    aria-expanded={editando === u.id}
                    onClick={() => setEditando(editando === u.id ? null : u.id)}
                  >
                    <IconeLapis />
                    {editando === u.id ? 'Fechar' : 'Editar'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {usuarios.length === 0 && !erro && (
        <div className="vazio">
          <Grelha etapa={0} de={3} alta />
          <p className="fraco">Nenhum usuário para este filtro.</p>
        </div>
      )}

      {editando && (
        <EdicaoDeUsuario
          usuario={usuarios.find((u) => u.id === editando)!}
          casas={casas}
          sessoes={sessoes}
          aoMudar={recarregar}
          aoMudarSessao={carregarCasas}
        />
      )}
    </>
  );
}

function situacao(u: UsuarioDaUnidade): string {
  if (!u.ativo) return 'morto';

  // RF-SEG-005 — a senha foi escolhida por outra pessoa: o acesso existe e ainda não é dele.
  return u.deveTrocarSenha ? 'aberto' : 'fim';
}

function rotuloDaSituacao(u: UsuarioDaUnidade): string {
  if (!u.ativo) return 'Inativo';
  return u.deveTrocarSenha ? 'Senha pendente' : 'Ativo';
}

/** Um bloco de sessão de suporte reaproveitado, com a casa do usuário sendo editado. */
function ExigeSessao({
  unidadeId,
  casas,
  sessoes,
  aoMudarSessao,
}: {
  unidadeId: string | null;
  casas: Estabelecimento[];
  sessoes: SessaoDeSuporte[];
  aoMudarSessao: () => Promise<void>;
}) {
  const casa = casas.find((c) => c.id === unidadeId);

  if (!casa) return null;

  return (
    <SessaoDeSuporteDaCasa
      casa={casa}
      sessao={sessaoVigente(sessoes, casa.id)}
      aoMudar={aoMudarSessao}
    />
  );
}

function EdicaoDeUsuario({
  usuario,
  casas,
  sessoes,
  aoMudar,
  aoMudarSessao,
}: {
  usuario: UsuarioDaUnidade;
  casas: Estabelecimento[];
  sessoes: SessaoDeSuporte[];
  aoMudar: () => Promise<void>;
  aoMudarSessao: () => Promise<void>;
}) {
  const [form, setForm] = useState({
    nome: usuario.nome,
    sobrenome: usuario.sobrenome ?? '',
    cpf: apenasDigitos(usuario.cpf ?? ''),
    email: usuario.email,
  });
  const [novaSenha, setNovaSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  const temSessao = usuario.unidadeId !== null && sessaoVigente(sessoes, usuario.unidadeId) !== null;
  const cpfErrado = apenasDigitos(form.cpf).length === 11 && !cpfValido(form.cpf);

  async function agir(acao: () => Promise<unknown>) {
    setOcupado(true);
    setErro(null);

    try {
      await acao();
      await aoMudar();
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  return (
    <div className="painel-cartao" style={{ maxWidth: 'none' }}>
      <h3 className="wordmark" style={{ fontSize: '1.05rem' }}>
        {usuario.nome}
        {usuario.sobrenome ? ` ${usuario.sobrenome}` : ''}
      </h3>

      {/* RN-GER-015 — mexer no dado de um usuário de cliente é SUPORTE. */}
      <ExigeSessao
        unidadeId={usuario.unidadeId}
        casas={casas}
        sessoes={sessoes}
        aoMudarSessao={aoMudarSessao}
      />

      <form
        onSubmit={(e) => {
          e.preventDefault();
          void agir(() =>
            api.put(`/v1/usuarios/${usuario.id}`, { ...form, cpf: apenasDigitos(form.cpf) }),
          );
        }}
        style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}
      >
        <fieldset disabled={!temSessao || ocupado}>
          <legend>Cadastro</legend>

          <div className="linha">
            <label>
              Nome
              <input
                value={form.nome}
                onChange={(e) => setForm((a) => ({ ...a, nome: e.target.value }))}
                required
              />
            </label>

            <label>
              Sobrenome
              <input
                value={form.sobrenome}
                onChange={(e) => setForm((a) => ({ ...a, sobrenome: e.target.value }))}
                required
              />
            </label>
          </div>

          <div className="linha">
            <label>
              CPF
              <input
                value={formatarCpf(form.cpf)}
                onChange={(e) => setForm((a) => ({ ...a, cpf: apenasDigitos(e.target.value) }))}
                required
                inputMode="numeric"
                aria-invalid={cpfErrado || undefined}
              />
            </label>

            <label>
              E-mail
              <input
                type="email"
                value={form.email}
                onChange={(e) => setForm((a) => ({ ...a, email: e.target.value }))}
                required
              />
            </label>
          </div>
        </fieldset>

        {/* Perfil não entra aqui: trocar perfil passa pela matriz de delegação (RN-GER-024), e
            permitir a troca por dentro de um "editar cadastro" faria ela escapar da matriz. */}
        <p className="nota">
          O <strong>perfil</strong> não é alterado aqui: ele passa pela matriz de delegação
          (<code>RN-GER-024</code>), e mudá-lo por dentro de um "editar cadastro" faria a troca
          escapar dela.
        </p>

        {cpfErrado && <p className="erro">Este CPF não é válido.</p>}

        <button type="submit" className="btn" disabled={!temSessao || ocupado || cpfErrado}>
          <IconeLapis />
          Salvar cadastro
        </button>
      </form>

      <fieldset disabled={!temSessao || ocupado}>
        <legend>Senha</legend>

        <label>
          Nova senha
          <input
            type="password"
            value={novaSenha}
            onChange={(e) => setNovaSenha(e.target.value)}
            minLength={8}
            autoComplete="new-password"
          />
        </label>

        {/* RN-GER-028 — a senha que um terceiro conhece não é senha. */}
        <p className="nota">
          Redefinir aqui faz a pessoa <strong>trocar a senha no próximo acesso</strong>
          (<code>RF-SEG-005</code>).
        </p>

        <button
          type="button"
          className="btn calada"
          disabled={!temSessao || ocupado || novaSenha.length < 8}
          onClick={() =>
            void agir(async () => {
              await api.post(`/v1/usuarios/${usuario.id}/senha`, { novaSenha });
              setNovaSenha('');
            })
          }
        >
          <IconeChave />
          Redefinir senha
        </button>
      </fieldset>

      <fieldset disabled={!temSessao || ocupado}>
        <legend>Acesso</legend>

        <p className="secundario">
          {usuario.ativo
            ? 'Inativar impede o login desta pessoa. Nada é apagado (RN-GER-002).'
            : 'Esta pessoa está inativa e não consegue entrar.'}
        </p>

        <button
          type="button"
          className={usuario.ativo ? 'btn perigo' : 'btn'}
          disabled={!temSessao || ocupado}
          onClick={() =>
            void agir(() =>
              api.post(
                `/v1/usuarios/${usuario.id}/${usuario.ativo ? 'desativar' : 'reativar'}`,
              ),
            )
          }
        >
          <IconeDesligar />
          {usuario.ativo ? 'Inativar usuário' : 'Reativar usuário'}
        </button>
      </fieldset>

      {erro && <p className="erro">{erro}</p>}
    </div>
  );
}

function FormularioDeUsuario({
  casas,
  sessoes,
  unidadeInicial,
  aoCriar,
  aoMudarSessao,
}: {
  casas: Estabelecimento[];
  sessoes: SessaoDeSuporte[];
  unidadeInicial: string;
  aoCriar: () => Promise<void>;
  aoMudarSessao: () => Promise<void>;
}) {
  const [unidadeId, setUnidadeId] = useState(unidadeInicial);
  const [perfis, setPerfis] = useState<PerfilDaUnidade[]>([]);
  const [form, setForm] = useState({
    nome: '',
    sobrenome: '',
    cpf: '',
    email: '',
    perfilNome: '',

  });
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  // Os perfis vêm da unidade escolhida: `perfilNome` é resolvido contra a unidade no servidor, e
  // oferecer um nome que aquela casa não tem daria 404 no meio do formulário.
  useEffect(() => {
    if (!unidadeId) {
      setPerfis([]);
      return;
    }

    let vivo = true;

    api
      .get<PerfilDaUnidade[]>('/v1/usuarios/perfis', { params: { unidadeId } })
      .then(({ data }) => {
        if (vivo) setPerfis(data);
      })
      .catch((e) => {
        if (vivo) setErro(mensagemDeErro(e));
      });

    return () => {
      vivo = false;
    };
  }, [unidadeId]);

  const cpfErrado = apenasDigitos(form.cpf).length === 11 && !cpfValido(form.cpf);
  const temSessao = unidadeId !== '' && sessaoVigente(sessoes, unidadeId) !== null;

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);

    try {
      // A unidade vai na ROTA: a plataforma não tem unidade própria (RN-GER-014), então não pode
      // usar a rota que a tira de quem está autenticado.
      await api.post(`/v1/plataforma/estabelecimentos/${unidadeId}/usuarios`, {
        ...form,
        cpf: apenasDigitos(form.cpf),
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
        <legend>Onde</legend>

        {/* A unidade é campo OBRIGATÓRIO, como qualquer outro do cadastro. É o que permitiu esta
            tela existir separada do painel de cada casa. */}
        <label>
          Estabelecimento
          <select value={unidadeId} onChange={(e) => setUnidadeId(e.target.value)} required>
            <option value="">Escolha o estabelecimento…</option>
            {casas.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nomeFantasia}
                {c.ativa ? '' : ' (inativa)'}
              </option>
            ))}
          </select>
        </label>
      </fieldset>

      {unidadeId && (
        <ExigeSessao
          unidadeId={unidadeId}
          casas={casas}
          sessoes={sessoes}
          aoMudarSessao={aoMudarSessao}
        />
      )}

      <fieldset disabled={!unidadeId || !temSessao}>
        <legend>A pessoa</legend>

        {/* Uma grade que se acomoda à largura, e não três `.linha` de duas colunas: a última
            delas tinha UM campo e deixava metade da linha vazia. Mesma correção de
            `Usuarios.tsx`, e o mesmo motivo — `.linha` só descreve bem par que anda junto. */}
        <div className="grade-usuario">
          <label>
            Nome
            <input
              value={form.nome}
              onChange={(e) => setForm((a) => ({ ...a, nome: e.target.value }))}
              required
            />
          </label>

          <label>
            Sobrenome
            <input
              value={form.sobrenome}
              onChange={(e) => setForm((a) => ({ ...a, sobrenome: e.target.value }))}
              required
            />
          </label>
          <label>
            CPF
            <input
              value={formatarCpf(form.cpf)}
              onChange={(e) => setForm((a) => ({ ...a, cpf: apenasDigitos(e.target.value) }))}
              required
              inputMode="numeric"
              placeholder="000.000.000-00"
              aria-invalid={cpfErrado || undefined}
            />
          </label>

          <label>
            Perfil
            <select
              value={form.perfilNome}
              onChange={(e) => setForm((a) => ({ ...a, perfilNome: e.target.value }))}
              required
            >
              <option value="">Escolha…</option>
              {perfis.map((p) => (
                <option key={p.id} value={p.nome}>
                  {p.nome}
                </option>
              ))}
            </select>
          </label>
          <label>
            E-mail
            <input
              type="email"
              value={form.email}
              onChange={(e) => setForm((a) => ({ ...a, email: e.target.value }))}
              required
            />
          </label>
        </div>

        {/* RN-GER-032 — sem campo de senha: o convite por e-mail define. Ver Usuarios.tsx. */}
        <p className="ajuda">
          Ele recebe um <strong>e-mail com link</strong> para definir a própria senha.
        </p>
      </fieldset>

      {cpfErrado && <p className="erro">Este CPF não é válido.</p>}
      {erro && <p className="erro">{erro}</p>}

      <p className="nota">
        A pessoa <strong>troca a senha no primeiro acesso</strong> (<code>RF-SEG-005</code>). A
        plataforma não passa pela matriz de delegação (<code>RN-GER-024</code>): ela não tem perfil
        de unidade — o que a limita é a sessão de suporte.
      </p>

      <button
        type="submit"
        className="btn bloco"
        disabled={enviando || cpfErrado || !unidadeId || !temSessao}
      >
        {enviando ? 'Criando…' : 'Criar usuário'}
      </button>
    </form>
  );
}
