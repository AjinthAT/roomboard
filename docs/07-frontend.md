# 07 — Front (PWA)

## Cible

**Safari 16.6** (iPadOS 16.7.16, version confirmée sur l'appareil) sur iPad 5
(A9, 2 Go RAM), en paysage, ajouté à l'écran d'accueil.
C'est la contrainte de performance principale du projet.

Toute dépendance front se vérifie contre Safari 16.6, pas contre « le dernier Safari ».
Tailwind 4 passe (seuil 16.4).

## Layout

Une seule page. Pas de router. Grille 2×2 + barre de scènes.

```
┌─────────────────────────────────────┐
│             ROOM CONTROL            │
├─────────────────┬───────────────────┤
│ PC              │ MUSIC             │
│ ● Online        │ Spotify           │
│ CPU  18%  47°C  │ After Hours       │
│ GPU  42%  53°C  │ ◀  ▶  ❚❚          │
│ Start / Restart / Shutdown          │
├─────────────────┼───────────────────┤
│ AUDIO           │ LIGHTS            │
│ [JBL] [Casque✓] │ Bureau      ●     │
│ ━━━━━●━━ 64%    │ Ambiance    ●     │
├─────────────────┴───────────────────┤
│ Work    Chill    Gaming    Night    │
└─────────────────────────────────────┘
```

## Règles de performance (non négociables)

1. **La télémétrie ne passe pas par le state React global.** Utiliser un store externe
   (Zustand ou un simple `useSyncExternalStore` maison) avec sélecteurs, pour qu'un tick
   CPU ne re-render que la ligne CPU.
2. **Throttle à 1 Hz** côté client même si le serveur pousse plus vite.
3. **Pas de lib d'animation.** Transitions CSS uniquement.
4. **Budget bundle : 200 Ko gzip.** Vérifié en CI.
5. **Target de build ES2020**, testé sur Safari 16. Pas de `??=` non transpilé,
   pas de `Array.prototype.at` sans vérification.
6. Pochettes d'album : une seule image à la fois, `loading="lazy"`, dimension fixe.

## Connexion

- Au montage : `GET /api/state`, remplit le store.
- Puis `@microsoft/signalr` sur `/hub/room`, avec `withAutomaticReconnect`.
- **La reconnexion ne renonce jamais.** La politique par défaut de SignalR abandonne
  après quatre tentatives, soit 42 secondes. Sur un panneau allumé en permanence,
  toute coupure plus longue — redémarrage du Core, iPad en veille, Wi-Fi qui tombe —
  laissait l'écran figé sur « Connexion perdue » jusqu'à un rechargement manuel.
  Backoff exponentiel plafonné à 15 s, sans limite de tentatives.
- **Toute reconnexion refait un `GET /api/state`.** Les deltas émis pendant la coupure
  sont définitivement perdus : repasser le bandeau au vert sans resynchroniser
  afficherait un état arbitrairement périmé avec l'apparence du direct.
- Bandeau discret « Reconnexion… » si le hub est down. Les contrôles restent visibles
  mais **désactivés**, et l'état du PC affiche « État inconnu » plutôt qu'une valeur
  figée : sans hub, on ne sait pas, et le dire vaut mieux que le deviner.
- Si un `POST` échoue, l'UI revient à l'état serveur. **Pas d'optimistic update**
  sur les actions physiques : un bouton qui ment est pire qu'un bouton lent.
- **Confirmation en deux temps sur les actions irréversibles** : redémarrer, éteindre,
  et toute scène qui éteint un PC. Le bouton devient « Confirmer ? » et se désarme
  seul au bout de quelques secondes. Pas de fenêtre modale : sur un panneau mural,
  une modale demande de viser une petite cible et se ferme par réflexe. Un bouton
  laissé armé serait un piège pour le passage suivant, d'où le désarmement.
- Le caractère sensible d'une scène est **calculé par le serveur** à partir de ses
  étapes, jamais d'une liste de noms : une scène ajoutée plus tard qui éteint le poste
  sera protégée sans qu'on ait à y penser.
- Si `activeOutputId` est `null`, la carte Audio affiche « sortie inconnue » et
  **aucun** bouton de sortie n'est marqué actif. Ne jamais retomber sur le premier
  de la liste : ce serait exactement le bouton qui ment.

## Feedback des actions

Chaque bouton d'action a trois états : idle, pending (spinner inline), résultat.
Les actions longues (wake, scènes) affichent leur progression via les événements
`SceneStepCompleted` / `PcStateChanged`, pas via un timer.

## Mode PWA

Dans `index.html` :
```html
<meta name="apple-mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover, user-scalable=no">
<link rel="apple-touch-icon" href="/icon-180.png">
```

**Pas de service worker en V1.** Il exige HTTPS, et un dashboard hors-ligne n'a aucun sens
quand le serveur qu'il pilote est injoignable. Le service worker et le manifest arrivent
en M6 avec Tailscale et son certificat.

## Confort iPad

- Réglages iPad : verrouillage automatique sur « Jamais » quand il est sur secteur.
- Accès guidé (Réglages → Accessibilité) pour verrouiller l'iPad sur RoomOS.
- Thème sombre par défaut, contraste élevé, cibles tactiles ≥ 44 px.
