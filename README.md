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

The mod has two independent export paths:

1. **JSON data + UI icons** — runs automatically when the game reaches the main menu (no
   save required). Writes 13 JSON files plus one PNG icon per building/item.
2. **Connection sprites** — a separate in-game tool, triggered from a button on the pause
   screen (Esc), that renders the 16 connection states of each connectable building. It must
   run inside a loaded game/sandbox because connectables can only be rendered there. See
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
| Tiles | `isKAnimTile` | Resample the building's `BlockTileAtlas` into a cell-anchored frame with the game's trim/overhang geometry so tiles join seamlessly (no placement needed) | [TileConnectionExtractor.cs](OniExtract2024/connection/TileConnectionExtractor.cs) |

To run: build + deploy the mod, launch ONI, load any colony or sandbox, open the pause screen
(Esc), and click **Export Connection Sprites**. Progress and the output path are logged to
`Player.log` (lines prefixed `OniExtract:`).

## Downstream use

[WEBSITE_POSTPROCESSING.md](WEBSITE_POSTPROCESSING.md) describes how a website consumes this
export. That document was authored in the consuming repo and is kept here only as a reference
to be validated; it will eventually be replaced by a link to that repo.