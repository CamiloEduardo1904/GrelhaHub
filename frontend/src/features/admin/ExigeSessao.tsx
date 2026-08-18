import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { lerSessao } from './sessao';

/**
 * O guardião das rotas autenticadas.
 *
 * **Duas perguntas, nesta ordem.** Sem sessão, login. Com sessão e `RF-SEG-005` pendente, troca de
 * senha — porque o pipeline da API recusa **todas** as outras rotas nesse estado, e renderizar o
 * painel só produziria a tela cheia de "Troque a sua senha antes de continuar" sem lugar para
 * trocá-la. Era exatamente o beco que existia antes desta interceptação.
 */
export function ExigeSessao({ children }: { children: ReactNode }) {
  const sessao = lerSessao();

  if (!sessao) {
    return <Navigate to="/entrar" replace />;
  }

  if (sessao.deveTrocarSenha) {
    return <Navigate to="/trocar-senha" replace />;
  }

  return <>{children}</>;
}
