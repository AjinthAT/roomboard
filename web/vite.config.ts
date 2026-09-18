import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// La cible est Safari 16.6 (iPadOS 16.7.16). Voir docs/07-frontend.md.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    target: 'es2020',
    outDir: '../src/RoomOS.Core/wwwroot',
    emptyOutDir: true,
    sourcemap: false,
  },
  server: {
    host: true,
    proxy: {
      '/healthz': 'http://localhost:8080',
      '/api': 'http://localhost:8080',
      '/hub': { target: 'http://localhost:8080', ws: true },
    },
  },
});
