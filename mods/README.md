# Mod Building Extraction — Playbook

How mod buildings get into the website export. Research background:
[../MOD_OFFLINE_EXTRACTION.md](../MOD_OFFLINE_EXTRACTION.md).

## Two paths — in-game first, offline as fallback

**Primary — the normal in-game export, with the mods enabled.** Every export pass
(main-menu JSON, in-game building-image sweep, connection-sprite tool) iterates
`Assets.BuildingDefs`, and the game registers every enabled mod's `IBuildingConfig`
buildings into that list before the export patch runs (`LegacyModMain.Load` collects
types from **all** loaded assemblies). So mod buildings ride the existing crawl with
**zero exporter changes**: full data, hi-res 200 px/cell images with `uiImageRect`, and
connection sprites for modded pipes. If a mod is compatible with the Extract mod, this
is the way — the offline kanim `ui`-symbol crops are small thumbnails by comparison
(see the image-quality note below).

**Fallback — this offline pipeline** (decompile + kanim parsing, no game launch), for
mods that **cannot be loaded** alongside the Extract mod (incompatible mods), or to
fill anything the in-game sweep missed. Its output is **`mods/mod_database.json`**, in
a building.json-compatible schema (below), applied additively by
`tools\Merge-ModExport.ps1`.

### Mod attribution (which mod does a building come from?)

Both paths record the source mod, with the same contract — the website can rely on it to
group modded buildings, toggle them per enabled-mod set, and warn when a blueprint
contains buildings from a mod:

- **Per building:** a `mod` field (Steam workshop id, e.g. `"2094698134"`; local dev mods
  use their folder name) plus `modTitle`. **Present only on modded buildings** — absence
  means base game. The in-game exporter fills these via `building/ModSourceTracker.cs`
  (a `BuildingConfigManager.RegisterBuilding` patch maps each registered def to the
  registering `IBuildingConfig`'s assembly, then to a KMod label); the offline pipeline
  writes `mod` in each `buildings.json` entry.
- **Root roster:** building.json gains a `mods` array — `{ id, title, buildings[] }` per
  contributing mod (in-game path); the offline merge records the same under
  `modMergeInfo`.

### Export run checklist (in-game path)

1. Enable the Extract mod **plus the content mods** you want exported. Consider
   disabling art-replacement mods (e.g. True Tiles) so vanilla art isn't contaminated.
2. Main-menu export → building.json now includes the mod buildings natively.
3. Load a throwaway save → pause-screen buttons: building-image export (hi-res icons +
   uiImageRect) and connection-sprite export (covers modded pipes). Don't save after —
   the sweep spawns buildings, and Buildable Natural Tile's spawn patch places a real
   solid block near the spawn cell.
4. Optionally run `tools\Merge-ModExport.ps1` — it appends only what the in-game run
   didn't cover and fills only missing icons, so it is always safe.

⚠️ **Buildable Natural Tile quirk:** its Harmony patch deletes the building the moment
a `NaturalTileComplete` spawns (that's the mod's whole point), so the image sweep may
fail to render it. The offline icon in `mods/images/` remains its fallback via
Merge-ModExport.

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

If the export ran **with the content mods enabled**, the mod buildings are already in
`building.json` natively — running the merge is optional (it's a no-op for them). If the
export ran without some mod, or for offline-only mods, run the fallback merge:

```powershell
.\tools\Merge-ModExport.ps1             # additive-only; default -ExportDir: Documents\Klei\OxygenNotIncluded\export
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

### Offline fallback icons — `mods/images/<prefabId>.png`

```powershell
.\tools\Export-ModImages.ps1     # re-run after a mod update changes art
```

Extracts the **`ui` symbol** (build-menu icon) from each building's kanim atlas —
every mod kanim surveyed ships one. Kanim folder = `mods/Steam/<id>/anim/assets/<kanim
minus "_kanim">/` (matched case-insensitively; Splitters' folders are mixed-case).
All 15 icons verified visually.

⚠️ **Quality caveat — these are fallbacks.** The kanim `ui` symbol is a small
atlas thumbnail (~120–160 px, loose framing), noticeably below the in-game
building-image sweep's 200 px/cell tight-cropped renders with `uiImageRect`. Prefer the
in-game export for any mod that can load alongside the Extract mod; Merge-ModExport
only uses these PNGs where no in-game render exists.

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
- **Fallback export merge — `tools\Merge-ModExport.ps1`:** additively merges the
  offline data into the game's export folder. Only buildings **not already present** in
  `building.json` are appended (entries the in-game export produced natively are never
  touched), menu pairs registered (honoring `addAfter`), and icons copied **only where
  no PNG exists** — an existing icon is assumed to be an in-game hi-res render and wins.
  Stamps a root `modMergeInfo` field recording what was appended vs. natively exported.
  Idempotent: previously offline-merged entries (marker: the `mod` property) are
  stripped and re-evaluated each run, so it converges after a fresh game re-export.
  An as-exported `building.pre-mod-merge.json` snapshot is written beside it each run.

### Connection sprites (2 buildings) — covered by the in-game path

`HighPressureGasConduit` and `HighPressureLiquidConduit` are drag-build utilities; for
proper run-tiling the website wants `connection_sprites/<prefabId>/0..15.png` (it treats a
building as connectable **only if that directory exists** — without it they fall back to the
flat icon).

The connection-sprite tool iterates `Assets.BuildingDefs` and keys off
`KAnimGraphTileVisualizer`, which both HPA pipes have — so running it with HPA enabled
exports their 16 states like any vanilla pipe. No offline equivalent exists (the tiling
states live in `_anim.bytes`, which we don't parse); an offline ANIM compositor via
kanimal-SE remains future work only if an *incompatible* mod ever ships a pipe.
