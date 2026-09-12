import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': 'http://localhost:5080',
      '/health': 'http://localhost:5080',
      // Public profile paths belong to ASP.NET keep Vite's asset URLs local.
      '^/[a-zA-Z0-9_-]+/?(?:\\?.*)?$': 'http://localhost:5080',
    },
  },
});
