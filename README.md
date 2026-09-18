# RoomOS — Dossier de conception

Pack de conception complet destiné à être donné à un agent de code (Claude Code, Codex, etc.)
pour construire le projet sans avoir à redécider l'architecture à chaque session.

## Comment l'utiliser

1. `git init` un repo vide, copie ce dossier dans `docs/`.
2. Copie `AGENTS.md` à la racine du repo. Pas besoin de le dupliquer en `CLAUDE.md` :
   Claude Code lit `AGENTS.md` directement.
3. Ouvre une session par jalon (M1, puis M2, etc.). Jamais deux jalons dans la même session.
4. Prompt de démarrage type :

   > Lis `docs/` en entier, puis implémente uniquement le jalon M1 décrit dans
   > `docs/10-roadmap.md`. Respecte `docs/11-conventions.md` et `AGENTS.md`.
   > Ne touche à rien qui ne soit pas dans M1. Liste-moi ton plan avant d'écrire du code.

## Contenu

| Fichier | Contenu |
|---|---|
| `AGENTS.md` | Règles pour l'agent de code. À copier à la racine du repo. |
| `docs/00-contexte.md` | Matériel, réseau, contraintes physiques |
| `docs/01-vision.md` | Ce qu'est RoomOS, ce qu'il n'est pas, périmètre V1 |
| `docs/02-stack.md` | Stack finale, versions, alternatives rejetées |
| `docs/03-architecture.md` | Composants, flux, décisions structurantes |
| `docs/04-domaine.md` | Modèle de données, schéma SQLite |
| `docs/05-api.md` | Contrat REST + SignalR + protocole agent |
| `docs/06-agent-windows.md` | Spec de l'agent Windows |
| `docs/07-frontend.md` | Spec de la PWA, contraintes iPad 5 |
| `docs/08-scenes.md` | Moteur de scènes, DSL, scènes V1 |
| `docs/09-integrations.md` | Spotify, Zigbee2MQTT |
| `docs/10-roadmap.md` | Jalons M0 → M6 avec definition of done |
| `docs/11-conventions.md` | Arbo repo, conventions de code, tests, CI |
| `docs/12-decisions.md` | ADR courts : décisions prises et alternatives écartées |
| `docs/13-installation.md` | Installation complète et restauration après perte |

## État

Pack relu intégralement le 2026-09-18, incohérences internes corrigées, chaîne
d'outils installée sur la VM cible. Voir `docs/02-stack.md` pour les versions
effectivement en place. Aucun code écrit : le jalon courant est **M0**.

## Règle d'or

Le périmètre V1 est fermé. Tout ajout se discute après M6, pas pendant.
