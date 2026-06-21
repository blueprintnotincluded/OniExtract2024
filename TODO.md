# OniExtract2024 — Open Items

## 1. Export Validation — Before Updating the Website

The 2024 mod produces 13 separate JSON files. The 2023 export (last known-good,
used to populate the website) was a **single `database.json`**. Full 2024 schema is in
[EXPORT_SCHEMA.md](EXPORT_SCHEMA.md).

### 1a. Structural Overview: 2023 → 2024

| 2023 `database.json` key | 2024 file | 2024 top-level key | Notes |
|---|---|---|---|
| `buildings` | `building.json` | `bBuildingDefList` | Key renamed |
| `elements` | `elements.json` | `elementTable` | List → Dict keyed by SimHash int |
| `uiSprites` | `uiSpriteInfo.json` | `uiSpriteInfos` | List → Dict keyed by prefab ID |
| `spriteModifiers` | — | — | Not present in 2024; audit website usage |
| `buildMenuCategories` | `building.json` | `buildMenuCategories` | Same shape |
| `buildMenuItems` | `building.json` | `buildingAndSubcategoryDataPairs` | Now `{Key, Value}[]` per category |
| *(new)* | `food.json` | `foodInfoList` | |
| *(new)* | `recipe.json` | `recipes` | |
| *(new)* | `geyser.json` | `geysers` | |
| *(new)* | `tags.json` | `SimHashes`, `RoomConstraintTags`, `mGameTags`, `prefabIDs` | |
| *(new)* | `po_string.json` | 27 namespaces (flat key→string dicts) | |
| *(new)* | `db.json` | 40+ arrays | Duplicate-key bug — see §1d |
| *(new)* | `entities.json` | `entities` | |
| *(new)* | `multiEntities.json` | `multiEntities` | |
| *(new)* | `items.json` | `eggs`, `seeds`, `equipments` | |
| *(new)* | `attribute.json` | enums + sickness defs | |

### 1b. Field-level Comparison Against 2023 Data

Need the 2023 `database.json` for these checks — extract `export-2023.zip` first.

- **Buildings:** Compare field names on a sample building. The 2024 shape has fewer
  top-level fields (no `BuildingComplete`/`UnderConstruction` component blobs that existed
  in older exports). Confirm the website only uses fields that are still present.
- **Elements:** Compare `color`, `conduitColor`, `uiColor`, `tag` values for 5 known
  elements (Water, Oxygen, Iron, Sand, Gold). New fields (`molarMass`, thermal props) are
  additive. Dict-vs-list is the main structural break.
- **Sprites:** Compare a sprite entry from the 2023 list against the 2024 dict on the same
  item. The 2024 `color` is `{r,g,b,a}` floats; the 2023 format may differ.

### 1c. Rich-text Link Tags in Names

All `nameString` and description fields include Unity tags:
`<link="CRUSHEDICE">Crushed Ice</link>`

Needs a decision: strip in the exporter, or strip in the website consumer?  
Strip regex: `/<link="[^"]*">([^<]*)<\/link>/g` → capture group 1.

Check whether the 2023 data already had these stripped — if it did, the website parser
won't handle them and must be updated before consuming 2024 data.

### 1d. `db.json` Duplicate-Key Issue

`db.json` objects contain both `id` (object) and `Id` (string) as sibling keys.
Case-insensitive parsers (PowerShell, some .NET configs) throw on this.
Use a case-sensitive parser: `JSON.parse` (browser/Node), `jq`, or Newtonsoft.Json defaults.
The website consumer needs to be verified against this file.

### 1e. Images — Missing PNGs for New Content

`uiSpriteInfo.json` references 1241 sprite textures, but `export/images/` was last
generated in Oct 2025 (OniExtract2020). New U59 content will have metadata but no PNG.

**To check:** diff `textureName` values in `uiSpriteInfo.json` against filenames in
`export/images/`. Any missing PNG = gap.

Whether the 2024 mod can regenerate PNGs depends on the `ExportUISprite` implementation —
check if it writes files to disk or only outputs metadata.

---

## 2. Obsolete API Warnings (Low Priority)

Build produces two deprecation warnings — no functional impact, but worth cleaning up:
- `IEquipmentConfig.GetDlcIds()` → use `IHasDlcRestrictions` interface
- `DlcManager.IsDlcListValidForCurrentContent` → use `IsCorrectDlcSubscribed`
