# Drains (1866754178) — extraction notes

**Author style:** vanilla (`BuildingTemplates.CreateBuildingDef` + field assignments),
with PLib options for user settings. Helper lib: SkyLib (embedded).

**Buildings:** `Drain` (1 — but with an option-selected kanim variant, see below).

## Where each value comes from

| Field | Source |
|---|---|
| Core def (1x1, HP 100, 30 s, TIER2 mass, ALL_METALS, 1600 K, Tile, PENALTY.TIER0, NONE) | `DrainConfig.CreateBuildingDef()` positional args |
| `materialMass` | `BUILDINGS.CONSTRUCTION_MASS_KG.TIER2` = [100] |
| `materialCategory` | `MATERIALS.ALL_METALS` = ["Metal"] |
| `isFoundation` | explicit `IsFoundation = true` + `CreateFoundationTileDef` |
| `sceneLayer` | `Grid.SceneLayer.TileMain` = 30 |
| `viewMode` | `OverlayModes.LiquidConduits.ID` → "LiquidConduit" |
| Liquid output port | `OutputConduitType = Liquid`, `UtilityOutputOffset = (0,0)` → LiquidOutput (0,0) |
| `storage` | `DoPostConfigureComplete`: `Storage.capacityKg = 1` |
| Intake | `ElementConsumer` AllLiquid, rate = FlowRate option (default 0.1 kg/s), radius 1 |
| `initialAnim` | `KBatchedAnimController.initialAnim = "built"` |
| Strings | `DrainPatch.Db_Initialize_Patch.Prefix` → `OniUtils.AddBuildingStrings` |
| Plan menu / tech | `DrainPatch` Postfix: `AddBuildingToBuildMenu("Plumbing", "Drain")` (appended, no subcategory → "uncategorized"), `AddBuildingToTech("SanitationSciences", "Drain")` |

## Option-dependent values (defaults committed)

`DrainOptions` (PLib, `[RestartRequired]`):

- `UseSolidDrain` (default **false**) — false: `drain_kanim`, `SimCellOccupier.doReplaceElement=false`.
  true: `solidDrain_kanim`, `isSolidTile=true`, `doReplaceElement=true`, samples the cell **above** `(0,1)`.
  The kanim is chosen **inside `CreateBuildingDef`** — the two variants are the same building ID.
- `FlowRate` (default **0.1**, range 0.1–1.0 kg/s) — `ElementConsumer.consumptionRate`.

## Quirks

- Sets `UseStructureTemperature = false` twice; `AudioSize = "small"` twice — harmless decompile noise.
- No power. `EnergyConsumptionWhenActive` explicitly 0 → `energyConsumer: null`, no PowerInput port.
- `BuildingHP.destroyOnDamaged = true` — breaks permanently instead of needing repair.

## How to update after a mod/game update

1. `tools\Refresh-ModSources.ps1` → diff `DrainConfig.cs`, `DrainOptions.cs`, `DrainPatch.cs`.
2. Re-check option defaults in the `DrainOptions` constructor (they define the committed values).
3. Update `buildings.json`, run `tools\Build-ModDatabase.ps1`.
