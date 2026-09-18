# 12 — Décisions et alternatives écartées

Format ADR court. Si tu veux revenir sur une de ces décisions, c'est possible,
mais fais-le explicitement — ne la contourne pas en cours de route.

---
### D1 — PWA plutôt qu'application SwiftUI
**Retenu** : client web ajouté à l'écran d'accueil.
**Écarté** : application iPad native.
**Pourquoi** : avec un compte Apple gratuit, le profil de provisionnement expire au bout
de 7 jours, ce qui est ingérable pour un écran fixe. Surtout, l'iPad 5 est en fin de vie :
l'architecture ne doit pas dépendre d'un matériel condamné. Un client web est remplaçable
par n'importe quel écran.
**Réversible** : oui. Un client SwiftUI reste un bon exercice après M6, comme second
client contre la même API.

---
### D2 — Core maison plutôt que Home Assistant
**Retenu** : registre d'appareils, moteur de scènes et intégrations écrits à la main.
**Écarté** : HA comme backend, avec RoomOS en simple UI par-dessus son API.
**Pourquoi** : brancher HA livrerait plus vite, mais l'intérêt de ce projet est justement
d'écrire cette logique. Décision assumée, prise en connaissant le coût.
**Limite** : on écrit la logique applicative, **pas les protocoles**. Zigbee2MQTT reste.

---
### D3 — .NET 10 partout
**Retenu** : Core et agent en .NET 10 (LTS).
**Écarté** : Node/Fastify, Go, Python côté serveur.
**Pourquoi** : aucune contrainte technique du projet ne départage ces options — le volume
de données est ridicule. Le seul critère discriminant est que C# est **obligatoire** pour
l'agent Windows, et qu'un seul langage vaut mieux que deux. Bénéfice secondaire :
c'est directement utile pour un poste d'ingénieur DevOps.
**Note** : ne pas migrer vers .NET 11 (release STS, 2 ans de support).

---
### D4 — Zigbee plutôt que Matter/Thread
**Retenu** : Zigbee2MQTT.
**Écarté** : Matter over Thread.
**Pourquoi** : il n'existe pas de contrôleur Matter exploitable en .NET. Choisir Matter
obligerait à réintroduire Home Assistant, ce que D2 écarte.
**Conséquence** : contrainte d'achat. Lampes Zigbee uniquement.

---
### D5 — SignalR plutôt que polling HTTP
**Retenu** : SignalR pour le client et pour l'agent.
**Écarté** : polling 1 s.
**Pourquoi** : le polling suffirait pour un seul client, mais le hub sert aussi de canal
de commande vers l'agent, et la connexion elle-même porte l'information « PC online ».
Deux usages pour un seul mécanisme.

---
### D6 — Télémétrie hors base de données
**Retenu** : dernier état en mémoire, Prometheus en M6 pour l'historique.
**Écarté** : table de séries temporelles en SQLite.
**Pourquoi** : écrire un échantillon par seconde en SQLite fait gonfler le fichier pour
une donnée sans valeur historique.

---
### D7 — Une seule abstraction « provider »
**Retenu** : `IMusicProvider` uniquement.
**Écarté** : `ILightingProvider`, `IPowerProvider`, etc.
**Pourquoi** : Spotify est la seule dépendance dont la politique d'accès peut changer
du jour au lendemain — c'est déjà arrivé en février 2026. Les autres abstractions
répondent à des besoins qui n'existent pas.

---
### D8 — Multi-pièces modélisé, pas implémenté
**Retenu** : `room_id` dans le schéma dès le départ, aucune UI multi-pièces.
**Pourquoi** : une migration de schéma coûte cher, une UI coûte cher aussi — on paie
seulement la première.

---
### D9 — HTTP en LAN, HTTPS repoussé en M6
**Retenu** : HTTP simple, token statique, pas de service worker.
**Pourquoi** : une PWA a besoin d'un contexte sécurisé pour le service worker. En V1,
le mode hors-ligne n'a aucun intérêt (le serveur piloté est sur le même réseau).
Tailscale fournira un certificat valide en M6 sans toucher à Let's Encrypt.
**Mais** : l'authentification par token est présente **dès M1**, pas en M6.

---
### D11 — Transfert d'appareil Spotify ajouté au périmètre
**Retenu** : `PUT /me/player` pour transférer la lecture vers le PC avant les
commandes musicales d'une scène.
**Écarté** : accepter que les scènes pilotent l'appareil actif du compte, quel qu'il
soit, et retirer `music.play` de la scène Gaming.
**Pourquoi** : constaté en M3, l'appareil actif était un téléphone. Une scène
« Gaming » qui lance la musique sur le téléphone de l'utilisateur pendant qu'il
s'installe devant son PC ne rend pas le service attendu. Le critère de réussite du
projet — « tu appuies sur Gaming, tu poses l'iPad, et tout est prêt » — n'est pas
tenable autrement.
**Coût** : un endpoint de plus dans le périmètre V1, et un indice de nom à configurer.
**Décidé le** 2026-09-18, à l'ouverture de M5.

---
### D10 — Interdits permanents
Kubernetes, PostgreSQL, Redis, Kafka, microservices, CQRS, Event Sourcing.
Le projet pilote quatre appareils dans une chambre. Toute infrastructure au-delà de
`docker compose` est de l'architecture pour l'architecture.
