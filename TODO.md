# OniExtract2024 — Open Items

## 1. Website Update — Breaking Changes from 2023 → 2024

The 2023 website consumed a single `database.json`. The 2024 mod produces 13 separate files.
Full schema in [EXPORT_SCHEMA.md](EXPORT_SCHEMA.md). `export-2023/` contains the last known-good
2023 data for reference.

### Field renames the website must handle

| 2023 field | 2024 equivalent | Notes |
|---|---|---|
| `buildings[].name` | `bBuildingDefList[].nameString` | Display name (has link tags) |
| `buildings[].prefabId` | `bBuildingDefList[].name` | Code/prefab ID |
| `buildings[].isTile` | `bBuildingDefList[].isFoundation \|\| isKAnimTile` | Two flags in 2024 |
| `elements` (array) | `elementTable` (dict) | Now keyed by SimHash int |
| `uiSprites` (array) | `uiSpriteInfos` (dict) | Now keyed by prefab tag name |
| `buildMenuItems[].buildingId` | `buildingAndSubcategoryDataPairs[category][].Key` | Grouped by category name |

### Image path change

Old: `export/images/{textureName}.png` (536 files, Oct 2025, OniExtract2020 — **obsolete**)  
New: `export/ui_image/{uiSpriteInfos[prefabTagName].name}.png` (1241 files, freshly exported)  
Also new: `export/ui_image_facade/` — facade/clothing/permit sprites

### `spriteModifiers` removed

The 2023 `spriteModifiers` list (6707 atlas transform entries) has no 2024 equivalent.
Audit website code for any usage of this field.

### `db.json` case-sensitive parser required (action needed)

`db.json` objects contain sibling keys `id` (object) and `Id` (string).
Case-insensitive parsers (PowerShell `ConvertFrom-Json`, some .NET configs) throw.
Verify website uses `JSON.parse` (browser/Node), `jq`, or Newtonsoft.Json defaults.

---

## 2. Connection-state sprites (NEW — separate in-game tool)

The website needs 16 connection-state sprites per connectable building (4-bit bitmask:
`left=1, right=2, up=4, down=8`), output as individual PNGs:

```
export/connection_sprites/{prefabId}/{bitmask}.png   (bitmask 0–15)
```

Implemented as a **separate, in-game tool** (`OniExtract2024/connection/`), independent of
the main-menu JSON export — connectables can only be rendered inside a loaded game/sandbox.

### How to run

1. Build + deploy the mod, launch ONI, **load any colony or sandbox**.
2. Open the pause screen (Esc) → click **"Export Connection Sprites"**.
3. Progress + the output path are logged to `Player.log` (`OniExtract:` lines).

### Two rendering paths (the two building types use different game systems)

| Type | Flag | Mechanism | Code |
|---|---|---|---|
| Utilities (15) | `isUtility` | Place temp instance → `kbac.Play(connectionManager.GetVisualizerString(conn))` → camera-snapshot the kanim batch | `ConnectionSpriteSnapshotter.cs` |
| Tiles (14) | `isKAnimTile` | Crop + alpha-composite matching items from `BuildingDef.BlockTileAtlas` (no camera/placement) | `TileConnectionExtractor.cs` |

`UtilityConnections` (Left=1, Right=2, Up=4, Down=8) maps 1:1 to the website bitmask.
Tiles use the game's 8-direction `Bits` scheme; we set only the 4 orthogonal bits per state.

### Open items for this feature

- [x] **Validate output in-game** — visual QA passed on the collected PNGs (15 utilities +
      15 tiles, 16 each). Utilities have clean alpha/centering; tile bodies are correct per
      bitmask (border on disconnected sides, open on connected). `GasConduit` reads very dark
      but that is faithful to the game's near-black pipe art (the gas-swirl icon and 4-way
      structure are correct). `TravelTube`'s corner-fillet junction matches the game's actual
      tube-junction visual. No blank/wrong states found. Decor-layer output validated live too.
- [x] **Base tile fidelity / size-mismatch skip** — `ComposeState` now selects the *first*
      matching atlas item and renders that single item, mirroring
      `BlockTileRenderer.RenderInfo.Rebuild` (which `break`s on first match — the game never
      overlays items per cell). This removes the old multi-item alpha-composite *and* the
      size-mismatch skip entirely: there is no second item to conflict, so no detail is
      dropped. The skip was a band-aid for overlaying items the game doesn't overlay.
- [x] **Tile decor "tops" layer** — implemented. `TileConnectionExtractor.RasterizeDecor`
      reproduces `BlockTileRenderer.DecorRenderInfo` for a single cell: matches each
      `DecorBlockTileInfo.decor` against the connection bits, picks a variant via the game's
      `PerlinSimplexNoise` gate (faithful — a decor may be absent for a given mask exactly as
      in-game), and rasterises the variant's `atlasItem` triangle mesh (vertices/uvs/indices)
      in world space, mapped onto the base crop via the matched item's own uv→world
      correspondence. Validated live — decor tops render correctly. Design choices that could
      still be revisited: (a) variant noise is sampled at cell (0,0) — deterministic but
      arbitrary; (b) decor that extends above the base item's uvBox rect is clipped to the
      crop (no canvas expansion yet).
- [ ] **`WireRubber`** (new in U59) has no prior export data; confirmed it renders (QA'd the
      15-state cross; looks correct).
- [ ] **In-game results/progress panel** — grow the pause-screen button into a small
      debugging UI (status, per-building results, re-run) so other devs can reason about it.
- [ ] **`tileableLeftRight`/`tileableTopBottom`** — `building.json` still omits these. The
      presence of a `connection_sprites/{prefabId}/` dir can serve as the connectable signal,
      or add the fields to `BBuildingEntity` if needed.

