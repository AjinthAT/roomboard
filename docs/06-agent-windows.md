# 06 — Agent Windows

## Nature

Worker Service .NET 10, installé en service Windows, démarrage automatique,
**exécuté en compte SYSTEM ou administrateur** (obligatoire pour lire les températures).

```
RoomOS.Agent.Windows.exe
```

Installation : `sc.exe create RoomOSAgent binPath= "..." start= auto`
ou `dotnet publish -r win-x64 --self-contained` puis `New-Service` en PowerShell.

## Responsabilités

1. Maintenir une connexion SignalR sortante vers le Core (reconnexion automatique
   avec backoff exponentiel plafonné à 30 s).
2. Publier la télémétrie toutes les 2 s.
3. Publier l'état audio à chaque changement + toutes les 10 s en filet.
4. Exécuter les commandes reçues et acquitter.

## Télémétrie

`LibreHardwareMonitorLib`, avec `Computer { IsCpuEnabled, IsGpuEnabled, IsMemoryEnabled = true }`.

### Prérequis : PawnIO

**Depuis LibreHardwareMonitor 0.9.5, la bibliothèque ne fournit plus de driver noyau.**
Elle embarque des modules bytecode (`IntelMSR.bin`, `AMDFamily17`, `LpcIO`…) et les
exécute dans [**PawnIO**](https://pawnio.eu), un driver signé qui exécute du code
sandboxé au lieu d'ouvrir un accès matériel brut. C'est ce qui le rend compatible
avec l'intégrité de la mémoire, là où l'ancien `WinRing0` figure désormais sur la
liste des pilotes vulnérables de Microsoft.

À installer sur le PC, une fois. **Sans lui, aucune lecture MSR** : températures,
fréquences et puissances CPU sortent toutes à `null`, sans erreur, sans exception.
`Computer.Open()` réussit quand même — c'est le piège.

Ce qui fonctionne sans PawnIO : charges CPU (compteurs Windows), RAM, et tous les
capteurs GPU (API du pilote graphique). D'où un diagnostic trompeur : la moitié des
valeurs remontent normalement.

`--sensors` affiche l'état de PawnIO en deuxième ligne.

**Risque bloquant à lever en tout premier dans M1** : LibreHardwareMonitor charge un
driver noyau pour lire les capteurs. Sur Windows 11 avec l'**intégrité de la mémoire**
(Sécurité Windows → Sécurité de l'appareil → Isolation du noyau) activée, ce chargement
peut être refusé. C'est le seul point de M1 qui peut échouer pour une raison hors du
code. À tester avant d'écrire quoi que ce soit d'autre dans l'agent : si ça bloque, le
choix est entre désactiver l'isolation du noyau sur le PC, ou renoncer aux températures
et se contenter des pourcentages d'usage — que l'API Windows expose sans driver.

Points d'attention connus :
- Il faut appeler `hardware.Update()` avant chaque lecture, sinon les valeurs sont figées.
- Les capteurs GPU changent de nom entre pilotes NVIDIA. Ne jamais matcher sur le nom exact :
  chercher le premier `SensorType.Temperature` du hardware de type `GpuNvidia`/`GpuAmd`.
- **Prévoir que ça casse à chaque mise à jour majeure de pilote GPU.** Toute valeur
  de température est nullable de bout en bout, jusqu'à l'UI.
- Si aucun capteur n'est trouvé, l'agent publie quand même usage/RAM. Il ne plante pas.

## Audio

`NAudio` / `MMDeviceEnumerator` pour énumérer les sorties.

Le changement de périphérique par défaut n'est pas exposé par une API publique Windows.

**Tranché en M2 : P/Invoke direct sur `IPolicyConfig`.**

Le pack recommandait initialement d'essayer `AudioSwitcher.AudioApi.CoreAudio` en
premier. Vérification faite au moment de l'implémenter, sa dernière version est
`4.0.0-alpha5`, **publiée le 6 octobre 2016**, jamais sortie de préversion, et elle
ne cible que .NETFramework 4.0 et 4.5 — ni netstandard, ni .NET Core. Elle ne se
charge pas sous .NET 10.

On écrit donc l'appel à `IPolicyConfig` à la main : une centaine de lignes, aucune
dépendance supplémentaire, et c'est exactement ce que faisait `AudioSwitcher` en
interne. L'interface reste non documentée par Microsoft ; son GUID et l'ordre de ses
méthodes sont figés depuis Windows 7, mais c'est le point du projet le plus exposé à
une rupture Windows. Si elle casse, le repli est l'absence de bascule automatique,
pas un plantage : `SetDefaultOutput` renvoie un échec, la commande est acquittée en
erreur et l'UI le montre.

Changer la sortie par défaut ne bascule pas toujours les applications déjà en cours
(Spotify notamment garde parfois son endpoint). Comportement à constater en M2 ; si le
problème se confirme, le contournement est de relancer la lecture après la bascule.

## Power

- `Shutdown` → `shutdown /s /t 0`
- `Restart` → `shutdown /r /t 0`
- L'agent acquitte **avant** d'exécuter, sinon l'ack ne part jamais.

## Configuration

`appsettings.json` à côté de l'exe :
```json
{ "Core": { "Url": "http://192.168.1.x:8080", "AgentToken": "...", "PcId": "gaming-pc" },
  "Telemetry": { "IntervalMs": 2000 } }
```

`AgentToken` doit être identique à `ROOMOS__AgentToken` dans `deploy/.env` côté Core.

## Mode diagnostic

```
.\RoomOS.Agent.Windows.exe --sensors
```

Liste tout le matériel et tous les capteurs détectés, avec leur nom exact et leur
valeur, et indique si le processus est élevé. À lancer avant toute chose quand une
valeur manque : c'est plus rapide que de deviner les noms de capteurs, et les noms
changent d'une version de pilote à l'autre.

## Installation (procédure M1)

Sur la VM, publier l'agent :

```bash
dotnet publish src/RoomOS.Agent.Windows -c Release -r win-x64 \
  --self-contained false -o /tmp/roomos-agent
```

Copier `/tmp/roomos-agent` sur le PC, par exemple dans `C:\RoomOS\Agent`, renseigner
`appsettings.json`, puis dans un **PowerShell administrateur** :

```powershell
# 1. Vérifier d'abord, en console : les températures remontent-elles ?
cd C:\RoomOS\Agent
.\RoomOS.Agent.Windows.exe

# 2. Si oui, installer en service
New-Service -Name RoomOSAgent `
            -BinaryPathName "C:\RoomOS\Agent\RoomOS.Agent.Windows.exe" `
            -StartupType Automatic
Start-Service RoomOSAgent
```

L'étape 1 n'est pas facultative : c'est là qu'on voit si le driver de
LibreHardwareMonitor se charge. En service, l'échec serait silencieux dans le
journal d'événements.

Le .NET Runtime 10 doit être présent sur le PC (`--self-contained false`). Sinon,
republier avec `--self-contained true`, ce qui alourdit le dossier d'environ 70 Mo
mais supprime la dépendance.

### Désinstallation

```powershell
Stop-Service RoomOSAgent
sc.exe delete RoomOSAgent
```

## Ce que l'agent ne fait pas

Pas de Wake-on-LAN (le PC est éteint), pas d'accès à Spotify, pas de logique métier,
pas de décision. L'agent est un exécutant. Toute la logique est dans le Core.
