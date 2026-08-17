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

api.interceptors.response.use(
  (resposta) => resposta,
  (erro: AxiosError) => {
    // Token expirado ou inválido: derruba a sessão em vez de deixar a tela em estado
    // ambíguo, mostrando dados velhos que o usuário acha que ainda são válidos.
    if (erro.response?.status === 401 && lerToken()) {
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
  const axiosErro = erro as AxiosError<{ detail?: string; title?: string; regra?: string }>;
  const dados = axiosErro.response?.data;

  if (dados?.detail) {
    return dados.regra ? `${dados.detail} (${dados.regra})` : dados.detail;
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
