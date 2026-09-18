# 13 — Installation et restauration

Objectif : **réinstaller la VM à partir de ce dépôt en moins de 30 minutes**, sans
avoir à se rappeler d'une étape.

Toutes les commandes se lancent sur la VM Debian, sauf la section agent Windows.

## 1. Prérequis système

```bash
sudo apt update && sudo apt install -y git curl ca-certificates
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER   # se reconnecter ensuite
```

La chaîne .NET et Node n'est nécessaire **que pour compiler l'agent Windows** et pour
développer. L'exploitation du Core ne demande que Docker.

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
echo 'export PATH="$HOME/.dotnet:$PATH"'   >> ~/.bashrc
echo 'export DOTNET_ROOT="$HOME/.dotnet"'  >> ~/.bashrc
sudo apt install -y libicu76      # sans quoi le SDK refuse de démarrer
```

## 2. Réseau

Adresse **statique** sur la VM, dans `/etc/network/interfaces` :

```
iface ens18 inet static
	address 192.168.1.x/24
	gateway 192.168.1.1
```

Le Core et le PC doivent être sur le **même segment L2** : le Wake-on-LAN est un
broadcast dirigé, il ne franchit pas un routeur.

## 3. Le Core

```bash
git clone https://github.com/AjinthAT/roomboard.git
cd roomboard/deploy
cp .env.example .env
```

Renseigner `.env`. Les valeurs indispensables au démarrage :

| Clé | Comment l'obtenir |
|---|---|
| `ROOMOS__ApiToken` | `openssl rand -hex 24` |
| `ROOMOS__AgentToken` | `openssl rand -hex 24`, différent du précédent |
| `ROOMOS__Pc__Mac` | `getmac /v` sur le PC, **carte Ethernet** |
| `ROOMOS__Pc__Ip` | adresse statique du PC |

Le Core **refuse de démarrer** si un jeton est vide : mieux vaut un échec au
démarrage qu'une API ouverte.

```bash
docker compose up -d --build
curl -s http://127.0.0.1:8080/healthz
```

## 4. L'agent Windows

Publier depuis la VM :

```bash
cd ~/roomboard
dotnet publish src/RoomOS.Agent.Windows -c Release -r win-x64 \
  --self-contained false -o ~/roomos-agent
```

Puis sur le PC, en **PowerShell administrateur** :

```powershell
New-Item -ItemType Directory -Force C:\RoomOS | Out-Null
scp -r user@192.168.1.x:/home/user/roomos-agent/* C:\RoomOS\Agent\
cd C:\RoomOS\Agent
```

Renseigner `appsettings.json` : `Core:Url` et `Core:AgentToken`, identique à
`ROOMOS__AgentToken`.

**Installer PawnIO** depuis https://pawnio.eu — sans lui, aucune lecture de
température. Puis vérifier avant d'installer le service :

```powershell
.\RoomOS.Agent.Windows.exe --sensors   # « Administrateur : True », « PawnIO : installé »
.\RoomOS.Agent.Windows.exe --audio     # relever le nom exact des sorties
```

Installer le service :

```powershell
New-Service -Name RoomOSAgent -BinaryPathName "C:\RoomOS\Agent\RoomOS.Agent.Windows.exe" -StartupType Automatic
Start-Service RoomOSAgent
```

> **Mise à jour ultérieure** : toujours `Stop-Service RoomOSAgent` avant de recopier,
> sinon `scp` échoue en « Broken pipe » sur les fichiers verrouillés — et l'ancienne
> version continue de tourner sans que rien ne le signale.

### Réglages Windows indispensables

- **BIOS** : *Power On By PCI-E* activé, *ErP Ready* désactivé.
- **Carte Intel Ethernet**, onglet *Gestion de l'alimentation* : autoriser la sortie
  de veille, *Magic Packet only*.
- **Démarrage rapide** de Windows désactivé : il met la machine dans un état hybride
  où la carte réseau ne répond plus au paquet magique.

## 5. Spotify

Créer une application sur le dashboard développeur, **Web API uniquement**.
Redirect URI, exactement :

```
http://127.0.0.1:8080/api/music/callback
```

Spotify impose HTTPS hors adresses de bouclage littérales : ni une adresse LAN ni
`localhost` ne sont acceptés.

Renseigner `ROOMOS__Spotify__ClientId`, puis autoriser une fois, depuis un
navigateur, à travers un tunnel SSH :

```bash
ssh -L 8080:127.0.0.1:8080 user@192.168.1.x
# puis ouvrir http://127.0.0.1:8080/api/music/authorize
```

Relever ensuite le nom Spotify du PC via `GET /api/music/devices` et le mettre dans
`ROOMOS__Spotify__PcDeviceHint`.

## 6. Zigbee

Brancher le coordinateur, relever son IP, la mettre dans
`deploy/zigbee2mqtt/configuration.yaml`, puis :

```bash
docker compose --profile zigbee up -d
```

Appairer les ampoules dans l'interface de Zigbee2MQTT, sur `http://127.0.0.1:8099`.
RoomOS consomme le réseau Zigbee, il ne l'administre pas.

Le nom donné à une ampoule dans Z2M doit correspondre à
`ROOMOS__Lights__0__Z2mFriendlyName`.

## 7. Accès distant et HTTPS (Tailscale)

Facultatif, mais c'est ce qui débloque le mode hors-ligne de la PWA : un service
worker exige un contexte sécurisé, et Tailscale fournit un certificat valide sans
toucher à Let's Encrypt ni exposer quoi que ce soit sur Internet (ADR D9).

```bash
curl -fsSL https://tailscale.com/install.sh | sh
sudo tailscale up            # affiche un lien à ouvrir pour authentifier la machine
```

Activer HTTPS dans la console d'administration Tailscale (*DNS → HTTPS
Certificates*), puis :

```bash
sudo tailscale cert "$(tailscale status --json | python3 -c 'import json,sys; print(json.load(sys.stdin)["Self"]["DNSName"].rstrip("."))')"
sudo tailscale serve --bg --https=443 http://127.0.0.1:8080
```

L'iPad, connecté au même réseau Tailscale, charge alors
`https://vm-102.<ton-tailnet>.ts.net`. Le service worker s'active tout seul : il
n'est enregistré qu'en contexte sécurisé, et rien dans le code ne change.

> **Le jeton d'API reste la seule authentification.** Tailscale apporte le chiffrement
> et la joignabilité, pas le contrôle d'accès applicatif.

## 8. Déploiement automatique (facultatif)

Un runner auto-hébergé sur la VM permet à `main` de se déployer seul, une fois la CI
verte.

```bash
mkdir -p ~/actions-runner && cd ~/actions-runner
curl -sL -o runner.tar.gz https://github.com/actions/runner/releases/latest/download/actions-runner-linux-x64.tar.gz
tar xzf runner.tar.gz

TOKEN=$(gh api -X POST repos/<owner>/<repo>/actions/runners/registration-token --jq .token)
./config.sh --unattended --url https://github.com/<owner>/<repo> --token "$TOKEN"             --name roomos-vm --labels self-hosted,linux,x64,roomos --replace

# Emplacement du clone que docker compose connaît. Propre à la machine, d'où
# l'environnement du runner plutôt que le workflow.
echo "ROOMOS_DIR=$HOME/roomboard" >> .env

sudo ./svc.sh install "$USER" && sudo ./svc.sh start
```

> La reconstruction de l'image compile le front **et** le Core sur la VM. Avec 3,8 Go
> de RAM, c'est le moment le plus tendu : un `docker builder prune -af` de temps en
> temps évite de se faire tuer par le gestionnaire de mémoire.

## 9. Supervision

Grafana sur `http://192.168.1.x:3000`, identifiant `admin`, mot de passe
`GRAFANA_PASSWORD`. Le tableau de bord et la source de données sont provisionnés
automatiquement.

## Restauration après perte de la VM

L'état à sauvegarder tient en deux éléments :

| Élément | Contenu | Perte si absent |
|---|---|---|
| `deploy/.env` | jetons, adresses, Client ID | tout est à reconfigurer |
| volume `roomos-data` | `roomos.db` : appareils, sorties audio, scènes, jeton Spotify | réautorisation Spotify, réappairage inutile |

**Arrêter le Core avant de sauvegarder.** SQLite tourne en mode WAL : copier les
fichiers pendant qu'il écrit peut produire un instantané incohérent, qui ne se
révélera qu'au moment où on en aura besoin.

```bash
cd ~/roomboard/deploy

# Sauvegarde
docker compose stop core
docker run --rm -v deploy_roomos-data:/data -v "$PWD":/backup alpine \
  tar czf /backup/roomos-data.tar.gz -C /data .
docker compose start core
cp .env env.backup

# Restauration
docker compose stop core
docker run --rm -v deploy_roomos-data:/data -v "$PWD":/backup alpine \
  sh -c 'rm -rf /data/* && tar xzf /backup/roomos-data.tar.gz -C /data'
docker compose start core
```

Le `rm -rf` avant extraction n'est pas décoratif : sans lui, un fichier `-wal`
résiduel plus récent que la base restaurée serait rejoué par SQLite au démarrage.

Sans sauvegarde, tout se reconstruit : le seed recrée la pièce, le PC, les sorties
audio et les scènes à partir de `.env`. Seule l'autorisation Spotify est à refaire.

Les données de Prometheus et Grafana ne sont **pas** critiques : l'historique de
télémétrie est un confort, pas un état.
