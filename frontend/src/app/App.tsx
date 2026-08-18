import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { Entrar } from '../features/admin/Entrar';
import { TrocarSenha } from '../features/admin/TrocarSenha';
import { DefinirSenha, EsqueciMinhaSenha } from '../features/admin/RecuperarAcesso';
import { Painel } from '../features/admin/Painel';
import { ExigeSessao } from '../features/admin/ExigeSessao';
import { Cardapio } from '../features/publico/Cardapio';
import { Acompanhar } from '../features/publico/Acompanhar';
import { Landing } from '../features/site/Landing';
import { Plataforma } from '../features/plataforma/Plataforma';

/**
 * Três áreas, um app.
 *
 * - `/`         → a vitrine, em `grelhahub.com` (docs/08-design-system.md §8)
 * - `/c/:slug`  → cardápio público, anônimo (RN-VEN-037)
 * - o resto     → gestão do restaurante, autenticada
 *
 * Um app só porque o deploy é único (ADR-0006, VPS única) e o build compartilhado evita
 * duplicar cliente HTTP, tipos e tema. Separar em dois pacotes é possível depois, se o
 * cardápio público passar a exigir carga inicial menor.
 */
export function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* A vitrine — o que o domínio serve para quem chega de fora */}
        <Route path="/" element={<Landing />} />

        {/* Área pública — sem autenticação */}
        <Route path="/c/:slug" element={<Cardapio />} />
        <Route path="/c/:slug/acompanhar" element={<Acompanhar />} />

        {/* Área do restaurante */}
        <Route path="/entrar" element={<Entrar />} />

        {/* RF-SEG-005 — fora de `ExigeSessao` de propósito: o guardião manda PARA cá quando a
            troca está pendente, e envolvê-la nele criaria um redirecionamento em círculo. */}
        <Route path="/trocar-senha" element={<TrocarSenha />} />

        {/* RN-GER-033 / RN-GER-034 — recuperação de acesso, anônima por natureza: quem esqueceu
            a senha não tem como provar quem é. As duas rotas de definição levam à MESMA tela; são
            dois caminhos porque é o que o e-mail promete em cada caso — convite e redefinição —,
            e link que abre tela com outro nome parece erro de quem recebeu. */}
        <Route path="/esqueci-minha-senha" element={<EsqueciMinhaSenha />} />
        <Route path="/definir-senha" element={<DefinirSenha />} />
        <Route path="/redefinir-senha" element={<DefinirSenha />} />

        {/* NAO existe rota publica de cadastro de restaurante, e a ausencia e decisao:
            ninguem ganha uma conta sem contrato assinado. Quem cria estabelecimento e o
            usuario de plataforma, em /plataforma (SPEC-GER-007 §4). */}
        {/* RN-GER-014 / ADR-0013 — a administração da plataforma. O componente redireciona
            quem não é plataforma; o gate de verdade é do servidor. */}
        <Route
          path="/plataforma"
          element={
            <ExigeSessao>
              <Plataforma />
            </ExigeSessao>
          }
        />

        <Route
          path="/painel/*"
          element={
            <ExigeSessao>
              <Painel />
            </ExigeSessao>
          }
        />

        {/* Rota desconhecida cai na vitrine, não no login: quem errou o endereço
            costuma ser visitante, e jogá-lo numa tela de senha é perder a visita. */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
