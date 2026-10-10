using System;
using System.Collections;
using System.Collections.Generic;
using PeterHan.PLib.Options;
using UnityEngine;

namespace OniExtract2024
{
    /// <summary>
    /// The JSON data + UI icon export, triggered from a pause-screen button
    /// (see ConnectionExportPatches). It used to run by itself while the game booted, which
    /// left anyone whose export threw stuck on the loading screen with no idea why. Now it
    /// only runs when asked, from a loaded colony, where a failure costs one export step.
    ///
    /// The game builds most prefabs while it boots, and some of them can only be caught
    /// there; the load-time patches in Patches.cs keep a reference to each and nothing more.
    /// Everything is read and written here.
    /// </summary>
    public static class ExportGameData
    {
        private const string ExportName = "game data";

        public static void Start()
        {
            if (InGameExport.TryBegin(ExportName))
                Game.Instance.StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            // Replaced on completion; what the user sees if the coroutine itself throws.
            string summary = "The game data export stopped early. See Player.log for the error.";
            try
            {
                var options = SingletonOptions<ModOptions>.Instance;
                var steps = new List<KeyValuePair<string, System.Action>>();
                if (options.Food) steps.Add(Step("Food", WriteFood));
                if (options.Recipe) steps.Add(Step("Recipes", WriteRecipes));
                if (options.Element) steps.Add(Step("Elements", WriteElements));
                if (options.PoString) steps.Add(Step("PO strings", WritePOStrings));
                if (options.Tags) steps.Add(Step("Tags", WriteTags));
                if (options.db) steps.Add(Step("Db", WriteDbResources));
                if (options.Building) steps.Add(Step("Buildings", WriteBuildings));
                if (options.UISprintInfo) steps.Add(Step("UI sprites", WriteUISprites));
                if (options.Entities) steps.Add(Step("Entities", WriteEntities));
                if (options.MultiEntities) steps.Add(Step("Multi-entities", WriteMultiEntities));
                if (options.Item) steps.Add(Step("Items", WriteItems));
                if (options.Attr) steps.Add(Step("Attributes", WriteAttributes));
                if (options.Geyser) steps.Add(Step("Geysers", WriteGeysers));

                Debug.Log("OniExtract: game data export started, " + steps.Count + " steps.");
                // Let the banner draw before the first step holds the frame.
                yield return null;

                var failed = new List<string>();
                foreach (var step in steps)
                {
                    Debug.Log("OniExtract: Export " + step.Key);
                    try
                    {
                        step.Value();
                    }
                    catch (Exception e)
                    {
                        // One broken exporter should not cost the others their output.
                        Debug.LogError("OniExtract: " + step.Key + " export failed: " + e);
                        failed.Add(step.Key);
                    }
                    yield return null;
                }

                Debug.Log("OniExtract: game data export complete, " + failed.Count + " steps failed.");
                summary = failed.Count == 0
                    ? "Game data exported: " + steps.Count + " steps."
                    : "Game data exported with errors. Failed: " + string.Join(", ", failed.ToArray())
                        + ".\nSee Player.log for the errors.";
            }
            finally
            {
                InGameExport.End(summary);
            }
        }

        private static KeyValuePair<string, System.Action> Step(string name, System.Action run)
        {
            return new KeyValuePair<string, System.Action>(name, run);
        }

        private static void WriteFood()
        {
            var export = new ExportFood();
            export.ExportAllFood();
            export.ExportJsonFile();
        }

        private static void WriteRecipes()
        {
            var export = new ExportRecipe();
            export.ExportComplexRecipes();
            export.ExportJsonFile();
        }

        private static void WriteElements()
        {
            var export = new ExportElement();
            export.AddAllElement();
            export.ExportJsonFile();
        }

        private static void WritePOStrings()
        {
            var export = new ExportPOString();
            export.ExportAll();
            export.ExportJsonFile();
        }

        private static void WriteTags()
        {
            var export = new ExportTag();
            export.AddAllGameTags();
            export.ExportJsonFile();
        }

        private static void WriteDbResources()
        {
            var export = new ExportDb();
            export.AddDbResources();
            export.ExportJsonFile();
        }

        private static void WriteBuildings()
        {
            var export = new ExportBuilding();
            export.ExportBuildMenu();
            foreach (BuildingDef buildingDef in Assets.BuildingDefs)
            {
                export.AddNewBuildingEntity(buildingDef);
                export.AddNewBuildingDef(buildingDef);
            }
            export.ExportRocketModuleMenu();
            export.ExportPortIcons();
            export.ExportJsonFile();
        }

        private static void WriteUISprites()
        {
            var export = new ExportUISprite();
            export.ExportAllUISprite();
            export.ExportJsonFile();
        }

        private static void WriteEntities()
        {
            var export = new ExportEntity();
            foreach (GameObject gameObject in Patches.CapturedEntities)
            {
                if (gameObject == null)
                    continue;
                KPrefabID prefabID = gameObject.GetComponent<KPrefabID>();
                BEntity bEntity = new BEntity(prefabID.PrefabID().Name, prefabID);
                ExportEntity.LoadEntityComponent(gameObject, bEntity);
                export.entities.Add(bEntity);
            }
            export.ExportJsonFile();
        }

        private static void WriteMultiEntities()
        {
            var export = new ExportMultiEntity();
            foreach (var captured in Patches.CapturedMultiEntities)
            {
                GameObject gameObject = captured.Key;
                if (gameObject == null)
                    continue;
                KPrefabID prefabID = gameObject.GetComponent<KPrefabID>();
                BMultiEntity bMultiEntity = new BMultiEntity(prefabID.PrefabID().Name, prefabID)
                {
                    nameString = prefabID.GetProperName(),
                    entityType = captured.Value
                };
                export.LoadEntityComponent(gameObject, bMultiEntity);
                export.multiEntities.Add(bMultiEntity);
            }
            foreach (OutMeteorShowerEvent meteorShowerEvent in Patches.CapturedMeteorShowerEvents)
                export.addNewMeteorShowerEvent(meteorShowerEvent);
            export.updateAllMeteorShowEvent();
            export.ExportJsonFile();
        }

        private static void WriteItems()
        {
            var export = new ExportItem();
            foreach (GameObject seed in Patches.CapturedSeeds)
            {
                if (seed == null)
                    continue;
                KPrefabID prefabID = seed.GetComponent<KPrefabID>();
                export.AddSeed(seed, new BSeed(prefabID.PrefabID().Name, prefabID));
            }
            // Eggs and equipment are found among the registered prefabs: eggs by their
            // incubation monitor, equipment by the Equippable that RegisterEquipment adds.
            foreach (KPrefabID kpid in Assets.Prefabs)
            {
                if (kpid == null)
                    continue;
                GameObject prefab = kpid.gameObject;
                if (prefab.GetDef<IncubationMonitor.Def>() != null)
                    export.AddEgg(prefab, new BEgg(kpid.PrefabID().Name, kpid));
                if (prefab.GetComponent<Equippable>() != null)
                    export.AddEquipment(prefab, new BEquipment(kpid.PrefabID().Name, kpid));
            }
            export.ExportJsonFile();
        }

        private static void WriteAttributes()
        {
            var export = new ExportAttr();
            export.AddAllSicknessModifier();
            export.AddAllEnumClass();
            export.ExportJsonFile();
        }

        private static void WriteGeysers()
        {
            var export = new ExportGeyser();
            export.AddGeyserPrefabParams(Patches.CapturedGeyserParams);
            export.ExportJsonFile();
        }
    }
}
