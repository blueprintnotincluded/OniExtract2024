# Buildable Natural Tile (1840755803) — extraction notes

**Author style:** vanilla (`BuildingTemplates.CreateBuildingDef`), config via a bespoke
`config.json` next to the DLL (CaiLib/CoolLib helpers, all merged into
`BuildableNaturalTile-merged.dll`).

**Buildings:** `NaturalTile` (1).

## Where each value comes from

| Field | Source |
|---|---|
| Core def (1x1, HP 100, Anywhere, BONUS.TIER0 decor, NONE noise, 1600 K) | `NaturalTileConfig.CreateBuildingDef()` |
| `constructionTime` | `Settings.BuildSpeed` — **config.json option, default 3** |
| `materialMass` | `Settings.BuildMass` — **config.json option, default 50** |
| `materialCategory` | `["Solid"]` — literally any solid element |
| Strings | `BuildableNaturalTilePatches.GeneratedBuildings_LoadGeneratedBuildings_Patch.Prefix` → `StringUtils.AddBuildingStrings` |
| Plan menu | same Prefix: appended to the end of the **Base** category (`PLANORDER[].data.Add`), no subcategory → "uncategorized" |
| `tech` | `Techs_Init_Patch`: added to the tech that unlocks **RationBox**. In the current game build that is `FarmingTech` (verified via `ilspycmd Assembly-CSharp.dll -t Database.Techs` → the `new Tech("FarmingTech", ...RationBox...)` entry). **Re-verify after game updates — this is an indirect reference.** |

## Quirks — this building is a self-deleting spawner

`BuildingComplete_OnSpawn_Patch`: when a `NaturalTileComplete` spawns, the mod immediately
replaces the cell with a **natural block** of the construction element
(mass = `Settings.BlockMass`, default 50, at build temperature), shoves any pickupables to a
neighboring cell, and **deletes the building object**. So:

- The "building" never persists; decor/overheatable/floodable values are effectively irrelevant in game.
- For the website, treat it as a 1x1 buildable that turns into a natural tile of the chosen element.
- It keeps default `floodable/entombable/overheatable = true` because the config never changes
  them — recorded as-is.
- `MakeBuildingAlwaysOperational` + `IgnoreDefaultKComponent(RequiresFoundation)` — placeable
  anywhere, always operational.

## How to update after a mod/game update

1. `tools\Refresh-ModSources.ps1` → diff `NaturalTileConfig.cs`, `BuildableNaturalTilePatches.cs`, `Config.cs`.
2. Re-check the defaults dictionary in `Config.Load()` (BuildMass/BlockMass/BuildSpeed).
3. Re-verify the RationBox→tech mapping against the game (`Database.Techs`).
4. Update `buildings.json`, run `tools\Build-ModDatabase.ps1`.
