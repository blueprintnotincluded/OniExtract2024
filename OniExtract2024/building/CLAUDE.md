# Building image export — design notes

Read before changing the render path, the pose selection, or how `uiImageRect` reaches
`building.json`. Root guide: [CLAUDE.md](../../CLAUDE.md).

## What this directory does

Re-renders every buildable building as a hi-res icon from its **live kanim**, at 200 px/cell,
and overwrites the low-res atlas sprites the main-menu JSON pass wrote to `ui_image/`. It runs
as a manual in-game tool (Esc → *Export Building Images*), not as part of the automatic export.

| File | Role |
|---|---|
| `ExportBuildingImages.cs` | The sweep coroutine; spawns each building, drives the snapshotter, patches rects into `building.json`. `ExportSingle` does one building for the inspector's touch-up. |
| `BuildingImageSnapshotter.cs` | Per-building: poses the kanim, snapshots at 200 px/cell, trims to the opaque bbox, writes `ui_image/{prefabId}.png`. |
| `BuildingKanimRenderer.cs` | The shared camera + RenderTexture pipeline, used by both the sweep and the inspector. |
| `BuildingSpawnFilter.cs` | **Single source of truth** for "can this building be spawned and rendered outside its normal context?" (`IsRenderable`). Used by the sweep *and* the inspector's chooser. |
| `BuildingPoseOverrides.cs` | `Overrides` dictionary of hand-picked anim+frame per building, plus percent↔frame helpers. |
| `BuildingPoseInspectorScreen.cs` | In-game pose-picker UI for populating that dictionary. |
| `UiImageRectStore.cs` | Durable sidecar for measured rects. See below — this is the non-obvious one. |
| `ImageCrop.cs`, `BiomeTintNeutralizer.cs` | Opaque-bbox trimming; suppressing biome tint on the render. |

## Never play the `"ui"` animation before a snapshot

It renders at atlas/icon scale (~100 px/cell) rather than live-kanim scale, and silently
shrinks every output — no error, just small images. The snapshotter deliberately poses each
building in its most *active* state instead (`generating_loop`, `working_loop`, …) seeked to
`PoseFramePercent = 0.5`, mid-loop, so the building is fully deployed rather than caught at the
start of its timeline. `"ui"` is in `InactiveMarkers` for this reason; do not remove it.

## Deprecated buildings are skipped except a vetted allowlist

Spawning deprecated content without full game context corrupts state and crashes the sweep. The
exact culprit was never isolated, so the exclusion is deliberately blunt. `SteamTurbine` is
opted back in because the website still shows it. Vet any addition by spawning it in isolation
first — a crash here loses the whole sweep, not one building.

Related filters: `RocketModuleCluster` buildings are excluded entirely, and rocket modules need
a `CraftModuleInterface` ancestor (the sweep attaches the cluster components to the world object
so DLC rocket modules bind instead of null-ref'ing on spawn).

## `uiImageRect` must come from the sidecar, not the render

The subtlest failure in this subsystem, and one that has already shipped once.

A rect can only be *measured* from a live render, but `building.json` is authored from scratch
by the main-menu JSON pass **on every game load**. Patch the rect straight into `building.json`
and it survives exactly until the next load, while the hi-res PNG on disk persists indefinitely.
The result is a tight-cropped tall image with no rect, which the website stretches to the
footprint — the "steam turbine squished to 5×3" bug.

So the flow is deliberately decoupled, mirroring `pose_overrides.json`:

1. The in-game pass measures rects and calls `UiImageRectStore.SaveAll` →
   `export/ui_image_rects.json`. It *also* patches `building.json` directly, for same-session
   immediacy only.
2. The main-menu pass reads `UiImageRectStore` by `buildingDef.Tag.Name` and emits
   `uiImageRect` on `BBuildingEntity`.

Anything that makes the rect travel only through `building.json` reintroduces the bug. The
field is **omitted when absent, never emitted as null**. Full diagnosis:
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
- **Run order:** main-menu export first, then this sweep, so images and rects land together.
  The sidecar removes the hard requirement but not the good practice.
- Progress is logged to `Player.log` on lines prefixed `OniExtract:`.

## Licence note

`BuildingImageSnapshotter` adapts camera-snapshot code from the GPL-2.0 project
Sgt_Imalas-Oni-Mods (`AnimExportTool/AETE_KbacSnapShotter.cs`), itself courtesy of
[aki-art/ONI-Mods](https://github.com/aki-art/ONI-Mods). Keep the attribution header intact.
