# Front-end Integration Guide — Connection Sprites & Power Data

Changes introduced in this export cycle and what the website needs to do with them.

---

## 1. Connection sprites (`export/connection_sprites/`)

### What it is

A new top-level export folder containing 16 PNGs per connectable building — one image per 4-bit connection-state bitmask. The website should use these to render wire/pipe/conduit segments with the correct shape for their neighbours.

### Bitmask encoding

The filename (0–15, no padding) directly encodes which sides are connected:

| Bit | Value | Side |
|-----|-------|------|
| 0 | 1 | Left |
| 1 | 2 | Right |
| 2 | 4 | Up |
| 3 | 8 | Down |

`5.png` = Left (1) + Up (4) — a corner segment. `15.png` = all four sides connected.

### File layout

```
export/connection_sprites/
  {prefabId}/       ← matches building.json `name` field
    0.png
    1.png
    ...
    15.png
```

### Coverage in this export (31 buildings, all complete with 16 PNGs)

**Electrical wires** (5 tiers):

| `name` (prefabId) | Display name |
|---|---|
| `Wire` | Wire |
| `WireRubber` | Insulated Wire |
| `WireRefined` | Conductive Wire |
| `HighWattageWire` | Heavy-Watt Wire |
| `WireRefinedHighWattage` | Conductive Heavy-Watt Wire |

**Gas conduits** (3): `GasConduit`, `GasConduitRadiant`, `InsulatedGasConduit`

**Liquid conduits** (3): `LiquidConduit`, `LiquidConduitRadiant`, `InsulatedLiquidConduit`

**Other utilities** (3): `SolidConduit`, `TravelTube`, `GasPermeableMembrane`

**Logic** (2): `LogicWire`, `LogicRibbon`

**Tiles** (15): `Tile`, `TilePOI`, `MetalTile`, `GlassTile`, `BunkerTile`, `CarpetTile`, `InsulationTile`, `MeshTile`, `MouldingTile`, `PlasticTile`, `RocketEnvelopeWindowTile`, `RocketWallTile`, `RubberTile`, `SnowTile`, `WoodTile`

### Pending: bridge connection sprites

The 14 bridge buildings below are **not yet covered**. A new export pass has been added to the exporter; the sprites will appear in the next export after a rebuild.

| `name` | Display name |
|---|---|
| `WireBridge` | Wire Bridge |
| `WireRubberBridge` | Insulated Wire Bridge |
| `WireRefinedBridge` | Conductive Wire Bridge |
| `WireBridgeHighWattage` | Heavy-Watt Wire Bridge |
| `WireRefinedBridgeHighWattage` | Conductive Heavy-Watt Wire Bridge |
| `GasConduitBridge` | Gas Pipe Bridge |
| `LiquidConduitBridge` | Liquid Pipe Bridge |
| `SolidConduitBridge` | Conveyor Rail Bridge |
| `LogicWireBridge` | Logic Wire Bridge |
| `LogicRibbonBridge` | Logic Ribbon Bridge |
| `ContactConductivePipeBridge` | Pneumatic Door Bridge |
| `TravelTubeWallBridge` | Transit Tube Bridge |
| `HEPBridgeTile` | Radiation Beam Bridge |
| `ModularLaunchpadPortBridge` | Launchpad Port Bridge |

Until the next export, the website should fall back to a single neutral/disconnected sprite (bitmask `0`) for these buildings.

---

## 2. `building.json` — `viewMode` field

### Fixed: game-native overlay name

`viewMode` now emits the game-native overlay name as a string, mapped from
`BuildingDef.ViewMode` via the `OverlayModes.*.ID` lookup (so conduit and logic overlays
resolve correctly instead of coming back null or as a hex hash). Buildings with no special
overlay emit `null`.

Note the conduit names are **singular** (`"GasConduit"`, not `"GasConduits"`) — these are the
exact `OverlayModes` ID strings.

| `viewMode` | Overlay |
|---|---|
| `"Power"` | Power |
| `"GasConduit"` | Gas |
| `"LiquidConduit"` | Liquid |
| `"SolidConveyor"` | Conveyor rail |
| `"Logic"` | Automation |
| `"Oxygen"` | Oxygen |
| `"Decor"` | Decor |
| `"Light"` | Light |
| `"Temperature"` | Temperature |
| `"Rooms"` | Rooms |
| `"Radiation"` | Radiation |
| `"Disease"` | Germs |
| `"Crop"` | Farming |
| `null` | No special overlay |

The fix appears in the next export after a rebuild. (Other overlay modes can be added to the
lookup in `ExportBuilding.ViewModeNames` if a building ever needs one not listed here; until
then an unmapped overlay emits `null`.)

---

## 2b. `building.json` — `utilities` field (restored)

Every building now carries a `utilities` array — one entry per connection port, each with its
cell offset. This restores the gas/liquid/solid/logic port offsets that were temporarily lost
when the power fields were added; the power-only `powerInputOffset` / `powerOutputOffset` fields
remain for backward compatibility and duplicate the `PowerInput` / `PowerOutput` entries here.

```jsonc
"utilities": [
  { "offset": { "x": 0, "y": 0 }, "type": "GasInput",   "isSecondary": false },
  { "offset": { "x": 1, "y": 2 }, "type": "GasOutput",  "isSecondary": false },
  { "offset": { "x": 0, "y": 1 }, "type": "LogicInput", "isSecondary": false },
  { "offset": { "x": 0, "y": 0 }, "type": "PowerInput", "isSecondary": false }
]
```

`type` is one of `PowerInput`, `PowerOutput`, `GasInput`, `GasOutput`, `LiquidInput`,
`LiquidOutput`, `SolidInput`, `SolidOutput`, `LogicInput`, `LogicOutput`, `LogicRibbonInput`,
`LogicRibbonOutput`, `LogicReset`. `offset` is relative to the building's bottom-left corner,
pre-rotation. `isSecondary` is `true` for secondary/filtered ports (e.g. `ElementFilter`
outputs on Gas/Liquid/Solid Filters).

---

## 3. `building.json` — power component fields

These fields are present in this export and reliable now.

### `energyConsumer` — power-consuming buildings

Non-null for any building that draws from the power network (~136 buildings: machines, lights, etc.).

```jsonc
"energyConsumer": {
  "baseWattageRating": 240.0,   // watts drawn when active (static; from BuildingDef)
  "powerSortOrder": 0           // overlay sort priority
}
```

`baseWattageRating` is sourced from `BuildingDef.EnergyConsumptionWhenActive`, **not** the runtime component field (which is 0 at export time). It is the correct design-time wattage.

Wires (`Wire`, `WireRefined`, etc.) have `energyConsumer: null` — they transport power but do not consume it.

### `energyGenerator` — fuel-burning generators

Non-null for **5 buildings only**: `Generator` (coal), `HydrogenGenerator`, `MethaneGenerator`, `PetroleumGenerator`, `WoodGasGenerator`. Other generator-like buildings (`ManualGenerator`, `SteamTurbine`, `SolarPanel`, etc.) do not use this component and will have `energyGenerator: null`.

```jsonc
"energyGenerator": {
  "hasMeter": true,
  "ignoreBatteryRefillPercent": false,
  "formula": {
    "inputs": [
      { "tag": { "Name": "Carbon", "IsValid": true }, "consumptionRate": 1.0, "maxStoredMass": 600.0 }
    ],
    "outputs": [
      { "element": 1960575215, "creationRate": 0.02, "store": false,
        "emitOffset": { "x": 1, "y": 2 }, "minTemperature": 383.15 }
    ],
    "meterTag": { "Name": null, "IsValid": false }
  },
  "meterOffset": 1
}
```

Note: the output wattage (e.g. 600 W for coal) is **not** included in `energyGenerator`. It is not currently exported. File an issue if the website needs it.

### `powerInputOffset` / `powerOutputOffset`

CellOffset `{x, y}` indicating which cell (relative to building origin, bottom-left) the wire port is on. Omitted (not null — the key is absent) when the connection does not exist.

```jsonc
"powerInputOffset": { "x": 1, "y": 0 }   // present for power-consuming buildings
"powerOutputOffset": { "x": 0, "y": 0 }  // present for generators
```

### `battery` — storage buildings

Non-null for batteries and transformers (6 buildings).

```jsonc
"battery": {
  "capacity": 10000.0   // joules
}
```

| `name` | `capacity` (J) |
|---|---|
| `Battery` | 10,000 |
| `BatterySmart` | 20,000 |
| `BatteryMedium` | 40,000 |
| `PowerTransformerSmall` | 1,000 |
| `PowerTransformer` | 4,000 |
| `BatteryModule` | 100,000 |

---

## 4. Recommended website changes

### Immediate (data available now)

1. **Wire/conduit/tile rendering**: For any building with a `connection_sprites/{name}/` folder, load `{bitmask}.png` based on which of its four neighbours share the same network type. Bitmask: Left=1, Right=2, Up=4, Down=8.

2. **Power consumption labels**: Use `energyConsumer.baseWattageRating` (watts) wherever you display a building's power draw.

3. **Battery capacity labels**: Use `battery.capacity` (joules) for storage buildings.

4. **Power port indicators**: Use `powerInputOffset` / `powerOutputOffset` to highlight or annotate the cell where a wire must connect.

### After the next export (pending rebuild)

5. **`viewMode` overlay filter**: Once `viewMode` emits readable strings (`"Power"`, `"GasConduits"`, etc.), use it to filter the building list by active overlay.

6. **Bridge connection sprites**: The 14 bridge buildings listed above will gain their 16-PNG sprite sets. Wire them up with the same bitmask logic as utilities.
