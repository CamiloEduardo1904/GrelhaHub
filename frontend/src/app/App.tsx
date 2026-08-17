import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { CadastrarRestaurante } from '../features/admin/CadastrarRestaurante';
import { Entrar } from '../features/admin/Entrar';
import { Painel } from '../features/admin/Painel';
import { ExigeSessao } from '../features/admin/ExigeSessao';
import { Cardapio } from '../features/publico/Cardapio';
import { Acompanhar } from '../features/publico/Acompanhar';

/**
 * Duas áreas, um app.
 *
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
        {/* Área pública — sem autenticação */}
        <Route path="/c/:slug" element={<Cardapio />} />
        <Route path="/c/:slug/acompanhar" element={<Acompanhar />} />

        {/* Área do restaurante */}
        <Route path="/entrar" element={<Entrar />} />
        <Route path="/cadastrar" element={<CadastrarRestaurante />} />
        <Route
          path="/painel/*"
          element={
            <ExigeSessao>
              <Painel />
            </ExigeSessao>
          }
        />

        <Route path="/" element={<Navigate to="/painel" replace />} />
        <Route path="*" element={<Navigate to="/painel" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
