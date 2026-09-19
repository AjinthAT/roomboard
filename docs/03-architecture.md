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

### 1 bis. Le Core tourne sur le réseau de l'hôte

`network_mode: host` dans `deploy/docker-compose.yml`, et ce n'est pas un détail
d'exploitation : c'est une conséquence directe du Wake-on-LAN.

Le paquet magique est un broadcast UDP dirigé vers `192.168.1.255`. Depuis un bridge
Docker, le conteneur est en `172.x` : cette adresse n'est pas sur son lien local, le
paquet part vers la passerelle, et Linux ne relaie pas les broadcasts dirigés. Il
n'atteint jamais le LAN — vérifié à la capture, zéro paquet sur `ens18` en mode
bridge, contre 102 octets aux ports 9 et 7 en mode hôte.

**Conséquence pour M4** : Mosquitto et Zigbee2MQTT devront soit être eux aussi en
réseau hôte, soit être joints par `127.0.0.1` et non par leur nom de service Docker.
Un conteneur en réseau hôte ne résout pas les noms du réseau bridge.

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

> **Appliqué aux scènes depuis le 2026-09-19.** Les étapes lampe et audio attendent
> l'effet dans le `StateStore` avant de se déclarer réussies. Auparavant elles ne
> vérifiaient que l'envoi, si bien qu'une scène annonçait « appliquée » avec un agent
> déconnecté ou une ampoule hors de portée.
>
> **Une exception, assumée** : `pc.shutdown` n'attend pas. Une extinction Windows
> prend dix à trente secondes, bien au-delà du délai par défaut d'une étape ; attendre
> ferait échouer systématiquement une commande qui réussit. Le résultat observable
> arrive par la déconnexion du hub agent, que l'UI voit.

## Projets / composants

| Composant | Type | Responsabilité |
|---|---|---|
| `RoomOS.Core` | ASP.NET Core | API, hubs, moteur de scènes, intégrations |
| `RoomOS.Domain` | Class library | Entités, DTOs, contrats. Aucune dépendance. |
| `RoomOS.Agent.Windows` | Worker Service | Télémétrie, audio, power |
| `roomos-web` | Vite/React | UI |

`RoomOS.Domain` est référencé par le Core **et** par l'agent : les DTOs du protocole
agent y sont définis une seule fois.
