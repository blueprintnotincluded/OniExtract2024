# uiImageRect durability — the "turbine squished to 5×3" regression

Diagnostic + fix notes from the 2026-06-22 session. Read before touching how
`uiImageRect` reaches `building.json`.

## Symptom

A fresh export shows the steam turbine squished into its 5×3 footprint instead of
its true ~5×5 visual extent (the exhaust that hangs below the footprint is crushed).
Reverting *only the image files* in the website repo — keeping the new
`building.json` — makes it look right again.

## What was NOT wrong

The rect geometry and the merge code from the earlier sessions are intact and
correct:

- `UiImageRect.FromCrop` (in `OniExtract2024.Core`) still maps the opaque crop back to
  footprint cells, and `BuildingKanimRenderer` sizes its texture / centres its camera
  with the exact formula `ComputeRect` reconstructs (`cells * 200 + 2*400` px, footprint
  centred). Verified consistent.
- `ExportBuildingImages.PatchBuildingJsonRects` still merges measured rects into
  `bBuildingDefList[].uiImageRect`.

So this is not a math or merge bug.

## Root cause — the rect data isn't durable

`uiImageRect` is produced **only** by the in-game building-image pass:
- the full **Export Building Images** sweep, or
- the inspector's single-building "touch-up" export (`ExportBuildingImages.ExportSingle`).

Both patch the rect into `building.json` *after the fact*.

But `building.json` is authored from scratch by the **main-menu JSON pass**
(`Patches.cs` → `ExportBuilding`), which:
- runs at **every game load**, and
- does **not** emit `uiImageRect` at all (the field didn't exist on `BBuildingEntity`).

The two outputs therefore drift apart:

| Output | Where it lives | Lifetime |
|---|---|---|
| hi-res tight-cropped PNGs | `export/ui_image/*.png` | persist on disk across sessions |
| `uiImageRect` placement | `building.json` only | **clobbered every game load** |

A hi-res image from a *prior* sweep survives, but its rect is wiped the next time
the main-menu pass rewrites `building.json`. Tight-cropped tall image **+ missing
rect** → the website falls back to stretch-to-footprint → squished. A footprint-shaped
image (the old atlas sprite) survives the stretch, which is why reverting just the
images "fixes" it.

## Evidence (the export that triggered this)

- `export/database/building.json` had **2** `uiImageRect` entries out of 449:
  `AdvancedResearchCenter` and `AirConditioner`.
- `Player.log` showed **no** full-sweep markers — only two inspector single-exports,
  each logging `buildings with uiImageRect placement: 1 / 449`. Those two are exactly
  the two rect-bearing entries.
- `SteamTurbine2` had no rect; its PNG on disk was the 151×122 low-res atlas sprite
  (the full sweep was never run this session).

So the export was: main-menu pass (atlas icons + rect-less `building.json`) + two
inspector touch-ups. Any building not re-rendered in that same session — including the
turbine — lost its rect while its (separately committed) hi-res image lived on.

## Fix — persist rects to a sidecar the main-menu pass reads

Decouple *measuring* the rect from *emitting* it, mirroring `pose_overrides.json`:

1. **`UiImageRectStore`** — a durable sidecar at `export/ui_image_rects.json`
   (prefabId → rect). `SaveAll` merges freshly measured rects and writes the whole
   store; `TryGet` reads it back. Survives across sessions.
2. **The in-game pass writes the sidecar.** `PatchBuildingJsonRects` now also calls
   `UiImageRectStore.SaveAll(rects)`, so every sweep / touch-up keeps the sidecar
   current (and still patches the live `building.json` for same-session immediacy).
3. **The main-menu pass emits from the sidecar.** `BBuildingEntity` gains a nullable
   `uiImageRect` field (omitted when absent, per the contract), and
   `ExportBuilding.AddNewBuildingEntity` populates it from `UiImageRectStore` by
   `buildingDef.Tag.Name`.

Result: `building.json` always carries the last-measured rect for every building that
has ever been rendered, regardless of whether you re-run the sweep this session. The
rect data now genuinely travels *in the export*, in sync with the images.

### Run-order note (still good practice)

The sidecar removes the hard ordering requirement, but the intended flow is unchanged:
run the main-menu export, then the in-game **Export Building Images** sweep, so newly
rendered images and their rects land together. The sidecar just means a later
main-menu-only export no longer silently drops the rects.
