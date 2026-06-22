# Work in Progress — Building Pose Inspector

## What we are doing and why

We are building hi-res in-game building icons for the website. Two pieces:

- **Export Building Images** (`ExportBuildingImages.cs`) — pause-screen button that spawns
  every renderable building one at a time, renders it at 200 px/cell via
  `BuildingImageSnapshotter` / `BuildingKanimRenderer`, and writes `ui_image/{prefabId}.png`
  plus a `uiImageRect` merged into `building.json`.
- **Inspect Building Poses** (`BuildingPoseInspectorScreen.cs`) — pause-screen button that
  opens a UI to pick the *right animation + frame* for each building, so the exported icon
  shows the building in an active/lit pose instead of its default (often dark) state. You
  record each choice in `BuildingPoseOverrides.Overrides`; the exporter reads it back.

The shared "can this building be spawned and rendered outside its context?" decision lives
in **`BuildingSpawnFilter.IsRenderable(def)`** — used by both the exporter sweep and the
inspector's chooser list.

### Supporting files

| File | Purpose |
|---|---|
| `BuildingSpawnFilter.cs` | Single source of truth for renderable buildings + `HasSMDef<T>` (uses `StateMachineController.GetDef<T>()`) |
| `BuildingKanimRenderer.cs` | Reusable camera + RenderTexture pipeline for both export and inspector |
| `BuildingPoseOverrides.cs` | `Overrides` dictionary (where you paste pose lines) + `PercentForFrame`/`FrameForPercent` helpers |
| `BuildingPoseInspectorScreen.cs` | In-game pose-picker UI |
| `BUILDING_POSE_WORKLIST.md` | 449-building checklist (sorted to match the tool's list order) for tracking progress |

---

## Current state (works)

- The inspector opens, the search box + prev/next + list build, and clicking a building
  spawns it at a single in-world cell and renders a live preview. The paste line updates as
  you cycle anim/frame.
- **`PlaceAllBuildings` was removed.** It spawned a grid that climbed off the top of the
  asteroid into off-world cells (`Grid.WorldIdx == 255`), causing `GetMyWorld()`-null NPEs
  (RailGun, interasteroid receiver). Those were placement artifacts, not bad buildings — the
  one-at-a-time picker/exporter spawn in-world and don't hit them. Filters now only exclude
  buildings that crash in *any* cell (rockets, launch pads, cluster sensors,
  `RocketUsageRestriction`).
- Preview-panel NPE fixed: the `PPanel` already owns an `Image` for its `BackColor`, so we
  reuse it via `AddOrGet<Image>()` instead of `AddComponent<Image>()` (which returned null).

---

## Planned UI improvements (NOT yet done — this is the plan)

### 1. The building list overflows the whole screen — make it a bounded list / search

**Symptom:** the chooser renders all ~300 renderable rows as one giant scrollable column
that overflows the top and bottom of the screen. Only a few buildings in the middle
(alphabetically ~the "E…L" range) are clickable, and the rest of the dialog UI (preview,
anim/frame controls, paste line) is pushed off-screen and unreachable.

**Root cause:** PLib's `PScrollPane` reports its *content's* full preferred height up to the
`PDialog` body's layout, so the dialog grows to fit all rows instead of giving the scroll
pane a fixed-height viewport. The `LayoutElement` (minHeight/preferredHeight = 150) added in
`BuildChooser`'s `scroll.OnRealize` is being ignored — PLib's custom box layout doesn't honor
`LayoutElement` on the scroll-pane root the way a stock Unity `LayoutGroup` would.

**Fix approaches, in order of preference:**

1. **Search-first, bounded results (recommended).** Don't materialize all ~300 rows. Show
   the search box; render only matching rows, capped to ~10–12, inside a small fixed-height
   scroll. Empty query shows the first N (or a "type to search" hint). prev/next steps
   through the current match set. Keeping the row count tiny means it can't overflow even if
   PLib sizing is imperfect, and it's faster to build. This best matches the request ("a
   small scrollable list or a search").
2. **Force a bounded viewport.** Set the realized scroll-pane `RectTransform` to a fixed
   `sizeDelta`/anchors and stop the body from auto-growing (fixed `PDialog.Size` + ensure the
   body panel doesn't expand to content). May require making the scroll *content* flexible
   while the *viewport* stays fixed.
3. **Cap dialog height, let the scroll flex.** Give the scroll pane `FlexSize = (1,1)` and
   constrain the dialog to a fixed total height so the leftover space bounds the viewport.

**Verification:** this is UI behaviour PLib doesn't make obvious from source — whichever
approach we take must be confirmed visually in-game (open the dialog, confirm the list is
small, all other controls are reachable, and scrolling/searching works).

### 2. Disable the biome tint so renders aren't darkened/colour-shifted — DONE

Implemented in `building/BiomeTintNeutralizer.cs`: a Harmony `Prefix` on
`SubworldZoneRenderData.GenerateTexture` whites out every `zoneColours` entry (via
`Traverse`, so it's robust to field accessibility), making the per-biome tint multiply
the identity. Applied globally (the zone texture is generated once at world init, not per
render, so a render-time toggle wouldn't help) and to ALL zones rather than skipping index
7 — the temp building can spawn in any biome, so whiting everything guarantees no tint
regardless of spawn cell. Side effect: the live biome overlay shows white while the mod is
enabled (acceptable for a dev/export mod). **Still needs an in-game visual confirmation**
that renders brighten as expected.

Original plan, for reference:

Buildings render dark/colour-tinted because ONI applies a per-biome tint (the zone overlay
colours) to world contents, including building kanims. Sgt_Imalas's AnimExportTool neutralises
this for clean icon export by whiting out the zone colours:

- Reference: https://github.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods/blob/master/AnimExportTool/Patches.cs#L332-L349

**Plan:** add a Harmony `Prefix` on `SubworldZoneRenderData.GenerateTexture(__instance)` that
sets every `__instance.zoneColours[i]` to white (255,255,255), skipping index 7 (space biome),
so the tint multiply becomes identity and buildings render in true colour. This benefits both
the inspector preview and the export sweep.

**Notes / caveats to decide when implementing:**
- This also whites out the biome tint in the *live* game overlay while the mod is active.
  For a dev/export mod that's acceptable (Sgt_Imalas applies it globally). If it's disruptive
  we can gate it behind a flag or only apply it around renders.
- Confirm whether index 7 should really be skipped for our case or whether all zones should
  go white.

### 3. "Dark" buildings — anim/frame selection (expected, partly fixed by #2)

Many buildings load in their default/unpowered (dark) pose. Two contributors:
- **Biome tint** — addressed by #2; should brighten a lot of them automatically.
- **Wrong anim/frame** — the auto-anim scorer (`ChooseActiveAnim`) doesn't always pick the
  lit/working anim. This is exactly what the pose picker is for: step anim/frame to the
  active pose and record it in `BuildingPoseOverrides.Overrides`.

**Scorer improvements landed (`BuildingImageSnapshotter.ScoreAnim`):** added `running` and
`deployed` to the top active tier and `powered` to the `on`-tier, and added construction/
placement/teardown markers (`place`, `construct`, `install`, `demo`) to `InactiveMarkers`
so those anims are never chosen. Mid-loop frame was already handled (`PoseFramePercent =
0.5`). These are conservative: new keywords only promote anims that previously scored 0, and
new inactive markers only demote anims that are never a valid icon pose.

**Still to do (needs the game):** open the inspector after these changes, sweep the
worklist, and judge how many buildings still need a hand-picked override. If it's still most
of them, the scorer needs data-driven tuning against the actual anim names — which requires
seeing them in-game. `BUILDING_POSE_WORKLIST.md` remains an inherently manual, visual task.

---

## Workflow reference

1. Open **Inspect Building Poses** from the pause menu.
2. Search/select a building; step anim (◄ Anim / Anim ►) and frame (◄ / slider / ►) until it
   looks right.
3. Copy the generated line into `Overrides` in
   `OniExtract2024/building/BuildingPoseOverrides.cs`, e.g.
   `{ "SteamTurbine2", new BuildingPose("generating_loop", 7) },`.
4. Tick the building in `BUILDING_POSE_WORKLIST.md`.
5. When done (or to spot-check), run **Export Building Images** to regenerate `ui_image/`
   PNGs and the `uiImageRect` values in `building.json`.
