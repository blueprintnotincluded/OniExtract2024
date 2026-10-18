using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace OniExtract2024.building
{
    /// <summary>
    /// export/building_image_report.json: how each building image was made (pose, canvas,
    /// clipping), keyed by prefab tag name like ui_image_rects.json. See
    /// <see cref="BuildingImageReportEntry"/>. The sweep replaces it; the inspector's
    /// single-image touch-up merges one entry into it.
    /// </summary>
    public static class BuildingImageReport
    {
        public static string PersistPath =>
            Path.Combine(Util.RootFolder(), "export", "building_image_report.json");

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static bool ReplaceAll(IDictionary<string, BuildingImageReportEntry> entries) =>
            Write(new SortedDictionary<string, BuildingImageReportEntry>(entries, StringComparer.Ordinal));

        public static bool Merge(string prefabId, BuildingImageReportEntry entry)
        {
            var all = new SortedDictionary<string, BuildingImageReportEntry>(StringComparer.Ordinal);
            try
            {
                if (File.Exists(PersistPath))
                {
                    var data = JsonConvert.DeserializeObject<Dictionary<string, BuildingImageReportEntry>>(
                        File.ReadAllText(PersistPath));
                    if (data != null)
                        foreach (var kv in data) all[kv.Key] = kv.Value;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("OniExtract: could not read " + PersistPath + ", rewriting it with one entry: " + e.Message);
            }
            all[prefabId] = entry;
            return Write(all);
        }

        private static bool Write(SortedDictionary<string, BuildingImageReportEntry> entries)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PersistPath));
                File.WriteAllText(PersistPath, JsonConvert.SerializeObject(entries, Settings));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("OniExtract: failed to write " + PersistPath + ": " + e);
                return false;
            }
        }
    }
}
