import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { AlternadorDeTema } from '../../components/AlternadorDeTema';
import { Grelha } from '../../components/Grelha';
import { encerrarSessao, lerSessao } from '../admin/sessao';
import { Estabelecimentos } from './Estabelecimentos';
import { UsuariosDaPlataforma } from './UsuariosDaPlataforma';
import { EquipeDaPlataforma } from './EquipeDaPlataforma';

/**
 * A administração da plataforma — `RN-GER-014`, ADR-0013, ADR-0014.
 *
 * **É a tela mais perigosa do sistema:** ela atravessa unidade de propósito. O gate de verdade é do
 * servidor (`EhPlataforma` no handler, e `RN-GER-015` exigindo sessão de suporte antes de qualquer
 * escrita); o `Navigate` abaixo é apenas para não mostrar uma tela que vai só dar 403.
 *
 * **Este arquivo é só a casca.** A primeira versão tinha lista, cadastro, módulos, sessão de
 * suporte, criação de usuário e leitura da operação **no mesmo cartão** — e ficou ilegível. Cada
 * responsabilidade agora tem o seu arquivo, e o detalhe de uma casa vive atrás do olho, não
 * empilhado embaixo da tabela.
 */

const ABAS = [
  { chave: 'estabelecimentos', nome: 'Estabelecimentos' },
  { chave: 'usuarios', nome: 'Usuários' },

  // RN-GER-030 — a equipe da plataforma. Aba separada de "Usuários" porque são duas populações
  // sem interseção: lá tem unidade, perfil e CPF; aqui não tem nenhum dos três.
  { chave: 'equipe', nome: 'Equipe' },
] as const;

type Aba = (typeof ABAS)[number]['chave'];

export function Plataforma() {
  const sessao = lerSessao();
  const navegar = useNavigate();

  const [aba, setAba] = useState<Aba>('estabelecimentos');

  // RN-GER-016 — o desvio de quem não é plataforma vive em `ExigeSessao` (`area="plataforma"`),
  // junto do desvio simétrico. Duas cópias em arquivos diferentes envelheceriam desencontradas, e
  // foi a ausência do outro lado que deixou `/painel` renderizar a loja para a plataforma.
  //
  // A conferência de nulo continua aqui porque o TypeScript precisa dela para o resto do corpo.
  if (!sessao) return <Navigate to="/entrar" replace />;

  return (
    <div className="painel">
      <header className="barra-app">
        <Grelha etapa={4} de={5} />
        <span className="wordmark">
          Grelha<span>Hub</span>
        </span>
        <span className="rotulo">Plataforma</span>

        <div className="quem">
          <strong>{sessao.nome}</strong>
          Suporte
        </div>

        <AlternadorDeTema />

        <button
          type="button"
          className="btn"
          onClick={() => {
            encerrarSessao();
            navegar('/entrar');
          }}
        >
          Sair
        </button>
      </header>

      <nav className="abas" role="tablist" aria-label="Áreas da plataforma">
        {ABAS.map(({ chave, nome }) => (
          <button
            key={chave}
            type="button"
            role="tab"
            className="aba"
            aria-selected={aba === chave}
            onClick={() => setAba(chave)}
          >
            {/* CHAPA-02 — uma barra âmbar por grupo: a aba ativa. */}
            <Grelha etapa={aba === chave ? 0 : -1} de={1} />
            {nome}
          </button>
        ))}
      </nav>

      <main className="conteudo">
        {aba === 'estabelecimentos' && <Estabelecimentos />}
        {aba === 'usuarios' && <UsuariosDaPlataforma />}
        {aba === 'equipe' && <EquipeDaPlataforma />}
      </main>
    </div>
  );
}
