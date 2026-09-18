# 02 — Stack

## Tableau final

| Couche | Choix | Version cible | Pourquoi ce choix |
|---|---|---|---|
| Langage serveur | C# / .NET | **.NET 10 (LTS)** | Imposé par l'agent Windows ; un seul langage, un seul outillage. LTS jusqu'à nov. 2028. |
| Framework API | ASP.NET Core Minimal API | .NET 10 | Suffisant, peu de cérémonie |
| Temps réel | SignalR | .NET 10 | WebSocket + reconnexion + fallback gérés ; sert aussi de canal agent |
| Base de données | SQLite | 3.x | Fichier unique, zéro administration |
| Accès données | EF Core + provider SQLite | 10.x | Migrations incluses |
| Agent Windows | .NET 10, Worker Service | .NET 10 | Seul moyen propre d'accéder aux capteurs et à CoreAudio |
| Capteurs matériels | LibreHardwareMonitorLib | dernière | Températures CPU/GPU ; nécessite privilèges admin |
| Audio Windows | NAudio (CoreAudio / MMDevice) | dernière | Énumération et bascule des périphériques de sortie |
| Front | React + TypeScript | React 19 / TS 5.x | Écosystème connu, client remplaçable |
| Build front | Vite | 7.x | Rapide, build ES2020 pour Safari 16 |
| CSS | Tailwind | 4.x | Pas de design system à maintenir |
| Format iPad | PWA ajoutée à l'écran d'accueil | — | Zéro distribution, zéro signature, zéro expiration |
| Bus IoT | MQTT (Mosquitto) | 2.x | Découplage avec Zigbee |
| Passerelle Zigbee | Zigbee2MQTT | 2.x | Le seul chemin viable pour un core maison |
| Musique | Spotify Web API | — | Endpoints Player toujours disponibles (voir `09-integrations.md`) |
| Réveil PC | Wake-on-LAN | — | Paquet magique en broadcast LAN |
| Conteneurisation | Docker + Docker Compose | — | Core, Mosquitto, Zigbee2MQTT |
| Hôte | VM Debian sur Proxmox | Debian 13 | Existant |
| Métriques (M6) | Prometheus + Grafana | — | La télémétrie ne va pas en base |
| CI/CD | GitHub Actions + runner self-hosted | — | Build des 3 artefacts, déploiement sur la VM |
| Accès distant (M6) | Tailscale | — | Donne aussi le certificat HTTPS gratuitement |

## Versions à vérifier au démarrage du projet

Le pack a été figé en septembre 2026.

Vérifié et installé le 2026-09-18 sur la VM cible (`vm-102`, Debian 13.7) :

| Outil | Version installée | Note |
|---|---|---|
| .NET SDK | **10.0.401** | dans `~/.dotnet`, PATH via `~/.bashrc`. Exige `libicu76`. |
| Node | **24.21.0 LTS** | dépôt NodeSource. Le Node 20 de Debian 13 est EOL upstream depuis avril 2026. |
| npm | **11.19.0** | |
| Docker / Compose | **29.8.1 / 5.5.1** | service actif et activé au boot |
| git | **2.47.3** | |

**Rester sur .NET 10 même si 11 est sorti** (.NET 11 est une release STS, 2 ans de support).

Reste à vérifier plus tard, au moment où ça sert :
- Dernières versions de LibreHardwareMonitorLib et NAudio sur NuGet (M1 / M2).
- Zigbee2MQTT : compatibilité du coordinateur acheté (M4).

## Ce qui n'est PAS dans la stack, et pourquoi

Voir `12-decisions.md`. Résumé : pas de Node côté serveur, pas de Home Assistant,
pas de Swift, pas de Matter, pas de Postgres/Redis/Kafka/Kubernetes.
