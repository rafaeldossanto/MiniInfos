/** Public URLs are independent of the HTML objects stored in S3. */
export const sitePages = [
  { pt: { path: '/', file: 'index.html' }, en: { path: '/en/', file: 'en/index.html' } },
  {
    pt: { path: '/comunidade', file: 'comunidade.html' },
    en: { path: '/en/community', file: 'en/community.html' },
  },
  {
    pt: { path: '/politica-de-privacidade', file: 'privacidade.html' },
    en: { path: '/en/privacy-policy', file: 'en/privacy.html' },
  },
  {
    pt: { path: '/termos-de-uso', file: 'termos.html' },
    en: { path: '/en/terms-of-use', file: 'en/terms.html' },
  },
  {
    pt: { path: '/pagina-nao-encontrada', file: '404.html' },
    en: { path: '/en/page-not-found', file: 'en/404.html' },
  },
] as const;

export const siteRoutes = sitePages.flatMap((page) => [page.pt, page.en]);

export function siteRoute(path: string) {
  return siteRoutes.find(
    (route) =>
      path === route.path ||
      path === route.path.replace(/\/$/, '') ||
      path === route.path + '/' ||
      path === '/' + route.file ||
      path === '/' + route.file.replace(/\.html$/, ''),
  );
}
