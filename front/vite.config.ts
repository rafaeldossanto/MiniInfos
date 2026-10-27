import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    rollupOptions: {
      input: {
        home: 'index.html',
        privacy: 'privacidade.html',
        terms: 'termos.html',
        notFound: '404.html',
      },
    },
  },
});
