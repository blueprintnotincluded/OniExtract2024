# Handoff: uiImageRect for terrain features

Direction: export → website.

Terrain features — geysers, vents, volcanoes, the oil reservoir — have always exported
their data (`geyser.json`, `entities.json`) and an icon (`ui_image/<prefabId>.png`). What
they never had was a `uiImageRect`, because the in-game building-image sweep iterates
`Assets.BuildingDefs` and terrain features are not `BuildingDef`s, so the sweep never
reached them. Their icons came from the low-res main-menu atlas pass instead.

They now go through the same ~200 px/cell kanim render the buildings use, and get a
measured rect alongside. `export/ui_image_rects.json` goes from 342 to **373** entries:

- 27 × `GeyserGeneric_*`
- `NiobiumGeyser`, `OilWell`, `SmallReefGeyser`, `UnderwaterVent`

Same coordinate space and semantics as buildings — origin at the footprint's bottom-left,
+y up, units = cells, real numbers. Example:

```json
"GeyserGeneric_big_volcano": { "x": -0.14, "y": -0.58, "w": 3.47, "h": 3.63 }
```

(3×3 footprint; art overhangs slightly on all four sides.)

> Separately, a fix landed for `uiImageRect`s that disagreed with their PNG on 302 of 342
> buildings. That is a data fix needing no website change — see `UIIMAGERECT_DURABILITY.md`,
> Part 2.

---

## ⚠️ The blocker: these rects currently cannot reach you

Terrain features have no entry in `building.json`'s `bBuildingDefList[]` — which is the
only place the importer reads `uiImageRect` from. They appear in `geyser.json` and
`entities.json`, neither of which the importer reads (it reads `building.json`,
`elements.json`, `uiSpriteInfo.json`).

So the rects exist, are correct, and ship inside the folder you already receive — but
nothing consumes them.

## ⚠️ And their icons changed, so doing nothing is a regression

The 31 terrain `ui_image/*.png` files are now tight-cropped renders instead of atlas
icons — e.g. `GeyserGeneric_big_volcano.png` went from **147×96 to 693×725**.

This is the exact framing change `WEBSITE_POSTPROCESSING.md` warns about: stretch-to-
footprint on a tight-cropped render gives squished aspect and lost overhang. The new
renders happen to be closer to footprint aspect than the old icons were, so it will not
look catastrophic — but the art will sit ~0.5 cells too high and ~15% too small until a
rect is applied.

## Pick one

**Option A — read `ui_image_rects.json` (recommended).**
The file already ships at the root of the folder you receive (36 KB, `prefabId → {x,y,w,h}`).
Have `import:2024` read it and apply rects to terrain entities by prefab id, feeding the
same rect-aware renderer buildings already use. No export change needed; the file is the
canonical source and covers buildings redundantly, so you could migrate buildings onto it
later too.

**Option B — we merge the rects into `uiSpriteInfo.json`.**
All 31 terrain features already have entries there (`id`, `name`, `spriteName`,
`textureName`, `color`), and you already read that file. We'd add a `uiImageRect` field to
those entries, and you'd read it off the entry exactly as you do for buildings. Say the
word and we'll ship it in the next export.

Either way the renderer work is already done on your side — this is plumbing to get the
rect to it, not new drawing logic.

## Minor caveat

`uiSpriteInfo.json` still reports `spriteName` / `textureName` from the atlas `ui` symbol
(e.g. `"geyser_gas_steam_0:ui:False"`) even for prefabs whose `ui_image/` PNG is now a
render. Harmless if you key images by `ui_image/<prefabId>.png` (which is what the pipeline
documents), but flagging it in case anything resolves images via `spriteName`.
