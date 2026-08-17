import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, mensagemDeErro } from '../../services/api';
import { autenticar } from './sessao';

interface RespostaCadastro {
  unidadeId: string;
  nomeFantasia: string;
  slug: string;
  linkDoCardapio: string;
}

export function CadastrarRestaurante() {
  const navegar = useNavigate();
  const [form, setForm] = useState({
    razaoSocial: '',
    nomeFantasia: '',
    cnpj: '',
    telefone: '',
    nomeAdministrador: '',
    email: '',
    senha: '',
  });
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  function campo(nome: keyof typeof form) {
    return {
      value: form[nome],
      onChange: (e: { target: { value: string } }) =>
        setForm((atual) => ({ ...atual, [nome]: e.target.value })),
    };
  }

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);

    try {
      await api.post<RespostaCadastro>('/v1/autenticacao/cadastrar-restaurante', form);
      // Entra direto: pedir para o dono digitar a senha de novo, dois segundos depois de
      // tê-la criado, é fricção sem ganho.
      await autenticar(form.email, form.senha);
      navegar('/painel');
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="tela-centro">
      <form className="cartao form largo" onSubmit={enviar}>
        <header className="marca">
          <h1>
            Grelha<span className="destaque">Hub</span>
          </h1>
          <p className="tagline">CADASTRE SEU RESTAURANTE</p>
        </header>

        <fieldset>
          <legend>O restaurante</legend>

          <label>
            Nome fantasia
            <input {...campo('nomeFantasia')} required minLength={3} placeholder="Brasa & Fogo Burger" />
          </label>

          <label>
            Razão social
            <input {...campo('razaoSocial')} required placeholder="Brasa e Fogo Alimentos LTDA" />
          </label>

          <div className="linha">
            <label>
              CNPJ
              <input {...campo('cnpj')} required placeholder="00.000.000/0001-00" />
            </label>

            <label>
              Telefone
              <input {...campo('telefone')} placeholder="(11) 98765-4321" />
            </label>
          </div>
        </fieldset>

        <fieldset>
          <legend>Seu acesso</legend>

          <label>
            Seu nome
            <input {...campo('nomeAdministrador')} required />
          </label>

          <div className="linha">
            <label>
              E-mail
              <input type="email" {...campo('email')} required autoComplete="username" />
            </label>

            <label>
              Senha
              <input
                type="password"
                {...campo('senha')}
                required
                minLength={8}
                autoComplete="new-password"
              />
            </label>
          </div>
        </fieldset>

        {erro && <p className="erro">{erro}</p>}

        <p className="nota">
          O endereço do seu cardápio online é gerado a partir do nome fantasia e{' '}
          <strong>não muda depois</strong> — o link circula em rede social e material
          impresso.
        </p>

        <button type="submit" disabled={enviando}>
          {enviando ? 'Criando…' : 'Criar restaurante'}
        </button>

        <p className="rodape-form">
          Já tem conta? <Link to="/entrar">Entrar</Link>
        </p>
      </form>
    </div>
  );
}
