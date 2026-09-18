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
| Lampes | **Non achetées** — doivent être Zigbee | Éclairage |
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

**Les températures exigent l'élévation.** Sans elle, les capteurs sont présents mais
valent `null` ; le GPU, lui, remonte sa température sans privilège via l'API NVIDIA.
En service Windows (compte SYSTEM) la question ne se pose pas.

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
| `CORE_HOST` | **`192.168.1.30`** | VM `vm-102`, `ens18`, **IP statique** dans `/etc/network/interfaces`. Rien à réserver côté DHCP. |
| `CORE_PORT` | `8080` | HTTP |
| `PC_MAC` | **`C8:7F:54:68:BB:40`** | Carte Intel Ethernet. Ni le Bluetooth, ni les TAP-Windows d'OpenVPN. |
| `PC_IP` | **`192.168.1.150`** | IP statique, confirmée |

> Les deux adresses sont statiques, configurées sur les machines elles-mêmes. Aucune
> dépendance au bail DHCP de la box, donc rien qui puisse bouger après une coupure.
| `BROADCAST` | `192.168.1.255` | Cible du paquet magique |

> Le Core tourne sur la machine où l'on développe : **dev et prod sont la même VM**.
> L'iPad peut donc charger `http://192.168.1.30:8080` dès M0, sans étape de déploiement.
>
> **Le plan d'adressage est complet.** Plus rien ne bloque M1.
>
> Le PC expose aussi deux adaptateurs virtuels TAP-Windows (OpenVPN) et une interface
> Bluetooth. Le paquet magique doit viser la carte **Intel Ethernet** : le WoL par
> Wi-Fi ou par interface virtuelle ne fonctionne pas.
