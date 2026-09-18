# Instructions agent — RoomOS

## Avant toute chose

Lis `docs/` en entier avant d'écrire une ligne de code. En particulier
`docs/02-stack.md`, `docs/11-conventions.md` et `docs/12-decisions.md`.

## Règles non négociables

1. **Un jalon à la fois.** Le jalon courant est défini dans `docs/10-roadmap.md`.
   Tu n'implémentes rien en dehors de ce jalon, même si c'est « rapide » ou « logique ».
2. **Pas de nouvelle dépendance** sans la justifier explicitement et attendre validation.
3. **Pas de refonte d'un jalon livré** sans demande explicite.
4. **Tu proposes ton plan avant de coder.** Liste des fichiers créés/modifiés, puis tu attends le go.
5. **Tu ne réintroduis pas une alternative écartée** dans `docs/12-decisions.md`.
   Si tu penses qu'une décision est mauvaise, tu le dis en une phrase, tu ne la contournes pas.

## Interdits explicites

Ces éléments ont été écartés volontairement. Ne les propose pas, ne les ajoute pas :

- Kubernetes, Helm, Nomad
- PostgreSQL, MySQL, Redis, MongoDB, InfluxDB, TimescaleDB
- Kafka, RabbitMQ (MQTT via Zigbee2MQTT est la seule file de messages)
- Microservices, gRPC, CQRS, Event Sourcing, MediatR, Clean Architecture à 6 couches
- Home Assistant comme dépendance du Core
- Swift / SwiftUI / application native iPad
- Node.js côté serveur
- Toute abstraction « provider » autre que `IMusicProvider`
- Toute UI multi-pièces (le modèle de données la prévoit, l'UI non)
- Framer Motion, GSAP, ou toute lib d'animation lourde côté front
- Stockage en base des échantillons de télémétrie

## Style de collaboration attendu

- Tu écris du code simple et lisible avant d'écrire du code élégant.
- Tu n'ajoutes pas d'abstraction pour un besoin qui n'existe pas encore.
- Tu signales les erreurs de conception que tu vois dans `docs/` au lieu de coder autour.
- Tu ne génères pas de commentaires qui paraphrasent le code.
- Tests obligatoires uniquement là où `docs/11-conventions.md` les exige.

## Definition of done d'un jalon

- Le code compile, tourne, et fait ce que le jalon décrit, vérifié à la main.
- Les tests exigés passent.
- `docs/10-roadmap.md` est coché.
- Un commit propre par unité logique, message en anglais, format conventionnel.
