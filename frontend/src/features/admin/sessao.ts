import { api, guardarToken, limparToken, lerToken } from '../../services/api';
import type { Sessao } from '../../services/tipos';

const CHAVE = 'grelhahub.sessao';

export function salvarSessao(sessao: Sessao) {
  guardarToken(sessao.token);
  localStorage.setItem(CHAVE, JSON.stringify(sessao));
}

/**
 * Os campos sem os quais a interface não funciona.
 *
 * ⚠ **A sessão vive no `localStorage` e sobrevive a deploy.** Quando o contrato do login ganha um
 * campo — foi o caso de `email`, acrescentado para a troca de senha do primeiro acesso —, a sessão
 * gravada pelo build ANTERIOR continua lá, sem ele. Um `as Sessao` cego devolvia esse objeto
 * incompleto e a tela quebrava adiante, longe da causa: "Entrando como" em branco e uma
 * reautenticação com e-mail `undefined`.
 *
 * Conferir aqui e **descartar** é melhor que remendar campo a campo: o custo é um login, e o
 * ganho é que nenhuma tela precisa se defender de sessão pela metade.
 */
const OBRIGATORIOS = ['token', 'usuarioId', 'nome', 'email', 'perfil'] as const;

export function lerSessao(): Sessao | null {
  const bruto = localStorage.getItem(CHAVE);
  if (!bruto || !lerToken()) return null;

  try {
    const guardada = JSON.parse(bruto) as Partial<Sessao>;

    const faltando = OBRIGATORIOS.filter((campo) => !guardada[campo]);

    if (faltando.length > 0) {
      // Sessão de um build anterior. Descarta em silêncio: o usuário vê a tela de login, que é o
      // estado correto, e não um erro sobre um campo que ele não sabe o que é.
      encerrarSessao();
      return null;
    }

    return guardada as Sessao;
  } catch {
    encerrarSessao();
    return null;
  }
}

export function encerrarSessao() {
  limparToken();
  localStorage.removeItem(CHAVE);
}

/**
 * RN-GER-010 — a pergunta é sobre PERMISSÃO, nunca sobre nome de perfil.
 *
 * A versão anterior era `ehGerencia(sessao)`, comparando `perfil === 'Administrador' || 'Gerente'`.
 * Era exatamente o que a regra proíbe: criar o perfil Operador (RN-GER-022) exigiria caçar essa
 * comparação e todas as suas irmãs.
 *
 * Esconder o botão é CONVENIÊNCIA. O gate real está no pipeline da API — o botão escondido não
 * protege nada contra quem chama a rota direto.
 *
 * Aqui a checagem é só de USABILIDADE: esconder um botão que o servidor recusaria.
 * ADR-0003 — a autoridade é sempre do backend; nenhuma regra vive só no front.
 */
export function pode(sessao: Sessao | null, permissao: string): boolean {
  if (!sessao) return false;

  // RN-GER-014 — o suporte da plataforma opera acima das permissões de unidade.
  if (sessao.ehPlataforma) return true;

  return sessao.permissoes?.includes(permissao) ?? false;
}

export async function autenticar(email: string, senha: string): Promise<Sessao> {
  const { data } = await api.post<Sessao>('/v1/autenticacao/login', { email, senha });
  salvarSessao(data);
  return data;
}
