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
symbols, but every anim carries its authored extent (`KAnim.Anim.unScaledSize`), and the states
without effects have the body's.

`ActivePose.Choose` takes every anim the name scorer accepts, drops anims with an unusable
extent, prefers steady states to `_pre`/`_pst` transitions, and keeps the anims within 0.25
cells of the one whose extent is closest to the footprint's. The most active of those wins,
ties going to name order, never to the controller's dictionary order (which moves when Klei
adds an anim). A building without effects has the same extent in every state and keeps its
most active one. `pose_overrides.json` is read first, as before.

Small overlay anims (`light_on`, `meter`) are far *smaller* than the footprint, so the
two-sided distance keeps them out; an excess-only measure picked them in an offline
simulation. Introducing this rule changed about 100 icons once, nearly all from `working_loop`
to `on`.

## The canvas is sized from the anim

The canvas used to have a fixed two-cell margin, and art past it was cut off with the crop
edge on the canvas edge. `RenderCanvas.ForArt` now sizes each side from the posed anim's
authored extent, scaled by the controller and placed at `PositionIncludingOffset`. Points that
matter:

- The extent is a **half-extent about the anim origin**, so the box is symmetric: it reaches
  as far below a floor building as above. Canvases are taller than the art needs; that costs
  memory, not pixels, because every margin is whole pixels and the footprint stays on pixel
  boundaries. The crop works on `Color32` to keep the biggest canvases (about 7 Mpx) cheap.
- Margins never shrink below two cells, and never grow past 16.
- Some anims carry garbage: wind_tunnel's working anims 536870 cells, MicrobeMusher's
  working_loop 37003, MiniFridge's `working` NaN. `RenderCanvas.IsUsableHalfExtent` rejects
  anything over 20 cells, and such an anim gets the default margin.
- The extent does not cover child controllers (meters and the like). If the crop still touches
  an edge, the snapshotter widens that side by 4 cells and draws again, up to three times.
  Art at the edge after that is logged and reported `clipped`, and Test-Export fails on it.
- A bigger canvas sees more of the world around the spawn point. `RenderBatch` draws whole
  32×32-cell batches, so a same-group building of the colony within view would be drawn into
  the image. Export from a save with clear space around the spawn point (12 cells down-left of
  the view centre).

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
  builds a meter, which art-only never does, so WallToilet showed a bar. Every render path calls
  `BuildingImageSnapshotter.PrepareForSnapshot`, which hides it and re-applies the hidden set.
  Placeholders a building names itself (`logicmeter_target` and so on) are not hidden. Spawning deprecated content corrupts state and
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
