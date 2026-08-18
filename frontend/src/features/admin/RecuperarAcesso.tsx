import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Grelha } from '../../components/Grelha';
import { api, mensagemDeErro } from '../../services/api';

/**
 * Recuperação de acesso — `RN-GER-033`, `RN-GER-034`, `RN-GER-035`, ADR-0016.
 *
 * Duas telas no mesmo arquivo porque são as duas metades de um movimento só: pedir o link e usar o
 * link. Separá-las esconderia que a mensagem de uma precisa combinar com o que a outra faz.
 */

/**
 * `RN-GER-034` ⚠ — **a resposta é a mesma exista ou não a conta.**
 *
 * A tela precisa dizer isso sem mentir e sem entregar nada. "Se houver conta com este e-mail,
 * enviamos o link" é a frase exata: verdadeira nos dois casos, e não confirma nem nega a existência
 * de ninguém. Um "e-mail não encontrado" aqui daria a quem sonda a lista de quem é cliente.
 */
export function EsqueciMinhaSenha() {
  const [email, setEmail] = useState('');
  const [enviado, setEnviado] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);

    try {
      await api.post('/v1/autenticacao/esqueci-minha-senha', { email });
      setEnviado(true);
    } catch (e) {
      // Só chega aqui por e-mail malformado ou API fora do ar: o servidor responde 202 mesmo
      // quando não há conta.
      setErro(mensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  if (enviado) {
    return (
      <div className="tela-centro">
        <div className="placa form">
          <header>
            <Grelha etapa={4} de={5} alta />
            <span className="wordmark">
              Grelha<span>Hub</span>
            </span>
          </header>

          <p>
            Se houver uma conta com <strong>{email}</strong>, o link de redefinição já está a
            caminho. Ele vale por <strong>60 minutos</strong> e só pode ser usado uma vez.
          </p>

          <p>Não chegou? Confira o spam e peça outro — o novo link cancela o anterior.</p>

          <Link className="btn" to="/entrar">
            Voltar
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="tela-centro">
      <form className="placa form" onSubmit={enviar}>
        <header>
          <Grelha etapa={3} de={5} alta />
          <span className="wordmark">
            Grelha<span>Hub</span>
          </span>
          <p className="tagline">Esqueci minha senha</p>
        </header>

        <p>Informe o e-mail da sua conta. Enviamos um link para você escolher uma senha nova.</p>

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

        {erro && <p className="erro">{erro}</p>}

        <button type="submit" className="btn primario" disabled={enviando}>
          {enviando ? 'Enviando…' : 'Enviar link'}
        </button>

        <Link className="btn calada" to="/entrar">
          Voltar para o login
        </Link>
      </form>
    </div>
  );
}

/**
 * `RN-GER-033` / `RN-GER-035` — consome o link e define a senha.
 *
 * Serve ao convite de primeiro acesso e à redefinição: para o sistema é a mesma operação, e o que
 * muda entre as duas já está gravado no token. A rota tem dois caminhos — `/definir-senha` e
 * `/redefinir-senha` — porque é o que o e-mail promete em cada caso, e um link que abre uma tela
 * com outro nome parece erro de quem recebeu.
 */
export function DefinirSenha() {
  const [busca] = useSearchParams();
  const navegar = useNavigate();

  const token = busca.get('token') ?? '';

  const [senha, setSenha] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [pronto, setPronto] = useState(false);

  const curta = senha.length > 0 && senha.length < 8;
  const diferente = confirmacao.length > 0 && senha !== confirmacao;

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);

    try {
      await api.post('/v1/autenticacao/definir-senha', { token, novaSenha: senha });
      setPronto(true);
    } catch (e) {
      // O servidor responde a mesma coisa para link inexistente, expirado, já usado e invalidado
      // (RN-GER-033): distinguir diria a um estranho que o token existiu.
      setErro(mensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  // Link sem token é link truncado pelo cliente de e-mail. Dizer isso é melhor do que mostrar um
  // formulário que vai falhar depois de a pessoa escolher a senha.
  if (!token) {
    return (
      <div className="tela-centro">
        <div className="placa form">
          <header>
            <Grelha etapa={-1} de={5} alta />
            <span className="wordmark">
              Grelha<span>Hub</span>
            </span>
          </header>

          <p className="erro">Este endereço está incompleto — falta o código do link.</p>
          <p>Abra o link direto do e-mail, ou peça um novo.</p>

          <Link className="btn" to="/esqueci-minha-senha">
            Pedir outro link
          </Link>
        </div>
      </div>
    );
  }

  if (pronto) {
    return (
      <div className="tela-centro">
        <div className="placa form">
          <header>
            <Grelha etapa={4} de={5} alta />
            <span className="wordmark">
              Grelha<span>Hub</span>
            </span>
          </header>

          {/* RN-GER-035 — a senha foi escolhida pelo próprio dono, então não há troca pendente
              esperando no primeiro acesso. É o que faz o botão abaixo levar ao login e não a
              /trocar-senha. */}
          <p>
            Senha definida. <strong>Ninguém além de você a conhece</strong> — pode entrar.
          </p>

          <button type="button" className="btn primario" onClick={() => navegar('/entrar')}>
            Entrar
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="tela-centro">
      <form className="placa form" onSubmit={enviar}>
        <header>
          <Grelha etapa={3} de={5} alta />
          <span className="wordmark">
            Grelha<span>Hub</span>
          </span>
          <p className="tagline">Defina a sua senha</p>
        </header>

        <label>
          Nova senha
          <input
            type="password"
            value={senha}
            onChange={(e) => setSenha(e.target.value)}
            autoComplete="new-password"
            minLength={8}
            required
          />
        </label>

        <label>
          Repita a senha
          <input
            type="password"
            value={confirmacao}
            onChange={(e) => setConfirmacao(e.target.value)}
            autoComplete="new-password"
            required
          />
        </label>

        {curta && <p className="erro">A senha deve ter ao menos 8 caracteres.</p>}
        {diferente && <p className="erro">As duas senhas não são iguais.</p>}
        {erro && <p className="erro">{erro}</p>}

        <button
          type="submit"
          className="btn primario"
          disabled={enviando || curta || diferente || senha.length === 0}
        >
          {enviando ? 'Salvando…' : 'Definir senha'}
        </button>
      </form>
    </div>
  );
}
