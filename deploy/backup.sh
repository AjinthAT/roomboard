#!/usr/bin/env bash
#
# Sauvegarde de l'état de RoomOS : le volume SQLite et le fichier .env.
#
# Ce que contient une sauvegarde, et pourquoi elle compte : l'autorisation Spotify
# (sinon il faut refaire le flux PKCE), les scènes et les routines écrites à la main,
# et dans .env l'adresse MAC — sans elle, plus de Wake-on-LAN.
#
# Installation : voir docs/13-installation.md.
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEST="${ROOMOS_BACKUP_DIR:-$HOME/backups/roomos}"
KEEP="${ROOMOS_BACKUP_KEEP:-14}"
STAMP="$(date +%Y%m%d-%H%M)"

mkdir -p "$DEST"
cd "$DEPLOY_DIR"

# SQLite tourne en mode WAL : copier le fichier pendant une écriture donne une base
# tronquée, et on ne s'en aperçoit qu'en tentant de restaurer. Deux secondes d'arrêt
# valent mieux qu'une archive qui ne sert à rien. Le `trap` garantit le redémarrage
# même si l'archivage échoue.
docker compose stop core >/dev/null
trap 'docker compose start core >/dev/null' EXIT

# `--user` : sans lui l'archive appartient à root, et elle contient le jeton de
# rafraîchissement Spotify — on ne pourrait même pas en restreindre les droits.
docker run --rm \
  --user "$(id -u):$(id -g)" \
  -v deploy_roomos-data:/data:ro \
  -v "$DEST":/backup \
  alpine tar czf "/backup/roomos-data-$STAMP.tar.gz" -C /data .

chmod 600 "$DEST/roomos-data-$STAMP.tar.gz"

cp .env "$DEST/env-$STAMP"
chmod 600 "$DEST/env-$STAMP"

# Rotation. `ls -t` trie du plus récent au plus ancien : tout ce qui dépasse le
# quota part.
find_old() { ls -1t "$DEST"/$1 2>/dev/null | tail -n "+$((KEEP + 1))"; }
find_old 'roomos-data-*.tar.gz' | xargs -r -d '\n' rm --
find_old 'env-*' | xargs -r -d '\n' rm --

echo "$(date --iso-8601=seconds) sauvegarde $STAMP — $(du -h "$DEST/roomos-data-$STAMP.tar.gz" | cut -f1)"
