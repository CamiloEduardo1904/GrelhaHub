import axios, { AxiosError } from 'axios';

/**
 * Cliente HTTP da API.
 *
 * ADR-0003 — em produção front e API vivem no mesmo domínio via Nginx (`/` e `/api/*`),
 * então a URL é sempre RELATIVA. Em desenvolvimento o Vite faz proxy de `/api`.
 * Nunca use URL absoluta: quebraria no deploy.
 */
export const api = axios.create({ baseURL: '/api' });

const CHAVE_TOKEN = 'grelhahub.token';

export function guardarToken(token: string) {
  localStorage.setItem(CHAVE_TOKEN, token);
}

export function lerToken(): string | null {
  return localStorage.getItem(CHAVE_TOKEN);
}

export function limparToken() {
  localStorage.removeItem(CHAVE_TOKEN);
}

api.interceptors.request.use((config) => {
  const token = lerToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

/**
 * Rotas em que 401 significa "credencial errada", e não "sessão expirada".
 *
 * ⚠ Sem esta lista, o interceptor abaixo transformava senha atual incorreta em LOGOUT: ele limpava
 * o token e navegava para /entrar, a tela de troca de senha era destruída no meio do fluxo e a
 * mensagem do servidor nunca chegava aos olhos de ninguém. Quem errava um caractere voltava para o
 * login achando que a troca havia falhado — depois de ela ter dado certo, em alguns casos.
 */
const ROTAS_DE_CREDENCIAL = [
  '/v1/autenticacao/login',
  '/v1/autenticacao/definir-senha',
  '/v1/autenticacao/esqueci-minha-senha',
  '/v1/usuarios/minha-senha',
];

api.interceptors.response.use(
  (resposta) => resposta,
  (erro: AxiosError) => {
    const rota = erro.config?.url ?? '';
    const eDeCredencial = ROTAS_DE_CREDENCIAL.some((r) => rota.startsWith(r));

    // Token expirado ou inválido: derruba a sessão em vez de deixar a tela em estado
    // ambíguo, mostrando dados velhos que o usuário acha que ainda são válidos.
    //
    // Mas só quando o 401 é de sessão. Nas rotas de credencial ele é resposta da operação, e a
    // tela precisa exibir a mensagem em vez de ser substituída pelo login.
    if (erro.response?.status === 401 && lerToken() && !eDeCredencial) {
      limparToken();
      if (!location.pathname.startsWith('/c/')) {
        location.href = '/entrar';
      }
    }
    return Promise.reject(erro);
  },
);

/**
 * Extrai a mensagem que a API devolveu.
 *
 * ADR-0009 — a mensagem ao usuário vem SEMPRE do servidor. O front não reescreve regra de
 * negócio em texto próprio: se o backend disser "X-Bacon está indisponível", é isso que
 * aparece, com o ID da regra quando houver.
 */
export function mensagemDeErro(erro: unknown): string {
  const axiosErro = erro as AxiosError<{
    detail?: string;
    title?: string;
    regra?: string;
    errors?: Record<string, string[]>;
  }>;

  const dados = axiosErro.response?.data;

  if (dados?.detail) {
    return dados.regra ? `${dados.detail} (${dados.regra})` : dados.detail;
  }

  /**
   * ⚠ `ProblemDetails` de VALIDAÇÃO guarda as mensagens em `errors`, uma lista por campo — e o
   * `title` dele é a frase genérica do ASP.NET, em INGLÊS: "One or more validation errors
   * occurred.".
   *
   * Era essa frase que chegava ao usuário. Numa tela em português, sem dizer qual campo, sem dizer
   * o quê: o pior erro possível, porque parece defeito do sistema quando é o formulário
   * incompleto. As mensagens de verdade — as dos validators, já em português — estavam logo
   * abaixo, no campo que este bloco passou a ler.
   */
  const doCampo = Object.values(dados?.errors ?? {})
    .flat()
    .filter((m) => typeof m === 'string' && m.trim() !== '');

  if (doCampo.length > 0) {
    return doCampo.join(' ');
  }

  return dados?.title ?? 'Não foi possível concluir a operação.';
}

export const moeda = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
});

/** Sem símbolo: usado nas linhas de item, onde o total já carrega o R$. */
export const valor = new Intl.NumberFormat('pt-BR', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});
