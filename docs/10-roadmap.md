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

Confirmé sur le matériel réel :
- [x] **Agent installé en service Windows** (`RoomOSAgent`, démarrage automatique,
      compte SYSTEM). Connecté au Core, télémétrie continue, aucune fenêtre visible.
- [x] Agent copié sur le PC, `--sensors` exécuté, matériel relevé
- [x] Températures **GPU** réelles : `GPU Core` à 40,4 °C
- [x] Températures **CPU** réelles : `CPU Package` à 51 °C après installation de
      PawnIO. L'intégrité de la mémoire est restée active — aucun compromis de
      sécurité. Voir `06-agent-windows.md`.
- [x] Les quatre corrections de capteurs confirmées sur le matériel : CPU 12,2 % /
      51 °C, GPU 1 % / 41,6 °C, VRAM 2568 sur 16303 Mo, RAM 24,9 sur 63,7 Go.
Restent à éprouver, en conditions réelles :
- [ ] Réveil effectif du PC (WoL activé dans le BIOS **et** sur la carte Intel)
- [ ] Extinction et redémarrage déclenchés depuis l'iPad
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
- [x] Énumération des sorties par l'agent
- [x] Bascule JBL / casque, volume, mute
- [x] Persistance du mapping `windowsDeviceId` ↔ sortie
- [x] Carte Audio dans l'UI

**DoD** : bascule fiable en une pression, y compris après un redémarrage du PC.

Vérifié le 2026-09-18 sur le matériel :
- Les deux sorties résolues par indice de nom, puis leurs identifiants Windows
  persistés : `jbl` → « Haut-parleurs (JBL Charge 6) », `headset` → « Casque pour
  téléphone (CORSAIR HS80 MAX WIRELESS) ».
- Bascule, volume et mute pilotés depuis l'iPad, quatre commandes acquittées `done`.
- `IPolicyConfig` fonctionne sur ce Windows 11, variante moderne, trois rôles appliqués.

Reste à éprouver :
- [ ] **Après un redémarrage du PC** — c'est le cœur de la DoD, et précisément ce que
      la persistance des identifiants doit garantir. Non vérifié à ce stade.
- [ ] Comportement des applications déjà ouvertes au moment de la bascule (Spotify
      est réputé conserver son point de sortie).

### Décisions prises pendant M2
- **P/Invoke direct sur `IPolicyConfig`** plutôt qu'`AudioSwitcher`, dont la dernière
  version est une alpha de 2016 ciblant .NETFramework. Voir `06-agent-windows.md`.
- **L'indice de correspondance vise le matériel, pas le rôle Windows.** « Casque »
  aurait désigné « Casque pour téléphone », le canal communications en bande étroite.
- **Le curseur de volume est le seul retour optimiste du projet.** Un curseur qui ne
  suit pas le doigt est inutilisable ; il repasse sous contrôle du serveur dès que
  celui-ci le rejoint.

## M3 — Spotify
- [x] OAuth PKCE, stockage du refresh token
- [x] `IMusicProvider` + `SpotifyMusicProvider`
- [x] Polling adaptatif, `NowPlayingChanged`
- [x] Carte Music dans l'UI, gestion du cas « aucun appareil actif »

**DoD** : pochette et contrôles fonctionnels, pas de plantage quand Spotify est fermé.

Vérifié le 2026-09-18 :
- Autorisation PKCE menée de bout en bout par tunnel SSH, jetons en base.
- `link = 2`, titre, artiste, pochette et appareil remontés.
- Sondage mesuré à **3,1 s** en lecture, conforme aux 3 s attendues.

Reste à éprouver :
- [ ] Contrôles précédent / lecture / suivant depuis l'iPad
- [ ] Comportement Spotify fermé (doit afficher « aucun appareil actif », pas planter)
- [ ] Renouvellement du jeton après une heure

### Problème ouvert reporté à M5
L'appareil actif était un téléphone, pas le PC. Les endpoints Player s'appliquent à
l'appareil actif du compte : la scène **Gaming** lancerait donc la musique sur le
téléphone. À trancher avant M5, voir `09-integrations.md`.

## M4 — Lumières

> **Matériel commandé : coordinateur SLZB-06 (CC2652P, Ethernet) et une seule
> ampoule E27 couleur.** Les deux E14 d'ambiance viendront plus tard.
>
> Une ampoule suffit à valider toute la chaîne — Mosquitto, Zigbee2MQTT, abonnement,
> carte Lumières. Les ajouts ultérieurs ne seront qu'un appairage dans Z2M, sans code.
>
> **Conséquence pour M5** : les scènes de `08-scenes.md` visent `desk-light` **et**
> `ambient-light`. Avec une seule ampoule, les étapes d'ambiance échoueraient à chaque
> exécution. Sans casser la scène — la règle 3 du moteur le prévoit — mais une scène
> qui rapporte un échec systématique finit par ne plus être lue. Les scènes seront
> alignées sur le matériel réellement présent au moment de M5.

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
