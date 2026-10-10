English | [简体中文](README_cn.md)

# OniExtract2024

Dumps game data and images from the game **Oxygen Not Included**, for the
[blueprintnotincluded](https://github.com/blueprintnotincluded/blueprintnotincluded) website.

Last built and verified in game against ONI **U59-744825** (2026-10-03), using the MSBuild that
ships with Visual Studio 2026 (version 18). The mod links against the game's own assemblies, so
it is rebuilt against whatever build is installed — there is no pinned game version to roll
back to, and a DLL built against an older game build can crash a newer one.

New here? Start with [CONTRIBUTING.md](CONTRIBUTING.md).

## Build

1. The game's `Managed` folder (`<GameLibsFolder>`) and the mod deploy folder (`<ModFolder>`) are
   set in `Directory.Build.props`, with defaults for a standard Steam install.
2. If yours differ, do not edit that file. Create `Directory.Build.user.props` beside it (it is
   gitignored) and set the properties there; the comment at the top of `Directory.Build.props`
   shows the shape.
3. Run MSBuild from the terminal:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "OniExtract2024\OniExtract2024.csproj" /p:Configuration=Debug /v:minimal
```

The post-build step (`CopyModsToDevFolder` in the csproj) automatically copies the DLLs and YAML
files to `<ModFolder>\OniExtract2024_dev\`. Default: `Documents\Klei\OxygenNotIncluded\mods\dev`.

## Rebuild and redeploy after a code change

1. Make your source edits.
2. Run the same MSBuild command above. The DLL is automatically deployed to the mod folder.
3. **Fully close and relaunch ONI.** The game loads mod DLLs once at startup and holds them in
   memory for the entire session — running the export after a rebuild without restarting will
   silently use the old code.

## Install

### From Build

1. Build project.
2. Check your mod installation. default path: `Documents\Klei\OxygenNotIncluded\mods\dev`.

### From Releases

This repository publishes no releases; build from source. (The upstream project it was forked
from, [cnctemaR/OniExtract2024](https://github.com/cnctemaR/OniExtract2024), has releases of
the original mod, which predate the building-image and connection-sprite tools and the website
contract described here.)

Enable the mod in game and restart. Load any colony, open the pause screen (Esc) and click
**Export Game Data**. Output will be in `Documents\Klei\OxygenNotIncluded\export`.

## What it exports

The mod has three independent export paths, each a button on the pause screen of a loaded
colony. Nothing is exported when the game starts.

1. **Game data** — pause screen → **Export Game Data**. Writes 13 JSON files plus one PNG
   icon per building/item.
2. **Building images** — a separate in-game tool (pause screen → **Export Building Images**)
   that re-renders every buildable building at 200 px/cell via live kanim camera snapshot,
   overwriting the low-res atlas icons from path 1. Run this *after* path 1. See
   [Building images](#building-images) below.
3. **Connection sprites** — a separate in-game tool (pause screen → **Export Connection
   Sprites**) that renders the 16 connection states of each connectable building. See
   [Connection sprites](#connection-sprites) below.

The pause screen has a third button, **Inspect Building Poses**. It exports nothing by itself:
it is the tool for choosing which animation and frame path 2 renders a building in, and it
saves those choices to `export/pose_overrides.json`.

## Output Result

Go to `Documents\Klei\OxygenNotIncluded\export` . Directory tree:

```
export
├─ database
│    ├─ attribute.json
│    ├─ building.json
│    ├─ db.json
│    ├─ elements.json
│    ├─ entities.json
│    ├─ food.json
│    ├─ geyser.json
│    ├─ items.json
│    ├─ multiEntities.json
│    ├─ po_string.json
│    ├─ recipe.json
│    ├─ tags.json
│    └─ uiSpriteInfo.json
├─ ui_image                       one PNG icon per building/item
├─ ui_image_facade                facade / clothing / permit sprites
│    ├─ ArtableStages
│    ├─ BalloonArtistFacades
│    ├─ BuildingFacades
│    ├─ ClothingItems
│    ├─ EquippableFacades
│    ├─ MonumentParts
│    └─ StickerBombs
├─ connection_sprites             written by the pause-screen tool, not the game-data export
│      └─ {prefabId}
│             ├─ 0.png ... 15.png  16 connection states per connectable
├─ ui_image_rects.json            measured icon rects, written by the building-image tool
└─ pose_overrides.json            pose choices saved from the pose inspector (only if used)
```

`ui_image_rects.json` is part of what the website reads (it is the only place terrain features'
rects live). `pose_overrides.json` is an input to this mod, not something the website consumes.

Field-level schema for every JSON file: see [docs/EXPORT_SCHEMA.md](docs/EXPORT_SCHEMA.md).
Website-side guidance for the rocket-module stacking data and blueprint-setting ranges in
`building.json`: [docs/WEBSITE_ROCKET_MODULES.md](docs/WEBSITE_ROCKET_MODULES.md).

## Building images

The game-data export writes `ui_image/` icons from the game's pre-baked UI atlas sprites —
small menu thumbnails whose resolution cannot be improved by cropping. The building-images
tool replaces them with live kanim renders at 200 px/cell.

**Run order:** run **Export Game Data** first (all icons at low res), then run **Export
Building Images** in-game to overwrite the building PNGs with hi-res versions. Non-building
icons (elements, items, critters, facades) are only written by the game-data export and are
not affected.

To run: build + deploy the mod, launch ONI, load any colony or sandbox, open the pause
screen (Esc), and click **Export Building Images**. A banner at the top of the screen says the export has
started and is replaced by a summary when it finishes (about a minute); progress is logged to
`Player.log` (lines prefixed `OniExtract:`). Only one of the two pause-screen exports runs at a
time — the second is refused, with a message, until the first has finished. The tool filters to `ShowInBuildMenu && !Deprecated`
buildings, so deprecated and dev-only entries are skipped automatically.

### Implementation notes

Non-obvious facts behind the render path
([BuildingImageSnapshotter.cs](OniExtract2024/building/BuildingImageSnapshotter.cs),
[ExportBuildingImages.cs](OniExtract2024/building/ExportBuildingImages.cs)) — the code comments
carry the full detail:

- **Never play the `"ui"` animation before a snapshot.** It renders at atlas/icon scale
  (~100 px/cell), not live-kanim scale, and silently shrinks every output. The tool poses each
  building in its most *active* state (e.g. `generating_loop`) seeked to a mid-loop frame instead.
- **Deprecated buildings are skipped except a vetted allowlist.** Spawning deprecated content
  without full game context corrupts state and crashes the sweep (exact culprit never isolated).
  `SteamTurbine` is opted back in because the website still shows it; vet any addition by spawning
  it in isolation first.
- **Rocket modules need a `CraftModuleInterface` ancestor.** The sweep attaches the cluster
  components to the world object so DLC rocket modules bind instead of null-ref'ing on spawn;
  `RocketModuleCluster` buildings are filtered out entirely.
- **Player names ≠ prefab IDs.** Icons are named by prefab ID (`building.json` `name`) — the
  Auto-Sweeper is `SolidTransferArm`, there is no `AutoSweeper.png`.
- **Material tint is captured by the render path.** Buildings that derive tint from their
  construction element (tiles, Tempshift Plate) render in the neutral debug element's colour,
  since the sweep builds every def from `Unobtanium`. Cosmetic, deferred.

The render writes a per-building `uiImageRect` (cell-space, footprint-relative) into
`building.json` so the website can place tight-cropped icons without squishing overhang — see
[docs/WEBSITE_POSTPROCESSING.md](docs/WEBSITE_POSTPROCESSING.md). Terrain features (geysers,
vents, volcanoes, the oil reservoir) get the same render and a measured rect, but only in
`ui_image_rects.json`, since they have no `building.json` entry. The website reads that file
directly — see "ui_image_rects.json" in [docs/EXPORT_SCHEMA.md](docs/EXPORT_SCHEMA.md).

**Open item:** spot-check `uiImageRect` placement on the website against the still-untested
branches of the rect math — an even-width building (validates the +0.5 horizontal centring), a
side-overhang building (non-zero `x`), an above-footprint overhang (tall art, `h > H`), and a
plain footprint-filling box (should read `x≈0, y≈0, w≈W, h≈H`).

## Connection sprites

A connectable building (wire, pipe, rail, tile) renders differently depending on which of its
four neighbours it connects to. The tool exports one PNG per state, named by a 4-bit bitmask:

```
left = 1, right = 2, up = 4, down = 8
export/connection_sprites/{prefabId}/{bitmask}.png   (bitmask 0–15)
```

`15.png` is connected on all four sides. There are two rendering paths, selected by building type:

| Type | Flag | Mechanism | Code |
|---|---|---|---|
| Utilities (wires/pipes/rails) | `isUtility` | Spawn a temp instance, snapshot each kanim state with the camera, then crop to one cell-centred square | [ConnectionSpriteSnapshotter.cs](OniExtract2024/connection/ConnectionSpriteSnapshotter.cs) |
| Tiles | `isKAnimTile` | Resample the building's `BlockTileAtlas` into a fixed 1.5-cell canvas (cell centred) reproducing the game's geometry: connected edges trim flush to the cell boundary, disconnected edges overhang it by ¼ cell so caps bleed into the neighbour and tiles join seamlessly (no placement needed). `15.png` fills exactly the centre cell — the website's scale reference | [TileConnectionExtractor.cs](OniExtract2024/connection/TileConnectionExtractor.cs) |

To run: build + deploy the mod, launch ONI, load any colony or sandbox, open the pause screen
(Esc), and click **Export Connection Sprites**. A banner at the top of the screen says the export has started
and is replaced by a summary when it finishes (about ten seconds). Progress and the output path
are logged to `Player.log` (lines prefixed `OniExtract:`).

Bridges (wire, pipe, rail, logic) are deliberately not in this export: a bridge has one fixed
sprite and does not redraw to match its neighbours, so its `ui_image` icon is all there is.

## Downstream use

This export is consumed by the **blueprintnotincluded** website. The ingestion work — the scripts
that eat this export — lives in the consuming repo
([blueprintnotincluded/blueprintnotincluded](https://github.com/blueprintnotincluded/blueprintnotincluded),
ingestion in [PR #90](https://github.com/blueprintnotincluded/blueprintnotincluded/pull/90)).

[docs/WEBSITE_POSTPROCESSING.md](docs/WEBSITE_POSTPROCESSING.md) records the export↔website contract — what
the website reads, the framing rules, and what must not break. Its authoritative source is the
consuming repo, so the two will drift over time; the copy here is a working snapshot kept beside
the exporter so export-side changes can be checked against it without leaving this repo.

## Documentation

| Where | What |
|---|---|
| [CONTRIBUTING.md](CONTRIBUTING.md) | How to build, test and send a change, and what CI does and does not check. |
| [CLAUDE.md](CLAUDE.md) | Canonical repository guide — architecture, build/deploy, game-assembly gotchas, export contract invariants. Written for coding agents, but the commands and constraints are the same for humans. |
| [AGENTS.md](AGENTS.md) | Entry point for coding agents; defers to `CLAUDE.md`. |
| [docs/](docs/) | Durable reference: [EXPORT_SCHEMA.md](docs/EXPORT_SCHEMA.md) (field-level JSON schema), [GAME_INTERNALS.md](docs/GAME_INTERNALS.md) (ONI assembly knowledge base), [FRONTEND_INTEGRATION.md](docs/FRONTEND_INTEGRATION.md), [WEBSITE_POSTPROCESSING.md](docs/WEBSITE_POSTPROCESSING.md), the per-feature website handoffs (`WEBSITE_*.md`), [AREA_OF_EFFECT.md](docs/AREA_OF_EFFECT.md). |
| [docs/archive/](docs/archive/) | Resolved diagnostics, kept for provenance. Not current state. |
| [agent/](agent/) | Dated working notes that go stale fast — session progress and the manual building-pose worklist. |
| [OniExtract2024/building/CLAUDE.md](OniExtract2024/building/CLAUDE.md) | Design notes for the hi-res building-image render path, next to the code. |

Local implementation plans belong in `specs/`, which is gitignored.
