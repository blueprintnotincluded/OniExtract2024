# Website handoff: rocket-module stacking data and blueprint-setting ranges

Direction: export → website. What `building.json` gained, why, and what the site can do with
it. Written against the analysis in `blueprints-included-feature-gap.md` (2026-09-24): the
Blueprints Included mod now places whole rocket stacks from a blueprint, and the site had no
data to understand a module beyond its footprint. Every new key here is **additive** — nothing
existing was renamed or reshaped — and every optional key is **omitted** when it does not apply
(never `null`, never `false`), so an importer that ignores them keeps working unchanged.

Field reference: [EXPORT_SCHEMA.md](EXPORT_SCHEMA.md) → building.json. Game internals behind
the values: [GAME_INTERNALS.md](GAME_INTERNALS.md).

---

## 1. What changed in `building.json`

### Root

| Key | Type | What |
|---|---|---|
| `rocketModuleMenu` | `string[]` | Prefab ids of every Spaced Out rocket module, in the order the game's module screen shows them, then any modded modules in name order. Empty without the DLC. |

### Per building (`bBuildingDefList[]`)

Always present:

| Key | Type | What |
|---|---|---|
| `showInBuildMenu` | `bool` | `BuildingDef.ShowInBuildMenu`. `false` for every rocket module and a few special parts. Sits beside `deprecated` / `debugOnly`. |

Rocketry, present only when applicable:

| Key | Type | Present on | What |
|---|---|---|---|
| `isRocketModule` | `true` | modules | Prefab carries `RocketModule` (or `RocketModuleCluster`). |
| `attachableTo` | `string` | modules | Hardpoint type this building must sit on. `"Rocket"` for every module (`BuildingDef.AttachmentSlotTag`). |
| `attachablePosition` | `{x,y}` | modules | Cell of *this* building (offset from its origin) that lands on the hardpoint. `(0,0)` for all vanilla modules. |
| `attachPoints` | `[{offset:{x,y}, tag}]` | modules that carry another module, `LaunchPad` | Hardpoints this building offers, offset from its origin. One `Rocket` point at `(0, heightInCells)` for stackable modules; `(0, 2)` for the LaunchPad. Absent on nosecones and other top-only modules. |
| `rocketModulePerformance` | `{burden, enginePower, fuelKilogramPerDistance}` | cluster modules | The stats behind rocket speed and fuel use. |
| `moduleBuildConditions` | `string[]` | cluster modules | Names of the game's module-screen constraints: `TopOnly`, `EngineOnBottom`, `LimitOneEngine`, `LimitOneCommandModule`, `RocketHeightLimit`, `NoFreeRocketInterior`, `LimitOneRoboPilotModule`, plus the always-present `ResearchCompleted`, `MaterialsAvailable`, `PlaceSpaceAvailable`. |

Settings ranges, present only when the component exists:

| Key | Type | What |
|---|---|---|
| `prioritizable` | `true` | Accepts a work priority (`buildingData.Prioritizable`). |
| `userNameable` | `true` | Player can rename it (`buildingData.UserNameable.savedName`). |
| `door` | `{doorType, hasComplexUserControls, allowAutoControl}` | `doorType` ∈ `Pressure`, `ManualPressure`, `Internal`, `Sealed` (`buildingData.Door.requestedState`). |
| `valve` | `{conduitType, maxFlow}` | Flow slider bound, kg/s (`buildingData.Valve.DesiredFlow`). |
| `limitValve` | `{conduitType, maxLimitKg, displayUnitsInsteadOfMass}` | Limit slider bound (`buildingData.LimitValve.Limit`). |
| `userControlledCapacity` | `{minCapacity, maxCapacity, wholeValues, units, source}` | Capacity slider bounds (`buildingData.IUserControlledCapacity.UserMaxCapacity`, `buildingData.StorageTile`). `source` is the game component the range came from. |

Unchanged but relevant: `rocketEngineCluster.maxHeight` and `.maxModules` (the engine's stack
limits) were already exported; `tags[]` already contained `RocketModule` / `NoseRocketModule` /
`LaunchButtonRocketModule` for anyone who wants to cross-check.

### Removed from the model, not from the file

`BBuildingEntity` used to declare raw game-component fields (`attachableBuilding`,
`buildingAttachPoint`, `rocketModule`, `reorderableBuilding`, `rocketModuleCluster`,
`passengerRocketModule`, `cargoBayConduit`, `deconstructable`, `demolishable`, `roomTracker`).
The serializer's `SkipUnityObjectContractResolver` has always dropped them, so **no export ever
contained these keys**; they are simply gone from the C# now. Nothing to change on the site.

---

## 2. Worked example: a three-module rocket

Values as exported by the game (U59, Spaced Out). Offsets are cells from each building's
origin (bottom-left of footprint, pre-rotation), the same convention as `utilities[].offset`.

| Prefab | w×h | `attachableTo` | `attachPoints` | `moduleBuildConditions` (beyond the three common ones) |
|---|---|---|---|---|
| `LaunchPad` | 7×2 | — | `[(0,2) Rocket]` | — |
| `KeroseneEngineCluster` | 7×5 | `Rocket` | `[(0,5) Rocket]` | `RocketHeightLimit`, `LimitOneEngine`, `EngineOnBottom` |
| `ArtifactCargoBay` | 3×1 | `Rocket` | `[(0,1) Rocket]` | `RocketHeightLimit` |
| `NoseconeBasic` | 5×2 | `Rocket` | *(absent)* | `RocketHeightLimit`, `TopOnly` |

A blueprint with the pad at `(0,0)` therefore has the engine at `(0,2)`, the cargo bay at
`(0,7)`, the nosecone at `(0,8)`. Each module's origin equals the origin of the thing below plus
that thing's `attachPoints[0].offset` (all `attachablePosition` are `(0,0)`).

Stack height for `rocketEngineCluster.maxHeight` is `Σ heightInCells` over **every module in
the stack, engine included** (`CraftModuleInterface.RocketHeight`); the LaunchPad does not
count. The game's `RocketHeightLimit` check admits a module while
`RocketHeight + newModule.heightInCells <= engine.maxHeight` (35 for the Kerosene engine, so
the example above uses 8 of 35).

---

## 3. Suggested website changes, in the order of the gap analysis

### Phase 1 — data plumbing (no editor behaviour change)

1. **Carry the new keys through `convert-export-2024.ts` `buildingRecord`** into `b-building.ts`
   / `b-export-2024.ts` and load them in `OniItem`: `showInBuildMenu`, `isRocketModule`,
   `attachableTo`, `attachablePosition`, `attachPoints`, `rocketModulePerformance`,
   `moduleBuildConditions`, plus the settings block. All optional except `showInBuildMenu`.
2. **`permittedRotations` override for modules.** The export keeps the game's truth
   (`0`, Unrotatable) because that is what `BuildingDef` says. The mod lets modules flip
   horizontally (`PermittedRotations.FlipH` = `3`), so apply the override in the converter for
   `isRocketModule` defs, next to the existing `manual-buildMenuRename.json` precedent, and
   note the reason there.
3. **Import test** with a real two-module capture (same def twice, second at the first's
   `attachPoints[0].offset`); assert both instances land at the right offsets.

### Phase 2 — editable rockets

4. **Build menu.** Add a `rocketry` sub-list fed by `rocketModuleMenu` (the game's own order),
   or split it by role using `moduleBuildConditions` / component keys: engines have
   `rocketEngineCluster`, crew modules have `LimitOneCommandModule`, top-only parts have
   `TopOnly`, the rest are cargo/tanks. `showInBuildMenu: false` is why these never appear in
   `buildingAndSubcategoryDataPairs`; do not filter them out on that flag.
5. **Snap to hardpoint.** When placing an item with `attachableTo`, look for a placed item whose
   `attachPoints` has a matching `tag` and whose resolved cell is under (or near) the cursor;
   snap the new item's origin so `origin + attachablePosition == hardpoint cell`. The LaunchPad's
   `(0,2)` entry makes the bottom of the stack use the same code path.

### Phase 3 — validation

6. **Stack warnings in `blueprint-analyzer`.** For each `isRocketModule` item: is there a
   matching hardpoint exactly under its `attachablePosition` cell? Is anything sitting on a
   `TopOnly` module or on one with no `attachPoints`? Is there more than one `LimitOneEngine`
   or `LimitOneCommandModule` item in a stack, is the engine not at the bottom
   (`EngineOnBottom`), does `Σ heightInCells` exceed the engine's `rocketEngineCluster.maxHeight`,
   does the module count exceed `maxModules`? Surface as warnings, not blockers — the mod
   refuses the invalid ones in game.
7. **Performance readout (optional).** `Σ enginePower / Σ burden` is the game's speed figure;
   `Σ fuelKilogramPerDistance` is fuel per hex. Enough for a "this stack is too heavy for its
   engine" hint.

### Settings catalogue

8. When adding `Prioritizable`, `Door`, `Valve`, `LimitValve`, `StorageTile`,
   `IUserControlledCapacity` and `UserNameable` to `SETTINGS_CATALOG`, use the per-building
   ranges here to render controls: show a priority control only when `prioritizable` is set,
   clamp the valve slider to `[0, valve.maxFlow]`, the limit slider to `[0, limitValve.maxLimitKg]`
   (label it as units when `displayUnitsInsteadOfMass`), and the capacity slider to
   `[minCapacity, maxCapacity]` stepping in whole numbers when `wholeValues`. `door.doorType`
   distinguishes airlocks from ordinary doors if the UI wants different labels.

---

## 4. Things the export deliberately does *not* do

- **No `permittedRotations` override.** The file states what the game's `BuildingDef` says;
  the mod's flip-only behaviour is a mod rule, applied site-side (item 2 above).
- **No `buildLocationRule` change.** Modules really are `Anywhere` (0); the game enforces
  attachment through `AttachmentSlotTag` + hardpoints, which is what `attachableTo` /
  `attachPoints` now describe.
- **No synthetic stacking marker.** Whether two placed modules form a stack is derivable from
  offsets and hardpoints; the blueprint format has no such key either.
- **Crew capacity** of habitat modules is not exported: the game derives it at runtime from
  the module's assignment group, not from a prefab field.

---

## 5. Verifying a fresh export

Spot checks after the next main-menu export (`export/database/building.json`):

- `rocketModuleMenu` starts `CO2Engine, SugarEngine, SteamEngineCluster, ...` and ends
  `..., ArtifactCargoBay, ScannerModule` (32 entries when every id in the game's list is
  present); every id resolves to a `bBuildingDefList` entry with `isRocketModule: true` and
  `showInBuildMenu: false`.
- `LaunchPad.attachPoints == [{offset:{x:0,y:2}, tag:"Rocket"}]`, no `attachableTo`.
- `KeroseneEngineCluster`: `attachableTo:"Rocket"`, `attachPoints[0].offset == {0,5}`,
  `rocketModulePerformance.enginePower > 0`, `moduleBuildConditions` contains `EngineOnBottom`.
- `NoseconeBasic`: `attachableTo:"Rocket"`, **no** `attachPoints`, `moduleBuildConditions`
  contains `TopOnly`.
- `ManualGenerator` (any ordinary building): none of the rocketry keys, `showInBuildMenu: true`.
- `LiquidValve.valve == {conduitType:"Liquid", maxFlow:10}`; `LiquidLimitValve.limitValve.maxLimitKg`
  present; `StorageLocker.userControlledCapacity.maxCapacity` equals its `storage.capacityKg`,
  `source:"StorageLocker"`, and `userNameable: true`, `prioritizable: true`;
  `StorageTile.userControlledCapacity.source == "StorageTile.Def"`; `Door.door.doorType == "Internal"`
  (`Door` is the plain Pneumatic Door) with `prioritizable: true`; `Wire` and `Tile` carry none of
  the settings keys.
