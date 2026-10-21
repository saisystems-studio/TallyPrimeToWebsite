export function registerServiceWorker() {
  if (!('serviceWorker' in navigator)) return;

  const start = () => {
    let hasController = Boolean(navigator.serviceWorker.controller);
    const registrationPromise = navigator.serviceWorker.register('/sw.js', { scope: '/', updateViaCache: 'none' });
    const onControllerChange = () => {
      if (hasController) window.location.reload();
      hasController = true;
    };
    navigator.serviceWorker.addEventListener('controllerchange', onControllerChange);

    registrationPromise
      .then((registration) => registration.update().catch(() => {}))
      .catch((error) => console.warn('Tally Web service worker registration failed:', error));

    const updateWhenVisible = () => {
      if (document.visibilityState === 'visible') {
        registrationPromise.then((registration) => registration.update()).catch(() => {});
      }
    };
    window.addEventListener('focus', updateWhenVisible);
    document.addEventListener('visibilitychange', updateWhenVisible);
  };

  if (document.readyState === 'complete') start();
  else window.addEventListener('load', start, { once: true });
}
