# 05 — Contrat d'API

## Authentification

En-tête `Authorization: Bearer <API_TOKEN>` sur **toutes** les routes `/api/*`.

Pour SignalR, le client JavaScript **ne peut pas** poser d'en-tête sur la connexion
WebSocket : le client passe par `accessTokenFactory`, qui envoie l'en-tête sur la
négociation HTTP puis bascule le jeton en query string `?access_token=…` sur le
WebSocket. Le Core doit donc accepter les deux formes sur `/hub/*` — en-tête **et**
query string — sinon la négociation passe et le WebSocket est rejeté juste après.

Le token est un secret statique (variable d'environnement `ROOMOS__ApiToken`),
stocké côté front dans `localStorage` après une saisie unique.
C'est volontairement minimal, mais présent dès M1 : un endpoint qui éteint un PC
ne reste pas ouvert « parce qu'on est en LAN ».

L'agent utilise un token distinct (`ROOMOS__AgentToken`).

## REST

| Méthode | Route | Corps / Réponse |
|---|---|---|
| GET | `/api/state` | Snapshot complet (voir plus bas) |
| POST | `/api/pc/{id}/wake` | 202 **immédiat**, surveillance côté Core (voir ci-dessous) |
| POST | `/api/pc/{id}/shutdown` | 202 |
| POST | `/api/pc/{id}/restart` | 202 |
| PUT | `/api/audio/{pcId}/output` | `{ "outputId": "headset" }` |
| PUT | `/api/audio/{pcId}/volume` | `{ "level": 60 }` (0–100) |
| PUT | `/api/audio/{pcId}/mute` | `{ "muted": true }` |
| GET | `/api/lights` | Liste + états |
| PUT | `/api/lights/{id}` | `{ "on": true, "brightness": 40, "colorHex": "#FF6A00" }` |
| GET | `/api/music/now-playing` | `NowPlaying` |
| POST | `/api/music/play` | `{ "uri": "spotify:playlist:..." }` (uri optionnel) |
| POST | `/api/music/pause` | |
| POST | `/api/music/next` | |
| POST | `/api/music/previous` | |
| PUT | `/api/music/volume` | `{ "level": 60 }` |
| GET | `/api/scenes` | Liste |
| POST | `/api/scenes/{id}/run` | 202 + `runId` |
| GET | `/api/scenes/runs/{runId}` | État d'exécution + log des étapes |
| GET | `/healthz` | 200 si le Core est vivant |
| GET | `/metrics` | Format Prometheus (M6) |

### `POST /api/pc/{id}/wake`

Le Core émet le paquet magique et répond **202 tout de suite**. Il ne tient pas la
requête HTTP ouverte : il surveille en tâche de fond l'arrivée de la connexion agent,
pendant `timeoutSec` (défaut 90).

Le client apprend le résultat par `PcStateChanged` sur le hub, jamais par la réponse
HTTP. C'est ce qu'impose `07-frontend.md` (« les actions longues affichent leur
progression via les événements, pas via un timer ») et ce que fait déjà le moteur de
scènes pour `pc.wake`.

> Une version antérieure de ce document disait « 202, attend l'agent jusqu'à 90 s ».
> Un 202 qui bloque 90 secondes n'est pas un 202, et une requête HTTP maintenue
> aussi longtemps depuis Safari sur un iPad est un bon moyen de récolter un timeout
> côté client.

### `GET /api/state`

```jsonc
{
  "room": { "id": "bedroom", "name": "Chambre" },
  "pcs": [{
    "id": "gaming-pc", "name": "PC",
    "online": true, "uptimeSec": 4021,
    "telemetry": { "cpuUsage": 18.2, "cpuTempC": 47, "gpuUsage": 42, "gpuTempC": 53,
                   "vramUsedMb": 3100, "vramTotalMb": 8192, "ramUsedMb": 9800, "ramTotalMb": 32768 }
  }],
  "audio": { "gaming-pc": {
    "activeOutputId": "headset", "volume": 64, "muted": false,
    "outputs": [{ "id": "jbl", "name": "JBL", "connected": true },
                { "id": "headset", "name": "Casque", "connected": true }]
  }},
  // activeOutputId peut valoir null : sortie active inconnue du registre.
  // Voir 04-domaine.md. Les types TypeScript doivent le refléter.
  "lights": [{ "id": "desk-light", "name": "Bureau", "on": true, "brightness": 60,
               "colorHex": "#FFB070", "reachable": true }],
  "music": { "title": "...", "artist": "...", "albumArtUrl": "...", "isPlaying": true,
             "progressMs": 42000, "durationMs": 201000, "deviceName": "GAMING-PC" },
  "scenes": [{ "id": "gaming", "name": "Gaming", "icon": "gamepad-2" }],
  "serverTime": "2026-09-17T18:00:00Z"
}
```

## SignalR — hub client `/hub/room`

Le serveur pousse (le client n'appelle aucune méthode, il utilise REST pour agir) :

| Événement | Charge utile |
|---|---|
| `PcStateChanged` | `{ id, online, uptimeSec }` |
| `TelemetryUpdated` | `{ id, telemetry }` — plafond **1 Hz** (garde-fou, voir note) |
| `AudioStateChanged` | `{ pcId, activeOutputId, volume, muted, outputs }` |
| `LightStateChanged` | `{ id, on, brightness, colorHex, reachable }` |
| `NowPlayingChanged` | `NowPlaying` |
| `SceneStarted` | `{ runId, sceneId }` |
| `SceneStepCompleted` | `{ runId, stepIndex, status, message }` |
| `SceneFinished` | `{ runId, status }` |

> **Sur le plafond 1 Hz** : l'agent publie toutes les 2 s (`06-agent-windows.md`),
> soit 0,5 Hz. Le plafond serveur n'est donc jamais atteint en fonctionnement normal.
> Il est là comme garde-fou si `Telemetry:IntervalMs` est baissé côté agent, pas
> comme mécanisme actif. Le front re-throttle de son côté (`07-frontend.md`) parce
> qu'il ne contrôle pas ce que le serveur lui envoie. Trois limiteurs, un seul
> actif : c'est voulu, chacun protège une couche différente.

## SignalR — hub agent `/hub/agent`

L'agent appelle :
| Méthode | Charge utile |
|---|---|
| `Register` | `{ pcId, agentVersion, outputs: [{ windowsDeviceId, name }] }` |
| `PushTelemetry` | `Telemetry` — toutes les 2 s |
| `PushAudioState` | `{ activeWindowsDeviceId, volume, muted, outputs }` |
| `Ack` | `{ commandId, status, message }` |

Le Core appelle sur l'agent :
| Méthode | Charge utile |
|---|---|
| `Shutdown` | `{ commandId }` |
| `Restart` | `{ commandId }` |
| `SetAudioOutput` | `{ commandId, windowsDeviceId }` |
| `SetVolume` | `{ commandId, level }` |
| `SetMute` | `{ commandId, muted }` |

**Règle** : la déconnexion du hub agent = PC offline. Pas de ping, pas de heuristique.
