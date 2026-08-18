import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { Grelha } from '../../components/Grelha';
import { mensagemDeErro } from '../../services/api';
import { autenticar, lerSessao } from './sessao';

export function Entrar() {
  const navegar = useNavigate();

  // Quem já tem sessão vai direto ao painel. Antes da vitrine, `/` redirecionava para
  // `/painel` e isto era resolvido pelo roteador; agora `/` é a página pública, e sem esta
  // checagem quem já está logado teria de digitar a senha de novo para entrar pelo domínio.
  const [sessaoExistente] = useState(lerSessao);

  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);

    try {
      const sessao = await autenticar(email, senha);

      // RF-SEG-005 antes de tudo: com a troca pendente, o pipeline recusa todas as outras rotas.
      if (sessao.deveTrocarSenha) {
        navegar('/trocar-senha');
        return;
      }

      // RN-GER-014 — o usuário de plataforma não tem unidade, e `/painel` pressupõe uma. Mandá-lo
      // para lá renderizaria um painel sem restaurante, que parece defeito.
      navegar(sessao.ehPlataforma ? '/plataforma' : '/painel');
    } catch (e) {
      // ADR-0009 — a mensagem vem do servidor. O back devolve a mesma resposta
      // para senha errada e usuário inativo, de propósito.
      setErro(mensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  if (sessaoExistente) {
    if (sessaoExistente.deveTrocarSenha) {
      return <Navigate to="/trocar-senha" replace />;
    }

    return <Navigate to={sessaoExistente.ehPlataforma ? '/plataforma' : '/painel'} replace />;
  }

  return (
    <div className="tela-centro">
      <form className="placa form" onSubmit={enviar}>
        <header>
          <Grelha etapa={2} de={5} alta />
          <span className="wordmark">
            Grelha<span>Hub</span>
          </span>
          <p className="tagline">Do pedido à brasa</p>
        </header>

        <label>
          E-mail
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="username"
            required
          />
        </label>

        <label>
          Senha
          <input
            type="password"
            value={senha}
            onChange={(e) => setSenha(e.target.value)}
            autoComplete="current-password"
            required
          />
        </label>

        {erro && <p className="erro">{erro}</p>}

        <button type="submit" className="btn bloco" disabled={enviando}>
          {enviando ? 'Entrando…' : 'Entrar'}
        </button>
        {/* Sem "criar conta": a conta do restaurante e criada pela plataforma, com o contrato. */}
      </form>
    </div>
  );
}
