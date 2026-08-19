import { useState } from 'react';
import { api, mensagemDeErro } from '../services/api';

/**
 * "Reenviar convite" para quem está com **senha pendente** — `RN-GER-032`, `RN-GER-033`.
 *
 * O selo *Senha pendente* aparecia em quatro usuários e **não havia nada a fazer com ele na mesma
 * tela**: quem administra via o problema e não tinha botão. Na tela de plataforma isso pesa mais,
 * porque ela existe justamente para atender o cliente — o atendente enxergava a pendência do outro
 * lado da linha e não podia resolver dali.
 *
 * ⚠ **Reusa a rota que já existe** (`POST /v1/autenticacao/esqueci-minha-senha`) em vez de abrir
 * uma nova. Para o sistema é a mesma operação — emitir um link de uso único para um e-mail —, e
 * `RN-GER-033` já define o resto: o token novo **invalida os anteriores**, então reenviar duas
 * vezes não deixa dois links válidos por aí. Rota nova seria uma segunda porta para a mesma regra.
 *
 * ⚠ `RN-GER-034` — a rota responde **igual** exista ou não a conta, e é isso que impede a tela de
 * login de virar sonda de usuários. Aqui a conta veio da listagem: quem clicou já sabe que ela
 * existe, e a resposta uniforme não esconde nada dele. O que a tela **não** pode fazer é afirmar
 * que o e-mail chegou — `RN-GER-032` mantém o envio fora da transação, e falha de SMTP é
 * registrada sem desfazer nada. Por isso o texto diz "enviamos", não "recebeu".
 */
export function ReenviarConvite({ email }: { email: string }) {
  const [estado, setEstado] = useState<'parado' | 'enviando' | 'enviado'>('parado');
  const [erro, setErro] = useState<string | null>(null);

  async function reenviar() {
    setEstado('enviando');
    setErro(null);

    try {
      await api.post('/v1/autenticacao/esqueci-minha-senha', { email });
      setEstado('enviado');
    } catch (e) {
      setErro(mensagemDeErro(e));
      setEstado('parado');
    }
  }

  if (estado === 'enviado') {
    // Não volta a "parado" sozinho: reenvio em sequência gera token novo a cada clique
    // (`RN-GER-033` invalida o anterior), e o link que a pessoa já abriu morre. Recarregar a tela
    // devolve o botão, o que é fricção proporcional.
    return <span className="secundario">Link enviado</span>;
  }

  return (
    <>
      <button
        type="button"
        className="btn calada minuscula"
        disabled={estado === 'enviando'}
        onClick={() => void reenviar()}
        title={`Envia para ${email} um link novo de definição de senha. O anterior deixa de valer.`}
      >
        {estado === 'enviando' ? 'Enviando…' : 'Reenviar convite'}
      </button>

      {erro && <p className="erro">{erro}</p>}
    </>
  );
}
