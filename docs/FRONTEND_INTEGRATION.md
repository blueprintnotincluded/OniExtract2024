# Front-end Integration Guide — Building Connection & Electrical Data

**Authoritative as of the export validated 2026-06-25** (build U56-674504, 449 buildings).

> ⚠️ This document **supersedes all earlier guidance**. Previous drafts described a broken
> interim state (`utilities[]` missing, `viewMode` as a hex hash or null, plural overlay names
> like `"GasConduits"`). Those were exporter bugs and are now fixed. Ignore any prior notes that
> say "broken", "pending rebuild", "don't use yet", or that list `viewMode` as a hex string.
> Everything below was verified against the actual exported `building.json`.

---

## 1. `building.json` — `utilities[]` (authoritative connection-port list)

Every building has a `utilities` array: **one entry per connection port**, each with the cell it
occupies. This is the source of truth for where wires/pipes/rails/automation connect. Buildings
that transport but don't connect (e.g. `Wire`, `GasConduit`) have an **empty** array.

```jsonc
"utilities": [
  { "offset": { "x": -1, "y": 0 }, "type": "GasInput",   "isSecondary": false },
  { "offset": { "x": 1,  "y": 0 }, "type": "GasOutput",  "isSecondary": false },
  { "offset": { "x": 0,  "y": 0 }, "type": "GasOutput",  "isSecondary": true  },
  { "offset": { "x": 0,  "y": 0 }, "type": "PowerInput", "isSecondary": false }
]
```

### Fields

| Field | Type | Meaning |
|---|---|---|
| `offset` | `{ x, y }` numbers | Cell offset from the building's **bottom-left** corner, **pre-rotation**. Values are JSON numbers and may be negative or have a `.0` (treat as numeric, not int-only). |
| `type` | string enum | Which network + direction the port belongs to (table below). |
| `isSecondary` | bool | `true` for a building's *second* port of the same network type — e.g. a filter's filtered-element output. Each non-secondary port has a distinct role; use this to disambiguate two ports of the same `type`. |

### `type` values (13)

| Network | Input | Output |
|---|---|---|
| Power | `PowerInput` | `PowerOutput` |
| Gas pipe | `GasInput` | `GasOutput` |
| Liquid pipe | `LiquidInput` | `LiquidOutput` |
| Conveyor rail (solid) | `SolidInput` | `SolidOutput` |
| Automation (logic) | `LogicInput` | `LogicOutput` |
| Automation ribbon | `LogicRibbonInput` | `LogicRibbonOutput` |
| Automation reset/update | `LogicReset` | — |

Notes:
- **Logic gate control ports** (Multiplexer/Demultiplexer selector bits) are emitted as
  `LogicInput`.
- **Secondary outputs** (`isSecondary: true`) appear on the Gas/Liquid/Solid Filters
  (`GasFilter`, `LiquidFilter`, `SolidFilter`) — the filtered stream exits a port distinct from
  the pass-through output.

### Validated examples

| Building | `utilities[]` |
|---|---|
| `GasFilter` | `GasInput(-1,0)`, `GasOutput(1,0)`, `GasOutput(0,0)` *secondary*, `PowerInput(0,0)` |
| `LiquidPump` | `LiquidOutput(1,1)`, `PowerInput(0,1)`, `LogicInput(0,1)` |
| `Generator` (coal) | `PowerOutput(0,0)`, `LogicInput(0,0)` |
| `BatterySmart` | `PowerOutput(0,0)`, `LogicOutput(0,0)` |
| `LogicGateAND` | `LogicInput(0,0)`, `LogicInput(0,1)`, `LogicOutput(1,0)` |
| `Wire` | *(empty — transport only)* |

Coverage: **275 / 449** buildings have at least one port.

---

## 2. `building.json` — `viewMode` (overlay assignment)

`viewMode` is the **game-native overlay name** the building belongs to, or `null` when it has no
special overlay. Mapped from `BuildingDef.ViewMode` via the game's `OverlayModes.*.ID` table, so
it is stable across the lookup (no more nulls for conduits, no hex hashes).

```jsonc
"viewMode": "GasConduit"   // or null
```

### Values seen in this export

| `viewMode` | Count | Overlay |
|---|---:|---|
| `null` | 175 | No special overlay |
| `"Power"` | 77 | Power |
| `"Logic"` | 49 | Automation |
| `"LiquidConduit"` | 46 | Liquid |
| `"GasConduit"` | 26 | Gas |
| `"Decor"` | 18 | Decor |
| `"SolidConveyor"` | 14 | Conveyor rail |
| `"Rooms"` | 11 | Rooms |
| `"Radiation"` | 10 | Radiation |
| `"Oxygen"` | 9 | Oxygen |
| `"Temperature"` | 7 | Temperature |
| `"Light"` | 7 | Light |

> **Names are singular** — `"GasConduit"`, `"LiquidConduit"` (not the plural forms used in earlier
> drafts). The exporter can also emit `"Disease"` and `"Crop"`; none appeared in this build but the
> mapping exists. An unmapped overlay would emit `null`.

---

## 3. `building.json` — power component fields

These remain available and are reliable. The cell-offset fields **duplicate** the corresponding
`utilities[]` entries; `utilities[]` is the authoritative list, these are a convenience for
power-only consumers.

### `energyConsumer` — power-consuming buildings (131)

```jsonc
"energyConsumer": {
  "baseWattageRating": 240.0,   // watts drawn when active (static, from BuildingDef)
  "powerSortOrder": 0
}
```
Sourced from `BuildingDef.EnergyConsumptionWhenActive` (the runtime component field is 0 at export
time). Wires have `energyConsumer: null`.

### `powerInputOffset` / `powerOutputOffset`

```jsonc
"powerInputOffset":  { "x": 0, "y": 1 }   // present on power-consuming buildings (133)
"powerOutputOffset": { "x": 0, "y": 0 }   // present on generators/batteries (5)
```
The key is **omitted** (absent, not `null`) when the connection doesn't exist.

> Minor edge case: `powerInputOffset` is gated on `RequiresPowerInput`, while the `PowerInput`
> entry in `utilities[]` is gated on `EnergyConsumptionWhenActive > 0`. These differ for a single
> building that requires power input but has zero active draw. **Prefer `utilities[]`** as the
> authoritative port source.

### `battery` — storage (6)

```jsonc
"battery": { "capacity": 20000.0 }   // joules
```
`Battery` 10k · `BatterySmart` 20k · `BatteryMedium` 40k · `PowerTransformerSmall` 1k ·
`PowerTransformer` 4k · `BatteryModule` 100k.

### `energyGenerator` — fuel-burning generators (5)

Non-null only for `Generator`, `HydrogenGenerator`, `MethaneGenerator`, `PetroleumGenerator`,
`WoodGasGenerator`. (Output wattage is not currently in this object — file an issue if needed.)

---

## 4. `export/connection_sprites/` — segment sprites

16 PNGs per connectable building, one per 4-bit neighbour bitmask.

```
export/connection_sprites/{name}/{bitmask}.png      // name == building.json `name`
```

| Bit | Value | Side |
|---|---|---|
| 0 | 1 | Left |
| 1 | 2 | Right |
| 2 | 4 | Up |
| 3 | 8 | Down |

`5.png` = Left+Up (corner). `15.png` = all four sides. Pick the file matching which of the four
neighbours share the same network type.

> **⚠️ Coverage status in *this* export:** the PNGs are fresh, but the folder still contains only
> **31 buildings (wires, gas/liquid conduits, logic, tiles)** and **none of the 14 bridges**. The
> bridge pass is in the current mod build, but the in-game sprite export that produced these ran
> against the *previously loaded* mod — rebuilding the DLL does not hot-swap a running game. To get
> the bridges: **fully restart the game so the new DLL loads, then trigger the connection-sprite
> export (pause-screen button) again.** Until then, fall back to bitmask `0` for any building
> without a `connection_sprites/{name}/` folder.

---

## 5. `export/ui_image/` — port indicator icons

Generic per-port-type indicator sprites (one set, shared by all buildings) are exported alongside
the per-building UI images:

| Icon file | Use for `type` |
|---|---|
| `input.png` / `output.png` | Gas / Liquid / Solid conduit ports |
| `electrical_disconnected.png` | `PowerInput` / `PowerOutput` |
| `logicInput.png` / `logicOutput.png` | `LogicInput` / `LogicOutput` |
| `logicResetUpdate.png` | `LogicReset` |
| `logic_ribbon_all_in.png` / `logic_ribbon_all_out.png` | `LogicRibbonInput` / `LogicRibbonOutput` |

---

## 6. Recommended front-end actions

1. **Port placement** — drive all connection-point rendering from `utilities[]` (`offset` +
   `type`), not the power-only `*Offset` fields. Use `isSecondary` to tell apart two ports of the
   same `type`.
2. **Overlay filtering** — group/filter buildings by `viewMode`; treat `null` as "no overlay".
3. **Labels** — `energyConsumer.baseWattageRating` (W) for draw; `battery.capacity` (J) for storage.
4. **Segment sprites** — load `connection_sprites/{name}/{bitmask}.png`; after the next in-game
   sprite re-run this will include the 14 bridges.
5. **Port icons** — map `type` → icon via the table in §5.
