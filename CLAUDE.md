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

The canonical repo is
[blueprintnotincluded/OniExtract2024](https://github.com/blueprintnotincluded/OniExtract2024).
Contributors may work from a fork, in which case that is `origin` and the canonical repo is
`upstream`; check `git remote -v` rather than assuming.

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
  - `Patches.cs` — load-time Harmony patches that only *capture* prefabs as the game registers
    them (transpilers inject a call into e.g. `EntityConfigManager.RegisterEntity`). They read
    nothing and write nothing; keep it that way, since a throw there strands the player on the
    loading screen.
  - `ExportGameData.cs` — path 1 below: reads what `Patches.cs` captured plus the game's own
    registries and writes the JSON files, one exporter per frame.
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
  with ONI installed. 48 tests.
- **`tools/`** — PowerShell helpers that run outside the game. `Test-Export.ps1` validates an
  export and diffs it against a snapshot (see "Validating an export" below);
  `Parse-KanimBuild.ps1` dumps the symbol table of a kanim `_build.bytes` file. Mod buildings
  need no tooling: run the normal export with the mods enabled and they are exported like any
  other building.

### Three independent export paths

All three are buttons on the pause screen of a loaded colony. Nothing is exported at boot:
exporting is something the player does on purpose, after the game has loaded far enough that
a crash in the export is not a crash in the loading screen.

| # | Path | Trigger | Writes |
|---|---|---|---|
| 1 | Game data | Manual: load any colony, **Esc** → *Export Game Data* | 13 JSON files in `export/database/`, one PNG per building/item in `export/ui_image/` |
| 2 | Building images | Manual: load any colony, **Esc** → *Export Building Images* | Re-renders buildings and terrain features (geysers, vents, volcanoes) at 200 px/cell, **overwriting** path 1's low-res icons; writes `uiImageRect` |
| 3 | Connection sprites | Manual: load any colony, **Esc** → *Export Connection Sprites* | `export/connection_sprites/{prefabId}/{0..15}.png` |

Path 1 authors `building.json` from scratch every time it runs. Run 1 before 2: path 2 patches
`uiImageRect` into the `building.json` that path 1 wrote.

## Development Commands

### Build and deploy

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "OniExtract2024\OniExtract2024.csproj" /p:Configuration=Debug /v:minimal
```

The `CopyModsToDevFolder` post-build target copies the DLLs and YAML to
`<ModFolder>\OniExtract2024_dev\` (default `Documents\Klei\OxygenNotIncluded\mods\dev`). There
is no separate install step.

`<GameLibsFolder>` and `<ModFolder>` are defined once, in `Directory.Build.props`, for both
projects that link against the game. If the game lives elsewhere, set them in a gitignored
`Directory.Build.user.props` beside it rather than editing a tracked file.

### Tests

```bash
dotnet test OniExtract2024.Core.Tests/OniExtract2024.Core.Tests.csproj   # anywhere; 6 tests
dotnet test OniExtract2024.Tests/OniExtract2024.Tests.csproj             # needs ONI installed; 48 tests
```

### Validating an export

```powershell
.\tools\Test-Export.ps1 -Snapshot   # BEFORE the in-game run: copy the export to export-baseline
.\tools\Test-Export.ps1             # AFTER it: invariants, spot checks, diff against the baseline
```

The game overwrites the export in place, so the snapshot has to be taken first. The second
command checks the contract invariants below and the documented spot checks, then compares
every JSON value and every PNG with the snapshot. A behaviour-neutral change should come back
`RESULT: clean`. Renders are not byte-stable between runs, so images are compared by size,
outline and colour rather than by hash. An export where only path 1 has run fails
the checks for `ui_image_rects.json` and `connection_sprites`; pass `-AllowPartialExport` if that
is what you meant to check. When a new field gets a spot check in the docs, add it to the script
as well.

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
- **`dotnet test OniExtract2024.Tests` builds and deploys the mod too.** The test project
  references the mod project, so the same post-build copy runs: it overwrites `mods\dev` with
  whatever branch is checked out, and fails the same way while the game is running. Pass
  `-p:ModFolder=<some-temp-dir>` to `dotnet test` as well to leave the deployed mod alone.
- **Fully restart ONI after every rebuild.** Mod DLLs are loaded once at startup and held for
  the session. Re-running the export without a restart silently uses the *old* code — the most
  expensive mistake available in this repo, because everything appears to work.
- **Never play the `"ui"` animation before a snapshot.** It renders at atlas/icon scale
  (~100 px/cell) instead of live-kanim scale and silently shrinks every output.
- **Deprecated buildings are never spawned, except a vetted allowlist.** Spawning deprecated
  content without full game context corrupts state and crashes the sweep (the exact culprit was
  never isolated). `SteamTurbine` is opted in. Vet any addition by spawning it in isolation.
  Deprecated buildings, rocket modules and anything else that cannot be spawned are rendered
  art-only instead. See [OniExtract2024/building/CLAUDE.md](OniExtract2024/building/CLAUDE.md).
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
- **`ui_image_rects.json` at the export root is read by the website**, not just by this mod. It
  is the only route by which terrain features' rects reach the site. Keep its location and its
  shape (`prefabId → {x, y, w, h}`).
- **Cell offsets are measured from the building's origin cell, not its bottom-left corner.**
  That covers `utilities[].offset`, `attachPoints`, `attachablePosition` and
  `areasOfEffect[].origin`. The origin is the bottom row at column `floor((width-1)/2)`, so it
  is the bottom-centre cell for odd widths. See "Offset conventions" in
  [docs/GAME_INTERNALS.md](docs/GAME_INTERNALS.md).
- **The `model/` DTOs are the schema.** Renaming a field there is a breaking change to the
  website. Update [docs/EXPORT_SCHEMA.md](docs/EXPORT_SCHEMA.md) in the same commit.

## Documentation layout

- **[CONTRIBUTING.md](CONTRIBUTING.md)** — the human-facing entry point: setup, what CI covers,
  the pull-request flow and the comment convention.
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
  - [WEBSITE_ROCKET_MODULES.md](docs/WEBSITE_ROCKET_MODULES.md) — website handoff for
    rocket-module stacking and settings ranges. Still open: the importer does not read that
    data yet. A handoff moves to `docs/archive/` once the website has acted on it.
  - [AREA_OF_EFFECT.md](docs/AREA_OF_EFFECT.md) — how `areasOfEffect[]` is derived.
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
