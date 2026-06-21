# OniExtract2024 — Open Items

## 1. Vanilla Game Crash (BlockTileMaterial null) — FIXED 2026-06-20

**Symptom:** Loading a save that contains tile-type buildings (`SnowTile`, `WoodTile`, etc.)
crashed with:
```
ArgumentNullException: Value cannot be null (Parameter: source)
→ UnityEngine.Material..ctor(Material source)
→ BlockTileRenderer+RenderInfo..ctor
→ BuildingUnderConstruction.OnSpawn / BuildingComplete.OnSpawn
```

**Root cause (corrected):** `ExportBuilding.AddNewBuildingDef` was calling
`buildingDef.BlockTileMaterial = null` on every tile building def during game init. That
mutation persisted on the in-memory `BuildingDef` ScriptableObject for the entire session.
When a save was loaded, `BlockTileRenderer` called `new Material(def.BlockTileMaterial)`,
which threw because we had nulled it. Our mod was the cause, not a vanilla bug.

The null was originally needed to prevent the JSON serializer from crashing when it tried to
serialize `List<BuildingDef> buildingDefs` (a field since deleted by Fix 1). After Fix 1,
`BBuildingEntity` is a pure POCO with no `Material` fields, so nulling `BlockTileMaterial`
was no longer needed.

**Fix:** Removed `buildingDef.BlockTileMaterial = null;` from `AddNewBuildingDef`
(see Fix 4 in PLAN.md).

---

## 2. Export Validation — Before Consuming on the Website

The 2024 mod run produced 13 separate JSON files. The 2023 export (last known-good,
used to populate the website) was a **single `database.json`**. Schema alignment needs
to be confirmed before updating the website.

### 2a. Structural Overview: Old vs New

| 2023 `database.json` key | 2024 file | 2024 top-level key | Shape change? |
|---|---|---|---|
| `buildings` | `building.json` | `buildingDefs` | Key renamed; has leading nulls (see §2b) |
| `elements` | `elements.json` | `elementTable` | **List → Dict** keyed by SimHash int (see §2c) |
| `uiSprites` | `uiSpriteInfo.json` | `uiSpriteInfos` | **List → Dict** keyed by item ID (see §2d) |
| `spriteModifiers` | *(unknown)* | *(unknown)* | May be gone or folded elsewhere (see §2e) |
| `buildMenuCategories` | `building.json`? | *(unknown)* | Needs verification |
| `buildMenuItems` | `building.json`? | *(unknown)* | Needs verification |
| *(not in 2023)* | `food.json` | `foodInfoList` | New |
| *(not in 2023)* | `recipe.json` | `recipes`, `preProcessRecipes` | New |
| *(not in 2023)* | `geyser.json` | `geysers` | New |
| *(not in 2023)* | `tags.json` | `SimHashes`, *(more keys)* | New |
| *(not in 2023)* | `po_string.json` | `BUILDING`, *(more keys)* | New |
| *(not in 2023)* | `db.json` | `diseases`, *(more keys)* | New |
| *(not in 2023)* | `entities.json` | `entities` | New |
| *(not in 2023)* | `multiEntities.json` | `multiEntities` | New |
| *(not in 2023)* | `items.json` | `EquipmentDefs`, `eggs`, `seeds` | New; has leading nulls |
| *(not in 2023)* | `attribute.json` | `ExposureType`, *(more)* | New |

### 2b. Null Entries in `building.json` and `items.json`

The `buildingDefs` array in `building.json` and `EquipmentDefs` in `items.json` both
start with a run of `null` entries before the real data. This is suspicious — it likely
means the lists are index-aligned to some internal enum and sparse. The website will
need to filter nulls, or the exporter should be changed to skip them.

**How to check:** Open `building.json`, search for the first non-null entry, and count
how many nulls precede it. Do the same for `items.json`. Compare total non-null building
count to the 2023 `buildings` array length.

### 2c. `elements` Shape Change: List → Dictionary

In 2023, `elements` was a list of element objects. In 2024, `elementTable` is a
dictionary keyed by the element's SimHash (a negative integer, e.g. `"-2123557039"`).
The 2024 POCO (`BElement`) also adds fields not in the 2023 model:
`state`, `materialCategory`, `molarMass`, `specificHeatCapacity`, `thermalConductivity`,
`hardness`, `lowTemp`, `highTemp`, `lowTempTransitionTarget`, `highTempTransitionTarget`,
`sublimateRate`.

Fields present in 2023 that may differ in 2024: `tag` (2023 used `GetHash()` just like
2024, so these should match), `color`/`conduitColor`/`uiColor` (same int packing).

**How to check:** Pick 5–10 known elements (e.g. Water, Oxygen, Iron, Sand, Gold) and
compare all shared fields between the 2023 list entry and the 2024 dict entry.

### 2d. `uiSprites` Shape Change: List → Dictionary

In 2023, `uiSprites` was a flat list of sprite objects. In 2024, `uiSpriteInfos` is a
dict keyed by item ID (e.g. `"FabricatedWood"`). The 2024 entries appear to have a
different field structure (`spriteName`, `textureName`, `color` as an object with r/g/b/a).

**How to check:** Pick a building that existed in 2023 (e.g. a simple one like `Algae
Distillery`) and compare its sprite info between the two formats.

### 2e. `spriteModifiers` — Status Unknown

The 2023 `database.json` had a `spriteModifiers` list. No 2024 file obviously contains
this key. It may have been:
- Folded into `uiSpriteInfo.json` under a different key
- Dropped entirely (if the website no longer uses it)
- Still present but under a different name

**How to check:** Search `uiSpriteInfo.json` and `building.json` for any key that
resembles a sprite modifier structure (translation, scale, rotation, multColour fields).

### 2f. HTML Link Tags in Names

Element names (and likely building/entity names) include Unity rich-text link tags:
`"<link=\"CRUSHEDICE\">Crushed Ice</link>"`. Check whether the 2023 data had the same
format or was already stripped. If stripped in 2023, the website parser may need updating,
or the exporter should strip them. The pattern `<link="...">(text)</link>` can be removed
with a simple regex.

### 2g. Images — Not Regenerated This Run

The 536 PNGs in `export/images/` are from Oct 2025, not this run. They were generated by
a previous OniExtract2020 session. New content added in U59 (new buildings, entities,
geysers) will be missing image files. The 2024 mod does export sprites via `ExportUISprite`
and writes sprite metadata in `uiSpriteInfo.json`, but whether it also saves PNGs depends
on the `ExportUISprite` implementation.

**How to check:** Compare the sprite names referenced in `uiSpriteInfo.json` against the
PNG files present in `export/images/`. Any reference without a matching PNG is missing.

---

## 3. Suggested Validation Workflow

Work through these in order — stop if the format diverges too much from the website's
expectations, and fix the exporter or the website consumer before continuing.

1. **Extract** `export-2023.zip` to a temp folder for side-by-side comparison.
2. **Buildings:** Open `building.json` and the 2023 `buildings` array. Count non-null
   entries, compare field names on a sample building, check `buildMenuCategories` and
   `buildMenuItems` are present somewhere.
3. **Elements:** Pick 5 elements by name in both, compare `color`, `conduitColor`,
   `uiColor`, `tag`, and the new thermal/phase fields.
4. **Sprites:** Compare `uiSprites` (2023 list) vs `uiSpriteInfos` (2024 dict) on a
   sample item. Determine if `spriteModifiers` is still needed.
5. **New files:** Skim `food.json`, `recipe.json`, `geyser.json`, `entities.json` for
   obvious problems (nulls, empty arrays, malformed names).
6. **Images:** Run the missing-PNG check from §2g.
7. **Names:** Search for `<link=` in a few files to confirm the tag format, decide
   whether to strip in the exporter or the website.

---

## 4. Obsolete API Warnings (Low Priority)

Build produces two deprecation warnings — no functional impact, but worth cleaning up:
- `IEquipmentConfig.GetDlcIds()` → use `IHasDlcRestrictions` interface
- `DlcManager.IsDlcListValidForCurrentContent` → use `IsCorrectDlcSubscribed`
