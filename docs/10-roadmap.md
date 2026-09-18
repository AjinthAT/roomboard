# 10 — Roadmap

Un jalon = une session de travail complète. On ne passe pas au suivant tant que
le précédent n'est pas utilisé pour de vrai pendant quelques jours.

## M0 — Squelette
- [ ] Repo, arbo de `11-conventions.md`, `.gitignore`, `.editorconfig`
- [ ] Solution .NET : `RoomOS.Core`, `RoomOS.Domain`, `RoomOS.Agent.Windows`, `RoomOS.Core.Tests`
- [ ] Projet Vite/React/TS/Tailwind
- [ ] `GET /healthz` répond 200, le front l'affiche
- [ ] GitHub Actions : build des 3 artefacts

**DoD** : `docker compose up` sur la VM, page blanche accessible depuis l'iPad, CI verte.

## M1 — PC (le cœur)
- [ ] SQLite + EF Core + migration initiale + seed d'un `gaming-pc`
- [ ] Hub agent, auth par token, enregistrement
- [ ] Agent Windows : service, connexion, télémétrie CPU/RAM/GPU 2 s
- [ ] Wake-on-LAN, shutdown, restart
- [ ] `GET /api/state`, hub client, carte PC dans l'UI
- [ ] Auth par token sur l'API et le front

**DoD** : depuis l'iPad, tu allumes et tu éteins le PC, et tu vois la température CPU
bouger en direct. **À ce stade le projet est déjà utile tous les jours.**

## M2 — Audio
- [ ] Énumération des sorties par l'agent
- [ ] Bascule JBL / casque, volume, mute
- [ ] Persistance du mapping `windowsDeviceId` ↔ sortie
- [ ] Carte Audio dans l'UI

**DoD** : bascule fiable en une pression, y compris après un redémarrage du PC.

## M3 — Spotify
- [ ] OAuth PKCE, stockage du refresh token
- [ ] `IMusicProvider` + `SpotifyMusicProvider`
- [ ] Polling adaptatif, `NowPlayingChanged`
- [ ] Carte Music dans l'UI, gestion du cas « aucun appareil actif »

**DoD** : pochette et contrôles fonctionnels, pas de plantage quand Spotify est fermé.

## M4 — Lumières
- [ ] Achat coordinateur + ampoules Zigbee, appairage dans Z2M
- [ ] Mosquitto + Z2M dans le compose
- [ ] Souscription MQTT, on/off/luminosité/couleur
- [ ] Carte Lights dans l'UI

**DoD** : les lampes répondent en moins d'une seconde et l'état reste juste
même si elles sont pilotées par leur interrupteur physique.

## M5 — Scènes
- [ ] Moteur de scènes + exécuteurs
- [ ] Tests unitaires du moteur (liste dans `08-scenes.md`)
- [ ] Les 4 scènes en base, barre de scènes dans l'UI
- [ ] Retour de progression par étape

**DoD** : « Gaming » sur un PC éteint aboutit à un poste prêt sans intervention.

## M6 — Durcissement
- [ ] `/metrics` Prometheus sur le Core et l'agent
- [ ] Prometheus + Grafana dans le compose, un dashboard
- [ ] Tailscale → certificat valide → service worker + manifest PWA
- [ ] Déploiement automatique via runner self-hosted
- [ ] README d'installation et de restauration

**DoD** : tu réinstalles la VM à partir du repo en moins de 30 minutes.

## Après M6 — à discuter, pas à coder

Prises connectées, capteurs, automatisations horaires, éditeur de scènes,
client SwiftUI en second client, pièce supplémentaire.
