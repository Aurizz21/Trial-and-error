(() => {
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  const sections = document.querySelectorAll('[data-scroll-reveal]');

  if (!sections.length) return;

  if (reducedMotion.matches) {
    sections.forEach(section => section.classList.add('is-visible'));
    return;
  }

  if (!('IntersectionObserver' in window)) {
    document.documentElement.classList.add('scroll-reveal-static');
    return;
  }

  const pendingChanges = new Map();
  const requestedStates = new WeakMap();
  const observer = new IntersectionObserver(entries => {
    entries.forEach(entry => {
      const section = entry.target;
      const shouldBeVisible = entry.isIntersecting;
      requestedStates.set(section, shouldBeVisible);
      const pendingTimer = pendingChanges.get(section);

      if (pendingTimer !== undefined) {
        window.clearTimeout(pendingTimer);
        pendingChanges.delete(section);
      }

      const isVisible = section.classList.contains('is-visible');
      if (shouldBeVisible === isVisible) return;

      const timer = window.setTimeout(() => {
        pendingChanges.delete(section);

        if (requestedStates.get(section) !== shouldBeVisible) return;

        if (shouldBeVisible) {
          requestAnimationFrame(() => {
            requestAnimationFrame(() => {
              if (requestedStates.get(section) === shouldBeVisible) {
                section.classList.add('is-visible');
              }
            });
          });
        } else {
          section.classList.remove('is-visible');
        }
      }, 180);

      pendingChanges.set(section, timer);
    });
  }, {
    threshold: 0.01,
    rootMargin: '160px 0px 160px 0px'
  });

  sections.forEach(section => observer.observe(section));

  reducedMotion.addEventListener?.('change', event => {
    if (!event.matches) return;
    pendingChanges.forEach(timer => window.clearTimeout(timer));
    pendingChanges.clear();
    observer.disconnect();
    sections.forEach(section => section.classList.add('is-visible'));
  });
})();
