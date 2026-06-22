# Building Images — Diagnostic Findings

Notes from the post-ship debugging session. Read before touching
`BuildingImageSnapshotter.cs` or `ExportBuildingImages.cs`.

---

## Current state (commit c9eb612)

The "Export Building Images" pass works end-to-end and completes without crashing.
All `ShowInBuildMenu && !Deprecated` buildings are rendered at 200 px/cell and
written to `ui_image/{prefabId}.png`. Two specific buildings the website shows still
have outstanding issues — see below.

---

## What the website actually does with ui_image/ icons

From `WEBSITE_POSTPROCESSING.md`:

> *"We scale every icon to the building footprint regardless of the file's pixel
> size."*

The website scales each icon to `widthInCells × heightInCells × 100 px/cell` on
screen. This means **image aspect ratio must match the building footprint** or the
icon will appear squished/stretched. If the image is the correct width but taller
(due to visual overhang below the footprint), the extra height hangs below the
selectable footprint area — that's fine and was the old sprite's behaviour too.

---

## Building name mismatches

The names the website and the player use differ from the prefab IDs in the database.
Confirmed via the exported `building.json`:

| Player name | prefab ID (`building.json` `name`) | cells | status |
|---|---|---|---|
| Steam Turbine (old) | `SteamTurbine` | 5×4 | **deprecated** (`DeprecatedContent` tag) |
| Steam Turbine | `SteamTurbine2` | 5×3 | current, non-deprecated |
| Steam Engine | `SteamEngineCluster` | 7×5 | current |
| Auto-Sweeper | `SolidTransferArm` | 3×1 | current, `defaultAnimState = "off"` |
| Sweepy's Dock | `SweepBotStation` | 2×2 | current |

There is no `AutoSweeper` prefab ID. Looking for `AutoSweeper.png` will always miss.

---

## The "ui" animation — do not play it on a spawned building

**Symptom:** Playing `kbac.Play("ui", KAnim.PlayMode.Paused)` before snapshotting
caused all exported images to shrink to ~100 px/cell instead of 200 px/cell.
`SolidTransferArm` came out 288×383 px when it should be ~600×200 px. All 1,239
files in the export ended up under 400 px wide.

**Why:** The `"ui"` KAnim animation is Klei's small icon animation. Its frames are
drawn in world-space at the same scale the build-menu atlas bakes them (~100 px/cell
equivalent in world coordinates). Rendering it through our 200 px/cell camera
captures those small frames and gives us atlas-sprite-sized output, not live-kanim
output. The `"ui"` animation is useful for the sprite-extraction path
(`Def.GetUISpriteFromMultiObjectAnim`) but is wrong for live camera rendering.

**Rule:** Never call `kbac.Play("ui")` before a camera snapshot.

---

## Deprecated buildings

Our filter `def.Deprecated || !def.ShowInBuildMenu` correctly skips `SteamTurbine`
(which carries the `DeprecatedContent` tag). However, the website still displays
deprecated buildings, so `ui_image/SteamTurbine.png` remains the old 144×124 atlas
sprite. The user sees it as a "steam turbine that cuts off a block and a half early."

**Attempted fix:** Removing `def.Deprecated` from the filter caused a game crash.
The exact cause was not isolated before reverting — some deprecated buildings may
share the crash-on-spawn properties as rocket modules (null virtual-network key,
`ReorderableBuilding` issues, etc.).

**Next step:** Before removing the `Deprecated` filter, audit which deprecated
buildings are in the database and check whether any carry components that are known
to crash (`RocketModuleCluster`, `WireUtilitySemiVirtualNetworkLink`, etc.).
A safe approach: add deprecated buildings to the render pass only after confirming
they don't crash.

---

## SolidTransferArm animation state

The Auto-Sweeper (`SolidTransferArm`, 3×1) spawns in `defaultAnimState = "off"`.
In `"off"` the arm is retracted — the building looks like a flat 3×1 bar. The
website/game shows it with the arm extended upward (a T-shape bleeding into the row
above), which requires a different animation state, likely `"on"` or `"working"`.

This is the Phase 3 fidelity issue flagged in `IMAGES_PLAN.md`. The fix is to detect
`defaultAnimState == "off"` and try `"on"` instead, or to inspect which animations
the building has and pick the fullest one. Do not use `"ui"` (see above).

---

## Crash history — what component combinations break

| Component on BuildingComplete | Effect when spawned without context | Fix applied |
|---|---|---|
| `WireUtilitySemiVirtualNetworkLink` | Registers null key in `CircuitManager` virtual-network dict; `CircuitManager.Rebuild()` throws every tick until game exits | Now caught by `RocketModuleCluster` filter (broader) |
| `RocketModuleCluster` | `OnSpawn`/`OnCleanUp` null-refs; may corrupt virtual-network state | Filtered by `def.BuildingComplete.TryGetComponent<RocketModuleCluster>()` |
| `ReorderableBuilding` (without `RocketModuleCluster`) | Logs NullRef on spawn but does NOT corrupt game state; safe to continue | No filter needed |
| Unknown deprecated building | Crashed the game when `def.Deprecated` filter was removed | Reverted; root cause unknown |

**The CraftModuleInterface fix:** before the spawn loop, we add
`ClusterDestinationSelector`, `ClusterTraveler`, `Clustercraft`, and
`CraftModuleInterface` to the world GameObject. This gives rocket-module components
an ancestor to bind to so they don't null-ref. Without it, many DLC rocket modules
crash on spawn even with the `RocketModuleCluster` filter in place.

---

## PaddingPx

Current value: **200 px** (1 cell of padding each side at 200 px/cell).

Doubling to 400 px did not fix the "steam turbine cuts off early" symptom because
the turbine involved was `SteamTurbine` (deprecated, skipped entirely — old atlas
sprite served). `SteamTurbine2` was being rendered but at icon scale due to the
`"ui"` animation bug. Once both bugs are fixed, revisit whether 200 px of padding
is sufficient for the actual buildings that render beyond their footprint.

---

## How to diagnose future image issues

1. **Check which prefab ID the website is using** — player names ≠ prefab IDs.
   Look up the name in `export/database/building.json` → `name` field.

2. **Check the PNG dimensions** using the PowerShell snippet:
   ```powershell
   function Get-PngSize($p) {
       $fs = [IO.File]::OpenRead($p); $b = New-Object byte[] 28
       $null = $fs.Read($b,0,28); $fs.Close()
       $w = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($b[16..19],0))
       $h = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($b[20..23],0))
       "${w}x${h}"
   }
   Get-PngSize "...\export\ui_image\SomeBuilding.png"
   ```
   At 200 px/cell a typical 2×3 building should be ~400–700 px wide after bbox-crop.
   Under 400 px almost certainly means the `"ui"` animation was played (icon scale),
   or the building was skipped and the old atlas sprite is still there.

3. **Check the modification timestamp** on the PNG to confirm it came from the
   animation pass (timestamps cluster within ~2 minutes during the in-game export)
   versus the main-menu pass (runs at game load, earlier timestamp).

4. **Check `def.Deprecated`** — if the prefab carries `DeprecatedContent` tag it
   will be skipped by the current filter and the old atlas sprite remains.
