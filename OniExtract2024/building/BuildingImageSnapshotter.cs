// Camera-snapshot rendering adapted from the GPL-2.0 project Sgt_Imalas-Oni-Mods
// (AnimExportTool/AETE_KbacSnapShotter.cs), itself courtesy of Aki
// (https://github.com/aki-art/ONI-Mods). Adapted for OniExtract2024 to render
// building icons at 200 px/cell and trim to the opaque bounding box.
//
// Difference from ConnectionSpriteSnapshotter: one shot per building (build-complete
// idle state), output cropped to the tightest opaque bbox rather than a cell-centred
// square, and written to ui_image/ (same path as the main-menu atlas pass so the
// hi-res file lands exactly on top of the low-res one).

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace OniExtract2024.building
{
    /// <summary>
    /// Attached to a temporary off-screen building. Snapshots the live kanim at
    /// 200 px/cell, trims to the opaque bounding box, writes ui_image/{prefabId}.png,
    /// then destroys the host GameObject. Driven by ExportBuildingImages' coroutine.
    /// </summary>
    public class BuildingImageSnapshotter : KMonoBehaviour
    {
        // Where in the chosen animation to snapshot when there is no override. 0.5 =
        // halfway through the timeline — the building is fully deployed/emitting rather
        // than in the starting/retracting pose at frame 0.
        private const float PoseFramePercent = 0.5f;

        // Substrings that mark an animation as a non-active state — never snapshot these.
        // Includes construction/placement/teardown anims ("place", "construct", "install",
        // "demo") since those are never the working pose we want for an icon.
        private static readonly string[] InactiveMarkers =
        {
            "off", "broken", "error", "dead", "disabled", "outofnetwork",
            "no_power", "nopower", "unpowered", "closed", "empty", "ui",
            "place", "construct", "install", "demo",
        };

        // Fallback probe order if the build's anim list can't be enumerated.
        private static readonly string[] ActiveAnimFallback =
        {
            "working_loop", "generating_loop", "channeling_loop", "working", "generating",
            "channeling", "dispensing", "on_loop", "on", "idle_loop", "idle",
        };

        public void StartExport(string outputDir, IDictionary<string, UiImageRect> rects = null)
        {
            StartCoroutine(DoExport(outputDir, rects));
        }

        private IEnumerator DoExport(string outputDir, IDictionary<string, UiImageRect> rects)
        {
            yield return new WaitForSecondsRealtime(0.1f);

            try
            {
                var kpid = GetComponent<KPrefabID>();
                var kbac = GetComponent<KBatchedAnimController>();

                if (kpid != null && kbac != null)
                {
                    PoseActive(kbac, kpid.PrefabTag.Name);
                    RenderAndWrite(gameObject, outputDir, rects);
                }
            }
            finally
            {
                Util.KDestroyGameObject(gameObject);
            }
        }

        // Renders an already-posed building, crops to the opaque bbox, writes
        // ui_image/{prefabId}.png, and (optionally) records its uiImageRect. Does NOT pose or
        // destroy the GameObject — the caller owns posing and lifetime. Shared by the export
        // sweep (DoExport) and the inspector's single-image touch-up export, so both produce
        // pixel-identical output. Static so the inspector can reuse it without an attached
        // snapshotter component on the temp building.
        public static void RenderAndWrite(GameObject go, string outputDir,
            IDictionary<string, UiImageRect> rects = null)
        {
            var kpid = go.GetComponent<KPrefabID>();
            if (kpid == null) return;

            SelectTool.Instance.Select(null);

            var building = go.GetComponent<Building>();
            int cellW = building != null ? building.Def.WidthInCells : 1;
            int cellH = building != null ? building.Def.HeightInCells : 1;

            var renderer = new BuildingKanimRenderer();
            renderer.Init(cellW, cellH, go.transform.GetPosition());

            Texture2D raw = null;
            try
            {
                CameraController.Instance.baseCamera.enabled = false;
                var kbacs = go.GetComponentsInChildren<KBatchedAnimController>()
                    .OrderBy(k => k.transform.position.z);
                renderer.Render(kbacs);
                raw = renderer.ReadPixels();
            }
            finally
            {
                // Always restore camera and release the RT — not doing so leaks graphics
                // memory at a rate that crashes the game before the export sweep finishes.
                CameraController.Instance.baseCamera.enabled = true;
                renderer.Cleanup();
            }
            if (raw == null) return;

            Texture2D cropped = TrimToOpaqueBBox(raw, out int minX, out int minY, out int cw, out int ch);
            UnityEngine.Object.Destroy(raw);
            if (cropped == null) return;

            Directory.CreateDirectory(outputDir);
            string fileName = ExportUISprite.GetFormatedUIImageFileName(kpid);
            string filePath = Path.Combine(outputDir, fileName + ".png");
            File.WriteAllBytes(filePath, cropped.EncodeToPNG());
            UnityEngine.Object.Destroy(cropped);
            Debug.Log("OniExtract: building image -> " + filePath);

            if (rects != null)
                rects[kpid.PrefabTag.Name] = ComputeRect(minX, minY, cw, ch, cellW, cellH);
        }

        // Pose the building's main kanim in the most "active" state. Checks
        // BuildingPoseOverrides first; falls back to the auto-scorer unchanged.
        // ONLY the root controller is posed — driving child controllers stamps
        // a full extra copy of the building at each child's transform offset.
        private void PoseActive(KBatchedAnimController rootKbac, string prefabId)
        {
            if (BuildingPoseOverrides.TryGet(prefabId, out var pose))
                PoseController(rootKbac, pose.Anim, pose.Frame);
            else
                PoseController(rootKbac, ChooseActiveAnim(rootKbac), -1);
        }

        // Poses an off-screen controller at a specific anim + frame so the chosen frame
        // actually renders. Shared by the export sweep, the single-image export, and the
        // inspector preview, so all three behave identically. Pass frame &lt; 0 for the
        // mid-loop default (PoseFramePercent). Returns the anim's frame count (>= 1).
        //
        // Two subtleties are handled here, in this exact order:
        //  1) Play() only QUEUES the anim; it isn't applied — and the timeline isn't reset to
        //     frame 0 — until the controller next updates. So we Play, then UpdateFrame(0f)
        //     ONCE to start the queued anim (landing on frame 0), and ONLY THEN set the frame
        //     position. Doing SetPositionPercent before the queued anim starts gets wiped back
        //     to frame 0 when it starts — which made every anim show only its first frame and
        //     the frame slider appear dead.
        //  2) The building is off-screen, so the engine flags it not-visible and skips the
        //     per-frame vertex write. SetVisiblity(true) + forceRebuild + SetDirty force the
        //     write. (forceRebuild/UpdateFrame are protected → Traverse; the rest are public.)
        public static int PoseController(KBatchedAnimController kbac, string anim, int frame)
        {
            if (kbac == null || string.IsNullOrEmpty(anim)) return 1;

            kbac.SetVisiblity(true);
            kbac.Play(anim, KAnim.PlayMode.Paused);
            kbac.SetDirty();
            Traverse.Create(kbac).Method("UpdateFrame", 0f).GetValue();   // start queued anim @ frame 0

            int numFrames = kbac.GetCurrentNumFrames();
            if (numFrames <= 0) numFrames = 1;
            float percent = frame < 0
                ? PoseFramePercent
                : BuildingPoseOverrides.PercentForFrame(frame, numFrames);
            kbac.SetPositionPercent(percent);

            Traverse.Create(kbac).Property("forceRebuild").SetValue(true);
            kbac.SetDirty();
            Traverse.Create(kbac).Method("UpdateFrame", 0f).GetValue();   // commit the scrubbed frame
            return numFrames;
        }

        // Returns the highest-scoring active animation the controller actually has, or
        // null to leave the spawned default untouched. Exposed internal so the inspector
        // can seed its anim picker with the same default the exporter would choose.
        internal static string ChooseActiveAnim(KBatchedAnimController kbac)
        {
            List<string> names = GetAnimNames(kbac);
            if (names == null || names.Count == 0)
                return ProbeFallback(kbac);

            string best = null;
            int bestScore = 0;
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name) || !kbac.HasAnimation(name))
                    continue;
                int score = ScoreAnim(name);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = name;
                }
            }
            return best ?? ProbeFallback(kbac);
        }

        // Returns the readable names of every animation this controller can actually play.
        // Reads the controller's own `anims` map (KAnimControllerBase.anims) — the authoritative
        // per-controller set — but resolves each entry to a real name string instead of the
        // dict KEY: those keys are HashedStrings whose source string isn't in HashCache, so
        // .ToString() returns the numeric hash (useless for Play()/overrides). Each value is a
        // KAnimControllerBase.AnimLookupData carrying an `animIndex`; GetAnim(animIndex) yields
        // the KAnim.Anim whose `name` field is the readable string the exporter must store.
        // (The even-older approach went via KAnimGroupFile.GetGroup(GetBuildHash()) — but
        // GetGroup is keyed by GROUP hash, not BUILD hash, so it returned null for most
        // buildings.) Exposed internal so the inspector can populate its animation picker.
        internal static List<string> GetAnimNames(KBatchedAnimController kbac)
        {
            var dict = Traverse.Create(kbac).Field("anims").GetValue() as IDictionary;
            if (dict == null || dict.Count == 0)
                return null;

            var names = new List<string>(dict.Count);
            foreach (var val in dict.Values)
            {
                int animIndex = Traverse.Create(val).Field("animIndex").GetValue<int>();
                KAnim.Anim anim = kbac.GetAnim(animIndex);
                if (anim != null && !string.IsNullOrEmpty(anim.name))
                    names.Add(anim.name);
            }
            return names;
        }

        private static string ProbeFallback(KBatchedAnimController kbac)
        {
            foreach (string name in ActiveAnimFallback)
                if (kbac.HasAnimation(name))
                    return name;
            return null;
        }

        private static int ScoreAnim(string name)
        {
            string n = name.ToLowerInvariant();
            foreach (string bad in InactiveMarkers)
                if (n.Contains(bad))
                    return -1;

            int score;
            if (n.Contains("working") || n.Contains("generating") || n.Contains("dispensing")
                || n.Contains("channeling") || n.Contains("emitting") || n.Contains("charged")
                || n.Contains("running") || n.Contains("deployed"))
                score = 100;
            else if (n == "on" || n.StartsWith("on_") || n.EndsWith("_on") || n.Contains("powered"))
                score = 50;
            else if (n.Contains("idle"))
                score = 20;
            else
                return 0;

            if (n.Contains("loop")) score += 10;
            if (n.EndsWith("_pre") || n.EndsWith("_pst")) score -= 8;
            return score;
        }

        private static Texture2D TrimToOpaqueBBox(Texture2D tex, out int minX, out int minY, out int cw, out int ch)
        {
            int w = tex.width, h = tex.height;
            Color[] px = tex.GetPixels();

            cw = 0; ch = 0;
            if (!ImageCrop.FindOpaqueBBox(px, w, h, out minX, out minY, out int maxX, out int maxY))
                return null;

            cw = maxX - minX + 1;
            ch = maxY - minY + 1;
            Color[] outPx = ImageCrop.CropPixels(px, w, minX, minY, cw, ch);

            Texture2D result = new Texture2D(cw, ch);
            result.SetPixels(outPx);
            result.Apply();
            return result;
        }

        private static UiImageRect ComputeRect(int minX, int minY, int cw, int ch, int cellW, int cellH) =>
            UiImageRect.FromCrop(minX, minY, cw, ch,
                Mathf.CeilToInt(cellW * BuildingKanimRenderer.PixelsPerCell + 2 * BuildingKanimRenderer.PaddingPx),
                Mathf.CeilToInt(cellH * BuildingKanimRenderer.PixelsPerCell + 2 * BuildingKanimRenderer.PaddingPx),
                cellW, cellH, BuildingKanimRenderer.PixelsPerCell);
    }
}
