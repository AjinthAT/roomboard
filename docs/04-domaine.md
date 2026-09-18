# 04 — Modèle de domaine

## Entités persistées (SQLite, via EF Core)

### `rooms`
| Colonne | Type | Note |
|---|---|---|
| `id` | TEXT PK | slug, ex. `bedroom` |
| `name` | TEXT | ex. « Chambre » |
| `sort_order` | INTEGER | |

> Une seule ligne en V1 (`bedroom`). La table existe pour ne pas avoir à migrer plus tard.
> **Aucune UI multi-pièces en V1.**

### `devices`
| Colonne | Type | Note |
|---|---|---|
| `id` | TEXT PK | slug, ex. `gaming-pc`, `desk-light` |
| `room_id` | TEXT FK | |
| `kind` | TEXT | `pc` \| `light` |
| `name` | TEXT | libellé affiché |
| `enabled` | INTEGER | |
| `config_json` | TEXT | config spécifique au kind (voir ci-dessous) |

`config_json` selon `kind` :
```jsonc
// pc
{ "mac": "AA:BB:CC:DD:EE:FF", "ip": "192.168.1.30", "broadcast": "192.168.1.255", "agentToken": "..." }
// light
{ "z2mFriendlyName": "desk_light", "supportsColor": true, "supportsBrightness": true }
```

> Une sortie audio **n'est pas** un `device`. Elle vit uniquement dans `audio_outputs`
> ci-dessous. Une version antérieure de ce document la décrivait aux deux endroits,
> avec les mêmes champs : deux sources de vérité pour la même donnée.

### `audio_outputs` (source de vérité unique des sorties)
| Colonne | Type | Note |
|---|---|---|
| `id` | TEXT PK | `jbl` \| `headset` |
| `pc_device_id` | TEXT FK | |
| `windows_device_id` | TEXT | identifiant stable Windows |
| `match_hint` | TEXT | fragment de nom, filet de sécurité si l'ID change |
| `friendly_name` | TEXT | « JBL », « Casque » |

> **Important** : ne jamais identifier une sortie par son nom Windows seul. L'ID est
> la clé, le `match_hint` est le repli, le `friendly_name` est ce que voit l'utilisateur.

### `scenes`
| Colonne | Type | Note |
|---|---|---|
| `id` | TEXT PK | `gaming`, `work`, `chill`, `night` |
| `room_id` | TEXT FK | |
| `name` | TEXT | |
| `icon` | TEXT | nom d'icône lucide |
| `steps_json` | TEXT | DSL décrit dans `08-scenes.md` |
| `sort_order` | INTEGER | |

### `integration_tokens`
| Colonne | Type | Note |
|---|---|---|
| `provider` | TEXT PK | `spotify` |
| `access_token` | TEXT | |
| `refresh_token` | TEXT | |
| `expires_at` | TEXT | ISO 8601 |

### `settings`
Clé/valeur, pour les réglages divers modifiables à chaud.

> **Pas de secret ici.** `ApiToken` et `AgentToken` viennent des variables
> d'environnement `ROOMOS__*` (`11-conventions.md` : « aucun secret dans le repo »,
> `05-api.md` : le token est un secret statique d'environnement). Les stocker aussi
> en base créerait une seconde source de vérité et un secret persisté sur disque.

## État runtime (mémoire uniquement)

```csharp
record PcState(bool Online, TimeSpan? Uptime, Telemetry? Telemetry, DateTimeOffset UpdatedAt);
record Telemetry(
    double CpuUsage, double? CpuTempC,
    double GpuUsage, double? GpuTempC, double? VramUsedMb, double? VramTotalMb,
    double RamUsedMb, double RamTotalMb);
record AudioState(string? ActiveOutputId, int Volume, bool Muted, IReadOnlyList<string> AvailableOutputIds);
record LightState(bool On, int? Brightness, string? ColorHex, bool Reachable);
record NowPlaying(string? Title, string? Artist, string? AlbumArtUrl, bool IsPlaying, int? ProgressMs, int? DurationMs, string? DeviceName);
```

> `ActiveOutputId` est **nullable** et c'est un cas nominal, pas une erreur : l'agent
> pousse un `windowsDeviceId`, que le Core mappe vers une ligne de `audio_outputs`.
> Si la sortie active est un périphérique qu'on ne connaît pas — écran HDMI, casque
> Bluetooth branché à la volée, sortie par défaut après une mise à jour de pilote —
> le mapping ne donne rien. L'UI affiche alors « sortie inconnue » et aucun bouton
> n'apparaît sélectionné, plutôt que d'en désigner un au hasard.

`StateStore` = un dictionnaire concurrent `deviceId -> state` + un `NowPlaying`.
Toute mutation émet un événement consommé par le hub client.
