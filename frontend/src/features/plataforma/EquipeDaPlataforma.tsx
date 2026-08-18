import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Grelha } from '../../components/Grelha';
import { IconeDesligar, IconeMais } from '../../components/Icone';
import { api, mensagemDeErro } from '../../services/api';
import { lerSessao } from '../admin/sessao';

/**
 * A equipe da plataforma — `RN-GER-030`, `RN-GER-031`, ADR-0013.
 *
 * **Aba própria, e não uma opção dentro do cadastro de usuário do restaurante.** As duas populações
 * não têm interseção: o usuário de restaurante tem unidade, perfil e CPF; o de plataforma não tem
 * nenhum dos três (`RN-GER-014`, `RN-GER-023`). Um formulário só, com metade dos campos aparecendo
 * e sumindo conforme um seletor, esconderia isso atrás de um `if` — e o campo escondido é o que
 * volta preenchido quando alguém troca o seletor no meio do preenchimento.
 *
 * **Sem escolher estabelecimento e sem sessão de suporte**, e não é atalho: não há unidade a
 * escolher, e `RN-GER-015` existe para explicar por que a plataforma mexeu no dado de um cliente.
 * Aqui não há cliente — a sessão é aberta PARA uma unidade, e exigi-la obrigaria a escolher um
 * restaurante ao acaso para registrar um motivo falso na conta dele.
 */

interface UsuarioDeSuporte {
  id: string;
  nome: string;
  email: string;
  ativo: boolean;
  deveTrocarSenha: boolean;
  criadoEm: string;

  /** `RN-GER-031` — quem está autenticado. Vem do servidor, que é quem recusa a auto-inativação. */
  ehVoce: boolean;
}

export function EquipeDaPlataforma() {
  const [equipe, setEquipe] = useState<UsuarioDeSuporte[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [criando, setCriando] = useState(false);

  const carregar = useCallback(async () => {
    try {
      const { data } = await api.get<UsuarioDeSuporte[]>('/v1/plataforma/usuarios');
      setEquipe(data);
      setErro(null);
    } catch (e) {
      setErro(mensagemDeErro(e));
    }
  }, []);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  const ativos = equipe.filter((u) => u.ativo).length;

  async function alternar(usuario: UsuarioDeSuporte) {
    setErro(null);

    try {
      await api.post(`/v1/plataforma/usuarios/${usuario.id}/situacao`, { ativo: !usuario.ativo });
      await carregar();
    } catch (e) {
      setErro(mensagemDeErro(e));
    }
  }

  return (
    <>
      <div className="cabeca-de-secao">
        <div>
          <h2>Equipe da plataforma</h2>

          {/* CHAPA-10 — prosa na cor do texto. O número é dado, e vai em mono. */}
          <p>
            Quem dá suporte aos restaurantes. Enxerga <strong>todas</strong> as casas e não pertence
            a nenhuma.
          </p>
        </div>

        <button type="button" className="btn primario" onClick={() => setCriando(!criando)}>
          <IconeMais />
          {criando ? 'Fechar' : 'Novo acesso de plataforma'}
        </button>
      </div>

      {criando && (
        <NovoUsuarioDeSuporte
          aoCriar={async () => {
            setCriando(false);
            await carregar();
          }}
        />
      )}

      {erro && <p className="erro">{erro}</p>}

      <div className="rolagem">
        <table>
          <caption>
            <span className="dado">{ativos}</span> ativo{ativos === 1 ? '' : 's'} de{' '}
            <span className="dado">{equipe.length}</span>
          </caption>
          <thead>
            <tr>
              <th>Nome</th>
              <th>E-mail</th>
              <th className="col-estreita">Situação</th>
              <th className="col-estreita" aria-label="Ações" />
            </tr>
          </thead>
          <tbody>
            {equipe.map((u) => (
              <tr key={u.id} className={u.ativo ? undefined : 'apagado'}>
                <td>
                  <strong>{u.nome}</strong>
                  {u.ehVoce && <span className="chave"> você</span>}
                </td>
                <td className="dado">{u.email}</td>
                <td className="col-estreita">
                  <span className="selo" data-fase={situacao(u)}>
                    {rotulo(u)}
                  </span>
                </td>
                <td className="col-estreita">
                  {/* RN-GER-031 — o botão não aparece para si mesmo nem para o último ativo. A
                      recusa continua sendo do servidor: esconder é conveniência, e a tela não é
                      o gate (o mesmo desenho da matriz de delegação em SPEC-GER-007 §4). */}
                  {!u.ehVoce && !(u.ativo && ativos === 1) && (
                    <button type="button" className="btn calada" onClick={() => void alternar(u)}>
                      <IconeDesligar />
                      {u.ativo ? 'Inativar' : 'Reativar'}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {equipe.length === 0 && !erro && (
        <div className="vazio">
          <Grelha etapa={0} de={3} alta />
          <p className="fraco">Nenhum acesso de plataforma.</p>
        </div>
      )}
    </>
  );
}

function situacao(u: UsuarioDeSuporte): string {
  if (!u.ativo) return 'morto';

  // RF-SEG-005 — a senha ainda é a que outra pessoa escolheu.
  return u.deveTrocarSenha ? 'aberto' : 'fim';
}

function rotulo(u: UsuarioDeSuporte): string {
  if (!u.ativo) return 'Inativo';
  return u.deveTrocarSenha ? 'Senha pendente' : 'Ativo';
}

function NovoUsuarioDeSuporte({ aoCriar }: { aoCriar: () => Promise<void> }) {
  const sessao = lerSessao();

  const [form, setForm] = useState({ nome: '', email: '', senhaInicial: '' });
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const curta = form.senhaInicial.length > 0 && form.senhaInicial.length < 8;

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);

    try {
      await api.post('/v1/plataforma/usuarios', form);
      await aoCriar();
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <form className="placa" onSubmit={enviar}>
      <fieldset>
        <legend>Novo acesso de plataforma</legend>

        {/* Dito na tela porque é a pergunta que o formulário provoca: cadê o estabelecimento?
            CHAPA-10 — prosa na cor do texto, não em --muted. */}
        <p>
          Sem estabelecimento e sem perfil: quem opera o produto não pertence a restaurante nenhum.
          Sem CPF, pelo mesmo motivo — não é pessoa física de nenhuma casa.
        </p>

        <div className="linha">
          <label>
            Nome
            <input
              value={form.nome}
              onChange={(e) => setForm((a) => ({ ...a, nome: e.target.value }))}
              required
              autoComplete="off"
            />
          </label>

          <label>
            E-mail
            <input
              type="email"
              value={form.email}
              onChange={(e) => setForm((a) => ({ ...a, email: e.target.value }))}
              required
              autoComplete="off"
            />
          </label>
        </div>

        <label>
          Senha inicial
          <input
            type="password"
            value={form.senhaInicial}
            onChange={(e) => setForm((a) => ({ ...a, senhaInicial: e.target.value }))}
            required
            minLength={8}
            autoComplete="new-password"
          />
        </label>

        {curta && <p className="erro">A senha inicial deve ter ao menos 8 caracteres.</p>}

        <p>
          Ele <strong>troca a senha no primeiro acesso</strong>: enquanto ela valer,{' '}
          {sessao?.nome ?? 'quem criou'} também entra na conta.
        </p>
      </fieldset>

      {erro && <p className="erro">{erro}</p>}

      <button type="submit" className="btn primario" disabled={enviando || curta}>
        {enviando ? 'Criando…' : 'Criar acesso'}
      </button>
    </form>
  );
}
