const SW_BUILD_ID = "2026-10-24T07:46:26.957Z";

self.addEventListener('install', (event) => {
  event.waitUntil(self.skipWaiting());
});

self.addEventListener('activate', (event) => {
  event.waitUntil(self.clients.claim());
});

// Deliberately network-only. No authentication, financial, or application data
// is written to Cache Storage, and sensitive GET responses bypass HTTP cache.
self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET' || !/^https?:$/.test(new URL(event.request.url).protocol)) return;
  event.respondWith(fetch(event.request, { cache: 'no-store' }));
});

void SW_BUILD_ID;

