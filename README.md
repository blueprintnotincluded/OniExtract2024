English | [简体中文](README_cn.md)

# OniExtract2024

Dumps game data and images from game **Oxygen Not Included**.

Compiled under **Visual Studio 2022** for ONI version **U51-600112**.

## SteamDB Info

Game Name: Oxygen Not Included

App ID: 457140

Depot ID: 457141

Manifest ID: 5347960185499743335

Rollback game in Steam console:

```
download_depot <AppID> <DepotsID> <ManifestID>
```

## Build

1. Open `OniExtract2024\OniExtract2024.csproj` in **Visual Studio Code**.
2. Check `<GameLibsFolder>` , adjust to your own game installation.
3. Check `<ModFolder>` , adjust to your mod installation.
4. Open `OniExtract2024.sln` in **Visual Studio**. Load up solution, click `Build`-`Build Solution`. 
5. Mod will be installed to your mod installation. default path: `Documents\Klei\OxygenNotIncluded\mods\dev`.

## Install

### From Build

1. Build project.
2. Check your mod installation. default path: `Documents\Klei\OxygenNotIncluded\mods\dev`.

### From Releases

1. Download package from **Releases**. Unzip package.
2. Copy **unzipped folder** to `Documents\Klei\OxygenNotIncluded\mods\dev` 

Enable the mod in game. Restart game. Output will be in `Documents\Klei\OxygenNotIncluded\export`.

## What it exports

The mod has three independent export paths:

1. **JSON data + UI icons** — runs automatically when the game reaches the main menu (no
   save required). Writes 13 JSON files plus one PNG icon per building/item.
2. **Building images** — a separate in-game tool (pause screen → **Export Building Images**)
   that re-renders every buildable building at 200 px/cell via live kanim camera snapshot,
   overwriting the low-res atlas icons from path 1. Run this *after* path 1. See
   [Building images](#building-images) below.
3. **Connection sprites** — a separate in-game tool (pause screen → **Export Connection
   Sprites**) that renders the 16 connection states of each connectable building. See
   [Connection sprites](#connection-sprites) below.

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
└─ connection_sprites             written by the pause-screen tool, not the main-menu export
       └─ {prefabId}
              ├─ 0.png ... 15.png  16 connection states per connectable
```

Field-level schema for every JSON file: see [EXPORT_SCHEMA.md](EXPORT_SCHEMA.md).

## Building images

The main-menu export writes `ui_image/` icons from the game's pre-baked UI atlas sprites —
small menu thumbnails whose resolution cannot be improved by cropping. The building-images
tool replaces them with live kanim renders at 200 px/cell.

**Run order:** run the main-menu export first (all icons at low res), then run **Export
Building Images** in-game to overwrite the building PNGs with hi-res versions. Non-building
icons (elements, items, critters, facades) are only written by the main-menu pass and are
not affected.

To run: build + deploy the mod, launch ONI, load any colony or sandbox, open the pause
screen (Esc), and click **Export Building Images**. Progress is logged to `Player.log`
(lines prefixed `OniExtract:`). The tool filters to `ShowInBuildMenu && !Deprecated`
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
[WEBSITE_POSTPROCESSING.md](WEBSITE_POSTPROCESSING.md).

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
(Esc), and click **Export Connection Sprites**. Progress and the output path are logged to
`Player.log` (lines prefixed `OniExtract:`).

## Downstream use

This export is consumed by the **blueprintnotincluded** website. The ingestion work — the scripts
that eat this export — lives in the consuming repo
([blueprintnotincluded/blueprintnotincluded](https://github.com/blueprintnotincluded/blueprintnotincluded),
ingestion in [PR #90](https://github.com/blueprintnotincluded/blueprintnotincluded/pull/90)).

[WEBSITE_POSTPROCESSING.md](WEBSITE_POSTPROCESSING.md) records the export↔website contract — what
the website reads, the framing rules, and what must not break. Its authoritative source is the
consuming repo, so the two will drift over time; the copy here is a working snapshot kept beside
the exporter so export-side changes can be checked against it without leaving this repo.