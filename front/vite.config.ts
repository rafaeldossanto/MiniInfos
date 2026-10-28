import { defineConfig } from 'vite';
import type { Connect, Plugin } from 'vite';
import { siteRoute, siteRoutes } from './src/routing.ts';

/** Match the CloudFront routes in development and when previewing the production build. */
const routePages: Connect.NextHandleFunction = (request, response, next) => {
  const url = new URL(request.url ?? '/', 'http://localhost');
  const route = siteRoute(url.pathname);
  if (!route) return next();
  if (url.pathname !== route.path) {
    response.writeHead(301, { Location: route.path + url.search });
    response.end();
    return;
  }
  request.url = '/' + route.file + url.search;
  next();
};

const cleanUrls: Plugin = {
  name: 'miniinfos-clean-urls',
  configureServer(server) {
    server.middlewares.use(routePages);
  },
  configurePreviewServer(server) {
    server.middlewares.use(routePages);
  },
};

export default defineConfig({
  plugins: [cleanUrls],
  build: {
    rollupOptions: {
      input: siteRoutes.map((route) => route.file),
    },
  },
});
