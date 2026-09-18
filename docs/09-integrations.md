# 09 — Intégrations

## Spotify

### Contraintes plateforme (état septembre 2026)

> **Web API uniquement** à la création de l'application. Pas de Web Playback SDK :
> il sert à faire du navigateur un lecteur, alors que RoomOS pilote le client Spotify
> déjà installé sur le PC.

Spotify a durci l'accès développeur en février 2026. À connaître **avant** de coder :

- Le Development Mode exige un **compte Spotify Premium**.
- **Un seul Client ID par développeur.** Si tu veux un jour du Spotify dans un autre
  projet, tu partages ce Client ID ou tu demandes un quota étendu.
- **5 utilisateurs autorisés maximum** par application, à ajouter manuellement dans
  le dashboard (Settings → User Management). Hors de cette liste, l'API renvoie 403.
- De nombreux endpoints catalogue ont été supprimés, et `GET /search` est plafonné
  à `limit=10`.
- Le champ `product` a disparu de `GET /me` : impossible de vérifier le statut Premium
  par l'API.

**Bonne nouvelle** : tous les endpoints Player sont conservés. `GET /me/player`,
`/me/player/currently-playing`, `/me/player/devices`, `PUT /me/player/play`, `/pause`,
`POST /me/player/next`, `/previous`, `PUT /me/player/volume`, `/me/player` (transfer).
La V1 musique est donc réalisable telle que spécifiée.

### Conséquence d'architecture

Spotify est la dépendance la plus fragile du projet. C'est **la seule raison** pour
laquelle `IMusicProvider` existe :

```csharp
public interface IMusicProvider {
    Task<NowPlaying?> GetNowPlayingAsync(CancellationToken ct);
    Task PlayAsync(string? uri, CancellationToken ct);
    Task PauseAsync(CancellationToken ct);
    Task NextAsync(CancellationToken ct);
    Task PreviousAsync(CancellationToken ct);
    Task SetVolumeAsync(int level, CancellationToken ct);
}
```

Une seule implémentation en V1 : `SpotifyMusicProvider`. N'en crée pas d'autre.
L'interface est une assurance contre un changement de politique Spotify, pas une
invitation à ajouter Plex.

### Implémentation

- Flux **Authorization Code + PKCE**, une fois, depuis un navigateur.
- **Redirect URI : `http://127.0.0.1:8080/api/music/callback`**, et rien d'autre.
  Spotify impose HTTPS pour toute adresse non-loopback depuis 2025. Une route directe
  du Core en `http://192.168.1.30:8080/...` est **refusée**, et `localhost` l'est aussi :
  seule l'IP de bouclage littérale est acceptée en HTTP.
- Conséquence pratique : le Core n'ayant pas de navigateur, l'autorisation se fait
  depuis le PC à travers un tunnel SSH — `ssh -L 8080:127.0.0.1:8080 ajin@192.168.1.30`
  — de sorte que `127.0.0.1:8080` dans le navigateur atteigne bien le Core. Une seule
  fois, à la mise en service.
- Scopes : `user-read-playback-state`, `user-modify-playback-state`,
  `user-read-currently-playing`.
- `refresh_token` stocké dans `integration_tokens`. Refresh automatique côté Core,
  jamais côté front.
- Polling de `GET /me/player` toutes les 3 s **uniquement quand une lecture est active**,
  toutes les 15 s sinon. Il n'existe pas de webhook.
- Les commandes Player exigent un **appareil actif**. S'il n'y en a pas, `PUT /me/player/play`
  renvoie 404. Gérer ce cas explicitement dans l'UI (« aucun appareil Spotify actif »)
  au lieu de laisser une erreur silencieuse.

## Zigbee2MQTT

### Achat

**Lampes Zigbee, pas Matter/Thread.** Il n'existe pas de contrôleur Matter viable en .NET ;
partir sur du Matter obligerait à mettre Home Assistant dans la boucle, ce qui est
explicitement écarté (`12-decisions.md`).

Prévoir un coordinateur en plus des ampoules : Sonoff Zigbee 3.0 Dongle Plus-E (USB)
ou SLZB-06 (Ethernet, plus pratique si la VM est sur un hôte sans USB dédié).

### Intégration

- Mosquitto et Zigbee2MQTT tournent dans le même `docker compose` que le Core.
- Le Core s'abonne à `zigbee2mqtt/+` et publie sur `zigbee2mqtt/<friendly_name>/set`.
- L'appairage se fait par l'UI de Zigbee2MQTT, pas par RoomOS. RoomOS consomme, il
  n'administre pas le réseau Zigbee.
- Le mapping `deviceId RoomOS` ↔ `friendly_name Z2M` est dans `devices.config_json`.
- Payload de commande typique :
  `{"state":"ON","brightness":153,"color":{"hex":"#FF6A00"}}`
- `availability` de Z2M alimente le champ `reachable`.
