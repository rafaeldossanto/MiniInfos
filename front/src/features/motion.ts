export function initMotion() {
  if (matchMedia('(prefers-reduced-motion: reduce)').matches || !('IntersectionObserver' in window))
    return;
  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('is-visible');
        observer.unobserve(entry.target);
      });
    },
    { threshold: 0.12 },
  );
  document.documentElement.classList.add('js-motion');
  document.querySelectorAll('[data-reveal]').forEach((element, index) => {
    (element as HTMLElement).style.setProperty('--reveal-delay', (index % 3) * 80 + 'ms');
    observer.observe(element);
  });

  const words = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('is-read');
        words.unobserve(entry.target);
      });
    },
    { threshold: 1, rootMargin: '0px 0px -15% 0px' },
  );
  let index = 0;
  document.querySelectorAll('[data-tagline-line]').forEach((line) => {
    const content = line.textContent?.trim().split(/\s+/) ?? [];
    line.replaceChildren(
      ...content.flatMap((word, wordIndex) => {
        const span = document.createElement('span');
        span.className = 'tagline-word';
        span.style.transitionDelay = index++ * 70 + 'ms';
        span.textContent = word;
        words.observe(span);
        return wordIndex === content.length - 1 ? [span] : [span, document.createTextNode(' ')];
      }),
    );
  });
}
