// Development service worker — no-op. The published build uses service-worker.published.js.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', () => self.clients.claim());
self.addEventListener('fetch', () => { /* pass-through */ });
