# 10 — Roadmap

Un jalon = une session de travail complète. On ne passe pas au suivant tant que
le précédent n'est pas utilisé pour de vrai pendant quelques jours.

## M0 — Squelette
- [x] Repo, arbo de `11-conventions.md`, `.gitignore`, `.editorconfig`
- [x] Solution .NET : `RoomOS.Core`, `RoomOS.Domain`, `RoomOS.Agent.Windows`, `RoomOS.Core.Tests`
- [x] Projet Vite/React/TS/Tailwind
- [x] `GET /healthz` répond 200, le front l'affiche
- [x] GitHub Actions : build des 3 artefacts

**DoD** : `docker compose up` sur la VM, page blanche accessible depuis l'iPad, CI verte.

Vérifié le 2026-09-18 :
- `docker compose up -d` → conteneur `roomos-core` up, image 341 Mo.
- `GET /healthz` répond `{"status":"ok"}` sur `192.168.1.30:8080`, le front affiche
  « Core en ligne ».
- `dotnet test` : 1/1. Agent Windows publié en `win-x64` depuis Linux.
- Bundle : **70,4 Ko gzip** sur un budget de 200 Ko.

Restent à confirmer par l'utilisateur, hors de portée de la machine :
- [ ] Page réellement affichée sur l'iPad
- [ ] CI verte (nécessite un remote GitHub, pas encore configuré)

### Décisions prises pendant M0, absentes du pack initial
- Le Core sert le front depuis `wwwroot` : une seule origine, donc pas de CORS,
  ce qui simplifie l'auth SignalR de M1.
- `RoomOS.Agent.Windows` cible `net10.0-windows` avec `EnableWindowsTargeting`,
  sinon ni cette VM ni la CI Linux ne peuvent le compiler.
- Solution au format `.slnx` (défaut .NET 10), pas `.sln`.
- `InvariantGlobalization=true` : pas d'ICU dans l'image runtime.

## M1 — PC (le cœur)
- [x] SQLite + EF Core + migration initiale + seed d'un `gaming-pc`
- [x] Hub agent, auth par token, enregistrement
- [x] Agent Windows : service, connexion, télémétrie CPU/RAM/GPU 2 s
- [x] Wake-on-LAN, shutdown, restart
- [x] `GET /api/state`, hub client, carte PC dans l'UI
- [x] Auth par token sur l'API et le front

**DoD** : depuis l'iPad, tu allumes et tu éteins le PC, et tu vois la température CPU
bouger en direct. **À ce stade le projet est déjà utile tous les jours.**

Vérifié le 2026-09-18, côté Core, avec un agent simulé :
- Auth : 401 sans jeton, 401 avec un mauvais jeton, 403 avec le jeton agent sur une
  route client, 200 avec le bon. 15 tests au vert.
- Hub agent : `Register` → `online: true` avec uptime ; déconnexion → `online: false`
  et télémétrie remise à zéro, sans ping.
- Commandes : `shutdown` → reçue par l'agent → `Ack` tracé par le Core. `409` quand
  l'agent est absent, `404` sur un PC inconnu.
- Wake-on-LAN : paquet magique réellement émis vers `C8:7F:54:68:BB:40`, 202 immédiat.
- Télémétrie : 6 ticks en 12 s côté client, soit 0,5 Hz — le plafond serveur de 1 Hz
  n'est jamais atteint, exactement comme prévu.
- Base persistée dans le volume Docker, survit à un `compose restart`.
- Bundle : **87,4 Ko gzip** sur 200 après l'ajout de `@microsoft/signalr`.

Restent à confirmer sur le matériel réel :
- [ ] Agent installé sur le PC Windows (procédure dans `06-agent-windows.md`)
- [ ] Températures CPU/GPU réelles — c'est ici que le driver LibreHardwareMonitor
      peut être bloqué par l'intégrité de la mémoire
- [ ] Réveil effectif du PC (WoL activé dans le BIOS **et** sur la carte Intel)
- [ ] Carte PC affichée sur l'iPad

### Décisions prises pendant M1
- **Aucun secret en base.** `AgentToken` suit la même règle qu'`ApiToken` : variable
  d'environnement. `config_json` ne contient que MAC, IP et broadcast, réécrits depuis
  l'environnement à chaque démarrage.
- **Le Core refuse de démarrer si un jeton est vide.** Un jeton vide n'authentifie
  personne : mieux vaut un échec au démarrage qu'une API ouverte.
- **Schéma limité à `rooms` et `devices`.** Les tables `audio_outputs`, `scenes`,
  `integration_tokens` et `settings` arriveront avec les jalons qui les utilisent.
- **EF Core monté en 10.0.12** : la 10.0.0 tirait un `SQLitePCLRaw` vulnérable
  (GHSA-2m69-gcr7-jv3q). Un contrôle a été ajouté à la CI pour éviter la récidive.
- **Le PC d'un agent est déduit de sa connexion**, jamais de la charge utile.
  Corrigé à la revue de sécurité de fin de jalon.

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
