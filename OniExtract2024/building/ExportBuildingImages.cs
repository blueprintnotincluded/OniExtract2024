using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OniExtract2024.building
{
    /// <summary>
    /// In-game building-image exporter. Triggered from a pause-screen button
    /// (see ConnectionExportPatches). Iterates Assets.BuildingDefs, spawns each
    /// building off-screen, renders it at 200 px/cell via BuildingImageSnapshotter,
    /// and writes ui_image/{prefabId}.png — overwriting the low-res atlas icons
    /// produced by the main-menu pass.
    ///
    /// Run order: main-menu JSON+icon pass first (all icons), then this in-game
    /// pass (overwrites building icons with hi-res kanim renders).
    /// </summary>
    public static class ExportBuildingImages
    {
        public static bool IsRunning { get; private set; }

        public static string OutputDir =>
            Path.Combine(Util.RootFolder(), "export", "ui_image");

        public static void Start()
        {
            if (Game.Instance == null)
            {
                Debug.LogWarning("OniExtract: building-image export requires a loaded game.");
                return;
            }
            if (IsRunning)
            {
                Debug.LogWarning("OniExtract: building-image export already running.");
                return;
            }
            Game.Instance.StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            IsRunning = true;
            Debug.Log("OniExtract: building-image export started -> " + OutputDir);

            // Rocket modules need a CraftModuleInterface ancestor to find on spawn.
            // Attach the minimum set to the world object so they bind rather than
            // null-ref. Pattern taken from Sgt_Imalas AnimExportTool/Patches.cs.
            var worldGO = ClusterManager.Instance.activeWorld.gameObject;
            worldGO.AddOrGet<ClusterDestinationSelector>();
            worldGO.AddOrGet<ClusterTraveler>();
            worldGO.AddOrGet<Clustercraft>();
            worldGO.AddOrGet<CraftModuleInterface>();

            // Spawn well off-screen to avoid disturbing the live colony.
            int cell = Grid.PosToCell(Camera.main.transform.position);
            for (int i = 0; i < 12; i++)
                cell = Grid.CellDownLeft(cell);
            Vector3 spawnPos = Grid.CellToPos(cell);

            int exported = 0, skipped = 0;
            foreach (var def in Assets.BuildingDefs)
            {
                if (def == null || def.BuildingComplete == null || def.Deprecated || !def.ShowInBuildMenu)
                {
                    skipped++;
                    continue;
                }
                if (!def.BuildingComplete.TryGetComponent<KBatchedAnimController>(out _))
                {
                    skipped++;
                    continue;
                }

                GameObject temp = def.Create(spawnPos, null,
                    new List<Tag> { SimHashes.Unobtanium.CreateTag() }, null, 100f, def.BuildingComplete);
                if (temp == null)
                {
                    skipped++;
                    continue;
                }

                var snapshotter = temp.AddOrGet<BuildingImageSnapshotter>();
                snapshotter.StartExport(OutputDir);
                // Wait for the snapshotter to destroy the temp building. Unity defers
                // Destroy to end-of-frame, so IsNullOrDestroyed becomes true one frame
                // after KDestroyGameObject — the while loop handles this robustly.
                while (!temp.IsNullOrDestroyed())
                    yield return null;
                exported++;
            }

            Debug.Log("OniExtract: building-image export complete -> " + exported + " exported, " + skipped + " skipped.");
            IsRunning = false;
        }
    }
}
