# Website Migration: OniExtract2020 → OniExtract2024

Game version: **U59-737790-SCA** (Spaced Out DLC active)

The 2023 website read a single `database.json`. The 2024 mod exports **13 separate JSON files**
plus a new image folder. This document covers every breaking change.

---

## 1. New file layout

| Old (2023) | New (2024) | Notes |
|---|---|---|
| `database.json` | *(gone)* | Replaced by the files below |
| — | `building.json` | Buildings, build menu, room constraints |
| — | `elements.json` | Elements (was `elements` array in database.json) |
| — | `entities.json` | Critters, plants |
| — | `multiEntities.json` | Space POIs, meteor showers, comets |
| — | `items.json` | Eggs, seeds, equipment |
| — | `food.json` | Food items |
| — | `geyser.json` | Geyser types |
| — | `recipe.json` | Fabricator recipes |
| — | `uiSpriteInfo.json` | UI sprite metadata |
| — | `tags.json` | Tag and hash lookups |
| — | `attribute.json` | Enums, sickness definitions |
| — | `po_string.json` | Localizable strings |
| — | `db.json` | Duplicant database (see warning below) |

All files share root-level metadata: `buildVersion`, `dlcs`, `ExportFileName`, `DatabaseDirName`.

Full schema for every file: see `EXPORT_SCHEMA.md`.

---

## 2. Field renames you must update

### Buildings (`building.json` → `bBuildingDefList`)

| 2023 field | 2024 field | Notes |
|---|---|---|
| `buildings[].name` | `bBuildingDefList[].nameString` | Display name — contains rich-text tags (see §5) |
| `buildings[].prefabId` | `bBuildingDefList[].name` | Code/prefab ID — plain string, safe for lookup keys |
| `buildings[].isTile` | `bBuildingDefList[].isFoundation \|\| isKAnimTile` | Split into two booleans in 2024 |

New fields added in 2024 that weren't in 2023:
`widthInCells`, `heightInCells`, `materialCategory` (string[]), `materialMass` (float[]),
`isUtility`, `dragBuild`, `buildLocationRule`, `permittedRotations`, `sceneLayer`,
`objectLayer`, `viewMode`, `defaultAnimState`, `uiSpriteName`

### Elements — array → dict

2023: `database.json` → `elements` (array)  
2024: `elements.json` → `elementTable` (dict keyed by SimHash **as a decimal integer string**)

Example key: `"-2123557039"` (for Crushed Ice). To look up by name, iterate entries and match `entry.id`.

### UI sprites — array → dict

2023: `database.json` → `uiSprites` (array)  
2024: `uiSpriteInfo.json` → `uiSpriteInfos` (dict keyed by **prefab tag name**)

Example key: `"FabricatedWood"`. Each entry has `id` (rich-text), `name` (plain), `spriteName`,
`textureName`, `color`.

### Build menu — flat array → dict of arrays

2023: `database.json` → `buildMenuItems[].buildingId`  
2024: `building.json` → `buildingAndSubcategoryDataPairs` (dict keyed by category name)

Each category value is an array of `{ "Key": "<prefabId>", "Value": "<subcategory>" }` pairs.

---

## 3. Image path change

Old path (obsolete, 536 files from OniExtract2020):
```
export/images/{textureName}.png
```

New path (1241 files, freshly exported):
```
export/ui_image/{uiSpriteInfos[prefabTagName].name}.png
```

The filename is the **plain display name** from the sprite entry (e.g. `"Plywood.png"`),
not the texture name or prefab ID. Facade/permit sprites are under `export/ui_image_facade/`.

---

## 4. `spriteModifiers` removed

The 2023 `database.json` had a `spriteModifiers` list (6,707 atlas transform entries).
This field does not exist in any 2024 export file. Audit the website for any usage and remove it.

---

## 5. `db.json` — use a case-sensitive parser

`db.json` (~20 MB) contains objects with sibling keys `id` (object) and `Id` (string).
Case-insensitive parsers (PowerShell `ConvertFrom-Json`, some .NET configs) will **throw**.

Safe parsers: browser `JSON.parse`, Node `JSON.parse`, `jq`, Newtonsoft.Json with default settings.

---

## 6. Rich-text tags in name strings

All `nameString` fields contain Unity rich-text link tags:

```
<link="MANUALGENERATOR">Manual Generator</link>
```

Strip them for display or search with:
```js
str.replace(/<link="[^"]*">([^<]*)<\/link>/g, '$1')
```

This was also true in 2023 — not a new issue, but worth noting if the website doesn't already handle it.

---

## Files to include in the handoff

Copy these from `%USERPROFILE%\Documents\Klei\OxygenNotIncluded\export\`:

- `database/` — all 13 JSON files (exported 2026-06-20, game version U59-737790-SCA)
- `ui_image/` — 1,241 PNG sprites
- `ui_image_facade/` — facade/clothing/permit sprites (if the website uses them)
