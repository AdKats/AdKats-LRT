# AdKatsLRT — Procon v2 Loadout Enforcer Plugin

## Project Overview

AdKatsLRT is a C# on-spawn loadout enforcement plugin for Procon v2 (Battlefield game server administration). It monitors player loadouts via Battlelog and enforces weapon/gadget/vehicle restrictions.

- **Language:** C#
- **License:** GPLv3
- **Supported games:** BF4 (primary), BF3/BFHL (partial)
- **Dependencies:** Flurl, Flurl.Http, Newtonsoft.Json, Procon v2 (runtime only)
- **Optional integration:** AdKats plugin (for enhanced player data, punishments, and admin features)

## Architecture

The plugin uses a **partial class** pattern — `AdKatsLRT` is split across files in `src/AdKatsLRT/`:

| File | Responsibility |
|------|---------------|
| `src/AdKatsLRT.cs` | Main entry point, enums, plugin metadata, lifecycle, models, Logger |
| `src/AdKatsLRT/Settings.cs` | Plugin variables and settings UI |
| `src/AdKatsLRT/Events.cs` | Procon event handlers (OnPlayerSpawned, OnPlayerKilled, etc.) |
| `src/AdKatsLRT/Loadouts.cs` | Infantry loadout enforcement, WARSAW library, processing thread |
| `src/AdKatsLRT/Battlelog.cs` | Battlelog API communication thread |
| `src/AdKatsLRT/Messaging.cs` | Chat/yell/tell messaging helpers |
| `src/AdKatsLRT/Utilities.cs` | String helpers, time formatting, validation |
| `src/AdKatsLRT/Data.cs` | Static data: map/mode definitions, WARSAW-RCON weapon mappings |

## Code Style

Style is enforced by `.editorconfig` and checked via `dotnet format` in CI.

**Critical conventions:**
- **Use `String`, `Int32`, `Boolean`, `Double`** — NOT `string`, `int`, `bool`, `double`. The codebase uses explicit System type names everywhere.
- **Allman brace style** — opening brace on its own line
- **4 spaces** for indentation, LF line endings
- **Block-scoped namespaces** (not file-scoped)
- **`using` directives outside namespace**, System usings first

## Build & CI

- `AdKatsLRT.csproj` at root is a **CI-only artifact** for `dotnet format`. It is NOT a real build file — Procon v2 assemblies are unavailable for compilation.
- **CI workflow** (`.github/workflows/ci.yml`): runs on push to `master` and PRs. Checks `dotnet format whitespace` and `dotnet format style --exclude-diagnostics IDE1007`.
- **Release workflow** (`.github/workflows/release.yml`): triggered by `v*` tags. Packages `.cs` files from `src/` into a zip and creates a GitHub Release.

### Running style checks locally

```bash
dotnet restore
dotnet format whitespace --verify-no-changes
dotnet format style --verify-no-changes --severity warn --exclude-diagnostics IDE1007
```

## Threading Model

The plugin runs dedicated consumer threads with queue + `EventWaitHandle` pattern:
- **SpawnProcessing** — processes loadout checks from the queue
- **BattlelogComm** — fetches player persona IDs from Battlelog

**Queue pattern:** Lock the queue, check count inside the lock, drain and clear. Never check `.Count` outside a lock (TOCTOU).

## Branch Structure

- `master` — current development, Procon v2 only
- `legacy` — archived Procon v1 version, no longer maintained
