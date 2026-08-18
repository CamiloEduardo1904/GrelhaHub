import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 5173,
    // Em produção, front e API vivem no mesmo domínio via Nginx e não há CORS
    // (ADR-0003). Este proxy reproduz esse arranjo no desenvolvimento local, para
    // que o código do front use sempre `/api/...` e nunca uma URL absoluta.
    proxy: {
      '/api': { target: 'http://localhost:5000', changeOrigin: true },

      // ⚠ LEGADO — ADR-0015, substituido pela ADR-0018. As imagens NOVAS saem por /api/v1/arquivos,
      // que ja cai na regra acima. Esta linha existe para as referencias gravadas ANTES de 18/08/2026.
      //
      // ⚠ Sem esta linha, `/arquivos/...` cai no fallback de SPA do Vite e responde o
      // `index.html`: a API devolve `image/png`, o Vite devolve `text/html`, e o navegador tenta
      // decodificar HTML como imagem — aparece o ícone de foto quebrada. Em produção quem serve é
      // o Nginx (`location /arquivos/`), então o defeito só existe no desenvolvimento, que é
      // justamente onde ele engana: parece que o upload falhou, e o arquivo está gravado.
      '/arquivos': { target: 'http://localhost:5000', changeOrigin: true },
    },
  },
  build: { outDir: 'dist', sourcemap: true },
});
