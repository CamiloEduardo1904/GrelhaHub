import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { Grelha } from '../../components/Grelha';
import { api, mensagemDeErro } from '../../services/api';
import { autenticar, encerrarSessao, lerSessao } from './sessao';

/**
 * `RF-SEG-005` — a troca de senha no primeiro acesso.
 *
 * **Esta tela fechou um beco sem saída.** O usuário criado por outra pessoa nascia com a pendência,
 * o `TrocaDeSenhaBehavior` recusava **todas** as rotas com "Troque a sua senha antes de continuar",
 * e não existia onde trocá-la: a conta nascia inutilizável. A API estava pronta desde a
 * `SPEC-GER-004` — faltava a porta.
 *
 * Era o terceiro caso do mesmo padrão no projeto (sessão de suporte e dependência de módulo foram
 * os outros dois), e é o que motivou o item na definição de pronto do guia de specs: **se esta
 * regra recusar a operação, para onde o usuário vai?**
 */

const MINIMO = 8;

export function TrocarSenha() {
  const navegar = useNavigate();
  const [sessao] = useState(lerSessao);

  const [senhaAtual, setSenhaAtual] = useState('');
  const [nova, setNova] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  if (!sessao) {
    return <Navigate to="/entrar" replace />;
  }

  const destino = sessao.ehPlataforma ? '/plataforma' : '/painel';

  // Quem já trocou não tem o que fazer aqui. Deixar a tela acessível seria oferecer "trocar senha"
  // como se fosse gestão de conta — que é outra coisa e não existe ainda.
  if (!sessao.deveTrocarSenha) {
    return <Navigate to={destino} replace />;
  }

  const curta = nova.length > 0 && nova.length < MINIMO;
  const diferentes = confirmacao.length > 0 && nova !== confirmacao;
  const repetida = nova.length > 0 && nova === senhaAtual;

  const impedido = curta || diferentes || repetida || nova.length < MINIMO || !senhaAtual;

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);

    try {
      await api.post('/v1/usuarios/minha-senha', { senhaAtual, novaSenha: nova });

      // ⚠ Reautentica, e não é conveniência: o claim `trocar_senha` viaja NO TOKEN, então o token
      // que está na mão continua bloqueando todas as rotas mesmo depois de a senha mudar. Entrar
      // de novo com a senha nova é o que devolve um token limpo.
      await autenticar(sessao!.email, nova);

      navegar(destino, { replace: true });
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="tela-centro">
      <form className="placa form" onSubmit={enviar}>
        <header>
          <Grelha etapa={0} de={2} alta />
          <span className="wordmark">
            Grelha<span>Hub</span>
          </span>
          <p className="tagline">Escolha a sua senha</p>
        </header>

        <p className="nota">
          A senha que você recebeu foi escolhida por outra pessoa, e por isso não é a sua. Defina
          uma agora para continuar.
        </p>

        <label>
          Senha que você recebeu
          <input
            type="password"
            value={senhaAtual}
            onChange={(e) => setSenhaAtual(e.target.value)}
            required
            autoComplete="current-password"
            autoFocus
          />
        </label>

        <label>
          Nova senha
          <input
            type="password"
            value={nova}
            onChange={(e) => setNova(e.target.value)}
            required
            minLength={MINIMO}
            autoComplete="new-password"
            aria-invalid={curta || repetida || undefined}
          />
        </label>

        <label>
          Repita a nova senha
          <input
            type="password"
            value={confirmacao}
            onChange={(e) => setConfirmacao(e.target.value)}
            required
            autoComplete="new-password"
            aria-invalid={diferentes || undefined}
          />
        </label>

        {/* A conferência aqui é de USABILIDADE. O mínimo de 8 é do validator do servidor, e a
            repetição não existe lá — ela existe porque digitar senha às cegas duas vezes erra. */}
        {curta && <p className="erro">A senha precisa de ao menos {MINIMO} caracteres.</p>}
        {repetida && <p className="erro">A nova senha não pode ser a que você recebeu.</p>}
        {diferentes && <p className="erro">As duas senhas não são iguais.</p>}

        {erro && <p className="erro">{erro}</p>}

        <button type="submit" className="btn bloco" disabled={enviando || impedido}>
          {enviando ? 'Salvando…' : 'Salvar e continuar'}
        </button>

        {/* A saída. Sem ela esta tela é um beco: quem clicou em "Acesse já" com a sessão de
            outra pessoa aberta no navegador — ou quem não tem a senha recebida em mão — fica preso
            num formulário que não consegue preencher. É o mesmo padrão de gate sem porta que esta
            própria tela veio corrigir, em miniatura. */}
        <p className="rodape-form">
          Entrando como <span className="dado">{sessao.email}</span>
          <br />
          <button
            type="button"
            className="link-discreto"
            onClick={() => {
              encerrarSessao();
              navegar('/entrar', { replace: true });
            }}
          >
            Entrar com outra conta
          </button>
        </p>
      </form>
    </div>
  );
}
