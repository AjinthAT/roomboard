# Changelog

Toutes les évolutions notables de RoomOS. Une entrée par jalon de `docs/10-roadmap.md`.

Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/).
Le projet ne suit pas SemVer : il suit ses jalons.

## M1 — PC — 2026-09-18

### Ajouté
- Persistance SQLite via EF Core : migration initiale (`rooms`, `devices`) et seed
  idempotent de la pièce et du `gaming-pc`.
- Authentification par jeton statique, avec deux rôles distincts pour le front et
  pour l'agent. Le jeton est accepté dans l'en-tête `Authorization` et, sur les hubs,
  en query string — seule façon d'authentifier un WebSocket depuis un navigateur.
- Hub agent : enregistrement, télémétrie, acquittements. La déconnexion du hub est
  le seul signal d'état « hors ligne » : pas de ping.
- Hub client et diffuseur relayant les mutations du `StateStore`.
- `GET /api/state`, `POST /api/pc/{id}/wake|shutdown|restart`.
- Wake-on-LAN : construction du paquet magique couverte par des tests unitaires,
  émission en broadcast sur les ports 9 et 7.
- Agent Windows : connexion sortante avec backoff plafonné à 30 s, télémétrie toutes
  les 2 s via LibreHardwareMonitor, acquittement **avant** exécution des commandes
  d'extinction.
- Front : saisie du jeton, store externe hors du state React, throttle à 1 Hz,
  carte PC avec CPU, GPU, RAM et actions, bandeau de reconnexion.

### Décidé
- **Aucun secret en base.** `AgentToken` rejoint `ApiToken` dans les variables
  d'environnement. `config_json` ne porte plus que la configuration réseau.
- **Démarrage refusé si un jeton est vide** : un jeton vide n'authentifie personne.
- **Schéma limité à ce que M1 utilise.** Les autres tables viendront avec leurs jalons.

### Sécurité
- **Revue de sécurité de fin de M1.** Deux failles d'autorisation corrigées sur le
  hub agent : `PushTelemetry` recevait le `pcId` dans sa charge utile, donc tout agent
  authentifié pouvait écrire l'état d'un autre PC — le PC est désormais déduit de la
  connexion, ce qui rejoint au passage la charge utile décrite dans `05-api.md` ;
  et `Register` acceptait n'importe quel identifiant, y compris absent de la base —
  il est maintenant validé. Le jeton agent dit « c'est un agent », pas « c'est cet
  agent-là ».
- EF Core monté de 10.0.0 à 10.0.12 : la version initiale tirait
  `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, affecté par GHSA-2m69-gcr7-jv3q (gravité
  élevée). Un contrôle `dotnet list package --vulnerable` a été ajouté à la CI.

### Corrigé
- Le volume `/data` était créé en root alors que le conteneur tourne en utilisateur
  non privilégié : SQLite ne pouvait pas ouvrir la base. Le répertoire est désormais
  créé et attribué dans l'image.

## M0 — Squelette — 2026-09-18

### Ajouté
- Dépôt git, arborescence de `docs/11-conventions.md`, `.gitignore`, `.editorconfig`,
  `.dockerignore`, `Directory.Build.props`.
- Solution `.slnx` avec `RoomOS.Domain`, `RoomOS.Core`, `RoomOS.Agent.Windows`
  et `RoomOS.Core.Tests`.
- `GET /healthz` sur le Core, avec son test de fumée.
- Front Vite + React 19 + TypeScript strict + Tailwind 4, buildé directement dans
  le `wwwroot` du Core. Affiche l'état du Core.
- Contrôle du budget de bundle : 200 Ko gzip, imposé par `docs/07-frontend.md`.
  Mesure actuelle : 70,4 Ko.
- Image Docker multi-étages (341 Mo) et `deploy/docker-compose.yml` pour le Core seul.
- CI GitHub Actions : trois jobs `core`, `agent`, `web`.

### Décidé
- **Le Core sert le front** depuis `wwwroot` plutôt qu'un conteneur nginx séparé.
  Une seule origine, donc pas de CORS — ce qui simplifie l'authentification SignalR.
- **`RoomOS.Agent.Windows` cible `net10.0-windows` avec `EnableWindowsTargeting`**,
  sans quoi ni la VM Debian ni la CI Linux ne peuvent le compiler.
- **`InvariantGlobalization=true`** : supprime la dépendance ICU de l'image runtime.
- Solution au format `.slnx`, défaut de .NET 10.

### Corrigé dans le pack de conception
Le pack `docs/` a été relu intégralement et six incohérences internes ont été levées :
sorties audio définies à deux endroits, `api_token` en base **et** en variable
d'environnement, `POST /wake` décrit comme un 202 bloquant, `ActiveOutputId` non
nullable alors qu'une sortie inconnue est un cas nominal, scène *Night* éteignant le
PC avant de mettre la musique en pause, et plafond de télémétrie jamais atteignable.
