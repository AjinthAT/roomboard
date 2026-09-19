# Changelog

Toutes les évolutions notables de RoomOS. Une entrée par jalon de `docs/10-roadmap.md`.

Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/).
Le projet ne suit pas SemVer : il suit ses jalons.

## M4 complété — appairage réel — 2026-09-19

### Vérifié sur le matériel
- SLZB-06 en Ethernet, cœur mis à jour en v2.9.8, adresse statique, canal 15.
- Philips Hue *white ambiance and color* appairée et pilotée depuis RoomOS.
- **Aller-retour complet de 55 à 110 ms**, de l'appel HTTP au retour d'état, en
  passant par MQTT, Zigbee2MQTT et la radio. La DoD demandait moins d'une seconde.

### Corrigé
- **`coordinator_backup.json` n'était pas ignoré par git.** Il contient la clé du
  réseau Zigbee : sur un dépôt public, le publier aurait donné l'accès radio à
  quiconque se trouve à portée.

### Appris
- Zigbee2MQTT réécrit sa configuration au démarrage et supprime les commentaires.
- Le SLZB-06 ne répond pas au ping : le mDNS est le seul moyen fiable de le trouver.

## M6 — Durcissement — 2026-09-19

### Ajouté
- **`/metrics` au format Prometheus**, écrit à la main. Aucune bibliothèque : le
  format est une ligne par échantillon et nos métriques sont une poignée de jauges
  déjà en mémoire. Les températures restent nullables — un capteur absent n'émet pas
  d'échantillon plutôt qu'un zéro que Grafana tracerait comme une mesure.
- **Prometheus et Grafana** dans le `docker compose`, avec source de données et
  tableau de bord provisionnés. C'est Prometheus qui garde l'historique, pas RoomOS
  (ADR D6).
- **Manifest PWA, icônes et service worker.** Le service worker ne met en cache que
  la coquille, jamais l'API ni les hubs, et ne s'enregistre qu'en contexte sécurisé.
- **Déploiement automatique** sur runner auto-hébergé, conditionné à une CI verte.
- **`docs/13-installation.md`** : installation complète et restauration, éprouvées.

### Décidé
- **Pas de `/metrics` sur l'agent**, contrairement à la roadmap. Il ne connaît rien
  que le Core n'ait déjà.

### Corrigé
- `index.html` référençait `/icon-180.png` depuis M0 et le fichier n'a jamais existé :
  l'icône d'écran d'accueil de l'iPad était un 404. Icônes générées par un encodeur
  PNG écrit à la main, la VM n'ayant aucune bibliothèque d'image.
- **La CI n'avait jamais tourné, et elle était cassée.** `dotnet restore` recevait
  deux projets alors que MSBuild n'en accepte qu'un : le job `core` échouait depuis
  M0. Invisible en local, où la solution entière est toujours construite d'un bloc.
- Procédure de sauvegarde corrigée avant publication : SQLite tourne en mode WAL,
  copier les fichiers sans arrêter le Core peut capturer un instantané incohérent —
  ce qui ne se découvre que le jour où la sauvegarde sert.

## Après M5 — Spotify enrichi — 2026-09-19

Quatre ajouts qui exploitent ce que M3 récupérait déjà sans l'afficher.

### Ajouté
- **Barre de progression du morceau.** Le serveur ne pousse la position qu'au
  changement de morceau et toutes les 15 s : sonder Spotify toutes les 3 s et tout
  rediffuser ferait vingt messages par minute pour une valeur qui avance de façon
  parfaitement prévisible. La barre progresse localement et se recale à chaque
  message reçu.
- **Volume Spotify**, distinct du volume Windows de la carte Audio.
- **Sélecteur d'appareil de lecture** : PC, téléphone, enceinte. La liste n'est
  rechargée que lorsque l'appareil actif change.
- **Playlists mises en avant**, déclarées dans `deploy/.env`. Il n'y a pas d'éditeur
  en V1, et coder des URI en dur dans le front serait pire.

### Vérifié
API et build. **Pas l'affichage** : Spotify était fermé au moment du test, donc aucun
appareil actif et aucune position à rendre.

## M5 — Scènes — 2026-09-18

### Ajouté
- Moteur de scènes déclaratif : séquentiel, timeout par étape, une étape en échec
  n'interrompt pas la scène sauf un `pc.wake` bloquant, une seule exécution à la fois
  et une nouvelle demande annule la précédente.
- `IStepExecutor`, seule abstraction de ce type que `08-scenes.md` autorise, pour que
  le moteur soit testable sans PC, sans ampoule et sans Spotify.
- Les quatre scènes, **générées depuis la configuration** plutôt qu'écrites en dur.
  Une étape visant un appareil non déclaré est omise, pas écrite puis vouée à l'échec.
- `music.transfer` ajouté au DSL (ADR D11).
- Barre de scènes avec progression réelle par étape, pilotée par les événements du
  hub et non par un minuteur.
- 7 tests du moteur et 8 du parsing du DSL, couvrant la liste imposée par `08-scenes.md`.

### Ajouté après coup
- **Confirmation en deux temps** sur redémarrer, éteindre et toute scène qui éteint
  un PC. Le caractère sensible est calculé par le serveur à partir des étapes, jamais
  d'une liste de noms.

### Corrigé à l'audit
- **Les exécutions n'étaient jamais purgées.** Chacune restait en mémoire à vie, sur
  un panneau utilisé quotidiennement. Les vingt dernières sont conservées.
- **Course à la libération du jeton d'annulation** : `Dispose()` précédait le verrou
  qui libère l'exécution courante, si bien qu'une scène lancée dans cet intervalle
  appelait `Cancel()` sur un objet libéré — `ObjectDisposedException`, donc 500,
  précisément en enchaînant deux scènes.
- Une exécution purgée pendant qu'elle tournait faisait lever `KeyNotFoundException`
  à la mise à jour de son état.

## M4 — Lumières — 2026-09-18

Codé et vérifié contre un Zigbee2MQTT simulé, en attendant le matériel.

### Ajouté
- Mosquitto dans le `docker compose`, publié sur la boucle locale uniquement.
- Zigbee2MQTT préconfiguré, derrière un profil compose tant qu'aucun coordinateur
  n'est branché.
- Pont MQTT : abonnement à `zigbee2mqtt/+` et `zigbee2mqtt/+/availability`, commandes
  publiées sur `zigbee2mqtt/<nom>/set`, reconnexion automatique à Mosquitto.
- `GET /api/lights` et `PUT /api/lights/{id}`, avec validation de la couleur et de la
  luminosité — Zigbee2MQTT ignore silencieusement une charge utile malformée.
- Conversions d'unités entre Zigbee et RoomOS, couvertes par des tests : luminosité
  0–254 contre 0–100, et couleur CIE xy vers hexadécimal.
- Carte Lumières : bascule, luminosité, pastille de couleur, état injoignable.

### Ajouté après coup
- **Confirmation en deux temps** sur redémarrer, éteindre, et toute scène qui éteint
  un PC. Demandé à l'usage : sur un panneau mural, un doigt qui glisse ne doit pas
  éteindre la machine.
- **Distinction entre « pas encore appairée » et « injoignable ».** Une lampe déclarée
  mais jamais vue par Zigbee2MQTT s'affichait comme injoignable, ce qui laissait
  croire à une panne alors que l'installation n'était pas finie.

### Décidé
- `Z2mFriendlyName` peut désigner une ampoule **ou un groupe** Zigbee2MQTT : plusieurs
  ampoules peuvent former une seule lumière sans toucher au modèle de données.
- Une lampe injoignable conserve son dernier état connu plutôt que d'être affichée
  éteinte : coupée au mur n'est pas éteinte.

## M3 — Spotify — 2026-09-18

### Ajouté
- `IMusicProvider` et son unique implémentation, seule abstraction « provider » du
  projet (ADR D7).
- Flux Authorization Code + PKCE, sans secret client. Le `refresh_token` est persisté
  et ne quitte jamais le Core.
- Sondage adaptatif, 3 s en lecture et 15 s sinon : Spotify n'expose aucun webhook,
  et un dashboard permanent qui interrogerait une API tierce toutes les 3 secondes
  jour et nuit finirait limité.
- Les réponses 204 et 404 des endpoints Player sont traduites en « aucun appareil
  actif » plutôt qu'en erreur : c'est l'état normal quand Spotify est fermé.
- Carte Musique : pochette en dimension fixe et chargement paresseux, titre, artiste,
  appareil, contrôles.

### Corrigé à l'audit
- **Un refus de Spotify s'affichait comme « aucun appareil actif ».** Un 403 — compte
  absent de la liste d'utilisateurs autorisés, le piège le plus fréquent du mode
  développement — était indiscernable d'une simple absence de lecture. Nouvel état
  `Denied`, avec l'explication à l'écran.
- **Les échecs du sondage étaient journalisés en `Debug`**, donc invisibles au niveau
  par défaut : un `catch` silencieux déguisé, que `11-conventions.md` interdit. Passés
  en avertissement, une seule fois à la bascule plutôt qu'à chaque tentative.
- **Les vérificateurs PKCE n'expiraient jamais.** Une autorisation abandonnée laissait
  une entrée à vie, sur une route non authentifiée. Durée de vie de 10 minutes et purge.
- Les erreurs inattendues de `GET /me/player` lèvent désormais, et l'état connu est
  conservé plutôt que remplacé par une contre-vérité.

### Connu
- Les endpoints Player s'appliquent à l'appareil actif du compte, qui n'est pas
  forcément le PC. Sans effet ici, bloquant pour la scène Gaming de M5.

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
- **La détection de changement d'état audio ne fonctionnait pas.** L'opérateur `==`
  d'un record compare ses membres avec le comparateur par défaut, donc
  `IReadOnlyList<T>` par *référence* ; comme chaque lecture construit une nouvelle
  liste, le test était toujours faux. L'agent publiait donc toutes les 2 s au lieu de
  publier au changement plus un filet à 10 s. Comparaison explicite des deux côtés,
  et le Core ne rediffuse plus un état identique.
- Clé étrangère manquante sur `audio_outputs.pc_device_id`, pourtant décrite comme
  telle dans `04-domaine.md`.
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
