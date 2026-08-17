import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { lerSessao } from './sessao';

export function ExigeSessao({ children }: { children: ReactNode }) {
  return lerSessao() ? <>{children}</> : <Navigate to="/entrar" replace />;
}
