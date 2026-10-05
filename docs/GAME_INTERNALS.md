# ONI Game Internals — Knowledge Base

This file documents things we've learned about the ONI game assembly that aren't
documented anywhere and that we'd otherwise have to re-derive from scratch each time.
**Treat everything here as potentially stale after a game update.** Use the tooling in
[Probing the assembly](#probing-the-assembly) to verify before acting on it.

Reference for working, up-to-date mod code: [Sgt_Imalas-Oni-Mods](https://github.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods) (clone it locally to grep against).

---

## Probing the assembly

`ilspycmd` is installed as a dotnet global tool:

```powershell
ilspycmd "C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed\Assembly-CSharp.dll" -t "TypeName"
```

Replace `TypeName` with any class name (e.g. `LogicGateBase`, `SolidConduitConsumer`).
Use it to confirm field names, method signatures, and initialisation patterns before
writing export or Harmony patch code.

> **Reflection alternative:** Doing deep reflection at runtime overflows the Unity stack.
> Use ilspycmd for static analysis instead.

---

## Building configuration lifecycle

Each building's data is set up in three phases. Only data set in the first two is
accessible from the `BuildingDef.BuildingComplete` prefab that the JSON export reads.
Data set in the third phase is only available in live game objects.

| Phase | Called via | Available on prefab? |
|---|---|---|
| `CreateBuildingDef()` | Called at startup; populates `BuildingDef` fields | ✅ Yes — it's the `BuildingDef` itself |
| `ConfigureBuildingTemplate()` | Adds components / Defs to the BuildingComplete prefab | ✅ Yes — via `GetComponent<T>()` on the prefab |
| `DoPostConfigureComplete()` | Finalises the BuildingComplete prefab; runs after all buildings are registered | ✅ Yes — this also modifies the prefab, so `GetComponent<T>()` works |
| `prefabInitFn` delegates | Runs every time a new instance is **spawned** | ❌ No — only on live game objects |
| `prefabSpawnFn` delegates | Runs every time a new instance is **spawned** | ❌ No — only on live game objects |
| `OnSpawn()` | KMonoBehaviour lifecycle; runs at spawn | ❌ No — only on live game objects |

**Practical consequence for the export:** always read from `buildingDef.BuildingComplete`
(not a spawned live object). Everything in `DoPostConfigureComplete()` is readable;
everything in `prefabInitFn` / `OnSpawn()` is not.

---

## Utility port component taxonomy

### Power ports

| What to check | Source |
|---|---|
| Has power input | `BuildingDef.EnergyConsumptionWhenActive > 0f` |
| Power input offset | `BuildingDef.PowerInputOffset` |
| Has power output | `go.GetComponent<EnergyGenerator>() != null \|\| go.GetComponent<Battery>() != null` |
| Power output offset | `BuildingDef.PowerOutputOffset` |

### Gas / liquid conduit ports

`ConduitConsumer` and `ConduitDispenser` handle **both** gas and liquid. The
`conduitType` field (`ConduitType.Gas` / `ConduitType.Liquid`) distinguishes them.
`ConduitType.Solid` is a valid enum value but is **never** used on these components
— solid conduit has entirely separate classes (see below).

| What | Source |
|---|---|
| Primary input offset | `BuildingDef.UtilityInputOffset` (when `c.useSecondaryInput == false`) |
| Secondary input offset | `ISecondaryInput.GetSecondaryConduitOffset(conduitType)` on the same GameObject |
| Primary output offset | `BuildingDef.UtilityOutputOffset` (when `d.useSecondaryOutput == false`) |
| Secondary output offset | `ISecondaryOutput.GetSecondaryConduitOffset(conduitType)` |

### Solid / conveyor belt conduit ports

**Separate component classes** from gas/liquid. Same `UtilityInputOffset`/`UtilityOutputOffset`
offset convention, same `ISecondaryInput`/`ISecondaryOutput` secondary-port convention.

| Component | Direction | `useSecondary*` field |
|---|---|---|
| `SolidConduitConsumer` | Input | `useSecondaryInput` |
| `SolidConduitDispenser` | Output | `useSecondaryOutput` |

`SolidConduitConsumer.GetInputCell()` (private) applies the same
"primary → `GetUtilityInputCell()`, secondary → scan `ISecondaryInput`" logic as
the gas/liquid equivalent. Mirror this pattern when computing offsets.

### Logic ports — standard buildings (sensors, controllers, etc.)

Most buildings that connect to the logic network set their ports in `CreateBuildingDef()`:

```csharp
buildingDef.LogicInputPorts  = new List<LogicPorts.Port> { ... };
buildingDef.LogicOutputPorts = new List<LogicPorts.Port> { ... };
```

These are readable from `BuildingDef` directly. Each `LogicPorts.Port` has:
- `cellOffset` — `CellOffset` from the building's origin cell (see "Offset conventions")
- `spriteType` — `LogicPortSpriteType` enum that identifies the port icon

`LogicPortSpriteType.ToString()` is used (rather than the int value) to classify port
subtypes, since the enum can gain values across game updates:
- Contains "ribbon" → ribbon port (look for "out" to distinguish input/output)
- Contains "reset" or "update" → `LogicReset` port
- Anything else → `LogicInput` or `LogicOutput` depending on which list it came from

> **The `LogicPorts` component is NOT the right source** for the export path. Its
> `inputPortInfo`/`outputPortInfo` arrays are written by `LogicPorts.OnSpawn()` from
> the `BuildingDef` lists, so they only exist on live spawned objects.

### Logic ports — gates (AND/OR/XOR/NOT/BUFFER/FILTER/MUX/DEMUX)

Logic gates **never** set `BuildingDef.LogicInputPorts`/`LogicOutputPorts`. Instead,
`LogicGateBaseConfig.DoPostConfigureComplete()` adds a `LogicGate` component
(subclass of `LogicGateBase`) and sets offsets on it directly:

```csharp
LogicGate logicGate = go.AddComponent<LogicGate>();
logicGate.inputPortOffsets  = InputPortOffsets;   // CellOffset[]
logicGate.outputPortOffsets = OutputPortOffsets;  // CellOffset[]
logicGate.controlPortOffsets = ControlPortOffsets; // CellOffset[] — MUX/DEMUX selector bits
```

These arrays **are** set at prefab configuration time (`DoPostConfigureComplete`),
so they survive on `buildingDef.BuildingComplete.GetComponent<LogicGateBase>()`.

The gate configs for reference (U59):

| Building | InputPortOffsets | OutputPortOffsets | ControlPortOffsets |
|---|---|---|---|
| AND, OR, XOR | `[(0,0), (0,1)]` | `[(1,0)]` | null |
| NOT, BUFFER, FILTER | `[(0,0)]` | `[(1,0)]` | null |
| MUX | `[(-1,3),(-1,2),(-1,1),(-1,0)]` | `[(1,3)]` | `[(0,0),(1,0)]` |
| DEMUX | `[(-1,3)]` | `[(1,3),(1,2),(1,1),(1,0)]` | `[(-1,0),(0,0)]` |

Control ports (MUX/DEMUX selector bits) are logic inputs and are exported as
`LogicInput` type.

`LogicGateBuffer` and `LogicGateFilter` add their own subclass components
(`LogicGateBuffer`, `LogicGateFilter`) rather than `LogicGate`, but all extend
`LogicGateBase`, so `go.GetComponent<LogicGateBase>()` catches all of them.

---

## ElementFilter buildings (GasFilter / LiquidFilter / SolidFilter)

These three buildings bypass the standard `ConduitConsumer` / `ConduitDispenser` /
`SolidConduitConsumer` / `SolidConduitDispenser` pattern entirely. Instead, their
`ConfigureBuildingTemplate()` adds an `ElementFilter` component, and the
`ElementFilter` registers its own cells on the conduit network at spawn time.

Port layout (all three follow the same pattern):

| Port | Offset | Source |
|---|---|---|
| Primary input | `BuildingDef.UtilityInputOffset` = `(-1, 0)` | `ConduitConsumer` (Gas/Liquid only; added by `ConfigureBuildingTemplate`) / `BuildingDef.InputConduitType` fallback (Solid) |
| Primary output | `BuildingDef.UtilityOutputOffset` = `(1, 0)` | `BuildingDef.OutputConduitType` fallback (no `ConduitDispenser` present) |
| Secondary output (filtered) | `ElementFilter.portInfo.offset` = `(0, 0)` | `ElementFilter.ISecondaryOutput.GetSecondaryConduitOffset` |

**Why does GasFilter have a `ConduitConsumer` but not a `ConduitDispenser`?**
`ConfigureBuildingTemplate` explicitly adds `ElementFilter` with `portInfo = secondaryPort`.
For gas/liquid, `ElementFilter.OnSpawn()` calls `GetComponent<ConduitConsumer>().isConsuming = false`
— implying a `ConduitConsumer` must already exist (likely added by `BuildingTemplates`
when `InputConduitType != None`). No `ConduitDispenser` is added; `ElementFilter` handles
both output cells internally.

**Export consequence:** Always add the primary/secondary output via the `BuildingDef`
fallback + `ISecondaryOutput` scan, not via `ConduitDispenser` loops.

---

## Secondary conduit ports

Buildings with two inputs or two outputs of the same conduit type (e.g. a building that
accepts two different liquid inputs) use the `ISecondaryInput` / `ISecondaryOutput`
interfaces. One component on the building implements these interfaces and responds to:

```csharp
bool HasSecondaryConduitType(ConduitType type)
CellOffset GetSecondaryConduitOffset(ConduitType type)
```

The secondary component is found by scanning `go.GetComponents<ISecondaryInput>()`.
Because `ISecondaryInput` may not be a public interface in all game builds, the export
code uses reflection to find any component with a matching `GetSecondaryConduitOffset`
method, falling back to `(0,0)` with `isSecondary=true` if none is found.

---

## Offset conventions

All port offsets (and `attachPoints`, `attachablePosition`, `areasOfEffect[].origin`) are
`CellOffset` values measured from the building's **origin cell** in its **unrotated
orientation**. The origin cell is the one `Grid.PosToCell(building)` returns: the bottom row,
at column `floor((WidthInCells - 1) / 2)` from the left — `EntityTemplates.GenerateOffsets`
spans `x = width/2 - width + 1 .. width/2` (integer division), `y = 0 .. height-1`, so the
origin is `width - 1 - width/2` columns in. That is the bottom-left cell only for widths 1
and 2; for odd widths it is the bottom-centre cell (a 7-wide LaunchPad spans `x = -3..3`,
which is why its logic ports sit at `(-1,0)` and `(1,0)`); a 4-wide building spans
`x = -1..2`. The game applies rotation at display/placement time; the exported offsets are
always pre-rotation.

`CellOffset` is a struct with `x` (column) and `y` (row) integer fields.
`CellOffset.none` == `(0, 0)`.

---

## Building replacement

Placing a building on an occupied cell sometimes queues a *replacement* (tile over tile, ladder
over ladder, wire over wire) instead of refusing. Read from the decompiled U59-744825 assembly;
not observed in game. Below, **C** is the def being placed and **E** the building already there.

Which layers a placed building occupies: `BuildingComplete.OnSpawn` marks its footprint on
`Def.ObjectLayer`, and also on `Def.TileLayer` when that is set (`IsTilePiece`). A `Tile` is on
`Building` and `FoundationTile`; a `Ladder` on `Building` and `LadderTile`; a `Wire` on `Wire`
and `WireTile`. `ObjectLayer.NumLayers` is the "unset" value of `TileLayer` and
`ReplacementLayer`.

The game has two placement tools, picked in `PlanScreen.OnRecipeElementsFullySelected`, and
each has its own rule.

**`BuildTool.TryBuild`** — every def that is not `isKAnimTile && isUtility`. This is the method
that holds the final rule. A replacement is queued when all of these hold:

1. Ordinary placement was refused (`C.TryPlace` returned null).
2. `C.ReplacementLayer != NumLayers`.
3. `C.GetReplacementCandidate(cell)` finds E at C's origin cell: with
   `C.ReplacementCandidateLayers`, the first layer in list order that holds a
   `BuildingComplete` there; without, whatever is on `C.TileLayer`.
4. Nothing is already queued: no footprint cell has an object on `C.ReplacementLayer` or on
   one of `C.EquivalentReplacementLayers` (`IsReplacementLayerOccupied`). Runtime state only.
5. `E.Def.Replaceable`, and `C.CanReplace(E)`: `C.ReplacementTags` is non-null and E's
   `KPrefabID` has at least one of them.
6. `E.Def != C`, or the selected material differs from E's (a same-def material swap).
7. `C.TryReplaceTile` then re-runs `IsValidPlaceLocation(replace_tile: true)`: the usual
   build-location rule and area check, with the candidate exempt from "occupied".

Only the origin cell's candidate is tested by 5 and 6. For a multi-cell C the other cells go
through the area check, and `Constructable.FinishConstruction` removes whatever
`GetReplacementCandidate` finds in each.

**`BaseUtilityBuildTool.BuildPath`** — the `isKAnimTile && isUtility` defs (wires, logic wire,
pipes, rails, travel tubes). Per path cell: `C.ReplacementLayer != NumLayers`,
`C.IsValidBuildLocation`, an object E on `C.TileLayer` and none on `C.ReplacementLayer`, E is
a `BuildingComplete`, and `E.Def != C` or the material differs. It checks **neither**
`ReplacementTags` **nor** `Replaceable`: any wire replaces any other wire. The `Vents` /
`Pipes` tags the conduit configs put in `ReplacementTags` are never read, because `CanReplace`
has one caller and it is `BuildTool`.

Who sets what (vanilla):

| Defs | `ReplacementLayer` | `ReplacementCandidateLayers` | `ReplacementTags` |
|---|---|---|---|
| `BuildingTemplates.CreateFoundationTileDef` (20 configs: the tiles, the farm tiles, `TravelTubeWallBridge`, `WireBridgeHighWattage`) | `ReplacementTile` | `FoundationTile`, `LadderTile`, `Backwall` | `FloorTiles`, `Ladders`, `Backwall` |
| `BuildingTemplates.CreateLadderDef`: `Ladder`, `LadderFast`, `FirePole` | `ReplacementLadder` | — (searches `LadderTile`) | `Ladders` |
| `ExteriorWall`, `GlassExteriorWall`, `ThermalBlock` | `ReplacementBackwall` | `FoundationTile`, `Backwall` | `FloorTiles`, `Backwall` |
| `FacilityBackWallWindow` | `ReplacementBackwall` | `FoundationTile`, `Backwall` | **null**, so `CanReplace` is always false |
| `FloorSwitch`, `MouldingTile` | `ReplacementTile` | — | **null**, so `CanReplace` is always false |
| `LargeBackwallFarm`, `ContactConductivePipeBridge` | **unset**, so never a replacement | — | set, unused |
| Wires, logic wire, gas/liquid/solid conduits, `TravelTube` | their own `Replacement*` layer | — | not read (utility tool) |

`Replaceable` is set false by the configs for `InsulatedDoor`, `WoodenDoor`, `PixelPack`,
`LargeBackwallFarm`, `UnderwaterMilkFeeder`, `TilePOI`, `RocketWallTile`,
`RocketEnvelopeWindowTile` and the four `RocketInterior*Port` tiles.

Two traps for the export:

- **The matching tag can be runtime-only.** `FloorTiles`, `Backwall`, `Vents` and `Pipes` are
  added in the configs and are on the prefab. `Ladders` is added by `Ladder.OnPrefabInit`, so
  the prefab's tags never carry it. The export lists it in `runtimeTags` for buildings with a
  `Ladder` component.
- **Being a foundation is not being replaceable by a tile.** `PressureDoor`,
  `ManualPressureDoor` and `BunkerDoor` sit on `FoundationTile` but carry no `FloorTiles` tag,
  and no door sets a `ReplacementLayer`, so a door and a tile never replace each other. The
  same goes for `FarmTile` as the existing building: it has `FarmTiles`, not `FloorTiles`.

---

## Things that look the same but are different

| Looks like | Is actually |
|---|---|
| `ConduitType.Solid` on a `ConduitConsumer` | Dead code — never occurs. Solid conduit uses `SolidConduitConsumer` |
| `LogicPorts` component → port data | Only valid on spawned objects; use `BuildingDef.LogicInputPorts` for export |
| The prefab's `KPrefabID` tags → the placed building's tags | A subset. Components add more in `OnPrefabInit` / `OnSpawn`: a ladder only gets `Ladders` once placed (see "Building replacement") |
| `LogicGate` component → same as `LogicPorts`? | No — completely separate system. `LogicGate` extends `LogicGateBase` and stores `CellOffset[]` arrays, not `List<LogicPorts.Port>` |
| `go.GetDef<LogicPorts.Def>()` | Compile error in this game version — `LogicPorts.Def` does not exist |
| `BuildingDef.LogicInputPorts` populated for gates | It is NOT. Gates use `LogicGateBase.inputPortOffsets` instead |
| `ConduitDispenser` present on GasFilter/LiquidFilter/SolidFilter | There is none. Outputs are managed by `ElementFilter`; use the `BuildingDef.OutputConduitType` fallback + `ISecondaryOutput` scan |
| A bridge (wire, pipe, rail, logic) as a "connectable" with 16 neighbour states | It is not one. Only buildings with `KAnimGraphTileVisualizer` redraw to match their neighbours, and every config that adds it also sets `isUtility` (wires, logic wire, the conduits, the travel tube). Bridges add `BuildingCellVisualizer` plus a network link and have one fixed sprite, so there is nothing for the connection-sprite export to render |
