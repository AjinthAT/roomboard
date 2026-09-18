// RoomOS — service worker minimal.
//
// Il ne met **rien** en cache des données : un dashboard qui affiche l'état d'un PC
// hors ligne mentirait, et c'est tout ce que le projet cherche à éviter. Il ne sert
// qu'à rendre la coquille de l'application disponible hors réseau, pour que l'iPad
// affiche « Core injoignable » au lieu du dinosaure de Safari.
//
// Il n'est enregistré qu'en contexte sécurisé, donc seulement derrière Tailscale
// (ADR D9). En HTTP sur le LAN, il n'existe pas.

const SHELL = 'roomos-shell-v1';

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(SHELL).then((cache) => cache.addAll(['/', '/manifest.webmanifest'])),
  );
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== SHELL).map((k) => caches.delete(k)))),
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);

  // Jamais l'API ni les hubs : ces réponses sont périssables par nature.
  if (url.pathname.startsWith('/api') || url.pathname.startsWith('/hub')) {
    return;
  }

  // Réseau d'abord, cache en secours : on veut toujours la dernière version servie.
  event.respondWith(
    fetch(event.request)
      .then((response) => {
        if (event.request.method === 'GET' && response.ok) {
          const copy = response.clone();
          caches.open(SHELL).then((cache) => cache.put(event.request, copy));
        }
        return response;
      })
      .catch(() => caches.match(event.request).then((hit) => hit ?? caches.match('/'))),
  );
});
