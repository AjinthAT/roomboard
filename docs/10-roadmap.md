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
- `GET /healthz` répond `{"status":"ok"}` sur `192.168.1.x:8080`, le front affiche
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
- Wake-on-LAN : paquet magique réellement émis vers `AA:BB:CC:DD:EE:FF`, 202 immédiat.
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

- [ ] Achat coordinateur + ampoule Zigbee, appairage dans Z2M — **en attente de livraison**
- [x] Mosquitto + Z2M dans le compose
- [x] Souscription MQTT, on/off/luminosité/couleur
- [x] Carte Lights dans l'UI

**DoD** : les lampes répondent en moins d'une seconde et l'état reste juste
même si elles sont pilotées par leur interrupteur physique.

Vérifié le 2026-09-18 **contre un Zigbee2MQTT simulé**, en publiant à la main les
messages qu'il émettrait :
- Disponibilité `online` puis `offline` : `reachable` suit, et l'état connu est
  conservé — une lampe coupée au mur n'est pas une lampe éteinte.
- État reçu `{"state":"ON","brightness":153,"color":{"x":0.5687,"y":0.3813}}` →
  RoomOS affiche allumée, **60 %**, **#FF6711**.
- Commandes émises sur `zigbee2mqtt/desk_light/set` : `{"state":"OFF"}`,
  `{"state":"ON","brightness":76}` pour 30 %, `{"color":{"hex":"#FF0000"}}`.
  Conformes aux charges utiles de `09-integrations.md`.
- Validation : couleur malformée et luminosité hors bornes refusées en 400, lampe
  inconnue en 404, absence de jeton en 401.

**Ce que la simulation ne prouve pas** : le délai de réponse sous la seconde, la
portée radio, et le comportement réel de l'ampoule. Tout cela demande le matériel.

### Décisions prises pendant M4
- **Mosquitto n'est publié que sur `127.0.0.1`.** Le Core l'atteint depuis le réseau
  hôte, Zigbee2MQTT par le réseau interne Docker, et rien depuis le LAN.
- **Zigbee2MQTT est derrière un profil compose** : sans coordinateur joignable il
  redémarre en boucle. `docker compose --profile zigbee up -d` une fois branché.
- **Conversion des unités faite explicitement.** La luminosité vaut 0–254 côté Zigbee
  et 0–100 côté RoomOS ; et Z2M accepte une couleur en hexadécimal mais la renvoie en
  coordonnées CIE xy. Sans conversion retour, la couleur affichée serait fausse dès
  qu'une lampe est pilotée hors de RoomOS. Couvert par des tests unitaires : c'est du
  calcul pur, et une erreur y serait silencieuse.

## M5 — Scènes
- [x] Moteur de scènes + exécuteurs
- [x] Tests unitaires du moteur (liste dans `08-scenes.md`)
- [x] Les 4 scènes en base, barre de scènes dans l'UI
- [x] Retour de progression par étape

**DoD** : « Gaming » sur un PC éteint aboutit à un poste prêt sans intervention.

Vérifié le 2026-09-18 :
- **55 tests**, dont 7 sur le moteur et 8 sur le parsing du DSL. La liste de cas de
  `08-scenes.md` est couverte : scène vide, étape en échec qui n'interrompt pas,
  `pc.wake` en timeout qui interrompt et saute la suite, annulation par une nouvelle
  scène, routage de chaque type vers son exécuteur.
- Les quatre scènes sont générées depuis la configuration et servies par l'API.

Reste à éprouver, et **c'est à l'utilisateur d'appuyer** : lancer une scène change
réellement la sortie audio, le volume et l'état du PC.
- [ ] « Work » depuis l'iPad
- [ ] « Gaming » sur un PC éteint — le vrai critère de la DoD
- [ ] « Night », qui éteint le PC

### Décisions prises pendant M5
- **`music.transfer` ajouté au DSL** (ADR D11), en étape explicite plutôt qu'en
  transfert implicite dans `music.play`.
- **Les scènes sont générées depuis la configuration**, et une étape visant un
  appareil non déclaré est omise plutôt qu'écrite puis vouée à l'échec.
- **Le blanc froid est approché par une couleur.** Le DSL n'a pas de champ de
  température ; en ajouter un avant d'avoir jugé le rendu réel serait prématuré.

## M6 — Durcissement
- [x] `/metrics` Prometheus sur le Core — **pas sur l'agent**, voir ci-dessous
- [x] Prometheus + Grafana dans le compose, un tableau de bord provisionné
- [x] Manifest PWA, icônes et service worker — **Tailscale reste à faire**
- [x] Déploiement automatique via runner self-hosted
- [x] README d'installation et de restauration

**DoD** : tu réinstalles la VM à partir du repo en moins de 30 minutes.

Vérifié le 2026-09-19 :
- `/metrics` sort les vraies valeurs au format Prometheus, point décimal compris —
  la VM est en français, la virgule aurait tout cassé. 3 tests de format.
- Prometheus scrute le Core par `host-gateway` et stocke : 12 séries `roomos_*`.
- Grafana provisionne seul sa source de données et son tableau de bord.
- **Sauvegarde et restauration éprouvées pour de vrai** : base détruite puis
  restaurée, les 4 scènes et l'autorisation Spotify survivent.
- Runner auto-hébergé `roomos-vm` en ligne, en service systemd.

Reste à faire, et **cela dépend de toi** :
- [ ] Tailscale : authentification de la machine, puis certificat. Procédure dans
      `13-installation.md`. Sans lui, le service worker reste dormant — il ne
      s'enregistre qu'en contexte sécurisé, sans que le code change.

### Décisions prises pendant M6
- **Aucune bibliothèque de métriques.** Le format texte de Prometheus est une ligne
  par échantillon, et nos métriques sont une poignée de jauges déjà en mémoire. Une
  dépendance apporterait compteurs, histogrammes et registre global, sans usage ici.
- **Pas de `/metrics` sur l'agent**, contrairement à la roadmap initiale. L'agent ne
  connaît rien que le Core n'ait déjà, puisqu'il lui pousse tout. Un serveur HTTP dans
  un service Windows pour réexposer les mêmes chiffres, c'est du code et une surface
  réseau de plus pour zéro information.
- **Le service worker ne met en cache que la coquille**, jamais l'API ni les hubs. Un
  dashboard qui afficherait l'état périmé d'un PC mentirait, ce que tout le projet
  cherche à éviter.
- **Le déploiement met à jour le clone canonique de la VM**, pas l'espace de travail
  du runner : un checkout là-bas lancerait une seconde pile à côté de la vraie.

## Après M6 — à discuter, pas à coder

Prises connectées, capteurs, automatisations horaires, éditeur de scènes,
client SwiftUI en second client, pièce supplémentaire.
