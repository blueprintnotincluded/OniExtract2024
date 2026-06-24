using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace OniExtract2024.building
{
    public static class BuildingPoseOverrides
    {
        public struct BuildingPose
        {
            public string Anim;
            public int Frame;

            public BuildingPose(string anim, int frame) { Anim = anim; Frame = frame; }
        }

        // Hard-coded baseline overrides (the committed source of truth). The runtime store
        // below is layered OVER this, so anything saved from the inspector wins until you
        // bake it back into this dictionary via the inspector's "Copy all (C#)" button.
        // Key: prefab tag name (KPrefabID.PrefabTag.Name).
        // Example entry: { "SteamTurbine2", new BuildingPose("generating_loop", 7) },
        public static readonly Dictionary<string, BuildingPose> Overrides =
            new Dictionary<string, BuildingPose>
        {
            // Add per-building overrides here.
        };

        // Runtime overrides saved live from the pose inspector. Persisted to disk so the
        // exporter picks them up the same session — no rebuild/restart needed — and so
        // reopening the inspector restores your last choice. Lazily loaded.
        private static Dictionary<string, BuildingPose> s_runtime;

        // Sits next to the exported images so it travels with the export output.
        public static string PersistPath =>
            Path.Combine(Util.RootFolder(), "export", "pose_overrides.json");

        private static Dictionary<string, BuildingPose> Runtime
        {
            get
            {
                if (s_runtime == null) Load();
                return s_runtime;
            }
        }

        public static void Load()
        {
            s_runtime = new Dictionary<string, BuildingPose>();
            try
            {
                if (File.Exists(PersistPath))
                {
                    var data = JsonConvert.DeserializeObject<Dictionary<string, BuildingPose>>(
                        File.ReadAllText(PersistPath));
                    if (data != null) s_runtime = data;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("OniExtract: failed to load pose overrides: " + e.Message);
            }
        }

        // Set to the error message when the last Save failed, else null. Surfaced in the
        // inspector status line so a write failure (vs. looking in the wrong folder) is visible.
        public static string LastSaveError { get; private set; }

        // Records a choice and writes the whole runtime store back to disk immediately, so a
        // crash mid-session never loses more than the in-flight entry. Returns true on a
        // successful disk write.
        public static bool Save(string prefabId, BuildingPose pose)
        {
            Runtime[prefabId] = pose;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PersistPath));
                File.WriteAllText(PersistPath,
                    JsonConvert.SerializeObject(Runtime, Formatting.Indented));
                LastSaveError = null;
                Debug.Log("OniExtract: saved pose " + prefabId + " -> " + pose.Anim
                    + " @ frame " + pose.Frame + "  (" + PersistPath + ")");
                return true;
            }
            catch (Exception e)
            {
                LastSaveError = e.Message;
                Debug.LogWarning("OniExtract: failed to save pose overrides to "
                    + PersistPath + ": " + e);
                return false;
            }
        }

        public static bool HasSaved(string prefabId) => Runtime.ContainsKey(prefabId);

        public static int SavedCount => Runtime.Count;

        // Runtime store wins over the hard-coded baseline.
        public static bool TryGet(string prefabId, out BuildingPose pose)
        {
            if (Runtime.TryGetValue(prefabId, out pose)) return true;
            return Overrides.TryGetValue(prefabId, out pose);
        }

        // One paste-ready C# line for a single building.
        public static string ToCodeLine(string prefabId, BuildingPose pose) =>
            "{ \"" + prefabId + "\", new BuildingPose(\"" + pose.Anim + "\", " + pose.Frame + ") },";

        // The full saved set as a C# block, ready to paste into Overrides above and commit.
        public static string ToOverridesCode()
        {
            var sb = new StringBuilder();
            foreach (var kv in Runtime.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine("            " + ToCodeLine(kv.Key, kv.Value));
            return sb.ToString();
        }

        // Maps a 0-based frame index to a SetPositionPercent value centred on that frame.
        // (frame + 0.5) / numFrames matches the exporter's own 0.5 default when frame = numFrames/2,
        // and ensures any frame number the inspector displays means the same thing at export time.
        public static float PercentForFrame(int frame, int numFrames)
        {
            if (numFrames <= 0) return 0f;
            return (Mathf.Clamp(frame, 0, numFrames - 1) + 0.5f) / numFrames;
        }

        // Inverse of PercentForFrame: maps a timeline percent back to the nearest frame index.
        public static int FrameForPercent(float percent, int numFrames)
        {
            if (numFrames <= 0) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(percent * numFrames), 0, numFrames - 1);
        }
    }
}
