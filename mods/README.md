# Mod Building Extraction — Playbook

Offline, per-mod extraction of buildable-building data from installed mod files (DLL +
kanims), **without launching the game and without loading the mods together**. Research
background: [../MOD_OFFLINE_EXTRACTION.md](../MOD_OFFLINE_EXTRACTION.md).

The output is **`mods/mod_database.json`** — modded buildings kept separate from the
base-game `database/building.json` export, in a compatible-but-extended schema (below).

## Directory layout

```
mods/
  README.md                ← this playbook
  manifest.json            ← list of supported mods + tracked decompile types (drives the scripts)
  tuning_constants.json    ← game TUNING/enums lookup (regenerate on game updates)
  mod_database.json        ← GENERATED — merged output of all per-mod buildings.json
  <workshopId>-<slug>/
    NOTES.md               ← per-mod heuristics: where every value came from, quirks, update steps
    buildings.json         ← hand-extracted data for that mod (the reviewed source of truth)
    decompiled/            ← ilspycmd snapshots of the tracked types (diff base for updates)
    source-state.json      ← GENERATED — DLL sha256/date at last extraction
tools/
  Refresh-ModSources.ps1   ← detects mod updates, re-decompiles, shows what changed
  Build-ModDatabase.ps1    ← merges per-mod buildings.json → mods/mod_database.json
```

## The repeatable loop

### When a mod (or the game) updates

```powershell
.\tools\Refresh-ModSources.ps1          # add -Force to re-decompile everything
```

- `UNCHANGED` — DLL hash matches the last extraction; data is still valid. Done.
- `CHANGED` — the script re-decompiles the tracked types into `decompiled/`. Review
  `git diff mods/<dir>/decompiled`, follow the **"How to update"** checklist in that mod's
  `NOTES.md`, edit `buildings.json`, then:

```powershell
.\tools\Build-ModDatabase.ps1           # regenerate mods/mod_database.json
.\tools\Merge-ModExport.ps1             # re-merge into the game export for the website
```

### After every in-game re-export

The game rewrites `building.json` clean, so re-run the merge before handing the export
to the website:

```powershell
.\tools\Merge-ModExport.ps1             # default -ExportDir: Documents\Klei\OxygenNotIncluded\export
```

If the **game** updated, also regenerate `tuning_constants.json` values that changed
(`ilspycmd Assembly-CSharp.dll -t TUNING.BUILDINGS`, etc. — the `_meta.sourceTypes` list in
that file says exactly which types feed it).

### Adding a new mod

1. **Does it add buildables?** Decompile and scan for `IBuildingConfig`:
   ```powershell
   ilspycmd "<mod>.dll" -l c | Select-String Config       # candidates
   # definitive: decompile fully and grep for ": IBuildingConfig"
   ```
   ⚠️ Kanim count is NOT a reliable signal — Dupery Fixed / Duplicant Stat Selector ship
   kanims but add no buildables (cosmetics/UI art). `IBuildingConfig` is definitive.
2. **Read each config** (`ilspycmd "<mod>.dll" -t <FullTypeName>`) and fill a
   `buildings.json` using the schema below. Identify the authoring style first — it decides
   where values live (see "Authoring styles").
3. **Find the registration** (strings, plan menu, tech) — always in Harmony patches on
   `GeneratedBuildings.LoadGeneratedBuildings` and/or `Db.Initialize` (or, for PLib mods,
   in the `PBuilding` properties). Search decompiled sources for:
   `Strings.Add`, `AddBuildingToPlanScreen`, `PLANORDER`, `unlockedItemIDs`, `TECH_GROUPING`.
4. **Resolve constants** via `tuning_constants.json` (TUNING tiers, enums). Anything not in
   the table: decompile the game type and add it to the table.
5. **Find option defaults** — values may be computed from mod options
   (PLib `SingletonOptions<T>` constructor defaults, or a bespoke config.json). Commit the
   **default-options** values and record the formula in `notes` + NOTES.md.
6. Write `NOTES.md` (use an existing one as a template — the per-field source table and the
   update checklist are the point), add the mod to `manifest.json` with its tracked types,
   run both scripts.

## Authoring styles seen so far

| Style | Recognize by | Where the data lives | Example |
|---|---|---|---|
| **Vanilla** | `BuildingTemplates.CreateBuildingDef(...)` positional call | The call args + subsequent `buildingDef.X = ...` assignments + `ConfigureBuildingTemplate`/`DoPostConfigureComplete` components | Drains, HPA, Splitters, BNT |
| **PLib PBuilding** | `new PBuilding(id, name) { ... }` initializer | The initializer + `CreateBuildingDef()` post-`CreateDef` overrides. Mass via ingredient tier → `CONSTRUCTION_MASS_KG.TIER<n>[0]`; melting point fixed 2400; `Overheatable=false` unless `OverheatTemperature` set; `ViewMode` stays None even with PowerInput | Airlock Door |

`CreateBuildingDef` positional order (vanilla):
`id, width, height, anim, hitpoints, construction_time, construction_mass[], construction_materials[], melting_point, build_location_rule, decor, noise[, temp_mod_scale]`.

Unset fields take `BuildingDef` defaults (see `BuildingDefDefaults` in
`tuning_constants.json`): Floodable/Entombable/Overheatable **true**, ThermalConductivity 1,
DefaultAnimState "off", Unrotatable, SceneLayer Building(19), ObjectLayer Building(1),
ViewMode null, PowerInputOffset (0,0).

## utilities[] port rules (mirror the in-game exporter / FRONTEND_INTEGRATION.md)

- Plain transport segments (pipes/wires, `isUtility: true` 1x1 drag-build) → **empty** `utilities`.
- Bridges → one port per end: `<Type>Input` at `UtilityInputOffset`, `<Type>Output` at `UtilityOutputOffset`.
- `InputConduitType`/`OutputConduitType` + offsets → `<Gas|Liquid|Solid><Input|Output>` ports.
- `RequiresPowerInput` (or PLib `PowerInput`) → `PowerInput` at `PowerInputOffset` (default (0,0)) **plus** `energyConsumer.baseWattageRating = EnergyConsumptionWhenActive`. Draw of 0 still gets the port if `RequiresPowerInput` is true.
- `BuildingDef.LogicInputPorts` / PLib `LogicIO` / `LogicOperationalController.CreateSingleInputPortList` → `LogicInput`/`LogicOutput` at the port's cellOffset.
- Secondary ports (`ConduitSecondaryOutput`/`ISecondaryOutput` component `portInfo`, or filter-style components like `MJFilter`) → extra `<Type>Output` with `isSecondary: true`.

## buildings.json schema

Per building: the `building.json` `bBuildingDefList` field names where the concept matches
(`name`, `widthInCells`, `materialCategory[]`, `materialMass[]`, `isFoundation`, `isKAnimTile`,
`isUtility`, `dragBuild`, `buildLocationRule`, `permittedRotations`, `sceneLayer`,
`objectLayer`, `viewMode`, `defaultAnimState`, `energyConsumer`, `powerInputOffset`,
`utilities[]`, `elementConsumers[]`, `storage`) — enums as ints, `viewMode` as the exporter's
string names ("GasConduit" singular etc.), offsets pre-rotation from bottom-left.

**Extensions not present in base building.json** (mod-specific but useful):

| Field | Meaning |
|---|---|
| `nameString` | **plain** display name (no `<link>` markup, unlike base export) |
| `description`, `effect` | plain-text DESC/EFFECT strings |
| `hitpoints`, `constructionTime`, `meltingPoint`, `thermalConductivity` | def scalars |
| `overheatable`, `floodable`, `entombable` | def bools |
| `decor`, `noise` | `{amount, radius}` |
| `kanim` | kanim name for the art pipeline (`<kanim>` folder under the mod's `anim/assets/`) |
| `initialAnim` | non-null when the mod sets `KBatchedAnimController.initialAnim` (the pose to composite; `defaultAnimState` stays the def value) |
| `selfHeatKilowattsWhenActive` | only when non-zero |
| `planCategory`, `planSubCategory`, `addAfter` | build-menu placement ("uncategorized" when the mod appends without a subcategory) |
| `tech` | research tech ID |
| `configClass` | decompile pointer |
| `notes` | quirks + option-dependence formulas |

Storage is simplified to `{ "capacityKg": n }` — the full `OutStorage` shape from the
in-game export is runtime data we can't reliably reconstruct offline.

`mod_database.json` top level: `{ ExportFileName, generatedAt, mods: [per-mod metadata incl.
dllSha256], bBuildingDefList: [all buildings] }`.

## Current coverage (2026-07-18 survey of 28 installed mods)

Mods adding buildables — **5 mods, 15 buildings**, all extracted:

| Mod | Buildings |
|---|---|
| Airlock Door (2094698134) | PAirlockDoor, PAirlockDoorInsulated |
| Drains (1866754178) | Drain |
| Buildable Natural Tile (1840755803) | NaturalTile |
| High Pressure Applications (2806200110) | 2 pumps, 2 pipes, 2 bridges, 2 valves |
| Splitters MK II (3544371748) | Gas/Liquid/Solid splitters |

Confirmed **no buildables** (IBuildingConfig scan): True Tiles, Dupery Fixed, Duplicant Stat
Selector, Mass Move Tool, Blueprints Expanded, and all the remaining QoL/UI mods.

## Art / images

### Icons — DONE, `mods/images/<prefabId>.png`

```powershell
.\tools\Export-ModImages.ps1     # re-run after a mod update changes art
```

Extracts the **`ui` symbol** (build-menu icon) from each building's kanim atlas —
every mod kanim surveyed ships one. Kanim folder = `mods/Steam/<id>/anim/assets/<kanim
minus "_kanim">/` (matched case-insensitively; Splitters' folders are mixed-case).
All 15 icons verified visually.

⚠️ **BILD UV quirk:** atlas UVs are **top-left origin** (`pxY = v1 * atlasH`). An earlier
draft of MOD_OFFLINE_EXTRACTION.md claimed bottom-left origin with a `(1 - v2)` flip — that
produces misaligned crops. Fixed in both `Parse-KanimBuild.ps1` and `Export-ModImages.ps1`
(pixel-verified 2026-07-19).

### How the website draws them (see WEBSITE_POSTPROCESSING.md for the full contract)

- **Icon naming contract:** `ui_image/<prefabId>.png`, filename == the building's `name`.
  `mods/images/` follows it — merge these PNGs into the export's `ui_image/` folder before
  `npm run import:2024`.
- **Placed non-connectable buildings** (13 of 15): the website stretches the icon to the
  footprint box (bottom-anchored, 1 cell = 100 px). Our icons are footprint-style `ui`
  sprites — the same kind the legacy base-game export used — so **no `uiImageRect` is
  needed**; the legacy stretch path renders them correctly.
- **Export merge — `tools\Merge-ModExport.ps1`:** merges the mod data into the game's
  export folder so the website ingests one combined extract. Appends the mod entries to
  `building.json` `bBuildingDefList` (they use the importer's field names/shapes),
  registers each in `buildingAndSubcategoryDataPairs` (honoring `addAfter`), copies
  `mods/images/*.png` into `ui_image/`, and stamps a root `modMergeInfo` provenance field.
  Idempotent: previously merged entries (marker: the `mod` property) are stripped first,
  so it converges after a fresh game re-export or a mod-data update. A vanilla-equivalent
  `building.pre-mod-merge.json` snapshot is written beside it each run.

### Connection sprites — the one remaining gap (2 buildings)

`HighPressureGasConduit` and `HighPressureLiquidConduit` are drag-build utilities; for
proper run-tiling the website wants `connection_sprites/<prefabId>/0..15.png` (it treats a
building as connectable **only if that directory exists** — without it they fall back to the
flat icon, which is acceptable short-term).

The 16 tiling states live in the kanim's `_anim.bytes` (ANIM format), which we don't parse
yet. Two routes:

1. **Pragmatic (recommended):** HPA is compatible with the Extract mod — load both in-game
   and run the existing connection-sprite tool (pause-screen button). The offline-only
   constraint matters for *incompatible* mods; HPA isn't one.
2. **Fully offline (future):** parse `_anim.bytes` (vendor kanimal-SE's reader) and
   composite the 16 states — also unlocks assembled default poses for incompatible mods'
   buildings, should we ever support one whose art can't be captured in-game.
