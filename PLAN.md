# OniExtract2024 — Export Fix Plan

Generated from validation session 2026-06-20. Do not execute fixes together; address one at a time and validate before moving on.

---

## Pre-flight: delete the stale artifact

`C:\Users\sinep\Documents\Klei\OxygenNotIncluded\export\database\database.json` was written **2025-10-11** by the old local `OniExtract` mod, not by OniExtract2024. It has the old 2023 schema (`buildings`, `uiSprites`, `spriteModifiers`, …). Delete it before the first re-export so it cannot be confused with current output.

The old mod lives at `…\mods\local\OniExtract\` and also uses `OniExtract2024.dll`, so it may regenerate this file. If it does, disable or remove the local mod installation after confirming the dev mod produces all the correct files.

---

## Fix 1 — Remove null Unity objects from `building.json` and `items.json`

**Status:** Done — validated 2026-06-20

### Root cause

`ExportBuilding` declares `List<BuildingDef> buildingDefs` and `ExportItem` declares `List<EquipmentDef> EquipmentDefs`. Both `BuildingDef` (a `ScriptableObject`) and `EquipmentDef` extend `UnityEngine.Object`. The `SkipUnityObjectConverter` in `BaseExport.cs` intercepts them at serialization time and writes `null` for every entry — confirmed: building.json has 449 nulls, items.json EquipmentDefs has 11 nulls.

The correct POCO data is already being captured in parallel fields:
- `bBuildingDefList` (`List<BBuildingEntity>`) — 449 non-null entries ✓
- `equipments` (`List<BEquipment>`) — 11 non-null entries ✓

### File: `OniExtract2024/ExportBuilding.cs`

**Remove** the field on line 9:
```csharp
// DELETE:
public List<BuildingDef> buildingDefs = new List<BuildingDef>();
```

**Simplify** `AddNewBuildingDef` — keep the `BlockTileMaterial` null-out (prevents the U59 vanilla crash from TODO §1) and the `roomConstraintTags` update; remove the now-dead `Add` call:
```csharp
public void AddNewBuildingDef(BuildingDef buildingDef)
{
    buildingDef.BlockTileMaterial = null;
    this.roomConstraintTags = RoomConstraints.ConstraintTags.AllTags;
}
```

### File: `OniExtract2024/ExportItem.cs`

**Remove** the field on line 8:
```csharp
// DELETE:
public List<EquipmentDef> EquipmentDefs = new List<EquipmentDef>();
```

**Remove** the entire `AddEquipmentDef` method (lines 17–24). It only populated the deleted field and uses the deprecated `GetDlcIds()` API. The useful BEquipment POCO data is captured by `OniExtract_Game_Equipment_Entity` in Patches.cs via `AddEquipment`, which is unaffected.

### File: `OniExtract2024/Patches.cs`

**Remove** the `OniExtract_Game_Equipment` patch class (lines 304–312). It was the only caller of `AddEquipmentDef`:
```csharp
// DELETE this entire class:
[HarmonyPatch(typeof(EquipmentConfigManager), "RegisterEquipment")]
internal class OniExtract_Game_Equipment
{
    private static void Postfix(IEquipmentConfig config)
    {
        if (!SingletonOptions<ModOptions>.Instance.Item) return;
        exportItem.AddEquipmentDef(config);
    }
}
```

`EquipmentConfigManager.RegisterEquipment` still has the `OniExtract_Game_Equipment_Entity` patch, which captures the correct data and must be kept.

### Validation

After build + re-export:

1. Open `building.json`. Confirm these top-level keys are present: `bBuildingDefList`, `buildMenuCategories`, `buildingAndSubcategoryDataPairs`, `roomConstraintTags`, `requiredSkillPerkMap`, `buildVersion`, `dlcs`.
2. Confirm `buildingDefs` key is **absent**.
3. `bBuildingDefList[0]` must be a non-null object with a `name` field (e.g. `"AdvancedApothecary"`).
4. Open `items.json`. Confirm `EquipmentDefs` key is **absent**.
5. `equipments` must have 11 non-null entries; `seeds` must have 40 non-null entries.

---

## Fix 2 — Restore egg export

**Status:** Done — validated 2026-06-20 (47 eggs, slightly above 30–40 estimate; DLC eggs account for the difference)

### Root cause

`OniExtract_Game_Egg` patches `EggConfig.CreateEgg` with an 11-parameter Harmony type array:
```csharp
[HarmonyPatch(new Type[] { typeof(string), typeof(string), typeof(string), typeof(Tag),
    typeof(string), typeof(float), typeof(int), typeof(float),
    typeof(string[]), typeof(string[]), typeof(bool) })]
```
In U59 (Unity 6) this method's signature changed. Harmony silently fails to match it at startup, so `AddEgg` is never called. Result: `items.json` has `"eggs": []`.

Eggs do not appear in `entities.json` either (only food-use items like `RawEgg` and `CookedEgg` show up there). The critter egg prefabs with incubation data are entirely missing from the export.

### Strategy

Remove the fragile patch and replace it with a post-load prefab scan in the existing `LegacyModMain.Load` postfix. At that point `Assets.Prefabs` contains every registered prefab. Eggs are identified by the presence of `IncubationMonitor.Def` on their state machine.

### File: `OniExtract2024/Patches.cs`

**Remove** the `OniExtract_Game_Egg` patch class entirely (lines 243–254).

**In** `OniExtract_Game_LegacyModMain.Postfix`, replace the existing Item export block (currently just `exportItem.ExportJsonFile()`) with:
```csharp
Debug.Log("OniExtract: " + "Export Items");
if (SingletonOptions<ModOptions>.Instance.Item)
{
    foreach (GameObject prefab in Assets.Prefabs)
    {
        if (prefab == null) continue;
        IncubationMonitor.Def incDef = prefab.GetDef<IncubationMonitor.Def>();
        if (incDef == null) continue;
        KPrefabID prefabID = prefab.GetComponent<KPrefabID>();
        if (prefabID == null) continue;
        BEgg bEgg = new BEgg(prefabID.PrefabID().Name, prefabID);
        exportItem.AddEgg(prefab, bEgg);
    }
    exportItem.ExportJsonFile();
}
```

`exportItem` is already a static field on the `Patches` class. `AddEgg` in `ExportItem.cs` is unchanged.

### Validation

After build + re-export:

1. Open `items.json`. `eggs` must have **> 0** entries (expect 30–40 across all DLCs).
2. Find `"HatchEgg"` (or any critter egg). Confirm:
   - `incubatorMonitorDef.spawnedCreature` is non-empty (e.g. `"Hatch"`)
   - `incubatorMonitorDef.baseIncubationRate` is a positive float
   - `primaryElement` is populated
3. **If `eggs` is still empty:** check `Player.log` for Harmony errors near IncubationMonitor. As a fallback, replace the `GetDef<IncubationMonitor.Def>()` check with a tag check:
   ```csharp
   if (!prefabID.HasTag(GameTags.Egg)) continue;
   ```
   then add the `BEgg` and call `AddEgg`.

---

## Fix 3 — Populate `recipes` from the correct source

**Status:** Done — validated 2026-06-20 (173 recipes, correct ingredient/result data)

### Root cause

`ExportRecipe.ExportComplexRecipes()` does:
```csharp
this.recipes = manager.recipes;
this.preProcessRecipes = manager.preProcessRecipes;
```

At `LegacyModMain.Load` time, `ComplexRecipeManager.Get().recipes` is **null** — the game populates that list in a later init phase that hasn't run yet. `manager.preProcessRecipes` is a `HashSet<ComplexRecipe>` populated during entity registration (which runs before `LegacyModMain.Load`) and contains all 173 recipes.

Result: `recipe.json` has `"recipes": null` and `"preProcessRecipes": [173 entries]`. The website would need to read the wrong field name.

`ComplexRecipe` is not a `UnityEngine.Object`, so the serialization of the 173 entries in `preProcessRecipes` is correct — confirmed by manual inspection of ingredient/result data.

### File: `OniExtract2024/ExportRecipe.cs`

Replace the entire class with:
```csharp
using OniExtract2024;
using System.Collections.Generic;
using System.Linq;

public class ExportRecipe : BaseExport
{
    public override string ExportFileName { get; set; } = "recipe";
    public List<ComplexRecipe> recipes;

    public ExportRecipe() { }

    public void ExportComplexRecipes()
    {
        this.recipes = ComplexRecipeManager.Get().preProcessRecipes.ToList();
    }
}
```

Three things change from the current code:
1. `recipes` is populated from `preProcessRecipes` (the live data) instead of `manager.recipes` (null at export time).
2. `preProcessRecipes` field is removed from the class — the website sees only `recipes`.
3. `obsoleteIDMapping` is removed — confirmed always empty in the current export.

### Fabricators note

During validation, `ComplexRecipe.fabricators` showed as empty/null for all recipes. The field records which machine produces the recipe (e.g. `ChemicalRefinery`). This is **not needed by the website** — the website cares about what materials an item is made *from* (ingredients), not which machine processes them. Ingredient and result data (`ingredients[].material`, `results[].material`, `results[].amount`) was confirmed correct in the current export.

If `fabricators` is still null after Fix 3, defer — it is not a blocker for the initial website update.

### Validation

After build + re-export:

1. Open `recipe.json`. `recipes` must be a non-empty array (expect ~173 entries).
2. Confirm `preProcessRecipes` key is **absent**.
3. Confirm `obsoleteIDMapping` key is **absent**.
4. Find `"ChemicalRefinery_I_Water_Salt_O_SaltWater"`:
   - `ingredients` must have Water (93 kg) and Salt (7 kg)
   - `results` must have SaltWater (100 kg)
5. Note whether `fabricators` is populated or null — record but do not block on it.

---

## Fix 4 — Stop nulling `BlockTileMaterial` on live BuildingDef objects

**Status:** Done — validated 2026-06-20

### Root cause

`ExportBuilding.AddNewBuildingDef` (originally also in `OniExtract_Game_Building_RegisterBuilding` patch) called `buildingDef.BlockTileMaterial = null` on every tile building def during game init. That write mutated the live `BuildingDef` ScriptableObject in memory and persisted for the entire session. When the player loaded a save, `BuildingComplete.OnSpawn` and `BuildingUnderConstruction.OnSpawn` called `BlockTileRenderer.AddBlock` → `new Material(def.BlockTileMaterial)` → `ArgumentNullException`.

The null-out existed to prevent the JSON serializer from crashing when serializing the old `List<BuildingDef> buildingDefs` field (removed by Fix 1). Since Fix 1 eliminated that field, `BBuildingEntity` is now a pure POCO — no `Material` field, no Unity object to serialize. The null-out became unnecessary and harmful.

### File: `OniExtract2024/ExportBuilding.cs`

**Remove** `buildingDef.BlockTileMaterial = null;` from `AddNewBuildingDef`:

```csharp
public void AddNewBuildingDef(BuildingDef buildingDef)
{
    this.roomConstraintTags = RoomConstraints.ConstraintTags.AllTags;
}
```

### Validation

1. Build and deploy the DLL.
2. Launch ONI and reach the main menu (export runs here).
3. Load an existing save that contains tile buildings (`SnowTile`, `WoodTile`, or any `Tile` type).
4. Confirm the save loads without `ArgumentNullException` in Player.log.
5. Confirm `building.json` still exports correctly (449 entries, no `buildingDefs` key).

---

## Out of scope for this iteration

### Images (TODO §2g)
178 images from the 2023 export are not present in the 2024 export under either `export/images/` or `export/ui_image/`. These include action icons, achievement images, and asteroid thumbnails. Deferred — the 1241 new UI sprites in `ui_image/` are what the website update primarily needs.

### `spriteModifiers` (TODO §2e)
The 2023 `database.json` had a `spriteModifiers` list. No equivalent exists in any 2024 file. Deferred — determine whether the website still uses this field before treating it as a gap.

### Additional null Unity fields in BBuildingEntity / BEntity / BEquipment
Several fields (`attachableBuilding`, `rocketModule`, `deconstructable`, `demolishable`, `roomTracker`, `durability`, `leadSuitTank`, `plantableSeed`, `mutantPlant`) are raw Unity MonoBehaviour types and always serialize to null. They are present in the export but carry no data. Deferred — lower priority cleanup once the three main fixes are validated.

### Duplicate `id`/`Id` keys (TODO §2, noted during validation)
Multiple JSON files contain sibling keys that differ only by capitalisation (`"id"` and `"Id"`). Most JSON parsers are case-sensitive and handle this without error, but PowerShell's `ConvertFrom-Json` rejects them. Deferred — not a blocker for the website unless its parser is strict about this.

### Obsolete API warnings (TODO §4)
- `IEquipmentConfig.GetDlcIds()` — the one caller (`AddEquipmentDef`) is deleted as part of Fix 1, so this warning will disappear automatically.
- `DlcManager.IsDlcListValidForCurrentContent` — still called in `ExportItem.AddEquipmentDef` which is also deleted in Fix 1. If the warning persists after Fix 1, grep for remaining callers.

---

## Build and deploy reference

```
1. Build Solution (Visual Studio or `dotnet build`)
2. Copy OniExtract2024/bin/Debug/OniExtract2024.dll
       → …\mods\dev\OniExtract2024_dev\OniExtract2024.dll
3. Delete …\export\database\database.json  (stale Oct-2025 artifact)
4. Launch OxygenNotIncluded; reach main menu (export runs at load, no save needed)
5. Monitor Player.log for "OniExtract:" lines; look for errors
6. Run the validation checklist for the fix just applied
```

Export directory: `C:\Users\sinep\Documents\Klei\OxygenNotIncluded\export\database\`
