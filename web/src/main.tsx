import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import './styles/index.css';

const container = document.getElementById('root');

if (!container) {
  throw new Error("L'élément #root est introuvable dans index.html");
}

// Le service worker exige un contexte sécurisé. En HTTP sur le LAN il n'existe pas,
// et c'est voulu : le mode hors-ligne n'a aucun intérêt tant que le serveur piloté
// est sur le même réseau (ADR D9). Il s'activera derrière Tailscale, sans rien
// changer au code.
if ('serviceWorker' in navigator && window.isSecureContext) {
  window.addEventListener('load', () => {
    void navigator.serviceWorker.register('/sw.js').catch(() => undefined);
  });
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
