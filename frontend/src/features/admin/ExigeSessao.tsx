import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { lerSessao } from './sessao';

/**
 * O guardião das rotas autenticadas.
 *
 * **Três perguntas, nesta ordem.** Sem sessão, login. Com sessão e `RF-SEG-005` pendente, troca de
 * senha — porque o pipeline da API recusa **todas** as outras rotas nesse estado, e renderizar o
 * painel só produziria a tela cheia de "Troque a sua senha antes de continuar" sem lugar para
 * trocá-la. Era exatamente o beco que existia antes desta interceptação. Por fim, a área: quem é de
 * plataforma não tem painel de loja, e quem é de loja não tem administração de plataforma.
 *
 * ⚠⚠ **A terceira pergunta nasceu de um defeito real** (revisão de UX, 19/08/2026): `/painel`
 * aberto por usuário de plataforma renderizava a casca inteira da loja — seis abas, filtro de
 * datas, estado vazio — com *"Restaurante não encontrado."* em vermelho no topo, porque
 * `/v1/restaurante/meu` não devolve nada para quem não pertence a unidade nenhuma
 * (`RN-GER-014`). A tela coexistia com o próprio erro: falha em cima, interface funcionando
 * embaixo, abas clicáveis pressupondo um contexto que o usuário não tem. **O sistema sabia quem
 * era** — o cabeçalho exibia "Plataforma" — e montou a tela errada de qualquer forma.
 *
 * ⚠ **O guarda vive aqui, e não dentro de cada tela**, por dois motivos. O primeiro é de React:
 * `return` antes dos `useCallback`/`useEffect` do painel viola a ordem dos hooks, e depois deles
 * a requisição já saiu — o erro chega a um componente desmontado. O segundo é de manutenção: com o
 * desvio nas duas telas, as duas direções precisariam ser mantidas em sincronia em arquivos
 * diferentes, e a segunda cópia é a que envelhece errada.
 *
 * O gate de verdade é sempre do **servidor** (`RN-GER-016`); isto é para não abrir tela que só
 * daria 403 — ou, pior, tela que carrega pela metade e não diz o que aconteceu.
 */
export function ExigeSessao({
  children,
  area,
}: {
  children: ReactNode;

  /** Para quem esta rota é. Sem valor, só as duas primeiras perguntas são feitas. */
  area?: 'loja' | 'plataforma';
}) {
  const sessao = lerSessao();

  if (!sessao) {
    return <Navigate to="/entrar" replace />;
  }

  if (sessao.deveTrocarSenha) {
    return <Navigate to="/trocar-senha" replace />;
  }

  if (area === 'loja' && sessao.ehPlataforma) {
    return <Navigate to="/plataforma" replace />;
  }

  if (area === 'plataforma' && !sessao.ehPlataforma) {
    return <Navigate to="/painel" replace />;
  }

  return <>{children}</>;
}
