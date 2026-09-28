# Modded Buildings in the Export — Import & UI Guide

**Take-to-website handoff, written 2026-07-19.** The export now contains buildings from
Steam Workshop mods, with per-building source-mod attribution. This documents everything
the `npm run import:2024` pipeline and the UI need to ingest it: new fields, relaxed
assumptions, and the features the data enables (supported-mods list, per-building mod
badges, "this blueprint uses modded content").

Direction: export → website. Companion to WEBSITE_POSTPROCESSING.md (that contract is
unchanged — icon naming, connection-sprite framing, uiImageRect all still hold).

---

## 0. TL;DR for the importer

1. `bBuildingDefList` is no longer 449 vanilla-only entries — it now also contains modded
   buildings (474 total in the current export: 449 vanilla + 25 modded from 7 mods).
2. Modded entries carry two new optional string fields: **`mod`** (Steam workshop id) and
   **`modTitle`**. **Absence of `mod` = vanilla.** Pass both through to the site DB.
3. building.json gains a root **`mods`** roster (and sometimes **`modMergeInfo`**) —
   metadata for the mods list UI.
4. One class of modded entry (marked **`offlineMerged: true`**) has a *reduced* schema —
   notably **no `kPrefabID`** — so don't hard-require vanilla-only fields (§3).
5. Icons and connection sprites follow the existing contracts; nothing image-side changes
   in the import script.

---

## 1. Per-building attribution: `mod` / `modTitle`

Every building that comes from a mod has, on its `bBuildingDefList` entry:

```jsonc
{
  "name": "PAirlockDoor",
  "mod": "2094698134",          // Steam workshop id (string). Local dev mods: folder name.
  "modTitle": "Airlock Door",   // display title
  ...
}
```

- **Field present ⇒ modded. Field absent ⇒ base game.** (Never emitted as `null`.)
- `mod` is the stable key — use it for filtering, grouping, and blueprint tagging.
  Workshop URL: `https://steamcommunity.com/sharedfiles/filedetails/?id=<mod>`.
- `modTitle` is display-only (comes from the mod's own metadata; may change across mod
  updates while `mod` stays fixed).

How it's produced (context, not contract): the exporter patches the game's
`BuildingConfigManager.RegisterBuilding` and maps each building to the assembly that
registered it, then to the owning mod. It is automatic for **any** enabled mod — new mods
show up in the export with attribution and zero exporter changes.

## 2. Root rosters: `mods` and `modMergeInfo`

```jsonc
// building.json root — mods whose buildings the GAME exported natively this run
"mods": [
  { "id": "2094698134", "title": "Airlock Door", "buildings": ["PAirlockDoor", "PAirlockDoorInsulated"] },
  ...
],

// building.json root — present only when the offline fallback merge ran (see §3)
"modMergeInfo": {
  "mergedAt": "2026-07-19 ...",
  "appended": ["NaturalTile"],            // entries added by the offline merge
  "nativelyExported": ["PAirlockDoor", ...], // offline data skipped for these (game won)
  "mods": [ { "workshopId": "1840755803", "title": "Buildable Natural Tile",
              "dllSha256": "...", "buildingCount": 1, "buildings": ["NaturalTile"] }, ... ]
}
```

⚠️ **Neither roster alone is the complete mod list.** `mods` covers natively exported
mods; `modMergeInfo.mods` covers the offline-pipeline mods (they overlap). The
authoritative "supported mods" set is **the distinct `mod` values across
`bBuildingDefList`**, with titles taken from the entries' `modTitle` (uniform on every
modded entry). Use the rosters for extra metadata (building lists, offline DLL hashes),
not as the source of truth.

## 3. Two provenance classes of modded entries

### Native (in-game export) — the normal case

The game exported these like any vanilla building: **full vanilla schema** (`kPrefabID`,
component objects, `utilities[]`, usually `uiImageRect`), plus `mod`/`modTitle`. Treat
them exactly like vanilla entries. 24 of the 25 modded buildings today are native.

### Offline-merged (fallback) — marked `offlineMerged: true`

For mods that cannot load in-game (incompatible with the Extract mod, or broken on the
current game version), an offline pipeline decompiles the mod DLL and appends
hand-verified entries. These have a **reduced, extended schema**:

| Difference vs vanilla entries | Detail |
|---|---|
| `offlineMerged: true` | The marker — test this, not field heuristics |
| **No `kPrefabID`**, no `tags` | Don't NPE; fall back to `name` |
| `nameString` is **plain text** | No `<link=...>` markup to strip |
| `storage` simplified | Just `{ "capacityKg": n }`, not the full OutStorage shape |
| No `conduitConsumer`/`conduitDispenser` objects | `utilities[]` still lists all ports and is complete |
| Extra fields | `description`, `effect`, `hitpoints`, `constructionTime`, `meltingPoint`, `thermalConductivity`, `decor`/`noise` (`{amount, radius}`), `kanim`, `initialAnim`, `planCategory`, `planSubCategory`, `addAfter`, `tech`, `configClass`, `notes` — ignore what you don't want, they're additive |
| Usually no `uiImageRect` | Legacy stretch-to-footprint applies (already shipped website-side) |

The shared, always-reliable core on **both** classes: `name`, `nameString`,
`widthInCells`, `heightInCells`, `materialCategory`, `materialMass`, `isFoundation`,
`isKAnimTile`, `isUtility`, `dragBuild`, `buildLocationRule`, `permittedRotations`,
`sceneLayer`, `objectLayer`, `viewMode`, `utilities[]`, `energyConsumer`, `mod`,
`modTitle`.

Today exactly **one** building is offline-merged: `NaturalTile` (Buildable Natural Tile
fails to register on current game builds, so the game can never export it).

## 4. Current export contents (2026-07-19)

| Mod (workshop id) | Buildings | Source |
|---|---|---|
| Airlock Door (2094698134) | PAirlockDoor, PAirlockDoorInsulated | native |
| Drains (1866754178) | Drain | native |
| High Pressure Applications (2806200110) | PressureGasPump, PressureLiquidPump, HighPressureGasConduit, HighPressureLiquidConduit, HighPressureGasConduitBridge, HighPressureLiquidConduitBridge, DecompressionGasValve, DecompressionLiquidValve | native |
| Splitters MK II (3544371748) | MJSplitterMKIIGas, MJSplitterMKIILiquid, MJSplitterMKIISolid | native |
| Smart Pumps (1887986467) | PeterHan_FilteredGasPump, PeterHan_FilteredLiquidPump, FilteredGasPump, FilteredLiquidPump, VacuumPump | native |
| Wall Pumps (3113986230) | FairGasWallPump, FairLiquidWallPump, FairGasWallVent, FairLiquidWallVent, VentHighPressure | native |
| Buildable Natural Tile (1840755803) | NaturalTile | **offlineMerged** |

**474 total** (449 vanilla + 25 modded). The mod set will vary run to run with whatever
was enabled at export time — import defensively, don't pin counts.

## 5. Images & sprites — existing contracts hold

- **`ui_image/<prefabId>.png` exists for every modded building** (import validation will
  pass). Native mod icons are the same renders/sprites as vanilla; offline-merged
  buildings ship a lower-res footprint-style icon extracted from the mod's kanim (fine
  for the legacy stretch path).
- **`uiImageRect`**: present on modded buildings that went through the in-game image
  sweep; absent on ones that didn't yet (currently the 10 Smart/Wall Pumps buildings and
  NaturalTile). The already-shipped fallback (stretch-to-footprint when absent) covers
  them — no work needed.
- **`connection_sprites/`**: `HighPressureGasConduit` and `HighPressureLiquidConduit`
  have full 0–15 sets. The existing dir-existence detection picks them up unchanged.
- Build menu: modded buildings appear in `buildingAndSubcategoryDataPairs` under real
  categories (e.g. the airlock doors in `base`/`doors`, ordered right after their vanilla
  counterparts). Offline-merged ones use subcategory `"uncategorized"`.

## 6. Import checklist

1. Pass `mod` + `modTitle` through to the site's building records (optional strings).
2. Build a mods index at import time: distinct `mod` across entries → `{ id, title,
   buildings[], offline: any(offlineMerged) }`; enrich from root `mods` /
   `modMergeInfo.mods` if wanted. Log something like `modded buildings: 25 from 7 mods`.
3. Relax vanilla-only assumptions: don't require `kPrefabID` (or any §3-missing field)
   when `offlineMerged` is true; tolerate unknown extra fields on any entry.
4. Don't pin building counts or name lists to the vanilla 449.
5. Everything image-side is unchanged.
6. (If the importer diffs against previous DBs:) key modded buildings by `name` exactly
   like vanilla ones — prefab ids are globally unique, mods included.

## 7. UI features this enables

- **Supported-mods page / legend** — from the mods index: title, workshop link, building
  count, per-mod building list.
- **Per-building badge** — any building with `mod` gets a "from <modTitle>" chip in the
  build menu / info panel, linkable to the workshop page.
- **Blueprint mod detection** — a blueprint's placed building ids → look up each
  building's `mod` → distinct set = "This blueprint uses: Airlock Door, High Pressure
  Applications". Use it to (a) tag/filter blueprints by required mods, (b) warn on
  open/paste when the viewer hasn't opted into those mods, (c) let users toggle mod sets
  on/off in the palette (vanilla = entries with no `mod`).
- **Graceful degradation** — offline-merged buildings render with the legacy icon path;
  everything placement-critical (`utilities[]`, footprint, rotations) is present on both
  classes.

## 8. Unrelated concurrent change (heads-up)

`bBuildingDefList` entries may also carry an `areasOfEffect` array (light/intake/operating
ranges — a separate feature, documented separately). Ignore it for this import if the
rendering work hasn't landed; it's additive and optional like the mod fields.
