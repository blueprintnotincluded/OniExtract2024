# Building image export — design notes

Read before changing the render path, the pose selection, or how `uiImageRect` reaches
`building.json`. Root guide: [CLAUDE.md](../../CLAUDE.md).

## What this directory does

Re-renders every buildable building, plus a fixed list of terrain features (geysers, vents,
volcanoes, the oil reservoir; `TerrainFeaturePrefabNames`), as a hi-res icon from its **live
kanim**, at 200 px/cell, and overwrites the low-res atlas sprites the game-data export wrote
to `ui_image/`. Terrain features have no `Building`, so their footprint comes from
`KBoxCollider2D` and their rect reaches the website only through `ui_image_rects.json`. It runs
as its own in-game tool (Esc → *Export Building Images*), separate from *Export Game Data*.

| File | Role |
|---|---|
| `ExportBuildingImages.cs` | The sweep coroutine; spawns each building, drives the snapshotter, patches rects into `building.json`, writes the report. `ExportSingle` does one building for the inspector's touch-up. |
| `BuildingImageSnapshotter.cs` | Per-building: poses the kanim, sizes the canvas from the anim, snapshots at 200 px/cell, trims to the opaque bbox, writes `ui_image/{prefabId}.png`. |
| `BuildingKanimRenderer.cs` | The shared camera + RenderTexture pipeline, used by both the sweep and the inspector. |
| `BuildingImageReport.cs` | Writes `export/building_image_report.json`: how each image was made. See below. |
| `OniExtract2024.Core/RenderCanvas.cs`, `ActivePose.cs` | The pure halves, unit-tested in CI: canvas layout, edge contact and rect mapping; the pose rule. |
| `BuildingSpawnFilter.cs` | **Single source of truth** for "can this building be spawned and rendered outside its normal context?" (`IsRenderable`) and "does it have art to render at all?" (`HasArt`). Used by the sweep *and* the inspector's chooser. |
| `BuildingPoseOverrides.cs` | `Overrides` dictionary of hand-picked anim+frame per building, plus percent↔frame helpers. |
| `BuildingPoseInspectorScreen.cs` | In-game pose-picker UI for populating that dictionary. |
| `UiImageRectStore.cs` | Durable sidecar for measured rects. See below — this is the non-obvious one. |
| `ImageCrop.cs`, `BiomeTintNeutralizer.cs` | Opaque-bbox trimming; suppressing biome tint on the render. |

## Never play the `"ui"` animation before a snapshot

It renders at atlas/icon scale (~100 px/cell) rather than live-kanim scale, and silently
shrinks every output — no error, just small images. The snapshotter deliberately poses each
building in an *active* state instead, seeked to `PoseFramePercent = 0.5`, mid-loop, so the
building is fully deployed rather than caught at the start of its timeline. `"ui"` is in
`ActivePose.InactiveMarkers` for this reason; do not remove it.

## Which active state: the one closest to the footprint

Effects are drawn outside the body and only in some states: steam (SteamTurbine2), wind
(VerticalWindTunnel), sparks, bubbles, light cones, a stray swoosh frame (FairGasWallPump). Posed
in `working_loop`, they inflated the crop and set the rect. The game has no "effect" flag on
symbols, but every anim's frames say how far its art reaches, and the states without effects
reach only as far as the body.

`ActivePose.Choose` takes every anim the name scorer accepts, drops anims with no measurable
extent, prefers steady states to `_pre`/`_pst` transitions, and keeps the anims within 0.25
cells of the one whose extent is closest to the footprint's. The most active of those wins,
ties going to name order, never to the controller's dictionary order (which moves when Klei
adds an anim). A building without effects has the same extent in every state and keeps its
most active one. `pose_overrides.json` is read first, as before.

Small overlay anims (`light_on`, `meter`) are far *smaller* than the footprint, so the
two-sided distance keeps them out; an excess-only measure picked them in an offline
simulation. Introducing this rule changed about 100 icons once, nearly all from `working_loop`
to `on`.

## How far an anim reaches: measured from its frames

`BuildingImageSnapshotter.TryGetAnimBox` walks every frame of an anim and puts each drawn
element's symbol-frame box (`SymbolFrameInstance.bboxMin/bboxMax`) through the element's
transform, then the controller's scale and flips: the box the GPU will draw into, over the
whole anim. The chooser compares its half-extents about the anim origin; the canvas uses the
box itself. Two traps on the way:

- **Hidden on the root is not the same as not drawn.** A building with a `ForegroundLayer`
  gets a layer controller (`KAnimLayering`) that draws the symbols flagged FG or SH, and the
  root hides them with `SetSymbolVisiblity`. The Wall Pumps mod puts its whole body there, so a
  box of what the root shows had no body and the swoosh pose won. Layered symbols count.
- **Resolve symbols through the controller's own build** (`curBuild`, then its other anim
  files), not the batch group's lookup, which returns the first build carrying the name.
  This one did not change the 2026-10-18 export, but it is the build the controller draws.

The game also stores an authored extent, `KAnim.Anim.unScaledSize`, and the first version of
this code used it. The first in-game run showed why not: it is a half-extent, symmetric about
the origin, so it reaches as deep below a floor building as the building is tall; and kanims
carry garbage there. wind_tunnel's working anims say 536870 cells, MicrobeMusher's working_loop
37003, MiniFridge's `working` NaN, and every anim of the Wall Pumps mod NaN, so FairGasWallPump
kept its swoosh. Offline, the frame box matched the authored extent of the base game's
building anims to within about 0.1 cell wherever the latter was sane. It remains the fallback
when no element resolves; anything over 20 cells or not finite is rejected
(`RenderCanvas.IsUsableHalfExtent` / `IsUsableBox`).

## The canvas is sized from the anim

The canvas used to have a fixed two-cell margin, and art past it was cut off with the crop
edge on the canvas edge. `RenderCanvas.ForBox` now sizes each side from the posed anim's box,
placed at `PositionIncludingOffset`. Points that matter:

- Margins never shrink below two cells, and never grow past 16. Every margin is whole pixels,
  so the footprint stays on pixel boundaries and a bigger canvas changes no pixel of the art.
  The crop works on `Color32`, a quarter of the memory of `Color`.
- The box does not cover child controllers (meters and the like). If the crop still touches
  an edge, the snapshotter widens that side by 4 cells and draws again, up to three times.
  Art at the edge after that is logged and reported `clipped`, and Test-Export fails on it.

## The spawn point is part of the picture

A spawned building draws some parts from its surroundings, and the sweep spawns every building
at one cell, 12 down and 12 left of the camera's centre:

- **Depth below.** The pitcher pump (`LiquidPumpingStation`) shows `pipe1..4` and the brackene
  fountain (`BuildingPointStraw`) `straw1..N` for each open cell under them. The first run of
  this code spawned them over a shaft and their icons gained a 4-cell hose.
- **Neighbours.** Glass and drywall panels (`AnimTileable`) join the backwalls next to them.
- **Batches.** `RenderBatch` draws whole 32×32-cell batches, so a colony building of the same
  render group inside the canvas is drawn into the image.

So the sweep picks its own spot (`SpawnArea.Find`, the search itself in Core as `SpawnSite`):
the cell nearest the old spot (12 down and 12 left of the camera's centre) that stands on 3
cells of **solid floor** (the cells the pump and the fountain look down from), has 7×6 cells of
**gas or vacuum** above (the largest footprint; nothing spawns into rock or liquid, where
buildings that set cells solid would change the world), and has **no building or geyser drawn
by kanim** and **nothing unrevealed** in a 17×15-cell box around it. Plants, debris and
critters do not count: they never share a render batch with a building. Natural cave floors
qualify; the player prepares nothing. Player.log says where it spawned and how many cells
passed each test; when the world has no such spot, the old one is used and the closing message
says so. The connection-sprite tool uses the same spot. The first version also counted plants
and debris and asked for 9×8 cells of air, and found nothing in a lived-in colony.

Status icons (no power, unreachable, ...) are not this problem: the game draws them as UI, and
the renderer only draws kanim batches.

## `building_image_report.json`

Written by the sweep beside `ui_image_rects.json`, one entry per rendered prefab: the anim and
frame rendered, where the pose came from (`auto`, `override`, `default`), `spawned` or
`artOnly`, the canvas size, the authored extent in `uiImageRect` space, clipping, and the edges
the canvas grew at. Field reference: [docs/EXPORT_SCHEMA.md](../../docs/EXPORT_SCHEMA.md). The
website does not read it. It exists so that the diff between two exports explains itself:
"its pose went from `working_loop` to `on`" instead of a pixel diff that renders' jitter can
fool. Read it first when an image changed.

## Buildings that cannot be spawned are rendered art-only

Some buildings crash or corrupt state when spawned outside their normal context: rocket modules
(`RocketModuleCluster`) and rocket interiors, `LaunchPad`, `RocketControlStation`, deprecated
content, and anything else `IsRenderable` rejects. The spawn path never sees them. Rocket modules
are also `showInBuildMenu: false`, so `IsRenderable` drops them before any rocket check.

None of those get a low-res fallback. The sweep renders each building that has kanim art
(`HasArt`) through `ExportBuildingImages.CreateArtOnly`. That method builds the game's
`EffectTemplate`, an entity with a `KBatchedAnimController` and nothing else, gives it the
building's anim files, prefab tag and controller settings, and places it the way
`BuildingDef.Create` would. The snapshotter then handles it like any other render, pose
overrides included. A building with no kanim on `BuildingComplete` (plain tiles, drawn by
`BlockTileRenderer`) is the only case left on the atlas icon.

The cost: art-only cannot show anything a building adds at spawn time, such as meters, child
controllers or symbols toggled in `OnSpawn`. If one of these renders wrong, fix the pose or the
symbols there. Do not loosen `IsRenderable`. Two such fixes are in place:

- **The visualizer offset.** A spawned building's controller gets `def.GetVisualizerOffset()`
  (half a cell right for an even width) in `BuildingComplete.OnPrefabInit`, or the same value
  from `Rotatable.OnSpawn`. The prefab's controller does not carry it, so `CreateArtOnly` sets
  it. Copying it from the prefab left every even-width art-only render half a cell left of its
  footprint, rect included, until 2026-10-18.
- **`meter_target`.** `MeterController` hides that placeholder symbol on the building whenever it
  builds a meter in `OnSpawn`, which art-only never runs, so WallToilet showed a bar reading
  "meter target". `BuildingImageSnapshotter.PrepareForSnapshot` hides it on art-only renders
  only. A real spawn keeps what its own `OnSpawn` chose: Polymerizer shows `meter_target` again
  after building its meter, because there the symbol is real art. On LiquidCooledFan (art-only)
  the placeholder is the water jug a meter would draw, so its icon has no jug. Placeholders a
  building names itself (`logicmeter_target` and so on) are not hidden.

Spawning deprecated content corrupts state and
crashes the sweep. The culprit was never isolated, so the spawn exclusion stays blunt.
`SteamTurbine` is allowlisted for a real spawn. Vet any other addition by spawning it in
isolation first: a crash here loses the whole sweep, not one building.

Rocket modules also need a `CraftModuleInterface` ancestor if spawned for real, so the sweep
attaches the cluster components to the world object. That predates art-only and is harmless.

## `uiImageRect` must come from the sidecar, not the render

The subtlest failure in this subsystem, and one that has already shipped once.

A rect can only be *measured* from a live render, but `building.json` is authored from scratch
by the game-data export **every time it runs**. Patch the rect straight into `building.json`
and it survives exactly until the next data export, while the hi-res PNG on disk persists
indefinitely.
The result is a tight-cropped tall image with no rect, which the website stretches to the
footprint — the "steam turbine squished to 5×3" bug.

So the flow is deliberately decoupled, mirroring `pose_overrides.json`:

1. The image sweep measures rects and calls `UiImageRectStore.ReplaceAll` →
   `export/ui_image_rects.json`: the sweep measured everything it can render, so its rects
   *are* the file. Merging kept rects of prefabs no longer loaded (a disabled mod), which
   reached the website as current buildings. The inspector's single touch-up merges with
   `SaveAll`. The sweep *also* patches `building.json` directly, for same-session immediacy only.
2. The game-data export reads `UiImageRectStore` by `buildingDef.Tag.Name` and emits
   `uiImageRect` on `BBuildingEntity`, then calls `UiImageRectStore.PruneTo` with the loaded
   prefabs and deletes `ui_image/` PNGs no step of the run accounted for.

Anything that makes the rect travel only through `building.json` reintroduces the bug. The
field is **omitted when absent, never emitted as null**.

The PNG has the same problem the other way round: the game-data export also writes
`ui_image/{prefabId}.png` every time it runs, and an atlas icon written over a render leaves the
rect describing an image that is no longer on disk. So `ExportUISprite` skips the icon write
when `ExportBuildingImages.PngMatchesRect` says the file already there is the measured render.
It checks the file itself, not just whether a rect exists, because the icon filename follows
the `SaveUIFileName` option and only matches the rect's prefab-tag key in ID mode. Full diagnosis:
[docs/archive/UIIMAGERECT_DURABILITY.md](../../docs/archive/UIIMAGERECT_DURABILITY.md).

## Other things worth knowing

- **Pose a controller and you must flush it.** An off-screen controller is flagged
  `isVisible=false`, so `SetDirty()` alone skips the frame write and you capture the spawn
  default. The working sequence is `SetVisiblity(true)` + `forceRebuild = true` + `SetDirty()` +
  `UpdateFrame(0f)`.
- **Anim names are not the dict keys.** `KAnimControllerBase.anims` is keyed by hash; the
  readable name comes from resolving each entry's `animIndex` through `GetAnim(int).name`.
  Reading the keys directly yields hex strings.
- **Material tint is captured by the render.** Buildings that derive tint from their
  construction element (tiles, Tempshift Plate) render in the neutral debug element's colour,
  because the sweep builds every def from `Unobtanium`. Cosmetic, deferred.
- **Icons are named by prefab ID**, not player-facing name — the Auto-Sweeper is
  `SolidTransferArm`.
- **Run order:** *Export Game Data* first, then this sweep, so images and rects land together.
  The sidecar removes the hard requirement but not the good practice.
- Progress is logged to `Player.log` on lines prefixed `OniExtract:`.

## Licence note

`BuildingImageSnapshotter` adapts camera-snapshot code from the GPL-2.0 project
Sgt_Imalas-Oni-Mods (`AnimExportTool/AETE_KbacSnapShotter.cs`), itself courtesy of
[aki-art/ONI-Mods](https://github.com/aki-art/ONI-Mods). Keep the attribution header intact.
