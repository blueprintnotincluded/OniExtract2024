# Area of Effect Export — `areasOfEffect[]`

How the export captures each building's **area of effect** — the cells around a building
that it lights, breathes from, digs, heats, sweeps, scans or irradiates — so the website
can shade those cells on the build grid the way the game (and the *Show Building Ranges*
mod) does.

Status: **implemented**, shipped in `building.json` as the optional per-building
`areasOfEffect` array. Field-level schema lives in EXPORT_SCHEMA.md; this file is the
deep documentation: where each range comes from in the game code, the exact geometry
semantics, and what is deliberately out of scope.

---

## Where ranges live in the game (investigation summary)

There is no single "range" system in ONI. Five separate mechanisms cover everything the
game itself visualizes, and all five are readable from the `BuildingComplete` prefab at
main-menu export time (each is configured in `ConfigureBuildingTemplate` /
`DoPostConfigureComplete` — see GAME_INTERNALS.md "Building configuration lifecycle"):

| Mechanism | Component on prefab | Example buildings | What defines the area |
|---|---|---|---|
| Light | `Light2D` | Ceiling Light, Lamp, Sun Lamp, Mercury Light, Printing Pod | `shape` (Circle/Cone/Quad) + `Range` + `Offset` (+ `Width`/`LightDirection` for Quad) |
| Sim element intake | `ElementConsumer` (incl. `PassiveElementConsumer`) | Deodorizer, gas/liquid pumps & mini-pumps, Liquid-Cooled Fan, Oxygen Mask Station, CO₂ Scrubber, Algae Terrarium | `sampleCellOffset` + `consumptionRadius` |
| Operating range | `RangeVisualizer` | Robo-Miner, Auto-Sweeper, Duplicant Motion Sensor, Space Heater, Campfire, Steam Turbine, Missile Launcher, Remote Worker Dock | `OriginOffset` + `RangeMin`/`RangeMax` rect |
| Sky scan | `ScannerNetworkVisualizer`, `SkyVisibilityVisualizer` | Space Scanner; Telescope, Enclosed Telescope | `OriginOffset` + horizontal `RangeMin`/`RangeMax`, columns up to the sky |
| Radiation | `RadiationEmitter` | Research Reactor, Manual Radbolt Generator | `emissionOffset` + `emitRadiusX/Y` ellipse (+ arc angle/direction) |

Notes from the dig:

- **The Deodorizer's config class is `AirFilterConfig`** (prefab ID `AirFilter`) —
  `consumptionRadius = 3`, sample cell `(0,0)`.
- **`StationaryChoreRangeVisualizer` is deprecated** in current game code; everything
  moved to `RangeVisualizer`, which is a pure data component (rect + line-of-sight
  flags) rendered by `RangeVisualizerEffect`. Reading it gives us Klei's own authoritative
  display rect per building — including buildings added by future DLCs, for free.
- The old *Show Building Ranges* "space scanner interference" circle no longer exists:
  scanner quality is now a **sky-visibility scan** (`SkyVisibilityInfo`, ±15 columns),
  which the game visualizes with `ScannerNetworkVisualizer`.
- The **AutoMiner / SolidTransferArm components' own x/y/width/height fields** duplicate
  their `RangeVisualizer` rect, so the export reads only the visualizer (no
  double-counting).

### What Show Building Ranges taught us

Decompiling the installed `ShowRange.dll` (Peter Han, workshop 1960996649) settled the
one genuinely undocumented semantic — the sim's element-consumer reach:

- The sim spreads consumption **orthogonally from the sample cell through non-solid
  cells, up to `consumptionRadius − 1` steps** (the mod's `FindReachableCells` BFS
  enqueues neighbours only while `cost < radius − 1`).
- In open space that is a **Manhattan diamond** of reach `radius − 1`: the Deodorizer's
  radius 3 → 13 cells ("a couple tiles in any direction"), a pump's radius 2 → 5 cells,
  radius 1 (many machines) → the sample cell only.
- The current mod version *only* visualizes element consumers — lights and machine rects
  are covered by the game's own `Light` overlay and `RangeVisualizer` — which confirmed
  that reading Klei's components covers the rest.

---

## The export contract

Each building whose prefab carries any of the five mechanisms gets:

```jsonc
"areasOfEffect": [
  {
    "kind": "light",                  // semantic family (below)
    "source": "Light2D",              // game component, for provenance/debugging
    "shape": "cone",
    "origin": { "x": 0, "y": 0 },     // cell the effect emanates from
    "blockedBySolids": true,          // solids occlude/shrink the area at runtime
    "cells": [[0,0],[-1,-1],[0,-1],[1,-1], ...],  // nominal affected cells
    // ... kind-specific params (lux, radius, rectMin/rectMax, ...) — see EXPORT_SCHEMA.md
  }
]
```

Design decisions, so the frontend stays dumb:

- **`cells` is precomputed** in the exporter for every finite shape (circle, cone, quad,
  diamond, rect). The website just shades the listed cells — no reimplementation of
  Klei's octant-scan boundary rules, no drift. Cells are `[x, y]` integer pairs written
  compactly (a custom converter keeps the indented JSON from exploding to 4 lines/cell).
- **Offsets use the `utilities[].offset` convention** — relative to the building's origin
  cell, pre-rotation, negative values allowed. Rotation is the same transform the site
  already applies to ports. `cells` are relative to the *building* origin (the entry's
  `origin` is already folded in; it's kept for drawing the emitter point).
- **`cells` is the nominal, unobstructed area.** In game, solid tiles shrink these areas
  (light shadow-casting, consumer BFS, rect line-of-sight). A static database can't know
  the surroundings, so the website should present the area as "reach, assuming clear
  space" — exactly what the game's own build-preview visualizers show.
- Two shapes ship **params-only (no `cells`)**:
  - `ellipse`/`ellipseArc` (radiation): trivially derivable
    (`(dx/radiusX)² + (dy/radiusY)² ≤ 1`), and the Research Reactor's 25×25 ellipse
    alone would be ~2 000 entries.
  - `skyColumns` (sky scans): the columns run to the top of the world, which depends on
    the map. Shade columns `origin.x+scanMinX .. origin.x+scanMaxX` from `origin.y`
    upward.
  A 1024-cell safety cap also drops `cells` (params remain) if some modded building
  produces an absurd area.

### Geometry semantics (validated by unit tests)

All geometry replicates the game's cell math (`DiscreteShadowCaster`,
`RangeVisualizerEffect`, sim BFS) with occlusion removed:

| `shape` | Definition (relative to `origin`) |
|---|---|
| `circle` | all cells with `dx² + dy² ≤ range²` (boundary inclusive) |
| `cone` | origin cell + for each row `d = 1..range` below: cells with `|dx| ≤ d` and `dx² + d² ≤ range²` (45° cone clipped to Euclidean range — the Ceiling Light's rounded fan) |
| `quad` | a strip `width` cells across (even widths are asymmetric: `-(width/2−1) .. width/2`), extruded `range` rows along `direction`, starting **at** the origin row; plus the origin cell |
| `diamond` | `|dx| + |dy| ≤ radius − 1` (element intake reach) |
| `rect` | `rectMin .. rectMax` inclusive (Klei's own `RangeVisualizer` rect) |
| `ellipse`(`Arc`) | `(dx/radiusX)² + (dy/radiusY)² ≤ 1`, optionally clipped to `arcAngle` degrees centred on `arcDirection` (0° = east, CCW) |
| `skyColumns` | columns `scanMinX .. scanMaxX`, from origin upward to the world top |

Light origin math: building prefabs sit at the **bottom-center of their origin cell**, so
a float component offset lands in cell `(floor(0.5 + x), floor(y))` — e.g. the Floor
Lamp's `Offset (0.05, 1.5)` emits from the lamp head at cell `(0, 1)`.

### Worked examples

| Building | Entry |
|---|---|
| Ceiling Light | `light`/`cone`, origin `(0,0)`, range 8, lux 1800 → 55 cells fanning down |
| Floor Lamp | `light`/`circle`, origin `(0,1)`, range 4, lux 1000 → 49-cell disc around the head |
| Mercury Ceiling Light | `light`/`quad`, width 3, direction South, range 8 → 3×8 beam |
| Deodorizer | `elementIntake`/`diamond`, radius 3, element ContaminatedOxygen → 13 cells |
| Gas Pump | `elementIntake`/`diamond`, radius 2 → 5 cells |
| Liquid-Cooled Fan | `elementIntake`/`diamond`, radius 8 → 113 cells |
| Robo-Miner | `operationRange`/`rect`, origin `(0,1)`, rect `(-7,-1)..(8,7)` → the 16×9 dig field |
| Auto-Sweeper | `operationRange`/`rect`, rect `(-4,-4)..(4,4)` → 9×9 |
| Duplicant Motion Sensor | `operationRange`/`rect`, rect `(-2,0)..(2,4)` |
| Space Heater | `operationRange`/`rect`, rect `(-4,-4)..(5,5)` (10×10 heating box) |
| Steam Turbine | `operationRange`/`rect`, rect `(-2,-2)..(2,-2)` → the 5 steam intake cells |
| Space Scanner | `skyScan`/`skyColumns`, `scanMinX -15 .. scanMaxX 15` |
| Telescope | `skyScan`/`skyColumns`, origin `(0,3)`, `-4 .. 5` |
| Research Reactor | `radiation`/`ellipse`, origin `(0,2)`, radius 25×25, Constant |
| Manual Radbolt Generator | `radiation`/`ellipse`, origin `(0,2)`, radius from `RAD_LIGHT_SIZE`, 120 rads |

(Values read from current game configs; the export always reflects the running game
version, so these are illustrations, not contracts.)

### Frontend rendering guidance

1. Pick the entries worth showing by `kind` (probably `light`, `elementIntake`,
   `operationRange` first; `radiation`/`skyScan` behind a toggle).
2. Shade `cells` (or derive ellipse/columns from params) in the building's rotated frame,
   using the same offset rotation as ports.
3. Tooltip material is in the params: lux + colour for lights, element + kg/s for
   intakes, rads for radiation.
4. Multiple entries per building are normal (Oxygen Mask Station has two intakes; the
   Algae Terrarium has an active and a passive one; a modded building may combine kinds).
   Buildings with **no** entries omit `areasOfEffect` entirely.

---

## Implementation map

| Piece | File |
|---|---|
| DTO + compact cell serializer | `OniExtract2024/model/OutAreaOfEffect.cs` |
| Prefab scan + pure geometry | `OniExtract2024/AreaOfEffectBuilder.cs` |
| Wiring (`bBuild.areasOfEffect`) | `OniExtract2024/ExportBuilding.cs` (`AddNewBuildingEntity`) |
| Schema reference | `EXPORT_SCHEMA.md` (bBuildingDefList entry + OutAreaOfEffect shape) |
| Geometry/serialization tests | `OniExtract2024.Tests/AreaOfEffectTests.cs` (8 tests, all passing) |

Because the scan is component-driven (never a building-name list), **modded buildings
picked up by the in-game export get their areas automatically** — e.g. a mod lamp's
`Light2D` or a mod pump's `ElementConsumer` exports without any per-mod work. The
offline `mods/` pipeline (MOD_OFFLINE_EXTRACTION.md) does not compute AoE; that's fine
while the in-game export remains the primary mod path.

### Verification checklist (next game run)

- [ ] Run the game → main-menu export → confirm `areasOfEffect` appears in
      `building.json`.
- [ ] Spot-check Deodorizer (13-cell diamond), Ceiling Light (cone, 55 cells incl.
      origin), Floor Lamp (circle at `(0,1)`, 49 cells), Robo-Miner (144-cell rect),
      Steam Turbine (5 cells at `y = -2`).
- [ ] Confirm no entry on plain buildings (e.g. Manual Generator has no `areasOfEffect`
      key at all).
- [ ] Confirm WaterTrap exports **no** `operationRange` (its rect is runtime-driven and
      deliberately skipped).

---

## Deliberately out of scope (future work)

| Range | Why skipped | If wanted later |
|---|---|---|
| Decor radius | Every building has a `DecorProvider`; exporting it as AoE would put an entry on ~every building, and decor is a diffuse stat rather than a crisp operational area | Export `baseDecor` + `baseRadius` as plain fields on the building entry |
| Noise (`NoisePolluter`) | The noise system is vestigial/disabled in the shipping game | Same pattern as decor |
| Sweepy Dock reach | No range component on the prefab; the 16-ish tile reach lives in chore/pathing logic | Hardcode from `SweepBotStationConfig` constants if the site wants it |
| Water Trap trail | `RangeVisualizer` rect is all-zero on the prefab and driven at runtime (grows down through water) | Needs bespoke handling; low value |
| Radiation falloff | Cells receive rads scaled by distance and material attenuation in the native sim; we export the nominal ellipse + source rads only | Falloff curve could be approximated if the site wants a gradient |
| Rocket engine flame `Light2D` | Exported as a normal `light` entry (it is one); site may want to filter `kind == "light"` on rocket modules | Filter client-side by category |
