using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        // Per-building rendered-image rectangle (cells, footprint-relative), keyed by
        // prefab tag name (== building.json `name`). Filled during the sweep, then merged
        // into database/building.json so the website can place each tight-cropped icon
        // without squishing its overhang. See WEBSITE_POSTPROCESSING.md "uiImageRect".
        private static readonly Dictionary<string, UiImageRect> Rects =
            new Dictionary<string, UiImageRect>();

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
            try
            {
                Rects.Clear();
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
                    if (!BuildingSpawnFilter.IsRenderable(def))
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
                    snapshotter.StartExport(OutputDir, Rects);
                    // Wait for the snapshotter to destroy the temp building. Unity defers
                    // Destroy to end-of-frame, so IsNullOrDestroyed becomes true one frame
                    // after KDestroyGameObject — the while loop handles this robustly.
                    while (!temp.IsNullOrDestroyed())
                        yield return null;
                    exported++;
                }

                Debug.Log("OniExtract: building-image export complete -> " + exported + " exported, " + skipped + " skipped.");

                PatchBuildingJsonRects(Rects);
                VerifyRectsMatchPngs(Rects);
            }
            finally
            {
                IsRunning = false;
            }
        }

        // The rect maps its PNG linearly onto the footprint, so w:h must equal the PNG's pixel
        // aspect. Because w/h are derived from the same crop that produced the PNG, a mismatch
        // means the file on disk is no longer that crop — i.e. something overwrote the render
        // (historically the main-menu Def.GetUISprite pass, which put an atlas "ui" symbol there
        // instead). Cheap header read, no texture decode. Logged, never fatal.
        private static void VerifyRectsMatchPngs(IDictionary<string, UiImageRect> rects)
        {
            if (rects == null || rects.Count == 0) return;

            int checkedCount = 0, mismatched = 0, absent = 0;
            foreach (var kv in rects)
            {
                string path = Path.Combine(OutputDir, kv.Key + ".png");
                if (!TryReadPngSize(path, out int pxW, out int pxH))
                {
                    absent++;
                    continue;
                }
                UiImageRect r = kv.Value;
                if (pxH == 0 || r.h == 0f) continue;
                checkedCount++;

                float pngAspect = (float)pxW / pxH;
                float rectAspect = r.w / r.h;
                float relative = Mathf.Abs(pngAspect - rectAspect) / pngAspect;
                if (relative > 0.02f)
                {
                    mismatched++;
                    Debug.LogWarning(string.Format(
                        "OniExtract: uiImageRect aspect mismatch for {0} — png {1}x{2} ({3:F3}) vs rect ({4:F3}), {5:P0} off",
                        kv.Key, pxW, pxH, pngAspect, rectAspect, relative));
                }
            }
            Debug.Log("OniExtract: uiImageRect aspect check -> " + checkedCount + " checked, "
                + mismatched + " mismatched, " + absent + " png missing.");
        }

        // Reads width/height from a PNG's IHDR chunk (bytes 16..23, big-endian) without
        // decoding the image.
        private static bool TryReadPngSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            try
            {
                if (!File.Exists(path)) return false;
                var header = new byte[24];
                using (var fs = File.OpenRead(path))
                {
                    if (fs.Read(header, 0, 24) < 24) return false;
                }
                width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                return width > 0 && height > 0;
            }
            catch
            {
                return false;
            }
        }

        // Re-export a single already-posed building from the inspector ("touch-up" path):
        // overwrite its ui_image PNG and refresh its uiImageRect in building.json, so a one-off
        // pose tweak lands in the same output the full sweep produces — no full re-run needed.
        // The crop bbox (and thus the rect) can shift when the pose changes, so the rect is
        // always re-merged, not just the PNG.
        public static void ExportSingle(GameObject posedBuilding)
        {
            if (posedBuilding == null || posedBuilding.IsNullOrDestroyed()) return;
            var rects = new Dictionary<string, UiImageRect>();
            BuildingImageSnapshotter.RenderAndWrite(posedBuilding, OutputDir, rects);
            if (rects.Count > 0)
                PatchBuildingJsonRects(rects);
        }

        // Merge the measured uiImageRect for each rendered building into the building.json
        // the main-menu pass already wrote. Done as a post-pass (not in ExportBuilding)
        // because the rect can only be measured from a live in-game render, which happens
        // long after the no-save JSON export. The website reads uiImageRect off each
        // bBuildingDefList entry; buildings we did not render keep the legacy
        // stretch-to-footprint fallback (field omitted).
        private static void PatchBuildingJsonRects(IDictionary<string, UiImageRect> rects)
        {
            if (rects == null || rects.Count == 0) return;

            // Persist to the durable sidecar first, so the rects survive the next game
            // load even if the full sweep isn't re-run (the main-menu pass reads this).
            // The direct building.json patch below keeps the CURRENT export correct
            // without waiting for a reload. See UIIMAGERECT_DURABILITY.md.
            UiImageRectStore.SaveAll(rects);

            string dbDir = BaseExport.BuildExportPath(
                Util.RootFolder(), "database", DlcManager.IsExpansion1Active());
            string path = Path.Combine(dbDir, "building.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("OniExtract: building.json not found at " + path +
                    " — run the main-menu export first so uiImageRect can be merged in.");
                return;
            }

            JObject root = JObject.Parse(File.ReadAllText(path));
            JArray list = root["bBuildingDefList"] as JArray;
            if (list == null)
            {
                Debug.LogWarning("OniExtract: building.json has no bBuildingDefList; skipping uiImageRect merge.");
                return;
            }

            int placed = 0;
            foreach (JObject entry in list)
            {
                string name = (string)entry["name"];
                if (name != null && rects.TryGetValue(name, out UiImageRect r))
                {
                    entry["uiImageRect"] = new JObject
                    {
                        ["x"] = Round(r.x),
                        ["y"] = Round(r.y),
                        ["w"] = Round(r.w),
                        ["h"] = Round(r.h),
                    };
                    placed++;
                }
            }

            File.WriteAllText(path, root.ToString(Formatting.Indented));
            Debug.Log("OniExtract: buildings with uiImageRect placement: " + placed + " / " + list.Count);
        }

        // 4 decimals ≈ 0.02 px at 200 px/cell — finer than the render — while keeping the
        // JSON readable. Real numbers, never rounded to whole cells (per the contract).
        private static float Round(float v) => Mathf.Round(v * 10000f) / 10000f;
    }
}
