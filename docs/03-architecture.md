# 03 — Architecture

## Vue d'ensemble

```
                        iPad 5 / navigateur
                     ┌────────────────────────┐
                     │      RoomOS Web        │
                     │  React + TS (PWA)      │
                     └───────────┬────────────┘
                                 │ HTTP + SignalR (LAN)
                                 ▼
             VM Debian (Proxmox)  ── docker compose ──
             ┌──────────────────────────────────────┐
             │            RoomOS.Core               │
             │        ASP.NET Core .NET 10          │
             │                                      │
             │  DeviceRegistry   SceneEngine        │
             │  StateStore       Integrations       │
             │  AgentHub(SignalR) ClientHub(SignalR)│
             └──────┬──────────────────┬────────────┘
                    │                  │
         SignalR (sortant)           MQTT
                    │                  │
                    ▼                  ▼
          ┌──────────────────┐   ┌──────────────┐   ┌─────────────┐
          │ RoomOS.Agent     │   │  Mosquitto   │◄──│ Zigbee2MQTT │──► Lampes
          │ Windows 11       │   └──────────────┘   └─────────────┘
          └──────────────────┘
                    │
              WoL (UDP 9) ◄── envoyé par le Core, pas par l'agent

          Spotify Web API ◄── appelé par le Core uniquement
```

## Décisions structurantes

### 1. L'agent se connecte au Core, pas l'inverse
L'agent Windows ouvre une connexion SignalR **sortante** vers le Core et la maintient.
Conséquences : aucune règle de pare-feu entrante sur le PC, pas de découverte réseau,
et la présence de la connexion = le PC est online. C'est le signal d'état le plus fiable
qui existe, plus fiable qu'un ping.

Exception : le Wake-on-LAN est émis par le Core (le PC est éteint, il n'y a pas d'agent).

### 2. L'état vit en mémoire, la configuration vit en base
- **En base (SQLite)** : pièces, appareils, sorties audio, scènes, jetons d'intégration.
  Ce qui change rarement et doit survivre à un redémarrage.
- **En mémoire (`StateStore`)** : dernier état connu de chaque appareil, télémétrie.
  Ce qui change toutes les secondes et n'a aucune valeur historique.

La télémétrie n'est **jamais** écrite en base. Si un historique est voulu un jour,
c'est Prometheus qui scrape `/metrics` (M6).

### 3. Le front ne parle qu'au Core
Le front n'appelle jamais Spotify, ni MQTT, ni l'agent directement. Aucun secret
ne descend dans le navigateur. Le Core est le seul point d'intégration.

### 4. Un seul flux d'état vers le client
Au chargement : `GET /api/state` renvoie le snapshot complet.
Ensuite : SignalR pousse uniquement les deltas. Pas de polling côté client.

### 5. Les commandes sont idempotentes et bornées
Toute commande (`shutdown`, `setOutput`, `wake`) a un timeout explicite et un résultat
observable. Le Core ne suppose jamais qu'une commande a réussi : il attend le changement
d'état correspondant, ou il échoue.

## Projets / composants

| Composant | Type | Responsabilité |
|---|---|---|
| `RoomOS.Core` | ASP.NET Core | API, hubs, moteur de scènes, intégrations |
| `RoomOS.Domain` | Class library | Entités, DTOs, contrats. Aucune dépendance. |
| `RoomOS.Agent.Windows` | Worker Service | Télémétrie, audio, power |
| `roomos-web` | Vite/React | UI |

`RoomOS.Domain` est référencé par le Core **et** par l'agent : les DTOs du protocole
agent y sont définis une seule fois.
