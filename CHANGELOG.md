# Changelog

Toutes les évolutions notables de RoomOS. Une entrée par jalon de `docs/10-roadmap.md`.

Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/).
Le projet ne suit pas SemVer : il suit ses jalons.

## [Non publié] — M1, PC

En cours.

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
