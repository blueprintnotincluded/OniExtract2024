using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace OniExtract2024.building
{
    /// <summary>
    /// Durable sidecar for measured uiImageRect values, keyed by prefab tag name
    /// (== building.json `name`). Mirrors <see cref="BuildingPoseOverrides"/>.
    ///
    /// Why this exists: a rect can only be measured from a live in-game render (the
    /// building-image sweep or an inspector touch-up), but building.json is authored
    /// from scratch by the game-data export every time it runs. Without a durable
    /// store the rects get clobbered each run while the hi-res images persist, so the
    /// website stretches a tight-cropped image to the footprint and squishes it (see
    /// docs/archive/UIIMAGERECT_DURABILITY.md). The image sweep writes here; the game-data export
    /// reads here, so building.json always carries the last-measured rects.
    /// </summary>
    public static class UiImageRectStore
    {
        // Sits next to the exported images and pose_overrides.json so it travels with
        // the export output.
        public static string PersistPath =>
            Path.Combine(Util.RootFolder(), "export", "ui_image_rects.json");

        private static Dictionary<string, UiImageRect> s_runtime;

        private static Dictionary<string, UiImageRect> Runtime
        {
            get
            {
                if (s_runtime == null) Load();
                return s_runtime;
            }
        }

        public static void Load()
        {
            s_runtime = new Dictionary<string, UiImageRect>();
            try
            {
                if (File.Exists(PersistPath))
                {
                    var data = JsonConvert.DeserializeObject<Dictionary<string, UiImageRect>>(
                        File.ReadAllText(PersistPath));
                    if (data != null) s_runtime = data;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("OniExtract: failed to load ui_image_rects: " + e.Message);
            }
        }

        public static bool TryGet(string prefabId, out UiImageRect rect) =>
            Runtime.TryGetValue(prefabId, out rect);

        public static int Count => Runtime.Count;

        // Merge a batch of freshly measured rects into the store and write it to disk. For a
        // touch-up of one building; the full sweep uses ReplaceAll. Values are rounded so the
        // sidecar and building.json show identical clean numbers. Returns true on a
        // successful disk write.
        public static bool SaveAll(IDictionary<string, UiImageRect> rects)
        {
            if (rects == null || rects.Count == 0) return false;
            foreach (var kv in rects)
                Runtime[kv.Key] = Rounded(kv.Value);
            return Flush();
        }

        // Make the store exactly these rects. For the full sweep, which measures every prefab
        // it can render: a rect it did not produce describes a PNG it did not write, either a
        // prefab that is no longer loaded or a render that failed this time. Merging kept
        // both, and the first kind reached the website as a current building.
        public static bool ReplaceAll(IDictionary<string, UiImageRect> rects)
        {
            if (rects == null || rects.Count == 0) return false;
            s_runtime = new Dictionary<string, UiImageRect>();
            return SaveAll(rects);
        }

        // Drop every rect whose prefab is not in `keep` and write the store if anything
        // changed. The game-data export calls this with the loaded prefabs, so a rect for a
        // building from a mod that has since been disabled does not outlive the mod. Returns
        // how many were removed.
        public static int PruneTo(IEnumerable<string> keep)
        {
            var keepSet = new HashSet<string>(keep);
            var stale = new List<string>();
            foreach (string key in Runtime.Keys)
                if (!keepSet.Contains(key))
                    stale.Add(key);
            foreach (string key in stale)
                Runtime.Remove(key);
            if (stale.Count > 0)
                Flush();
            return stale.Count;
        }

        private static bool Flush()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PersistPath));
                File.WriteAllText(PersistPath,
                    JsonConvert.SerializeObject(Runtime, Formatting.Indented));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("OniExtract: failed to save ui_image_rects to "
                    + PersistPath + ": " + e);
                return false;
            }
        }

        // 4 decimals ≈ 0.02 px at 200 px/cell — finer than the render — while keeping the
        // JSON readable. Real numbers, never rounded to whole cells (per the contract).
        private static UiImageRect Rounded(UiImageRect r) => new UiImageRect
        {
            x = Round(r.x),
            y = Round(r.y),
            w = Round(r.w),
            h = Round(r.h),
        };

        private static float Round(float v) => Mathf.Round(v * 10000f) / 10000f;
    }
}
