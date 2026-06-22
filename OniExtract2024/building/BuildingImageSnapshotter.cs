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

        // Render geometry set during Init, needed to map the crop back to cell coords.
        private int cellW;
        private int cellH;

        public void StartExport(string outputDir, IDictionary<string, UiImageRect> rects = null)
        {
            StartCoroutine(DoExport(outputDir, rects));
        }

        private IEnumerator DoExport(string outputDir, IDictionary<string, UiImageRect> rects)
        {
            yield return new WaitForSecondsRealtime(0.1f);

            var kpid = GetComponent<KPrefabID>();
            var kbac = GetComponent<KBatchedAnimController>();

            if (kpid != null && kbac != null)
            {
                PoseActive(kbac, kpid.PrefabTag.Name);

                string fileName = ExportUISprite.GetFormatedUIImageFileName(kpid);
                Texture2D raw = SnapShot();
                if (raw != null)
                {
                    Texture2D cropped = TrimToOpaqueBBox(raw, out int minX, out int minY, out int cw, out int ch);
                    Destroy(raw);
                    if (cropped != null)
                    {
                        Directory.CreateDirectory(outputDir);
                        string filePath = Path.Combine(outputDir, fileName + ".png");
                        File.WriteAllBytes(filePath, cropped.EncodeToPNG());
                        Destroy(cropped);
                        Debug.Log("OniExtract: building image -> " + filePath);

                        if (rects != null)
                            rects[kpid.PrefabTag.Name] = ComputeRect(minX, minY, cw, ch);
                    }
                }
            }

            Util.KDestroyGameObject(gameObject);
        }

        private Texture2D SnapShot()
        {
            SelectTool.Instance.Select(null);

            var building = GetComponent<Building>();
            cellW = building != null ? building.Def.WidthInCells : 1;
            cellH = building != null ? building.Def.HeightInCells : 1;

            var renderer = new BuildingKanimRenderer();
            renderer.Init(cellW, cellH, transform.GetPosition());

            CameraController.Instance.baseCamera.enabled = false;
            var kbacs = gameObject.GetComponentsInChildren<KBatchedAnimController>()
                .OrderBy(k => k.transform.position.z);
            renderer.Render(kbacs);
            CameraController.Instance.baseCamera.enabled = true;

            Texture2D tex = renderer.ReadPixels();
            // Release the RT immediately — not doing so leaks graphics memory at a rate
            // that crashes the game before the export sweep finishes.
            renderer.Cleanup();
            return tex;
        }

        // Pose the building's main kanim in the most "active" state. Checks
        // BuildingPoseOverrides first; falls back to the auto-scorer unchanged.
        // ONLY the root controller is posed — driving child controllers stamps
        // a full extra copy of the building at each child's transform offset.
        private void PoseActive(KBatchedAnimController rootKbac, string prefabId)
        {
            if (BuildingPoseOverrides.TryGet(prefabId, out var pose))
            {
                rootKbac.Play(pose.Anim, KAnim.PlayMode.Paused);
                int numFrames = rootKbac.GetCurrentNumFrames();
                rootKbac.SetPositionPercent(BuildingPoseOverrides.PercentForFrame(pose.Frame, numFrames > 0 ? numFrames : 1));
            }
            else
            {
                string anim = ChooseActiveAnim(rootKbac);
                if (anim == null) return;
                rootKbac.Play(anim, KAnim.PlayMode.Paused);
                rootKbac.SetPositionPercent(PoseFramePercent);
            }
        }

        // Returns the highest-scoring active animation the controller actually has, or
        // null to leave the spawned default untouched. Exposed internal so the inspector
        // can seed its anim picker with the same default the exporter would choose.
        internal static string ChooseActiveAnim(KBatchedAnimController kbac)
        {
            List<HashedString> names = GetAnimNames(kbac);
            if (names == null || names.Count == 0)
                return ProbeFallback(kbac);

            string best = null;
            int bestScore = 0;
            foreach (HashedString hashed in names)
            {
                string name = hashed.ToString();
                if (string.IsNullOrEmpty(name) || !kbac.HasAnimation(hashed))
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

        // Returns all animation names for the build via the group file. Exposed internal
        // so the inspector can populate its animation picker.
        internal static List<HashedString> GetAnimNames(KBatchedAnimController kbac)
        {
            var group = KAnimGroupFile.GetGroup(kbac.GetBuildHash());
            return group?.animNames;
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

        private UiImageRect ComputeRect(int minX, int minY, int cw, int ch) =>
            UiImageRect.FromCrop(minX, minY, cw, ch,
                Mathf.CeilToInt(cellW * BuildingKanimRenderer.PixelsPerCell + 2 * BuildingKanimRenderer.PaddingPx),
                Mathf.CeilToInt(cellH * BuildingKanimRenderer.PixelsPerCell + 2 * BuildingKanimRenderer.PaddingPx),
                cellW, cellH, BuildingKanimRenderer.PixelsPerCell);
    }
}
