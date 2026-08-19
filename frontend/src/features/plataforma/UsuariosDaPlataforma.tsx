import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { CpfNaListagem } from '../../components/CpfNaListagem';
import { ReenviarConvite } from '../../components/ReenviarConvite';
import { Grelha } from '../../components/Grelha';
import { IconeChave, IconeDesligar, IconeLapis, IconeMais } from '../../components/Icone';
import { api, mensagemDeErro } from '../../services/api';
import { MenuDePerfis, type PerfilDaUnidade } from '../admin/MenuDePerfis';
import { apenasDigitos, cpfValido, formatarCpf } from '../../services/cpf';
import type { UsuarioDaUnidade } from '../../services/tipos';
import type { Estabelecimento } from './tipos';

/**
 * A tela dedicada de usuários — `RN-GER-028`.
 *
 * **A unidade é um campo do formulário, obrigatório como qualquer outro.** Foi o que permitiu tirar
 * o cadastro de usuário de dentro do painel de cada casa: antes ele só existia lá porque a unidade
 * vinha do contexto da tela, e o resultado era tudo empilhado num cartão só.
 *
 * O filtro por loja usa `?unidadeId=` — o mesmo parâmetro que só a plataforma pode informar
 * (`RN-GER-016`: para o restaurante ele é ignorado).
 *
 * O tipo do perfil vem de `MenuDePerfis`, e não de uma cópia local: a cópia daqui não tinha
 * `permissoes`, e foi o que fez esta tela oferecer perfil sem dizer o que ele concede.
 */

export function UsuariosDaPlataforma() {
  const [casas, setCasas] = useState<Estabelecimento[]>([]);
  const [filtro, setFiltro] = useState('');
  const [usuarios, setUsuarios] = useState<UsuarioDaUnidade[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [criando, setCriando] = useState(false);
  const [editando, setEditando] = useState<string | null>(null);

  const carregarCasas = useCallback(async () => {
    try {
      const lista = await api.get<Estabelecimento[]>('/v1/plataforma/estabelecimentos');

      setCasas(lista.data);
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
            unidadeInicial={filtro}
            aoCriar={async () => {
              setCriando(false);
              await recarregar();
            }}
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
                {/* ⚠⚠ RN-GER-039 — o caso PIOR da regra, e a razão de ela existir: esta
                    listagem atravessa todas as casas (RN-GER-014). Com centenas de lojas, uma
                    coluna de CPF aberta é um cadastro consolidado de pessoas físicas de cliente
                    numa única tela. */}
                <td>
                  <CpfNaListagem cpf={u.cpf} />
                </td>
                <td className="dado">{u.email}</td>
                <td>
                  <span className="chave">{u.perfil}</span>
                </td>
                <td className="col-estreita">
                  <span className="selo" data-fase={situacao(u)}>
                    {rotuloDaSituacao(u)}
                  </span>

                  {/* ⚠⚠ Aqui pesa mais que na loja: esta tela existe para ATENDER o cliente, e o
                      atendente enxergava a pendência do outro lado da linha sem poder resolver
                      dali. RN-GER-032, RN-GER-033. */}
                  {u.ativo && u.deveTrocarSenha && <ReenviarConvite email={u.email} />}
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
          aoMudar={recarregar}
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

// ⚠ `casas` saiu das propriedades: existia só para o bloco de sessão de suporte descobrir a casa
// do usuário editado. Com a sessão removida (ADR-0020), a lista não tem mais para que servir aqui.
function EdicaoDeUsuario({
  usuario,
  aoMudar,
}: {
  usuario: UsuarioDaUnidade;
  aoMudar: () => Promise<void>;
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

      <form
        onSubmit={(e) => {
          e.preventDefault();
          void agir(() =>
            api.put(`/v1/usuarios/${usuario.id}`, { ...form, cpf: apenasDigitos(form.cpf) }),
          );
        }}
        style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}
      >
        <fieldset disabled={ocupado}>
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

        <button type="submit" className="btn" disabled={ocupado || cpfErrado}>
          <IconeLapis />
          Salvar cadastro
        </button>
      </form>

      <fieldset disabled={ocupado}>
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
          disabled={ocupado || novaSenha.length < 8}
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

      <fieldset disabled={ocupado}>
        <legend>Acesso</legend>

        <p className="secundario">
          {usuario.ativo
            ? 'Inativar impede o login desta pessoa. Nada é apagado (RN-GER-002).'
            : 'Esta pessoa está inativa e não consegue entrar.'}
        </p>

        <button
          type="button"
          className={usuario.ativo ? 'btn perigo' : 'btn'}
          disabled={ocupado}
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
  unidadeInicial,
  aoCriar,
}: {
  casas: Estabelecimento[];
  unidadeInicial: string;
  aoCriar: () => Promise<void>;
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

  const cpfCompleto = apenasDigitos(form.cpf).length === 11;
  const cpfErrado = cpfCompleto && !cpfValido(form.cpf);
  

  /**
   * O que ainda falta, nomeado — igual a `Usuarios.tsx`.
   *
   * ⚠ Aqui a checagem do PERFIL é obrigatória, e não conveniência: `<MenuDePerfis>` é um grupo de
   * `<button>`, então o navegador não tem o que validar. Sem esta linha, enviar sem escolher
   * perfil só falha no servidor — e era exatamente o caminho do defeito relatado.
   */
  const faltando = [
    unidadeId === '' && 'estabelecimento',
    form.nome.trim() === '' && 'nome',
    form.sobrenome.trim() === '' && 'sobrenome',
    !cpfCompleto && 'CPF',
    form.email.trim() === '' && 'e-mail',
    form.perfilNome === '' && 'perfil',
  ].filter((f): f is string => typeof f === 'string');

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


      <fieldset disabled={!unidadeId}>
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
            E-mail
            <input
              type="email"
              value={form.email}
              onChange={(e) => setForm((a) => ({ ...a, email: e.target.value }))}
              required
            />
          </label>
        </div>

        {/* RN-GER-010 — aqui SIM o perfil vem com o que ele concede, e não como `<select>`.
            Decisão do dono do produto em 18/08/2026 (§3 do design system): é a plataforma
            escolhendo perfil para uma operação que não é a dela, e "Gerente" num `<option>` não
            diz o que está sendo entregue a um cliente. Do outro lado do produto — o
            administrador do restaurante, em `Usuarios.tsx` — basta o nome: ele convive com os
            perfis todo dia.

            ⚠ Sem filtro por `delegavel`: a plataforma não passa pela matriz de delegação
            (`RN-GER-024`), como diz a nota no rodapé deste formulário. O que a limita é a
            sessão de suporte (`RN-GER-015`), e é ela que desabilita o fieldset inteiro. */}
        <MenuDePerfis
          perfis={perfis}
          // A rota da plataforma recebe `perfilNome`, e o menu trabalha com `id` — o perfil é
          // resolvido contra a unidade no servidor. A tradução fica aqui, nas duas pontas.
          selecionado={perfis.find((p) => p.nome === form.perfilNome)?.id ?? ''}
          aoSelecionar={(perfilId) =>
            setForm((a) => ({
              ...a,
              perfilNome: perfis.find((p) => p.id === perfilId)?.nome ?? '',
            }))
          }
          desabilitado={!unidadeId}
        />

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
        disabled={enviando || cpfErrado || faltando.length > 0}
      >
        {enviando ? 'Criando…' : 'Criar usuário'}
      </button>

      {/* A sessão de suporte tem mensagem própria, acima. Aqui só o que o formulário deve. */}
      {faltando.length > 0 && (
        <p className="ajuda">
          Falta preencher: <strong>{faltando.join(', ')}</strong>.
        </p>
      )}
    </form>
  );
}
