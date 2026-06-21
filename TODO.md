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

