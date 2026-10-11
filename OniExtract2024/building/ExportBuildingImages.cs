using System;
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
    /// produced by the game-data export. Also handles terrain features (geysers, vents,
    /// volcanoes) which export their icons but need rects measured in-game.
    ///
    /// Run order: Export Game Data first (all icons), then this in-game
    /// pass (overwrites building icons with hi-res kanim renders, adds terrain rects).
    /// </summary>
    public static class ExportBuildingImages
    {
        private const string ExportName = "building images";

        public static bool IsRunning => InGameExport.Running == ExportName;

        public static string OutputDir =>
            Path.Combine(Util.RootFolder(), "export", "ui_image");

        // Terrain features (geyser variants, vents, volcanoes, oil reservoir) to export rects for.
        // These appear in geyser.json and entities.json but are not BuildingDefs.
        private static readonly string[] TerrainFeaturePrefabNames = new[]
        {
            "GeyserGeneric_big_volcano",
            "GeyserGeneric_chlorine_gas",
            "GeyserGeneric_chlorine_gas_cool",
            "GeyserGeneric_filthy_water",
            "GeyserGeneric_hot_co2",
            "GeyserGeneric_hot_hydrogen",
            "GeyserGeneric_hot_po2",
            "GeyserGeneric_hot_steam",
            "GeyserGeneric_hot_water",
            "GeyserGeneric_liquid_co2",
            "GeyserGeneric_liquid_sulfur",
            "GeyserGeneric_methane",
            "GeyserGeneric_molten_aluminum",
            "GeyserGeneric_molten_cobalt",
            "GeyserGeneric_molten_copper",
            "GeyserGeneric_molten_gold",
            "GeyserGeneric_molten_iron",
            "GeyserGeneric_molten_niobium",
            "GeyserGeneric_molten_tungsten",
            "GeyserGeneric_murky_brine",
            "GeyserGeneric_oil_drip",
            "GeyserGeneric_salt_water",
            "GeyserGeneric_slimy_po2",
            "GeyserGeneric_slush_salt_water",
            "GeyserGeneric_slush_water",
            "GeyserGeneric_small_volcano",
            "GeyserGeneric_steam",
            "NiobiumGeyser",
            "OilWell",
            "SmallReefGeyser",
            "UnderwaterVent",
        };

        // Per-building rendered-image rectangle (cells, footprint-relative), keyed by
        // prefab tag name (== building.json `name`). Filled during the sweep, then merged
        // into database/building.json so the website can place each tight-cropped icon
        // without squishing its overhang. See docs/WEBSITE_POSTPROCESSING.md "uiImageRect".
        private static readonly Dictionary<string, UiImageRect> Rects =
            new Dictionary<string, UiImageRect>();

        public static void Start()
        {
            if (InGameExport.TryBegin(ExportName))
                Game.Instance.StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            // Replaced on success; what the user sees if the export throws part-way.
            string summary = "The building images export stopped early. See Player.log for the error.";
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

                // notRendered: spawned, but gone or empty by the time the snapshot ran, so no
                // image was written (a mod that deletes its own building on spawn, say).
                int exported = 0, skipped = 0, artOnly = 0;
                var notRendered = new List<string>();
                foreach (var def in Assets.BuildingDefs)
                {
                    GameObject temp;
                    bool spawned = BuildingSpawnFilter.IsRenderable(def);
                    if (spawned)
                    {
                        temp = def.Create(spawnPos, null,
                            new List<Tag> { SimHashes.Unobtanium.CreateTag() }, null, 100f, def.BuildingComplete);
                    }
                    else if (BuildingSpawnFilter.HasArt(def))
                    {
                        temp = CreateArtOnly(def, spawnPos);
                    }
                    else
                    {
                        skipped++;
                        continue;
                    }
                    if (temp == null)
                    {
                        skipped++;
                        continue;
                    }

                    // The footprint is passed explicitly because an art-only object has no
                    // Building to derive it from; for a real spawn it is the same value.
                    var snapshotter = temp.AddOrGet<BuildingImageSnapshotter>();
                    snapshotter.StartExport(OutputDir, Rects, def.WidthInCells, def.HeightInCells);
                    // Wait for the snapshotter to destroy the temp building. Unity defers
                    // Destroy to end-of-frame, so IsNullOrDestroyed becomes true one frame
                    // after KDestroyGameObject — the while loop handles this robustly.
                    while (!temp.IsNullOrDestroyed())
                        yield return null;
                    // The snapshotter records a rect exactly when it writes the PNG, so the
                    // rect is the evidence that this building was exported.
                    if (Rects.ContainsKey(def.PrefabID))
                    {
                        exported++;
                        if (!spawned) artOnly++;
                    }
                    else
                        notRendered.Add(def.PrefabID);
                }

                // Terrain features (geysers, vents, volcanoes, oil reservoir). Not BuildingDefs,
                // so the loop above never reaches them. Spawned through the same path the game
                // uses — KInstantiate + SetActive — because ONI prefabs are stored inactive: a
                // raw Object.Instantiate clone never runs Awake/OnSpawn, so its
                // KBatchedAnimController never registers with KAnimBatchManager and
                // BuildingKanimRenderer draws nothing. The snapshotter then poses and destroys
                // it exactly as it does for buildings, so both paths share one measurement path.
                int terrainExported = 0, terrainSkipped = 0;
                foreach (string prefabName in TerrainFeaturePrefabNames)
                {
                    GameObject prefab = Assets.TryGetPrefab(prefabName);
                    if (prefab == null)
                    {
                        Debug.LogWarning("OniExtract: terrain feature prefab not found: " + prefabName);
                        terrainSkipped++;
                        continue;
                    }

                    // Footprint comes from KBoxCollider2D, which EntityTemplates.ConfigPlacedEntity
                    // sets to exactly (width, height) in cells — the same values geyser.json carries.
                    if (!TryGetFootprint(prefab, out int cellW, out int cellH))
                    {
                        Debug.LogWarning("OniExtract: no KBoxCollider2D footprint on " + prefabName);
                        terrainSkipped++;
                        continue;
                    }

                    GameObject temp = Util.KInstantiate(prefab, spawnPos, Quaternion.identity);
                    if (temp == null)
                    {
                        terrainSkipped++;
                        continue;
                    }
                    temp.SetActive(true);

                    var snapshotter = temp.AddOrGet<BuildingImageSnapshotter>();
                    snapshotter.StartExport(OutputDir, Rects, cellW, cellH);
                    while (!temp.IsNullOrDestroyed())
                        yield return null;
                    if (Rects.ContainsKey(prefabName))
                        terrainExported++;
                    else
                        notRendered.Add(prefabName);
                }

                Debug.Log("OniExtract: building-image export complete -> " + exported + " exported ("
                    + artOnly + " of them art-only), " + skipped + " skipped.");
                Debug.Log("OniExtract: terrain-feature export complete -> " + terrainExported + " exported, " + terrainSkipped + " skipped.");
                if (notRendered.Count > 0)
                    Debug.LogWarning("OniExtract: spawned but not rendered (no image written): " + string.Join(", ", notRendered));

                PatchBuildingJsonRects(Rects);
                VerifyRectsMatchPngs(Rects);

                summary = "Building images exported: " + exported + " buildings and "
                    + terrainExported + " terrain features rendered"
                    + (notRendered.Count > 0 ? ", " + notRendered.Count + " spawned but not rendered" : "")
                    + ".\n" + OutputDir;
            }
            finally
            {
                InGameExport.End(summary);
            }
        }

        // Relative aspect deviation past which a PNG is considered not to be the crop its
        // rect was measured from.
        private const float AspectTolerance = 0.02f;

        // The name of the icon file for a prefab, which is NOT always its tag name: the
        // SaveUIFileName option switches between the tag and the localised proper name, and
        // rects are always keyed by tag. Both export passes resolve the filename through
        // ExportUISprite.GetFormatedUIImageFileName, so go through the prefab to get it and
        // fall back to the tag name when the prefab can't be resolved.
        public static string ResolveIconFileName(string prefabTagName)
        {
            try
            {
                GameObject prefab = Assets.TryGetPrefab(prefabTagName);
                KPrefabID kpid = prefab != null ? prefab.GetComponent<KPrefabID>() : null;
                if (kpid != null) return ExportUISprite.GetFormatedUIImageFileName(kpid);
            }
            catch (Exception)
            {
                // fall through
            }
            return prefabTagName;
        }

        // True when the PNG at pngPath is the render this rect was measured from. The contract
        // says the PNG maps linearly onto the rect, so its pixel aspect must equal w:h; an atlas
        // icon or a missing file fails that. Lets the game-data export tell "my write would
        // destroy a measured render" from "my write would restore a missing icon" without
        // trusting the rect key, which can name a different file than the one being written.
        public static bool PngMatchesRect(string pngPath, UiImageRect rect)
        {
            if (rect.w == 0f || rect.h == 0f) return false;
            if (!TryReadPngSize(pngPath, out int pxW, out int pxH) || pxH == 0) return false;
            float pngAspect = (float)pxW / pxH;
            return Mathf.Abs(pngAspect - rect.w / rect.h) / pngAspect <= AspectTolerance;
        }

        // The rect maps its PNG linearly onto the footprint, so w:h must equal the PNG's pixel
        // aspect. Because w/h are derived from the same crop that produced the PNG, a mismatch
        // means the file on disk is no longer that crop — i.e. something overwrote the render
        // (historically the game-data export's Def.GetUISprite icon, which put an atlas "ui" symbol there
        // instead). Cheap header read, no texture decode. Logged, never fatal.
        private static void VerifyRectsMatchPngs(IDictionary<string, UiImageRect> rects)
        {
            if (rects == null || rects.Count == 0) return;

            int checkedCount = 0, mismatched = 0, absent = 0;
            foreach (var kv in rects)
            {
                string path = Path.Combine(OutputDir, ResolveIconFileName(kv.Key) + ".png");
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

        private static readonly byte[] PngHeaderPrefix =
        {
            0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A,
            0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
        };

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
                // 8-byte signature, then a 13-byte IHDR as the first chunk. Anything else is
                // not a PNG we wrote, so it must not count as a measured render.
                for (int i = 0; i < PngHeaderPrefix.Length; i++)
                    if (header[i] != PngHeaderPrefix[i]) return false;
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

        // A stand-in for a building that cannot be spawned for real (rocket modules need a
        // rocket, LaunchPad corrupts an achievement, deprecated content corrupts state, ...):
        // the game's own EffectTemplate — an entity carrying a KBatchedAnimController and
        // nothing else, which FXHelpers.CreateEffect uses for one-off effects — given the
        // building's kanim. With no building logic there is nothing to crash, and the image is
        // all the sweep wants. It is placed exactly as BuildingDef.Create places a building
        // (GameUtil.KInstantiate at the def's scene layer), so the renderer frames it and
        // UiImageRect maps it the same way, and it carries the building's prefab tag so the
        // PNG, the pose override and the rect are all keyed as for a real spawn.
        //
        // What it cannot reproduce is anything the building adds at spawn: meters, child
        // controllers, symbols shown or hidden by OnSpawn. The controller settings below are
        // the ones BuildingLoader puts on BuildingComplete, so the authored art matches.
        internal static GameObject CreateArtOnly(BuildingDef def, Vector3 pos)
        {
            var source = def.BuildingComplete.GetComponentInChildren<KBatchedAnimController>(true);
            GameObject template = Assets.GetPrefab(EffectConfigs.EffectTemplateId);
            if (source == null || template == null) return null;

            GameObject go = GameUtil.KInstantiate(template, pos, def.SceneLayer);
            go.name = def.PrefabID;
            go.GetComponent<KPrefabID>().PrefabTag = def.Tag;

            var kbac = go.GetComponent<KBatchedAnimController>();
            kbac.AnimFiles = def.AnimFiles;
            // EffectTemplate draws with the Simple material; buildings use Default.
            kbac.materialType = source.materialType;
            kbac.initialAnim = source.initialAnim;
            kbac.initialMode = KAnim.PlayMode.Paused;
            kbac.fgLayer = source.fgLayer;
            kbac.animScale = source.animScale;
            go.SetActive(true);

            // Offset's setter re-registers the controller with the batcher, so set it once
            // the controller is live rather than on the inactive clone.
            if (source.Offset != Vector3.zero)
                kbac.Offset = source.Offset;
            return go;
        }

        // Footprint of a non-building placed entity, in cells. EntityTemplates.ConfigPlacedEntity
        // sets KBoxCollider2D.size = new Vector2f(width, height) with the same width/height the
        // config passes (and that geyser.json exports), so the collider is an exact integer
        // footprint, not an approximation.
        private static bool TryGetFootprint(GameObject go, out int cellW, out int cellH)
        {
            cellW = 0;
            cellH = 0;
            KBoxCollider2D collider = go.GetComponent<KBoxCollider2D>();
            if (collider == null) return false;
            cellW = Mathf.RoundToInt(collider.size.x);
            cellH = Mathf.RoundToInt(collider.size.y);
            return cellW > 0 && cellH > 0;
        }

        // Merge the measured uiImageRect for each rendered building into the building.json
        // the game-data export already wrote. Done as a post-pass (not in ExportBuilding)
        // because the rect can only be measured from a live in-game render, which happens
        // in a separate run from the JSON export. The website reads uiImageRect off each
        // bBuildingDefList entry; buildings we did not render keep the legacy
        // stretch-to-footprint fallback (field omitted).
        private static void PatchBuildingJsonRects(IDictionary<string, UiImageRect> rects)
        {
            if (rects == null || rects.Count == 0) return;

            // Persist to the durable sidecar first, so the rects survive the next data
            // export even if the full sweep isn't re-run (the game-data export reads this).
            // The direct building.json patch below keeps the CURRENT export correct
            // without another data export. See docs/archive/UIIMAGERECT_DURABILITY.md.
            UiImageRectStore.SaveAll(rects);

            string dbDir = BaseExport.BuildExportPath(
                Util.RootFolder(), "database", DlcManager.IsExpansion1Active());
            string path = Path.Combine(dbDir, "building.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("OniExtract: building.json not found at " + path +
                    " — run Export Game Data first so uiImageRect can be merged in.");
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
