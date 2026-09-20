# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OniExtract2024 is a **mod for the game Oxygen Not Included** whose only job is to dump the
game's own data and art to disk. It loads inside ONI, reads the live game assembly, and writes
JSON + PNGs to `Documents\Klei\OxygenNotIncluded\export`.

The export is consumed by the **blueprintnotincluded** website
([blueprintnotincluded/blueprintnotincluded](https://github.com/blueprintnotincluded/blueprintnotincluded)),
whose importer is `app/api/batch/convert-export-2024.ts` in that repo. This repo produces; that
repo consumes. Changing an emitted field is a change to a cross-repo contract — see
[docs/WEBSITE_POSTPROCESSING.md](docs/WEBSITE_POSTPROCESSING.md).

This is a fork. `origin` is the working fork; `upstream` is
[blueprintnotincluded/OniExtract2024](https://github.com/blueprintnotincluded/OniExtract2024).

## The single most important constraint

**Almost nothing here can be verified without running the game.** The mod links against ONI's
`Assembly-CSharp.dll` and `UnityEngine.dll`, which are not redistributable, so CI builds only
the Unity-free `OniExtract2024.Core` project. A green CI run means *six unit tests passed* — it
does not mean the mod compiles, loads, or exports correctly.

Never report an export-affecting change as working on the strength of a build or a test run.
Say what was compiled, what was tested, and that the in-game behaviour is unverified. A human
with the game installed has to confirm it.

## Architecture

- **`OniExtract2024/`** — the mod itself (.NET Framework 4.8, Harmony patches, Unity types).
  Cannot be built or tested on CI.
  - `Mod.cs` — `KMod.UserMod2` entry point; registers PLib options.
  - `Patches.cs` — the Harmony patches that drive the main-menu JSON pass. Transpilers inject
    collection calls into the game's own registration methods (e.g.
    `EntityConfigManager.RegisterEntity`).
  - `Export*.cs` — one exporter per output file, all deriving from `BaseExport`, which owns
    JSON serialization and path resolution.
  - `model/` — ~90 plain DTOs (`B*` = core records, `Out*` = per-component payloads). These
    types *are* the export schema; a field added here appears in the JSON.
  - `building/` — the hi-res building-image render path. Has its own
    [CLAUDE.md](OniExtract2024/building/CLAUDE.md) — read it before touching the render or
    `uiImageRect`.
  - `connection/` — the 16-state connection-sprite export for wires/pipes/rails/tiles.
- **`OniExtract2024.Core/`** — netstandard2.0, **no Unity or ONI reference**. The only code CI
  can verify. Currently `ExportPaths` and `UiImageRect`. Pure logic belongs here.
- **`OniExtract2024.Core.Tests/`** — xunit, runs anywhere. 6 tests.
- **`OniExtract2024.Tests/`** — xunit, net48, references the game DLLs. Runs only on a machine
  with ONI installed. 13 tests.

### Three independent export paths

They are separate, run at different times, and are easy to confuse:

| # | Path | Trigger | Writes |
|---|---|---|---|
| 1 | JSON data + UI icons | Automatic, when the game reaches the **main menu**. No save needed. | 13 JSON files in `export/database/`, one PNG per building/item in `export/ui_image/` |
| 2 | Building images | Manual: load any colony, **Esc** → *Export Building Images* | Re-renders buildings at 200 px/cell, **overwriting** path 1's low-res icons; writes `uiImageRect` |
| 3 | Connection sprites | Manual: load any colony, **Esc** → *Export Connection Sprites* | `export/connection_sprites/{prefabId}/{0..15}.png` |

Path 1 runs on **every game load** and authors `building.json` from scratch. Paths 2 and 3 are
one-shot tools. Run 1 before 2.

## Development Commands

### Build and deploy

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "OniExtract2024\OniExtract2024.csproj" /p:Configuration=Debug /v:minimal
```

The `CopyModsToDevFolder` post-build target copies the DLLs and YAML to
`<ModFolder>\OniExtract2024_dev\` (default `Documents\Klei\OxygenNotIncluded\mods\dev`). There
is no separate install step.

Both `OniExtract2024.csproj` and `OniExtract2024.Tests.csproj` hardcode `<GameLibsFolder>`. If
the game lives elsewhere, both must be edited — they are tracked files, so that edit shows up
as a dirty diff.

### Tests

```bash
dotnet test OniExtract2024.Core.Tests/OniExtract2024.Core.Tests.csproj   # anywhere; 6 tests
dotnet test OniExtract2024.Tests/OniExtract2024.Tests.csproj             # needs ONI installed; 13 tests
```

### Probing the game assembly

`ilspycmd` is a dotnet global tool; use it to confirm field names and signatures before writing
patch or export code. Runtime reflection overflows the Unity stack — decompile instead. Full
recipe and a large body of findings: [docs/GAME_INTERNALS.md](docs/GAME_INTERNALS.md).

## Gotchas that cost real time

- **Close ONI before building.** The post-build copy fails with `MSB3027 / MSB3021 … a file
  with a user-mapped section open` when the game has the mod DLLs loaded. The compile succeeds
  and only the deploy fails, so it looks like a build error but isn't. To check that the code
  *compiles* without closing the game, redirect the deploy to a scratch directory:
  `-p:ModFolder=<some-temp-dir>`. Do not kill the user's game process to make a build pass.
- **Fully restart ONI after every rebuild.** Mod DLLs are loaded once at startup and held for
  the session. Re-running the export without a restart silently uses the *old* code — the most
  expensive mistake available in this repo, because everything appears to work.
- **Never play the `"ui"` animation before a snapshot.** It renders at atlas/icon scale
  (~100 px/cell) instead of live-kanim scale and silently shrinks every output.
- **Deprecated buildings are skipped except a vetted allowlist.** Spawning deprecated content
  without full game context corrupts state and crashes the sweep (the exact culprit was never
  isolated). `SteamTurbine` is opted back in. Vet any addition by spawning it in isolation.
- **Player names are not prefab IDs.** Icons are named by prefab ID — the Auto-Sweeper is
  `SolidTransferArm`; there is no `AutoSweeper.png`.
- **Read from `buildingDef.BuildingComplete`, never a spawned instance.** Anything set in
  `prefabInitFn` / `prefabSpawnFn` / `OnSpawn()` exists only on live objects and is invisible to
  the export. See the lifecycle table in [docs/GAME_INTERNALS.md](docs/GAME_INTERNALS.md).
- **Progress and errors go to `Player.log`**, on lines prefixed `OniExtract:`. That is the
  primary debugging surface for all three export paths.

## Export contract invariants

Breaking one of these breaks the website silently — the site renders, just wrongly.

- **`utilities[]` is the authoritative port list** and must keep carrying every connection type
  with cell offsets. The `powerInputOffset` / `energyConsumer` / `battery` fields are
  *additive*; they cover power only and must never be treated as a replacement. This was
  regressed once already ([docs/archive/EXPORT_REGRESSION.md](docs/archive/EXPORT_REGRESSION.md)).
- **`viewMode` emits the game-native overlay ID string** via an `OverlayModes.*.ID` lookup, and
  `null` when there is no special overlay — not a `HashedString` hex, not a pluralized name.
- **`uiImageRect` is omitted when absent, never emitted as null.**
- **The `model/` DTOs are the schema.** Renaming a field there is a breaking change to the
  website. Update [docs/EXPORT_SCHEMA.md](docs/EXPORT_SCHEMA.md) in the same commit.

## Documentation layout

- **`docs/`** — durable reference, kept current:
  - [EXPORT_SCHEMA.md](docs/EXPORT_SCHEMA.md) — field-level schema for every JSON file.
  - [GAME_INTERNALS.md](docs/GAME_INTERNALS.md) — knowledge base for the ONI assembly: building
    config lifecycle, port taxonomy, logic gates, assembly probing. **Potentially stale after
    any game update** — verify with `ilspycmd` before relying on it.
  - [FRONTEND_INTEGRATION.md](docs/FRONTEND_INTEGRATION.md) — connection/electrical data as the
    front end consumes it.
  - [WEBSITE_POSTPROCESSING.md](docs/WEBSITE_POSTPROCESSING.md) — the export↔website contract.
    Its authoritative source is the consuming repo, so this copy drifts; treat the website repo
    as truth on conflict.
- **`docs/archive/`** — resolved diagnostics kept for provenance. **Not current state.** Their
  durable conclusions are already folded into `docs/` and the invariants above; read them only
  for the "why".
- **`agent/`** — dated working notes that go stale fast. Do not treat a status heading here as
  current without checking the code:
  - [SESSION_NOTES.md](agent/SESSION_NOTES.md) — session-by-session progress on the building
    pose inspector, including items awaiting in-game confirmation.
  - [BUILDING_POSE_WORKLIST.md](agent/BUILDING_POSE_WORKLIST.md) — 449-building manual checklist.
- **Subsystem guides** live next to the code they describe and load automatically when Claude
  Code reads that directory: [OniExtract2024/building/CLAUDE.md](OniExtract2024/building/CLAUDE.md).
- **`specs/`** — gitignored. Working plans and implementation drafts go here, not in `docs/`.

## Work Session Lifecycle

**Session start:**

1. `git fetch origin master`
2. Create a branch from `origin/master`. Never work on `master`.

**Committing:** commit autonomously at every logical break point — do not pause to ask.
Conventional commits (`feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`), subject ≤72
chars, body when the *why* is non-obvious. Append a `Co-Authored-By` trailer naming the current
model. Stage only relevant files — never `git add -A` blindly. Do not skip hooks.

**Session end:** push and open a **draft** PR without asking. The description states what
shipped, design decisions, and how it was verified — **leading with what was not verified**,
which for this repo usually means "not run in-game". A PR leaves draft only when a human has
confirmed it against the running game; never undraft it yourself.

## Important Instructions

Do what has been asked; nothing more, nothing less.
NEVER create files unless they're absolutely necessary for achieving your goal.
ALWAYS prefer editing an existing file to creating a new one.
NEVER proactively create documentation files (*.md) or README files. Only create documentation files if explicitly requested by the User.
