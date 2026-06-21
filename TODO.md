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
- [x] **Seamless framing (utilities + tiles)** — restored edge-to-edge sprites that tile like
      the pre-2024 site. *Utilities:* the snapshot framed a 1×1 building in a 300px canvas with a
      full extra cell of padding (~2–12% fill); now `ConnectionSpriteSnapshotter.CropAndWrite`
      crops all 16 states to one cell-centred square sized to the largest state (one shared
      window, so the cell anchor and inter-state alignment are preserved). *Tiles:* stopped
      cropping the raw `uvBox` (which placed the cell at a different spot/scale per state and
      left seams); `ComposeState` now resamples the matched item into a cell-anchored 1.5-cell
      frame applying the game's `AddVertexInfo` geometry — connected edges trimmed flush to the
      cell boundary, disconnected edges overhanging 0.25 cell — so adjacent tiles join exactly
      as in game.
- [~] **Tile decor "tops"/corner layer** — **dropped for now** (future enhancement). A faithful
      rasteriser exists in git history (the decor-rasteriser commit) and is documented in
      `TileConnectionExtractor`'s header. It's left off because decor placement depends on the
      full 8-neighbour (incl. diagonal) state, which the website's 4-bit/16-state model can't
      encode — so top-surface highlights and corner embellishments would mismatch at junctions.
      Reinstating it would need either an 8-bit/256-state export or website-side decor logic.
- [x] **Edge-to-edge icons (remove transparent margin)** — both intentional margins dropped so
      sprites butt up flush on the website. *Utilities:* `ConnectionSpriteSnapshotter.CropMargin`
      set 6 → 0, so the shared cell-centred crop is tight to content (1px AA fringe acceptable;
      the single-shared-window logic is unchanged, so states stay aligned). *Tiles:*
      `TileConnectionExtractor` now renders a 1.0-cell frame (`FrameCells = 1`) — the matched
      item's full `uvBox` maps to world [0,1]² (overhang extension dropped), `cellPx`/`frame`
      derive from the full uvBox pixel width, and the resample origin is world 0. The
      connected-edge 1/32 UV trim is kept (adjacent connected tiles stay flush); a disconnected
      edge uses the full uvBox edge, so its rounded border renders inside the cell instead of
      overhanging. Introduces an imperceptible ~3% scale delta between fully-connected and
      fully-disconnected states (matches the old tileset convention). **Pending:** re-run the
      in-game "Export Connection Sprites" and confirm via alpha-bbox scan that content reaches the
      frame edges (border 0–1px) on e.g. MetalTile/15, MeshTile/15, Wire/15.
- [ ] **`WireRubber`** (new in U59) has no prior export data; confirmed it renders (QA'd the
      15-state cross; looks correct).
- [ ] **In-game results/progress panel** — grow the pause-screen button into a small
      debugging UI (status, per-building results, re-run) so other devs can reason about it.
- [ ] **`tileableLeftRight`/`tileableTopBottom`** — `building.json` still omits these. The
      presence of a `connection_sprites/{prefabId}/` dir can serve as the connectable signal,
      or add the fields to `BBuildingEntity` if needed.

