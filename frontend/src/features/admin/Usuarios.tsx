import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Dialogo } from '../../components/Dialogo';
import { Grelha } from '../../components/Grelha';
import { api, mensagemDeErro } from '../../services/api';
import { apenasDigitos, cpfValido, formatarCpf } from '../../services/cpf';
import type { UsuarioDaUnidade } from '../../services/tipos';
import { MenuDePerfis, type PerfilDaUnidade } from './MenuDePerfis';
import { lerSessao, pode } from './sessao';

/**
 * `SPEC-GER-007` — os usuários da unidade.
 *
 * **A lista de perfis oferecida vem da matriz de delegação (`RN-GER-024`), mas esconder não é
 * impedir.** O `<select>` é do cliente: quem decide é o servidor, que recusa com 403 citando a
 * regra. O filtro aqui existe para não oferecer ao gerente uma opção que ele vai levar um erro
 * ao escolher.
 */

/**
 * Os perfis vêm da API já marcados com `delegavel`, calculado pela `MatrizDeDelegacao` do
 * **domínio**.
 *
 * A primeira versão desta tela espelhava a matriz em uma constante aqui. Dois problemas: a cópia
 * divergiria da do C# na primeira mudança — e a divergência apareceria ao usuário como "o sistema
 * me ofereceu e depois recusou" — e o `perfilId` não existia, porque eu o derivava dos usuários
 * já cadastrados. Numa unidade recém-criada só há o administrador, então o gerente nunca
 * conseguiria criar o primeiro operador.
 */
// O tipo vive com o componente que o consome: quem acrescentar campo na API mexe nos dois no
// mesmo arquivo.
export type { PerfilDaUnidade } from './MenuDePerfis';

export function Usuarios() {
  const sessao = lerSessao();

  const [usuarios, setUsuarios] = useState<UsuarioDaUnidade[]>([]);
  const [perfis, setPerfis] = useState<PerfilDaUnidade[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [criando, setCriando] = useState(false);
  const [desativando, setDesativando] = useState<UsuarioDaUnidade | null>(null);

  const carregar = useCallback(async () => {
    try {
      const [lista, comPerfis] = await Promise.all([
        api.get<UsuarioDaUnidade[]>('/v1/usuarios'),
        api.get<PerfilDaUnidade[]>('/v1/usuarios/perfis'),
      ]);

      setUsuarios(lista.data);
      setPerfis(comPerfis.data);
      setErro(null);
    } catch (e) {
      setErro(mensagemDeErro(e));
    }
  }, []);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  const podeGerenciar = pode(sessao, 'usuario.gerenciar');

  // RN-GER-024 — quem marca `delegavel` é o servidor. Esconder o que não é delegável é
  // conveniência; a recusa de verdade é 403 com a regra citada.
  const perfisConhecidos = perfis.filter((p) => p.delegavel);

  async function desativar(usuario: UsuarioDaUnidade) {
    try {
      await api.post(`/v1/usuarios/${usuario.id}/desativar`);
      setDesativando(null);
      await carregar();
    } catch (e) {
      setErro(mensagemDeErro(e));
    }
  }

  return (
    <>
      {erro && <p className="erro">{erro}</p>}

      {podeGerenciar && (
        <div className="painel-cartao">
          <div className="comanda-id">
            <h2 className="wordmark" style={{ fontSize: '1.1rem' }}>
              Equipe
            </h2>
            <button
              type="button"
              className={criando ? 'btn calada' : 'btn'}
              onClick={() => setCriando((estava) => !estava)}
            >
              {criando ? 'Cancelar' : 'Novo usuário'}
            </button>
          </div>

          {criando && (
            <FormularioDeUsuario
              perfis={perfisConhecidos}
              aoCriar={async () => {
                setCriando(false);
                await carregar();
              }}
            />
          )}

          {criando && perfisConhecidos.length === 0 && (
            <p className="nota">
              O seu perfil não pode atribuir perfil nenhum ainda nesta unidade —{' '}
              <strong>RN-GER-024</strong>.
            </p>
          )}
        </div>
      )}

      <div className="rolagem">
        <table>
          <caption>Usuários da unidade</caption>
          <thead>
            <tr>
              <th>Nome</th>
              <th>CPF</th>
              <th>E-mail</th>
              <th>Perfil</th>
              <th>Situação</th>
              {podeGerenciar && <th aria-label="Ações" />}
            </tr>
          </thead>
          <tbody>
            {usuarios.map((usuario) => (
              <tr key={usuario.id} className={usuario.ativo ? undefined : 'apagado'}>
                <td>
                  {usuario.nome}
                  {usuario.sobrenome ? ` ${usuario.sobrenome}` : ''}
                </td>

                {/* CHAPA-03 — dado em mono. CPF é dado. */}
                <td className="dado">{usuario.cpf ?? '—'}</td>
                <td className="dado">{usuario.email}</td>
                <td>
                  <span className="chave">{usuario.perfil}</span>
                </td>
                <td>
                  {/* CHAPA-06 — estado é selo com borda e cor, nunca emoji. */}
                  <span className="selo" data-fase={situacao(usuario)}>
                    {rotuloDaSituacao(usuario)}
                  </span>
                </td>
                {podeGerenciar && (
                  <td className="num">
                    {usuario.ativo && usuario.id !== sessao?.usuarioId && (
                      <button
                        type="button"
                        className="btn perigo"
                        onClick={() => setDesativando(usuario)}
                      >
                        Desativar
                      </button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {usuarios.length === 0 && !erro && (
        <div className="vazio">
          <Grelha etapa={0} de={3} alta />
          <p className="fraco">Nenhum usuário ainda além de você.</p>
        </div>
      )}

      {desativando && (
        <Dialogo
          titulo={`Desativar ${desativando.nome}?`}
          // RN-GER-002 — desativar, não excluir. A confirmação existe porque o efeito é imediato:
          // a pessoa perde o acesso no próximo login.
          descricao={
            'A pessoa deixa de conseguir entrar. O cadastro não é apagado — dá para reativar ' +
            'depois, e o histórico do que ela fez continua na trilha.'
          }
          rotuloConfirmar="Desativar"
          perigo
          aoConfirmar={() => desativar(desativando)}
          aoFechar={() => setDesativando(null)}
        />
      )}
    </>
  );
}

function situacao(usuario: UsuarioDaUnidade): string {
  if (!usuario.ativo) return 'morto';

  // RF-SEG-005 — a senha inicial foi escolhida por outra pessoa: o acesso existe, mas ainda não
  // é dele. É estado intermediário, e "aberto" é o âmbar do design system.
  return usuario.deveTrocarSenha ? 'aberto' : 'fim';
}

function rotuloDaSituacao(usuario: UsuarioDaUnidade): string {
  if (!usuario.ativo) return 'Inativo';
  return usuario.deveTrocarSenha ? 'Senha pendente' : 'Ativo';
}

function FormularioDeUsuario({
  perfis,
  aoCriar,
}: {
  perfis: PerfilDaUnidade[];
  aoCriar: () => Promise<void>;
}) {
  const [form, setForm] = useState({
    nome: '',
    sobrenome: '',
    cpf: '',
    email: '',
    perfilId: '',
  });
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  function campo(nome: 'nome' | 'sobrenome' | 'email') {
    return {
      value: form[nome],
      onChange: (e: { target: { value: string } }) =>
        setForm((atual) => ({ ...atual, [nome]: e.target.value })),
    };
  }

  const cpfCompleto = apenasDigitos(form.cpf).length === 11;
  const cpfErrado = cpfCompleto && !cpfValido(form.cpf);

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);

    try {
      // RN-GER-013 — sem máscara no corpo.
      await api.post('/v1/usuarios', { ...form, cpf: apenasDigitos(form.cpf) });
      await aoCriar();
    } catch (e) {
      // ADR-0009 — a mensagem vem do servidor. Quando é RN-GER-024, ela nomeia o perfil recusado.
      setErro(mensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <form onSubmit={enviar} style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
      <fieldset>
        <legend>Novo usuário</legend>

        <div className="linha">
          <label>
            Nome
            <input {...campo('nome')} required autoComplete="off" />
          </label>

          <label>
            Sobrenome
            <input {...campo('sobrenome')} required autoComplete="off" />
          </label>
        </div>

        <div className="linha">
          <label>
            CPF
            <input
              value={formatarCpf(form.cpf)}
              onChange={(e) => setForm((atual) => ({ ...atual, cpf: apenasDigitos(e.target.value) }))}
              required
              inputMode="numeric"
              placeholder="000.000.000-00"
              aria-invalid={cpfErrado || undefined}
            />
          </label>

        </div>

        {/* RN-GER-010 — o menu de perfis, com o que cada um concede. Substituiu o `<select>`:
            escolher perfil é conceder acesso, e a palavra "Gerente" num `<option>` não diz o
            que está sendo entregue. */}
        <MenuDePerfis
          perfis={perfis}
          selecionado={form.perfilId}
          aoSelecionar={(perfilId) => setForm((atual) => ({ ...atual, perfilId }))}
        />

        <div className="linha">
          <label>
            E-mail
            <input type="email" {...campo('email')} required autoComplete="off" />
          </label>

        </div>

        {/* RN-GER-032 / RN-GER-033 — NÃO se digita senha aqui, e a ausência do campo é o ponto.
            O usuário recebe um link de uso único por e-mail e define a própria senha: a senha que
            passa pelo WhatsApp de quem cadastrou não é senha. Substituir a senha é operação de
            EDIÇÃO (RN-GER-028), e lá ela reativa a troca obrigatória. */}
        <p className="ajuda">
          Ele vai receber um <strong>e-mail com um link</strong> para definir a própria senha. O
          link vale 72 horas e só pode ser usado uma vez.
        </p>
      </fieldset>

      {cpfErrado && <p className="erro">Este CPF não é válido — confira os dígitos.</p>}
      {erro && <p className="erro">{erro}</p>}

      <p className="nota">
        A pessoa <strong>troca a senha no primeiro acesso</strong> — senha escolhida por outra
        pessoa não é senha (<code>RF-SEG-005</code>).
      </p>

      <button type="submit" className="btn bloco" disabled={enviando || cpfErrado}>
        {enviando ? 'Criando…' : 'Criar usuário'}
      </button>
    </form>
  );
}
