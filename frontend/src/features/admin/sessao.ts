import { api, guardarToken, limparToken, lerToken } from '../../services/api';
import type { Sessao } from '../../services/tipos';

const CHAVE = 'grelhahub.sessao';

export function salvarSessao(sessao: Sessao) {
  guardarToken(sessao.token);
  localStorage.setItem(CHAVE, JSON.stringify(sessao));
}

export function lerSessao(): Sessao | null {
  const bruto = localStorage.getItem(CHAVE);
  if (!bruto || !lerToken()) return null;
  try {
    return JSON.parse(bruto) as Sessao;
  } catch {
    return null;
  }
}

export function encerrarSessao() {
  limparToken();
  localStorage.removeItem(CHAVE);
}

/**
 * RN-GER-010 — o perfil determina quais operações o usuário pode realizar.
 *
 * Aqui a checagem é só de USABILIDADE: esconder um botão que o servidor recusaria.
 * ADR-0003 — a autoridade é sempre do backend; nenhuma regra vive só no front.
 */
export function ehGerencia(sessao: Sessao | null): boolean {
  return sessao?.perfil === 'Administrador' || sessao?.perfil === 'Gerente';
}

export async function autenticar(email: string, senha: string): Promise<Sessao> {
  const { data } = await api.post<Sessao>('/v1/autenticacao/login', { email, senha });
  salvarSessao(data);
  return data;
}
