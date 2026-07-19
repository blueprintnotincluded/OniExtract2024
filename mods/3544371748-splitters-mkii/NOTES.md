# Splitters MK II (3544371748) — extraction notes

**Author style:** vanilla configs, three near-identical classes differing only in conduit
type/overlay/plan category. No options. Strings in `MJSplittersMKIIStrings`; registration in
`MJSplittersMKII.Patch.Patches`.

**Buildings (3):** `MJSplitterMKIIGas` (AeroSplitter Mk.II), `MJSplitterMKIILiquid`
(AquaSplitter Mk.II), `MJSplitterMKIISolid` (SolidSplitter Mk.II).

## Where each value comes from

| Field | Source |
|---|---|
| Core def (3x1, HP 30, 10 s, TIER3 mass, REFINED_METALS, 1600 K, Anywhere, PENALTY.TIER0, NOISY.TIER1) | each `Create BuildingDef()` — identical across the three |
| `kanim` | `ID.ToLowerInvariant() + "_kanim"` → `mjsplittermkiigas_kanim` etc. |
| Power | `RequiresPowerInput = true`, 240 W; `PowerInputOffset` **not set** → default (0,0) |
| Ports | `InputConduitType`/`OutputConduitType` same type; in (-1,0), out (1,0) |
| Secondary (filtered) port | `MJFilter.portInfo = ConduitPortInfo(type, (0,0))` (added in `ConfigureBuildingTemplate`; `MJFilter` extends the game's secondary-output mechanism) → `<Type>Output` at (0,0) with `isSecondary: true` — same pattern as vanilla Gas/Liquid/Solid Filter |
| Multi-select filtering | `TreeFilterable` + `Storage` (capacityKg 0, storageFilters per type) in `DoPostConfigureComplete` |
| Strings | `Patches.GeneratedBuildings_LoadGeneratedBuildings_Patch` (NAME uses `UI.FormatAsLink`) |
| Plan / tech | `Patches.Db_Initialize_Patch`: gas→HVAC + AdvancedFiltration, liquid→Plumbing + AdvancedFiltration, solid→Conveyance + SolidManagement (no subcategory → "uncategorized") |

## Quirks

- The three configs are copy-paste clones — when the mod updates, diff one fully and then
  just spot-check the other two for the type-specific lines.
- `viewMode` per variant: GasConduit / LiquidConduit / **SolidConveyor**.
- The tech patch has a fallback path for the old `TECH_GROUPING` dictionary; on current game
  versions the `unlockedItemIDs` path is what runs.

## How to update after a mod/game update

1. `tools\Refresh-ModSources.ps1` → diff the 3 configs + `Patches.cs` + strings.
2. Update `buildings.json`, run `tools\Build-ModDatabase.ps1`.
