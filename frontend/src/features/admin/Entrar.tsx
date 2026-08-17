import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Grelha } from '../../components/Grelha';
import { mensagemDeErro } from '../../services/api';
import { autenticar } from './sessao';

export function Entrar() {
  const navegar = useNavigate();
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);

    try {
      await autenticar(email, senha);
      navegar('/painel');
    } catch (e) {
      // ADR-0009 — a mensagem vem do servidor. O back devolve a mesma resposta
      // para senha errada e usuário inativo, de propósito.
      setErro(mensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
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

        <p className="rodape-form">
          Ainda não tem conta? <Link to="/cadastrar">Cadastre seu restaurante</Link>
        </p>
      </form>
    </div>
  );
}
