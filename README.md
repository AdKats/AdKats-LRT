# AdKatsLRT - On-Spawn Loadout Enforcer

A Procon v2 plugin for Battlefield 4 that enforces weapon, gadget, and vehicle loadout restrictions on player spawn.

## Features

- **Infantry Loadout Enforcement** — Restrict specific weapons, gadgets, grenades, knives, and weapon accessories
- **Vehicle Loadout Enforcement** — Restrict vehicle weapons, upgrades, optics, and countermeasures
- **Battlelog Integration** — Fetches player loadouts from Battlelog for spawn-time enforcement
- **AdKats Integration** (optional) — Enhanced player data, reputation-based enforcement, admin notifications
- **Map/Mode Filtering** — Enforce restrictions on specific maps and game modes only
- **Backup AutoAdmin** — Kill-based enforcement when Battlelog data is unavailable

## Installation

1. Copy all `.cs` files from `src/` into your Procon plugins directory
2. Enable the plugin in Procon
3. Configure weapon/gadget restrictions via the plugin settings

## Requirements

- Procon v2
- Battlefield 4 server
- AdKats plugin (optional, for enhanced features)

## Development

This plugin uses a partial class architecture. See [CLAUDE.md](CLAUDE.md) for development conventions.

```bash
# Run style checks
dotnet restore
dotnet format whitespace --verify-no-changes
dotnet format style --verify-no-changes --severity warn --exclude-diagnostics IDE1007
```

## License

Copyright Daniel J. Gradinjan (ColColonCleaner). Licensed under [GPLv3](LICENSE).
