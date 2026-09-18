# Changelog

Toutes les évolutions notables de RoomOS. Une entrée par jalon de `docs/10-roadmap.md`.

Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/).
Le projet ne suit pas SemVer : il suit ses jalons.

## M2 — Audio — 2026-09-18

### Ajouté
- Table `audio_outputs` et sa migration, alimentée depuis la configuration.
- Résolveur associant les périphériques énumérés par l'agent aux sorties déclarées :
  identifiant Windows d'abord, indice de nom en repli, puis persistance de
  l'identifiant trouvé pour ne plus rejouer la recherche approximative.
- `PUT /api/audio/{pcId}/output|volume|mute`, commandes et acquittements côté agent.
- Énumération WASAPI via NAudio, bascule de sortie par défaut via `IPolicyConfig`
  en P/Invoke, avec les deux variantes connues de l'interface et les trois rôles.
- Mode diagnostic `--audio`, listant les périphériques et éprouvant la bascule sans
  passer par le Core.
- Carte Audio : sélecteur de sortie, volume, mute, désactivés quand le hub ou le PC
  est coupé.

### Décidé
- **P/Invoke plutôt qu'`AudioSwitcher`** : sa dernière version est une alpha d'octobre
  2016 ciblant .NETFramework, inutilisable sous .NET 10.
- **Le curseur de volume est le seul retour optimiste du projet**, et c'est assumé :
  un curseur qui ne suit pas le doigt est inutilisable.

### Corrigé
- Le binder de configuration .NET ajoute aux collections au lieu de les remplacer :
  deux sorties par défaut plus deux venant de l'environnement en auraient produit
  quatre. Le repli est passé dans le seeder.
- `GET /api/state` ne renvoyait aucune sortie tant que l'agent n'avait rien poussé,
  laissant la carte Audio vide après un redémarrage du Core.

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
- **Sélection des capteurs par nom.** Le relevé sur la machine réelle (i7-12700K,
  RTX 5070 Ti) a montré quatre lectures fausses : température CPU et GPU pouvant
  tomber sur un « Distance to TjMax » ou sur la jonction mémoire, VRAM sur un
  compteur D3D, et RAM sur le bloc de mémoire virtuelle qui coexiste avec le bloc
  physique. Chacune produisait un nombre plausible et faux.
- **PawnIO rendu explicite.** LibreHardwareMonitor ne fournit plus de driver depuis
  la 0.9.5 : sans PawnIO, toute lecture MSR renvoie `null` alors qu'`Open()` réussit
  et que les capteurs GPU continuent de fonctionner. `--sensors` affiche désormais
  son état.
- **« Connexion perdue » définitif sur le panneau.** `withAutomaticReconnect()` sans
  argument abandonne après quatre tentatives ; passé 42 secondes de coupure, l'iPad
  ne réessayait plus jamais tout en continuant d'afficher le dernier état connu comme
  s'il était vivant. Politique de reconnexion illimitée, resynchronisation par
  `GET /api/state` à chaque reprise, contrôles désactivés et état marqué « inconnu »
  tant que le hub est coupé.
- **Wake-on-LAN inopérant en conteneur.** Le paquet magique est un broadcast dirigé ;
  depuis un bridge Docker il n'atteint jamais le LAN, et l'émission réussit pourtant
  sans erreur. Capture à l'appui : zéro paquet sur `ens18` en bridge, 102 octets aux
  ports 9 et 7 en `network_mode: host`. Le Core tourne désormais sur le réseau de
  l'hôte.
- **Télémétrie fantôme sur un PC hors ligne.** Une mesure en vol traitée après la
  déconnexion réinjectait des valeurs dans l'état d'un PC déclaré éteint ; le
  snapshot suivant les affichait comme vivantes.
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
