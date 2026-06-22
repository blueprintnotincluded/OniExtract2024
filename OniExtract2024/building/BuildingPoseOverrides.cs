using System.Collections.Generic;
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

        // Edit this dictionary to override the auto-posed animation for specific buildings.
        // Key: prefab tag name (KPrefabID.PrefabTag.Name).
        // Example entry: { "SteamTurbine2", new BuildingPose("generating_loop", 7) },
        public static readonly Dictionary<string, BuildingPose> Overrides =
            new Dictionary<string, BuildingPose>
        {
            // Add per-building overrides here.
        };

        public static bool TryGet(string prefabId, out BuildingPose pose) =>
            Overrides.TryGetValue(prefabId, out pose);

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
