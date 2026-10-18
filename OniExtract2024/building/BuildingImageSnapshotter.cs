// Camera-snapshot rendering adapted from the GPL-2.0 project Sgt_Imalas-Oni-Mods
// (AnimExportTool/AETE_KbacSnapShotter.cs), itself courtesy of Aki
// (https://github.com/aki-art/ONI-Mods). Adapted for OniExtract2024 to render
// building icons at 200 px/cell and trim to the opaque bounding box.
//
// Difference from ConnectionSpriteSnapshotter: one shot per building (build-complete
// idle state), output cropped to the tightest opaque bbox rather than a cell-centred
// square, and written to ui_image/ (same path as the game-data export's atlas icons so the
// hi-res file lands exactly on top of the low-res one).

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using OniExtract2024.connection;
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

        // Fallback probe order if the build's anim list can't be enumerated.
        private static readonly string[] ActiveAnimFallback =
        {
            "working_loop", "generating_loop", "channeling_loop", "working", "generating",
            "channeling", "dispensing", "on_loop", "on", "idle_loop", "idle",
        };

        // How much margin a side gains when the art reaches it, and how many times, before the
        // render is reported clipped. The authored extent sized the canvas already; this
        // catches what it does not cover, such as child controllers (meters and the like) and
        // anims whose extent is garbage.
        private const float GrowCells = 4f;
        private const int MaxGrowAttempts = 3;

        // cellW/cellH override the footprint derived from the Building component. Terrain
        // features (geysers, vents, volcanoes) are not BuildingDefs and carry no Building,
        // so the caller passes their KBoxCollider2D extents; pass -1 to derive as usual.
        // `report` receives how the image was made, keyed like `rects`; `render` says whether
        // this object is a real spawn or an art-only stand-in.
        public void StartExport(string outputDir, IDictionary<string, UiImageRect> rects = null,
            int cellW = -1, int cellH = -1,
            IDictionary<string, BuildingImageReportEntry> report = null, string render = "spawned")
        {
            StartCoroutine(DoExport(outputDir, rects, cellW, cellH, report, render));
        }

        private IEnumerator DoExport(string outputDir, IDictionary<string, UiImageRect> rects,
            int cellW, int cellH, IDictionary<string, BuildingImageReportEntry> report, string render)
        {
            yield return new WaitForSecondsRealtime(0.1f);

            try
            {
                var kpid = GetComponent<KPrefabID>();
                var kbac = GetComponent<KBatchedAnimController>();

                if (kpid != null && kbac != null)
                {
                    string pose = PoseActive(kbac, kpid.PrefabTag.Name, cellW, cellH);
                    var entry = RenderAndWrite(gameObject, outputDir, rects, cellW, cellH);
                    if (entry != null && report != null)
                    {
                        entry.pose = pose;
                        entry.render = render;
                        report[kpid.PrefabTag.Name] = entry;
                    }
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
        //
        // The canvas is sized from the posed anim's authored extent (see RenderCanvas). If the
        // art still reaches an edge, that side is widened and the building drawn again; art
        // that reaches an edge even then is reported clipped, both in Player.log and in the
        // returned entry. Returns how the image was made, with `pose` and `render` left for
        // the caller, or null when nothing was written.
        //
        // overrideCellW/H supply the footprint for objects with no Building component (terrain
        // features); pass -1 to derive it from the Building as usual.
        public static BuildingImageReportEntry RenderAndWrite(GameObject go, string outputDir,
            IDictionary<string, UiImageRect> rects = null,
            int overrideCellW = -1, int overrideCellH = -1)
        {
            var kpid = go.GetComponent<KPrefabID>();
            if (kpid == null) return null;

            SelectTool.Instance.Select(null);

            var building = go.GetComponent<Building>();
            int cellW = overrideCellW > 0 ? overrideCellW : (building != null ? building.Def.WidthInCells : 1);
            int cellH = overrideCellH > 0 ? overrideCellH : (building != null ? building.Def.HeightInCells : 1);

            var rootKbac = go.GetComponent<KBatchedAnimController>();
            Vector3 footprint = BuildingKanimRenderer.FootprintBottomLeft(go.transform.GetPosition(), cellW);
            bool hasExtent = TryGetAuthoredExtent(rootKbac, footprint,
                out float originX, out float originY, out float halfW, out float halfH);
            RenderCanvas canvas = hasExtent
                ? RenderCanvas.ForArt(cellW, cellH, BuildingKanimRenderer.PixelsPerCell, originX, originY, halfW, halfH)
                : RenderCanvas.Default(cellW, cellH, BuildingKanimRenderer.PixelsPerCell);

            Color32[] px = null;
            int minX = 0, minY = 0, maxX = -1, maxY = -1;
            CanvasEdge touched = CanvasEdge.None, grown = CanvasEdge.None;
            for (int attempt = 0; ; attempt++)
            {
                px = RenderPixels(go, canvas, footprint);
                if (px == null) return null;
                if (!ImageCrop.FindOpaqueBBox(px, canvas.Width, canvas.Height, out minX, out minY, out maxX, out maxY))
                    return null;
                touched = canvas.TouchedBy(minX, minY, maxX - minX + 1, maxY - minY + 1);
                if (touched == CanvasEdge.None || attempt >= MaxGrowAttempts || canvas.AtMaximum(touched))
                    break;
                grown |= touched;
                canvas = canvas.Grow(touched, GrowCells);
            }

            int cw = maxX - minX + 1, ch = maxY - minY + 1;
            var cropped = new Texture2D(cw, ch);
            cropped.SetPixels32(ImageCrop.CropPixels(px, canvas.Width, minX, minY, cw, ch));
            cropped.Apply();

            Directory.CreateDirectory(outputDir);
            string fileName = ExportUISprite.GetFormatedUIImageFileName(kpid);
            string filePath = Path.Combine(outputDir, fileName + ".png");
            File.WriteAllBytes(filePath, cropped.EncodeToPNG());
            UnityEngine.Object.Destroy(cropped);
            Debug.Log("OniExtract: building image -> " + filePath);
            if (touched != CanvasEdge.None)
                Debug.LogWarning("OniExtract: building image for " + kpid.PrefabTag.Name + " is clipped: its art reaches the "
                    + string.Join(", ", RenderCanvas.EdgeNames(touched)) + " edge of a " + canvas.Width + "x" + canvas.Height + " canvas.");

            if (rects != null)
                rects[kpid.PrefabTag.Name] = canvas.RectOf(minX, minY, cw, ch);

            return new BuildingImageReportEntry
            {
                anim = rootKbac != null && rootKbac.CurrentAnim != null ? rootKbac.CurrentAnim.name : null,
                frame = rootKbac != null ? Mathf.Max(0, rootKbac.currentFrame) : 0,
                frameCount = rootKbac != null ? rootKbac.GetCurrentNumFrames() : 0,
                canvasPx = new PixelSize { w = canvas.Width, h = canvas.Height },
                animBBoxCells = hasExtent
                    ? new UiImageRect { x = originX - halfW, y = originY - halfH, w = 2f * halfW, h = 2f * halfH }
                    : (UiImageRect?)null,
                clipped = touched != CanvasEdge.None,
                clippedEdges = touched != CanvasEdge.None ? RenderCanvas.EdgeNames(touched) : null,
                canvasGrownEdges = grown != CanvasEdge.None ? RenderCanvas.EdgeNames(grown) : null,
            };
        }

        // The placeholder symbol a building's art carries where a meter attaches. The game's
        // MeterController hides it on the building's controller whenever it builds a meter
        // (every constructor does, by this name unless the building names another), so it is
        // never seen in game. Art-only renders build no meters and showed it, as a bar on
        // WallToilet and others.
        private const string MeterTargetSymbol = "meter_target";

        // Puts a posed object's controllers into the state the game would draw them in: the
        // meter placeholder hidden, and every hidden symbol written back to the controller's
        // GPU data, so nothing the pose step did can leave a hidden symbol showing. Called by
        // every render path (sweep, touch-up, inspector preview) right before drawing.
        internal static void PrepareForSnapshot(IEnumerable<KBatchedAnimController> kbacs)
        {
            foreach (var kbac in kbacs)
            {
                if (kbac == null || !kbac.batchGroupID.IsValid || kbac.batchGroupID == KAnimBatchManager.NO_BATCH)
                    continue;
                kbac.SetSymbolVisiblity(MeterTargetSymbol, false);
                kbac.UpdateAllHiddenSymbols();
            }
        }

        // Draws every controller on the object into a canvas laid out as `canvas` and returns
        // the pixels, bottom row first.
        private static Color32[] RenderPixels(GameObject go, RenderCanvas canvas, Vector3 footprint)
        {
            var renderer = new BuildingKanimRenderer();
            renderer.Init(canvas, footprint);

            Texture2D raw = null;
            try
            {
                CameraController.Instance.baseCamera.enabled = false;
                var kbacs = go.GetComponentsInChildren<KBatchedAnimController>()
                    .OrderBy(k => k.transform.position.z).ToList();
                PrepareForSnapshot(kbacs);
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
            if (raw == null) return null;
            Color32[] px = raw.GetPixels32();
            UnityEngine.Object.Destroy(raw);
            return px;
        }

        // The current anim's authored extent: KAnim.Anim.unScaledSize is the largest distance
        // from the anim origin, over every frame, that its frame bounds reach (KGlobalAnimParser
        // computes it at load), in kanim pixels. The controller scales those into world units
        // and draws them about PositionIncludingOffset. Origin and half-extents come back in
        // cells, the origin measured from the footprint's bottom-left. False when the anim has
        // no usable extent: see RenderCanvas.IsUsableHalfExtent.
        private static bool TryGetAuthoredExtent(KBatchedAnimController kbac, Vector3 footprint,
            out float originX, out float originY, out float halfW, out float halfH)
        {
            originX = originY = halfW = halfH = 0f;
            KAnim.Anim anim = kbac != null ? kbac.CurrentAnim : null;
            if (anim == null) return false;

            Vector3 origin = kbac.PositionIncludingOffset;
            originX = origin.x - footprint.x;
            originY = origin.y - footprint.y;
            HalfExtent(kbac, anim, out halfW, out halfH);
            return RenderCanvas.IsUsableHalfExtent(halfW, halfH);
        }

        // An anim's authored half-extent in world units (cells) as this controller draws it.
        private static void HalfExtent(KBatchedAnimController kbac, KAnim.Anim anim, out float halfW, out float halfH)
        {
            Vector3 scale = kbac.transform.lossyScale;
            halfW = anim.unScaledSize.x * kbac.animScale * Mathf.Abs(kbac.animWidth * scale.x);
            halfH = anim.unScaledSize.y * kbac.animScale * Mathf.Abs(kbac.animHeight * scale.y);
            // A rotated controller can reach the box's corner distance in any direction.
            if (kbac.Rotation != 0f)
                halfW = halfH = Mathf.Sqrt(halfW * halfW + halfH * halfH);
        }

        // Pose the building's main kanim in the most "active" state. Checks
        // BuildingPoseOverrides first; falls back to ChooseActiveAnim.
        // ONLY the root controller is posed — driving child controllers stamps
        // a full extra copy of the building at each child's transform offset.
        // cellW/cellH as for StartExport. Returns where the pose came from, for the report.
        private string PoseActive(KBatchedAnimController rootKbac, string prefabId, int cellW, int cellH)
        {
            if (BuildingPoseOverrides.TryGet(prefabId, out var pose))
            {
                PoseController(rootKbac, pose.Anim, pose.Frame);
                return "override";
            }
            string anim = ChooseActiveAnim(rootKbac, cellW, cellH);
            PoseController(rootKbac, anim, -1);
            return anim != null ? "auto" : "default";
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

        // Returns the active animation to pose the controller in (ActivePose.Choose: the most
        // active state whose authored extent is close to the footprint's, so effects art does
        // not decide the pose), or null to leave the spawned default untouched. cellW/cellH
        // give the footprint; pass -1 to read it from the Building. Exposed internal so the
        // inspector can seed its anim picker with the same default the exporter would choose.
        internal static string ChooseActiveAnim(KBatchedAnimController kbac, int cellW = -1, int cellH = -1)
        {
            List<string> names = GetAnimNames(kbac);
            if (names == null || names.Count == 0)
                return ProbeFallback(kbac);

            var building = kbac.GetComponent<Building>();
            if (cellW <= 0) cellW = building != null ? building.Def.WidthInCells : 1;
            if (cellH <= 0) cellH = building != null ? building.Def.HeightInCells : 1;

            var candidates = new List<AnimCandidate>(names.Count);
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name) || !kbac.HasAnimation(name))
                    continue;
                KAnim.Anim anim = kbac.GetAnim(name);
                float halfW = float.NaN, halfH = float.NaN;
                if (anim != null)
                    HalfExtent(kbac, anim, out halfW, out halfH);
                candidates.Add(new AnimCandidate(name, halfW, halfH));
            }
            return ActivePose.Choose(candidates, cellW, cellH) ?? ProbeFallback(kbac);
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
    }
}
