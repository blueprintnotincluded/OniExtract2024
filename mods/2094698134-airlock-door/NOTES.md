# Airlock Door (2094698134) — extraction notes

**Author style:** PeterHan / PLib (`PBuilding` object initializer). PLib is ILMerged into
`AirlockDoor.dll`, so the PLib helper classes decompile out of the same DLL.

**Buildings:** `PAirlockDoor`, `PAirlockDoorInsulated` (2).

## Where each value comes from

| Field | Source |
|---|---|
| Everything in the `PBuilding { ... }` initializer | `AirlockDoorConfig.CreateBuilding()` / `AirlockDoorInsulatedConfig.CreateBuilding()` |
| `materialMass` | `BuildIngredient(name, tier)` → `CONSTRUCTION_MASS_KG.TIER<tier>[0]` (see `PeterHan.PLib.Buildings.BuildIngredient`). Airlock: RefinedMetal tier 4 = 400. Insulated: Insulator tier 4 = 400 + RefinedMetal tier 2 = 100 |
| `meltingPoint` | **Fixed 2400** — `PBuilding.CreateDef()` always passes 2400 to `CreateBuildingDef` |
| `isFoundation`, `thermalConductivity`, `sceneLayer` overrides | `CreateBuildingDef()` post-`CreateDef` assignments in each config |
| `overheatable` | `false` — PLib sets `Overheatable=false` whenever `OverheatTemperature` is null |
| `viewMode` | `null` — PLib default `OverlayModes.None.ID`; **`PowerInput` does NOT flip it to Power** |
| Power port | `PowerInput = new PowerRequirement(120f, CellOffset(0,0))` → `RequiresPowerInput`, 120 W, offset (0,0) |
| Logic port | `LogicIO = { LogicPorts.Port.InputPort(..., CellOffset.none, ...) }` → LogicInput at (0,0) |
| `initialAnim` | `DoPostConfigureComplete`: `KBatchedAnimController.initialAnim = "closed"` (def's `DefaultAnimState` stays "off") |
| Names/desc/effect | `AirlockDoorStrings.BUILDINGS.PREFABS.*` LocStrings (strip `UI.FormatAsLink` markup) |
| Plan menu | `Category`/`SubCategory`/`AddAfter` properties (Base / doors, after PressureDoor resp. InsulatedDoor) |
| `tech` | `Tech` property (ImprovedGasPiping / Catalytics) |

## Quirks

- The `AirlockDoor` component stores energy (capacity 10000 J, 1500 J per use) — it behaves
  like a mini internal battery. `EnergyConsumer` is present; the 120 W figure is the def's
  `EnergyConsumptionWhenActive` (charging draw).
- Both doors destroy `BuildingEnabledButton` (no enable/disable toggle).
- Door is 3x2, Unrotatable, placed in the Tile layer (acts as foundation).

## How to update after a mod/game update

1. `tools\Refresh-ModSources.ps1` flags the DLL change and re-decompiles `decompiled/`.
2. Diff `AirlockDoorConfig.cs` / `AirlockDoorInsulatedConfig.cs`: re-check the `PBuilding`
   initializer values and the `CreateBuildingDef()` overrides.
3. Diff `PBuilding.cs` for changed defaults (melting point, decor default, etc.) — only
   needed if PLib was updated inside the DLL.
4. Re-check tier→kg mapping against `mods/tuning_constants.json` (regenerate if the game
   updated).
5. Update `buildings.json`, then run `tools\Build-ModDatabase.ps1`.
