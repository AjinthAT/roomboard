# 11 — Conventions

## Arborescence

```
roomboard/
├── AGENTS.md              # règles agent, lu automatiquement
├── README.md
├── docs/                  # ce pack
├── src/
│   ├── RoomOS.Domain/          # entités, DTOs, contrats. Zéro dépendance externe.
│   ├── RoomOS.Core/            # ASP.NET Core : API, hubs, moteur, intégrations
│   │   ├── Api/                # endpoints minimal API, groupés par domaine
│   │   ├── Agents/             # registre des connexions agent
│   │   ├── Auth/               # authentification par jeton
│   │   ├── Configuration/      # options liées aux variables ROOMOS__
│   │   ├── Hubs/
│   │   ├── Data/               # DbContext, migrations, seed
│   │   ├── State/              # StateStore
│   │   ├── Scenes/             # moteur + exécuteurs (M5)
│   │   └── Integrations/       # Spotify, Mqtt, Wol
│   └── RoomOS.Agent.Windows/
├── web/                   # Vite + React + TS
│   └── src/{components,store,api,styles}
├── tests/
│   └── RoomOS.Core.Tests/
├── deploy/
│   ├── docker-compose.yml
│   ├── mosquitto/
│   ├── zigbee2mqtt/
│   └── .env.example
└── .github/workflows/
```

> Le dépôt s'appelle `roomboard`, pas `roomos`. `RoomOS` reste le nom du produit
> et le préfixe des projets .NET.
>
> Pas de `CLAUDE.md` : Claude Code lit `AGENTS.md` directement. Un doublon serait
> une seconde source de vérité pour les règles agent, à désynchroniser à la première
> modification.

## Code

- C# : conventions .NET standard, `nullable` activé, warnings as errors sur `RoomOS.Domain`.
- Endpoints : minimal API groupés par `MapGroup("/api/pc")`, un fichier par groupe.
- Pas d'injection d'`IServiceProvider`, pas de service locator.
- Async partout, `CancellationToken` propagé.
- Aucun `catch` silencieux. Une exception avalée est un bug de conception.
- TypeScript : `strict: true`. Types de l'API générés ou écrits à la main dans
  `web/src/api/types.ts`, tenus synchrones avec `RoomOS.Domain`.
- Nommage : `deviceId` en camelCase dans le JSON, `PascalCase` en C#,
  sérialisation configurée en camelCase une fois pour toutes.

## Tests

Tests exigés uniquement sur :
1. Le moteur de scènes (liste précise dans `08-scenes.md`).
2. Le parsing du DSL de scènes.
3. La construction du paquet Wake-on-LAN.

Aucun test d'intégration Spotify ou MQTT. Aucun test d'UI. Ce n'est pas du dogmatisme
inversé : tester ce qui appelle une API externe non maîtrisée donne une fausse sécurité.

## Configuration

Tout par variables d'environnement, préfixe `ROOMOS__`. Aucun secret dans le repo.
`deploy/.env.example` liste toutes les clés avec des valeurs factices.

## Git

- Une branche par jalon : `m1-pc`, `m2-audio`…
- Commits conventionnels en anglais : `feat(agent): stream cpu telemetry`
- Pas de commit qui touche deux jalons.

## CI (GitHub Actions)

| Job | Contenu |
|---|---|
| `core` | `dotnet build` + `dotnet test` |
| `agent` | `dotnet publish -r win-x64` |
| `web` | `npm ci`, `tsc --noEmit`, `npm run build`, **vérification du budget bundle 200 Ko gzip** |

Le déploiement (M6) passe par un runner self-hosted sur la VM Debian qui fait
`docker compose pull && up -d`.
