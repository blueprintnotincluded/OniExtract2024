using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace OniExtract2024.building
{
    /// <summary>
    /// Trees whose branches are separate prefabs (ForestTreeBranch and the like) that the
    /// game spawns at offsets it fixes in the trunk's PlantBranchGrower.Def. The sweep draws
    /// such a tree whole: the spawned trunk plus one posed art stand-in per branch offset,
    /// at the offset and with the controller offset the branch config gives a live branch,
    /// so the icon is the full canopy and the footprint (PlantFootprint) takes the branches
    /// in. Branches are never spawned for real: a PlantBranch without a trunk state machine
    /// destroys itself, and a live tree carries at most MAX_BRANCH_COUNT of its offsets, at
    /// random, so a real spawn would not be canonical either.
    ///
    /// Only the trees listed in <see cref="Specs"/> get a canopy. The Ovagro Node and Tower
    /// Kelp grow their branches along whatever terrain is there, with no fixed offsets, so
    /// they are drawn as their trunk alone.
    /// </summary>
    internal static class PlantCanopy
    {
        private class Spec
        {
            // The branch's IEntityConfig, which keeps a private static animationSets
            // (CellOffset -> anim set) and animOffset (CellOffset -> controller offset).
            public Type BranchConfig;

            // The grown, at-rest anim of one branch's anim set, and the frame to pose it at
            // (-1 for the middle of the anim, as buildings are posed).
            public Func<object, string> Anim;
            public int Frame;

            // Meter placeholder symbols the branch's anim set names, hidden as the game's
            // meter would hide them; null when the set has none.
            public Func<object, string[]> HiddenSymbols;
        }

        private static readonly Dictionary<string, Spec> Specs = new Dictionary<string, Spec>
        {
            // Arbor Tree: seven branch offsets, each with its own idle_full.
            {
                "ForestTree", new Spec
                {
                    BranchConfig = typeof(ForestTreeBranchConfig),
                    Anim = set => ((StandardCropPlant.AnimSet)set).idle_full,
                    Frame = -1,
                }
            },
            // Bonbon Tree: five branch offsets. A grown branch shows its fill anim paused at
            // the trunk's syrup level (SpaceTreeBranch.PlayFillAnimation); frame 0 is the
            // everyday, unfilled look. Its leaves meter draws on a placeholder symbol.
            {
                "SpaceTree", new Spec
                {
                    BranchConfig = typeof(SpaceTreeBranchConfig),
                    Anim = set => ((SpaceTreeBranch.AnimSet)set).fill,
                    Frame = 0,
                    HiddenSymbols = set => ((SpaceTreeBranch.AnimSet)set).meterTargets,
                }
            },
        };

        public static bool HasCanopy(string prefabId) => Specs.ContainsKey(prefabId);

        // The branch offsets drawn into this trunk's image, for its footprint: the game's own
        // list when the tree has a canopy here, otherwise none.
        public static CellOffset[] BranchOffsets(GameObject prefab)
        {
            var kpid = prefab.GetComponent<KPrefabID>();
            if (kpid == null || !Specs.ContainsKey(kpid.PrefabTag.Name)) return new CellOffset[0];
            var def = prefab.GetDef<PlantBranchGrower.Def>();
            return def != null && def.BRANCH_OFFSETS != null ? def.BRANCH_OFFSETS : new CellOffset[0];
        }

        // Adds a posed stand-in for each branch offset as a child of the spawned trunk, so the
        // snapshotter draws, measures and destroys them with it. Returns how many were added;
        // 0 for a plant with no canopy or when the branch data cannot be read (logged).
        public static int Attach(GameObject trunk)
        {
            var kpid = trunk.GetComponent<KPrefabID>();
            if (kpid == null || !Specs.TryGetValue(kpid.PrefabTag.Name, out Spec spec)) return 0;
            string prefabId = kpid.PrefabTag.Name;

            var def = trunk.GetDef<PlantBranchGrower.Def>();
            if (def == null || def.BRANCH_OFFSETS == null || def.BRANCH_OFFSETS.Length == 0)
            {
                Debug.LogWarning("OniExtract: " + prefabId + " has no PlantBranchGrower.Def branch offsets; drawing the trunk alone.");
                return 0;
            }
            GameObject branchPrefab = Assets.TryGetPrefab(def.BRANCH_PREFAB_NAME);
            var source = branchPrefab != null ? branchPrefab.GetComponent<KBatchedAnimController>() : null;
            if (source == null)
            {
                Debug.LogWarning("OniExtract: branch prefab " + def.BRANCH_PREFAB_NAME + " of " + prefabId + " not found; drawing the trunk alone.");
                return 0;
            }
            var sets = Traverse.Create(spec.BranchConfig).Field("animationSets").GetValue() as IDictionary;
            var offsets = Traverse.Create(spec.BranchConfig).Field("animOffset").GetValue() as IDictionary;
            if (sets == null || offsets == null)
            {
                Debug.LogWarning("OniExtract: " + spec.BranchConfig.Name + " has no animationSets/animOffset; drawing " + prefabId + "'s trunk alone.");
                return 0;
            }

            // Where the game puts a live branch: the trunk's position plus the offset (not the
            // grid cell, since the sweep spawns half a cell off the grid), on the BuildingFront
            // layer, each branch a little nearer the camera than the one before
            // (PlantBranchGrower.Instance.RefreshBranchZPositionOffset).
            Vector3 trunkPos = trunk.transform.GetPosition();
            float frontZ = Grid.GetLayerZ(Grid.SceneLayer.BuildingFront);
            Tag branchTag = branchPrefab.GetComponent<KPrefabID>().PrefabTag;
            int count = 0;
            for (int i = 0; i < def.BRANCH_OFFSETS.Length; i++)
            {
                CellOffset offset = def.BRANCH_OFFSETS[i];
                if (!sets.Contains(offset))
                {
                    Debug.LogWarning("OniExtract: " + prefabId + " has no branch anim set for offset (" + offset.x + ", " + offset.y + "); skipping it.");
                    continue;
                }
                object set = sets[offset];
                string anim = spec.Anim(set);
                if (string.IsNullOrEmpty(anim) || !source.HasAnimation(anim))
                {
                    Debug.LogWarning("OniExtract: " + prefabId + " branch at (" + offset.x + ", " + offset.y + ") names anim " + (anim ?? "null") + ", which its kanim lacks; skipping it.");
                    continue;
                }

                var pos = new Vector3(trunkPos.x + offset.x, trunkPos.y + offset.y,
                    frontZ - 0.8f / def.BRANCH_OFFSETS.Length * i);
                GameObject go = ExportBuildingImages.CreateArtStandIn(source, pos, Grid.SceneLayer.BuildingFront,
                    def.BRANCH_PREFAB_NAME, branchTag);
                if (go == null) continue;

                var kbac = go.GetComponent<KBatchedAnimController>();
                if (offsets.Contains(offset))
                    kbac.Offset = (Vector3)offsets[offset];
                string[] hidden = spec.HiddenSymbols != null ? spec.HiddenSymbols(set) : null;
                if (hidden != null)
                    foreach (string symbol in hidden)
                        if (!string.IsNullOrEmpty(symbol))
                            kbac.SetSymbolVisiblity(symbol, false);

                go.transform.SetParent(trunk.transform, true);
                var posed = go.AddComponent<PosedStandIn>();
                posed.anim = anim;
                posed.frame = spec.Frame;
                count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Marks an art stand-in parented under a spawned object, with the anim and frame to pose
    /// it in. BuildingImageSnapshotter poses these right before the snapshot, after the root.
    /// </summary>
    public class PosedStandIn : MonoBehaviour
    {
        public string anim;
        public int frame = -1;
    }
}
