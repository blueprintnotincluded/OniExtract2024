using System;
using HarmonyLib;
using static GeyserGenericConfig;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using Klei.AI;
using PeterHan.PLib.Options;
using UnityEngine;
using static STRINGS.UI.UISIDESCREENS;

namespace OniExtract2024
{
    public class Patches
    {
        // The game builds these prefabs while it boots, and some can only be caught there.
        // The patches below keep a reference to each and do nothing else, so nothing here can
        // hold up the loading screen. ExportGameData reads them when the player asks for an
        // export.
        internal static readonly List<GameObject> CapturedEntities = new List<GameObject>();
        // Each prefab with the name of the IMultiEntityConfig that made it.
        internal static readonly List<KeyValuePair<GameObject, string>> CapturedMultiEntities =
            new List<KeyValuePair<GameObject, string>>();
        internal static readonly List<OutMeteorShowerEvent> CapturedMeteorShowerEvents = new List<OutMeteorShowerEvent>();
        internal static readonly List<GameObject> CapturedSeeds = new List<GameObject>();
        internal static List<GeyserPrefabParams> CapturedGeyserParams = new List<GeyserPrefabParams>();

        [HarmonyPatch(typeof(EntityConfigManager), "RegisterEntity")]
        internal class OniExtract_Game_EntityConfig
        {
            private static readonly MethodInfo InjectBehind = AccessTools.Method(typeof(IEntityConfig), nameof(IEntityConfig.CreatePrefab));
            private static readonly MethodInfo RegisterExportEntityMethod = AccessTools.Method(typeof(OniExtract_Game_EntityConfig), nameof(OniExtract_Game_EntityConfig.RegisterPatch));

            public static GameObject RegisterPatch(GameObject gameObject)
            {
                if (gameObject != null)
                    CapturedEntities.Add(gameObject);
                return gameObject;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
            {
                var code = instructions.ToList();
                var insertionIndex = code.FindIndex(ci => ci.Calls(InjectBehind));

                if (insertionIndex != -1)
                {
                    if (!SingletonOptions<ModOptions>.Instance.Entities) return code;
                    code.Insert(++insertionIndex, new CodeInstruction(OpCodes.Call, RegisterExportEntityMethod));
                }
                return code;
            }
        }

        [HarmonyPatch(typeof(MeteorShowerEvent))]
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(string), typeof(float), typeof(float), typeof(MathUtil.MinMax), typeof(MathUtil.MinMax), typeof(string), typeof(bool) })]
        public class MeteorShowerEvent_Constructor_Patch
        {
            static void Postfix(string id, float duration, float secondsPerMeteor, MathUtil.MinMax secondsBombardmentOff, MathUtil.MinMax secondsBombardmentOn, string clusterMapMeteorShowerID, bool affectedByDifficulty)
            {
                if (!SingletonOptions<ModOptions>.Instance.MultiEntities) return;
                OutMeteorShowerEvent meteorShowEvent = new OutMeteorShowerEvent(id, (float)duration, (float)secondsPerMeteor, secondsBombardmentOff, secondsBombardmentOn, clusterMapMeteorShowerID, affectedByDifficulty);
                CapturedMeteorShowerEvents.Add(meteorShowEvent);
            }
        }

        [HarmonyPatch(typeof(EntityConfigManager), "RegisterEntities")]
        internal class OniExtract_Game_IMultiEntityConfig
        {
            private static readonly MethodInfo InjectBehind = AccessTools.Method(typeof(IMultiEntityConfig), nameof(IMultiEntityConfig.CreatePrefabs));
            private static readonly MethodInfo RegisterExportEntityMethod = AccessTools.Method(typeof(OniExtract_Game_IMultiEntityConfig), nameof(OniExtract_Game_IMultiEntityConfig.RegisterPatch));
            static string entityType = null;

            public static void Prefix(IMultiEntityConfig config)
            {
                entityType = config.GetType().Name;
            }

            public static List<GameObject> RegisterPatch(List<GameObject> gameObjects)
            {
                foreach (var gameObject in gameObjects)
                {
                    if (gameObject != null)
                        CapturedMultiEntities.Add(new KeyValuePair<GameObject, string>(gameObject, entityType));
                }
                return gameObjects;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
            {
                var code = instructions.ToList();
                var insertionIndex = code.FindIndex(ci => ci.Calls(InjectBehind));

                if (insertionIndex != -1)
                {
                    if (!SingletonOptions<ModOptions>.Instance.MultiEntities) return code;
                    code.Insert(++insertionIndex, new CodeInstruction(OpCodes.Call, RegisterExportEntityMethod));
                }
                return code;
            }
        }

        [HarmonyPatch(typeof(GeyserGenericConfig), "GenerateConfigs")]
        internal class OniExtract_Game_Geysers
        {
            static void Postfix(ref List<GeyserPrefabParams> __result)
            {
                // A copy, because CreatePrefabs removes the non-generic geysers from this same
                // list straight afterwards. Replaced rather than appended to, so if the game
                // ever calls this twice the last call wins.
                if (__result != null)
                    CapturedGeyserParams = new List<GeyserPrefabParams>(__result);
            }
        }

        [HarmonyPatch(typeof(EntityTemplates), "CreateAndRegisterSeedForPlant")]
        [HarmonyPatch(new Type[] { typeof(GameObject), typeof(IHasDlcRestrictions), typeof(SeedProducer.ProductionType), typeof(string), typeof(string), typeof(string), typeof(KAnimFile), typeof(string), typeof(int), typeof(List<Tag>), typeof(SingleEntityReceptacle.ReceptacleDirection), typeof(Tag), typeof(int), typeof(string), typeof(EntityTemplates.CollisionShape), typeof(float), typeof(float), typeof(Recipe.Ingredient[]), typeof(string), typeof(bool) })]
        internal class OniExtract_Game_Seed
        {
            private static void Postfix(ref GameObject __result)
            {
                if (__result != null)
                    CapturedSeeds.Add(__result);
            }
        }

        [HarmonyPatch(typeof(Localization), nameof(Localization.Initialize))]
        internal class Localization_Initialize_Patch
        {
            private static void Postfix()
            {
                LocString.CreateLocStringKeys(typeof(ModStrings.Options));
            }
        }

    }
}
