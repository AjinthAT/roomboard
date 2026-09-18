# 00 — Contexte matériel et réseau

## Matériel existant

| Élément | Détail | Rôle dans RoomOS |
|---|---|---|
| iPad 5e gén. (2017) | A9, 2 Go RAM, iPadOS 16 max, Safari 16 | Panneau de contrôle permanent |
| Mac Pro | Hyperviseur Proxmox | Hôte |
| VM Debian | Sur Proxmox, allumée H24 | Héberge RoomOS Core |
| PC Windows 11 | ASUS PRIME Z690-A, i7-12700K (8 P-cores + 4 E-cores), 64 Go, RTX 5070 Ti 16 Go | Cible pilotée + hôte de l'agent |
| JBL USB | Sortie audio | Sortie audio 1 |
| Casque + dongle USB | Sortie audio | Sortie audio 2 |
| Lampes | 3 douilles : 1× E27 et 2× E14. **Commande en cours : coordinateur SLZB-06 + 1 ampoule E27 couleur.** Les E14 plus tard. | Éclairage |
| Mac | Disponible | Dev uniquement, pas de rôle runtime |

## Contraintes dures

- **L'iPad 5 est en fin de vie logicielle.** Il ne doit jamais être un composant
  dont dépend l'architecture. Il est un client parmi d'autres.
- **Safari 16 est la cible front.** Pas de fonctionnalité JS/CSS plus récente sans polyfill vérifié.
- **iPadOS 16.7.16 confirmé** sur l'appareil, soit Safari 16.6. Tailwind 4 exige
  Safari 16.4+ (`@property`, `color-mix()`, cascade layers) : le seuil est franchi,
  **Tailwind 4 est validé**, pas de repli en 3.4. Toute nouvelle dépendance front
  doit être vérifiée contre Safari 16.6, pas contre « le dernier Safari ».
- **A9 + 2 Go de RAM.** C'est le vrai budget de performance du projet, pas le serveur.
- **Achat de lampes contraint : Zigbee, pas Matter/Thread.** Voir `12-decisions.md`.
  Prévoir un coordinateur Zigbee (Sonoff dongle-E ou SLZB-06 en Ethernet) en plus des ampoules.

## Capteurs du PC — relevé du 2026-09-18

Relevé avec `RoomOS.Agent.Windows.exe --sensors`. Les noms exacts comptent : la
sélection des capteurs se fait par nom, pas par « premier du bon type ».

| Mesure | Capteur retenu | Piège écarté |
|---|---|---|
| Charge CPU | `CPU Total` | les 20 capteurs `CPU Core #n Thread #m` |
| Température CPU | `CPU Package` | les `… Distance to TjMax`, qui sont des écarts |
| Charge GPU | `GPU Core` | les nombreux `D3D …` |
| Température GPU | `GPU Core` | `GPU Memory Junction`, 10 °C plus haut |
| VRAM | `GPU Memory Used` / `GPU Memory Total` | `D3D Dedicated Memory Used` |
| RAM | bloc `Total Memory` | le bloc `Virtual Memory`, qui coexiste |

**Deux prérequis pour les températures CPU**, constatés sur cette machine :
1. **PawnIO installé** (https://pawnio.eu). L'intégrité de la mémoire est active ici
   (`SecurityServicesRunning = {2, 7}`), ce qui bloque l'ancien driver `WinRing0`.
   PawnIO est signé et compatible HVCI : rien à désactiver côté sécurité.
2. **Processus élevé**, ou service Windows en compte SYSTEM.

Sans ces deux conditions, les capteurs de température CPU existent mais valent `null`,
sans la moindre erreur. Le GPU, lui, remonte tout via l'API NVIDIA sans privilège
particulier — d'où un diagnostic trompeur si on ne regarde que lui.

## Sorties audio du PC — relevé du 2026-09-18

Relevé avec `RoomOS.Agent.Windows.exe --audio`.

| Périphérique Windows | Rôle | Indice retenu |
|---|---|---|
| `Haut-parleurs (JBL Charge 6)` | sortie 1 | `JBL` |
| `Casque pour téléphone (CORSAIR HS80 MAX WIRELESS Gaming Headset)` | sortie 2 | `CORSAIR` |
| `Odyssey G65B (NVIDIA High Definition Audio)` | écran, non piloté | — |
| `Realtek Digital Output (Realtek(R) Audio)` | non utilisé | — |

**L'indice vise le matériel, pas le rôle.** « Casque » correspondrait à « Casque pour
téléphone », qui est le canal *communications* du Corsair : bande étroite, prévu pour
la voix. Y router la musique donnerait un son dégradé sans que rien ne signale d'erreur.

L'écran et la sortie Realtek restent visibles de Windows mais hors du registre RoomOS :
si l'une devient la sortie active, `activeOutputId` vaut `null` et l'UI affiche
« sortie inconnue » — c'est le comportement voulu, pas une panne.

## Réseau

- Tout se passe en LAN, en HTTP, sur le réseau domestique. Pas d'exposition Internet en V1.
- Le PC Windows doit être sur le même segment L2 que le Core pour le Wake-on-LAN
  (paquet magique en broadcast). WoL à activer dans le BIOS **et** dans les propriétés
  de la carte réseau Windows (« Autoriser ce périphérique à sortir l'ordinateur de veille »
  + « Magic Packet only »).
- L'agent Windows se connecte **en sortant** vers le Core. Aucune règle de pare-feu
  entrante n'est nécessaire sur le PC.

## Adressage à figer avant M1

| Nom | Valeur | Note |
|---|---|---|
| `CORE_HOST` | **`192.168.1.x`** | VM `vm-102`, `ens18`, **IP statique** dans `/etc/network/interfaces`. Rien à réserver côté DHCP. |
| `CORE_PORT` | `8080` | HTTP |
| `PC_MAC` | **`AA:BB:CC:DD:EE:FF`** | Carte Intel Ethernet. Ni le Bluetooth, ni les TAP-Windows d'OpenVPN. |
| `PC_IP` | **`192.168.1.y`** | IP statique, confirmée |

> Les deux adresses sont statiques, configurées sur les machines elles-mêmes. Aucune
> dépendance au bail DHCP de la box, donc rien qui puisse bouger après une coupure.
>
> **Le dernier octet est masqué dans ce dépôt public** : `192.168.1.x` désigne le Core
> et `192.168.1.y` le PC. Les valeurs réelles vivent dans `deploy/.env`, qui n'est pas
> versionné.
| `BROADCAST` | `192.168.1.255` | Cible du paquet magique |

> Le Core tourne sur la machine où l'on développe : **dev et prod sont la même VM**.
> L'iPad peut donc charger `http://192.168.1.x:8080` dès M0, sans étape de déploiement.
>
> **Le plan d'adressage est complet.** Plus rien ne bloque M1.
>
> Le PC expose aussi deux adaptateurs virtuels TAP-Windows (OpenVPN) et une interface
> Bluetooth. Le paquet magique doit viser la carte **Intel Ethernet** : le WoL par
> Wi-Fi ou par interface virtuelle ne fonctionne pas.
