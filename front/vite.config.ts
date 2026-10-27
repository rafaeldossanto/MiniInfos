import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    rollupOptions: {
      input: {
        home: 'index.html',
        privacy: 'privacidade.html',
        terms: 'termos.html',
        notFound: '404.html',
        englishHome: 'en/index.html',
        englishPrivacy: 'en/privacy.html',
        englishTerms: 'en/terms.html',
        englishNotFound: 'en/404.html',
      },
    },
  },
});
