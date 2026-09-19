# 01 — Vision et périmètre

## Définition

> RoomOS est une interface locale permettant de contrôler, surveiller et automatiser
> l'environnement physique et numérique d'une pièce.

Sa particularité est de mélanger le monde physique (lampes, prises, capteurs) et le
monde informatique (PC, audio, applications, musique, monitoring) dans une seule surface
de contrôle, accessible en moins de deux secondes depuis un écran fixe.

## Ce que RoomOS n'est pas

- Un clone de Home Assistant
- Une plateforme domotique universelle
- Un outil de monitoring serveur
- Une simple télécommande PC
- Une application iPad

## Périmètre V1 (fermé)

### PC
- État online / offline
- Wake-on-LAN
- Shutdown, restart
- Télémétrie : CPU (usage, température), GPU (usage, température, VRAM), RAM, uptime

### Audio
- Bascule entre JBL et casque
- Volume, mute
- Affichage de la sortie active

### Lumières
- On/off, luminosité, couleur si l'ampoule le permet
- **Étendu après M6** : température de blanc, fondus, effets, comportement au
  rallumage mural, identification, qualité du lien radio. Les capacités sont déduites
  de l'inventaire Zigbee2MQTT, donc l'interface s'adapte à chaque ampoule appairée
  sans configuration.

### Musique
- Morceau en cours, pochette, artiste
- Play / pause / suivant / précédent

### Scènes
- Work, Chill, Gaming, Night
- Exécutées par un moteur déclaratif (voir `08-scenes.md`)
- **Pas d'éditeur de scènes en V1**

## Hors périmètre V1, explicitement

Prises connectées, capteurs (température, présence), TV, UI multi-pièces,
multi-utilisateurs, application native.

**Les automatisations horaires sont sorties du hors-périmètre le 2026-09-19**, après
M6, comme le prévoyait le README. Une routine déclenche une scène : elle n'ajoute
aucune logique d'exécution, seulement un déclencheur posé devant l'existant.

## Critère de réussite

Tu appuies sur « Gaming » depuis l'iPad, tu poses l'iPad, et tout est prêt
sans avoir touché au clavier. Si ça marche de façon fiable, la V1 est réussie.
