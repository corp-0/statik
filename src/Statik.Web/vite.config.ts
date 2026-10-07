import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { readFileSync } from 'node:fs';

const backend = { target: 'http://localhost:5080', xfwd: true };

export default defineConfig(({ command }) => ({
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    allowedHosts: ['statik.dev', 'app.statik.dev'],
    https: command === 'serve' ? {
      key: readFileSync(new URL('../../.certs/statik-dev.key', import.meta.url)),
      cert: readFileSync(new URL('../../.certs/statik-dev.crt', import.meta.url)),
    } : undefined,
    proxy: {
      '/api': backend,
      '/health': backend,
      '/admin': backend,
      '/_content': backend,
      '^/[a-zA-Z0-9_-]+/?(?:\\?.*)?$': {
        ...backend,
        bypass(request) {
          const authority = request.headers[':authority'] ?? request.headers.host;
          if (typeof authority !== 'string' || authority.split(':')[0].toLowerCase() !== 'statik.dev') return request.url;
        },
      },
    },
  },
}));
