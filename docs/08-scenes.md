# 08 — Moteur de scènes

C'est le seul composant du projet qui mérite vraiment des tests unitaires.
Une scène est une séquence d'étapes déclaratives, stockée en JSON, exécutée par
un interpréteur.

## DSL

```jsonc
{
  "steps": [
    { "type": "pc.wake",         "deviceId": "gaming-pc", "waitForOnline": true, "timeoutSec": 90 },
    { "type": "audio.setOutput", "deviceId": "gaming-pc", "outputId": "headset" },
    { "type": "audio.setVolume", "deviceId": "gaming-pc", "level": 60 },
    { "type": "light.set",       "deviceId": "desk-light", "on": true, "brightness": 30, "colorHex": "#FF4400" },
    { "type": "light.set",       "deviceId": "ambient-light", "on": false },
    { "type": "music.play",      "uri": "spotify:playlist:xxxx" },
    { "type": "delay",           "ms": 1500 }
  ]
}
```

## Types d'étapes V1

| Type | Paramètres |
|---|---|
| `pc.wake` | `deviceId`, `waitForOnline`, `timeoutSec` |
| `pc.shutdown` | `deviceId` |
| `audio.setOutput` | `deviceId`, `outputId` |
| `audio.setVolume` | `deviceId`, `level` |
| `audio.setMute` | `deviceId`, `muted` |
| `light.set` | `deviceId`, `on`, `brightness?`, `colorHex?` |
| `music.transfer` | — |
| `music.play` / `music.pause` | `uri?` |
| `music.setVolume` | `level` |
| `delay` | `ms` |

Pas d'autre type en V1. Pas de condition, pas de boucle, pas de branche.

> **`music.transfer` a été ajouté en M5** (ADR D11). Les endpoints Player de Spotify
> s'appliquent à l'appareil actif du compte, qui peut être un téléphone. Sans cette
> étape, « Gaming » lancerait la musique ailleurs que sur le PC.
>
> Étape explicite plutôt que transfert implicite dans `music.play` : un DSL déclaratif
> doit dire ce qu'il fait, et une magie cachée serait invisible à la lecture d'une scène.

## Règles d'exécution

1. **Séquentiel**, dans l'ordre déclaré.
2. Chaque étape a un **timeout** (défaut 10 s, 90 s pour `pc.wake`).
3. Une étape qui échoue **n'interrompt pas la scène** par défaut : elle est marquée
   `failed`, on continue. Sauf `pc.wake` avec `waitForOnline: true`, qui est bloquante —
   inutile de régler le volume d'un PC éteint.
4. Chaque étape émet `SceneStepCompleted` sur le hub client.
5. Une seule exécution de scène à la fois par pièce. Une nouvelle demande annule la précédente.
6. **Idempotence** : relancer « Gaming » alors que tout est déjà en place ne doit rien casser.

## Tests exigés

- Une scène vide se termine en `completed`.
- Une étape en échec n'empêche pas les suivantes.
- `pc.wake` en timeout marque la scène `failed` et saute les étapes suivantes.
- L'annulation d'une scène en cours interrompt bien l'exécution.
- Chaque type d'étape est exécuté via un exécuteur mocké, vérifié une fois.

Les exécuteurs sont derrière une interface `IStepExecutor` pour être mockables.
C'est la **seule** abstraction de ce type autorisée dans le projet.

## Ordre des étapes : l'extinction vient en dernier

Une scène est séquentielle, donc l'ordre est sémantique. Toute étape qui dépend du
PC doit précéder son extinction.

C'est le piège de la scène **Night** : `pc.shutdown` en première position rend
`music.pause` impossible. L'appareil Spotify disparaît avec le PC, et `PUT /me/player`
renvoie 404 sur un appareil absent (`09-integrations.md`). L'étape serait marquée
`failed` sans que rien ne soit cassé — la musique s'arrête de toute façon quand le PC
s'éteint — mais la scène rapporterait un échec à chaque exécution, et une scène qui
échoue toujours finit par ne plus être lue.

## Scènes V1

| Scène | Comportement |
|---|---|
| **Work** | PC on, sortie JBL, volume 40, bureau 100 % blanc froid, ambiance off |
| **Chill** | Bureau 30 % chaud, ambiance 60 %, JBL, volume 35, playlist chill |
| **Gaming** | PC wake, casque, volume 60, bureau 20 % rouge, ambiance off |
| **Night** | Musique pause, toutes lampes off, **puis** PC shutdown |

> **Les scènes sont générées à partir de la configuration**, pas écrites en dur : les
> identifiants d'appareils viennent des options. Une étape visant un appareil non
> déclaré est **omise** à la génération, pas écrite puis vouée à l'échec. Tant qu'une
> seule ampoule est appairée, les scènes ne parlent que d'elle.
>
> Le blanc froid de **Work** est approché par une couleur (`#F2F6FF`) : le DSL n'a pas
> de champ de température de couleur.
>
> **À rouvrir** : l'ampoule appairée en M4, une Philips Hue *white ambiance and
> color*, gère nativement la température de 154 à 455 mireds. Une approximation RVB
> rendra sans doute moins bien qu'un vrai `color_temp`. À juger à l'œil avant
> d'ajouter un champ au DSL pour une seule scène.
