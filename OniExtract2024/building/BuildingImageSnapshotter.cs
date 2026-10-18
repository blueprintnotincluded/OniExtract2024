// Camera-snapshot rendering adapted from the GPL-2.0 project Sgt_Imalas-Oni-Mods
// (AnimExportTool/AETE_KbacSnapShotter.cs), itself courtesy of Aki
// (https://github.com/aki-art/ONI-Mods). Adapted for OniExtract2024 to render
// building icons at 200 px/cell and trim to the opaque bounding box.
//
// Difference from ConnectionSpriteSnapshotter: one shot per building (build-complete
// idle state), output cropped to the tightest opaque bbox rather than a cell-centred
// square, and written to ui_image/ (same path as the game-data export's atlas icons so the
// hi-res file lands exactly on top of the low-res one).

using System;
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
        // render is reported clipped. The drawn box sized the canvas already; this
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
                    string pose = PoseActive(kbac, kpid.PrefabTag.Name, cellW, cellH, out var considered);
                    var entry = RenderAndWrite(gameObject, outputDir, rects, cellW, cellH,
                        artOnly: render == "artOnly");
                    if (entry != null && report != null)
                    {
                        entry.pose = pose;
                        entry.render = render;
                        entry.candidates = considered;
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
        // The canvas is sized from the box the posed anim's frames cover (see RenderCanvas and
        // TryGetAnimBox). If the art still reaches an edge, that side is widened and the
        // building drawn again; art that reaches an edge even then is reported clipped, both in
        // Player.log and in the returned entry. Returns how the image was made, with `pose` and
        // `render` left for the caller, or null when nothing was written.
        //
        // overrideCellW/H supply the footprint for objects with no Building component (terrain
        // features); pass -1 to derive it from the Building as usual. artOnly marks a stand-in
        // that never ran OnSpawn (see PrepareForSnapshot).
        public static BuildingImageReportEntry RenderAndWrite(GameObject go, string outputDir,
            IDictionary<string, UiImageRect> rects = null,
            int overrideCellW = -1, int overrideCellH = -1, bool artOnly = false)
        {
            var kpid = go.GetComponent<KPrefabID>();
            if (kpid == null) return null;

            SelectTool.Instance.Select(null);

            var building = go.GetComponent<Building>();
            int cellW = overrideCellW > 0 ? overrideCellW : (building != null ? building.Def.WidthInCells : 1);
            int cellH = overrideCellH > 0 ? overrideCellH : (building != null ? building.Def.HeightInCells : 1);

            var rootKbac = go.GetComponent<KBatchedAnimController>();
            Vector3 footprint = BuildingKanimRenderer.FootprintBottomLeft(go.transform.GetPosition(), cellW);
            bool hasBox = TryGetDrawnBox(go, footprint, out UiImageRect animBox);
            RenderCanvas canvas = hasBox
                ? RenderCanvas.ForBox(cellW, cellH, BuildingKanimRenderer.PixelsPerCell, animBox)
                : RenderCanvas.Default(cellW, cellH, BuildingKanimRenderer.PixelsPerCell);

            Color32[] px = null;
            int minX = 0, minY = 0, maxX = -1, maxY = -1;
            CanvasEdge touched = CanvasEdge.None, grown = CanvasEdge.None;
            for (int attempt = 0; ; attempt++)
            {
                px = RenderPixels(go, canvas, footprint, artOnly);
                if (px == null) return null;
                if (!ImageCrop.FindOpaqueBBox(px, canvas.Width, canvas.Height, out minX, out minY, out maxX, out maxY))
                    return null;
                touched = canvas.TouchedBy(minX, minY, maxX - minX + 1, maxY - minY + 1);
                if (touched == CanvasEdge.None || attempt >= MaxGrowAttempts || canvas.AtMaximum(touched))
                    break;
                RenderCanvas wider = canvas.Grow(touched, GrowCells);
                if (wider.Pixels > RenderCanvas.MaxPixels)
                    break;
                grown |= touched;
                canvas = wider;
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
                animBBoxCells = hasBox
                    ? new UiImageRect { x = Round(animBox.x), y = Round(animBox.y), w = Round(animBox.w), h = Round(animBox.h) }
                    : (UiImageRect?)null,
                clipped = touched != CanvasEdge.None,
                clippedEdges = touched != CanvasEdge.None ? RenderCanvas.EdgeNames(touched) : null,
                canvasGrownEdges = grown != CanvasEdge.None ? RenderCanvas.EdgeNames(grown) : null,
            };
        }

        // The placeholder symbol a building's art carries where a meter attaches. The game's
        // MeterController hides it on the building's controller whenever it builds a meter, in
        // OnSpawn (every constructor does, by this name unless the building names another), so
        // it is not seen in game. Art-only renders never run OnSpawn and showed it, as a bar on
        // WallToilet and others.
        private const string MeterTargetSymbol = "meter_target";

        // Puts a posed object's controllers into the state the game would draw them in: every
        // hidden symbol written back to the controller's GPU data, so nothing the pose step did
        // can leave a hidden symbol showing, and, on an art-only stand-in, the meter placeholder
        // hidden as OnSpawn would have. A real spawn keeps the visibility its own OnSpawn chose:
        // Polymerizer shows meter_target again after building its meter. Called by every render
        // path (sweep, touch-up, inspector preview) right before drawing.
        internal static void PrepareForSnapshot(IEnumerable<KBatchedAnimController> kbacs, bool artOnly)
        {
            foreach (var kbac in kbacs)
            {
                if (kbac == null || !kbac.batchGroupID.IsValid || kbac.batchGroupID == KAnimBatchManager.NO_BATCH)
                    continue;
                if (artOnly)
                    kbac.SetSymbolVisiblity(MeterTargetSymbol, false);
                kbac.UpdateAllHiddenSymbols();
            }
        }

        // 4 decimals, as for the rects: finer than a pixel, and a clean diff between runs.
        private static float Round(float v) => Mathf.Round(v * 10000f) / 10000f;

        // Draws every controller on the object into a canvas laid out as `canvas` and returns
        // the pixels, bottom row first.
        private static Color32[] RenderPixels(GameObject go, RenderCanvas canvas, Vector3 footprint, bool artOnly)
        {
            var renderer = new BuildingKanimRenderer();
            renderer.Init(canvas, footprint);

            Texture2D raw = null;
            try
            {
                CameraController.Instance.baseCamera.enabled = false;
                var kbacs = go.GetComponentsInChildren<KBatchedAnimController>()
                    .OrderBy(k => k.transform.position.z).ToList();
                PrepareForSnapshot(kbacs, artOnly);
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

        // The box every controller the render draws covers in its current anim: the root and its
        // children (meters, foreground layers, arms, the feeder's food), in uiImageRect's space.
        // Measuring the root alone left the children's art outside the box, which sized the
        // canvas short (FishFeeder grew on every run) and buried real surprises in Test-Export's
        // containment check under meters and arms. False when nothing can be measured.
        private static bool TryGetDrawnBox(GameObject go, Vector3 footprint, out UiImageRect box)
        {
            box = default(UiImageRect);
            bool any = false;
            foreach (var kbac in go.GetComponentsInChildren<KBatchedAnimController>())
            {
                if (!TryGetFootprintBox(kbac, kbac.CurrentAnim, footprint, out UiImageRect b))
                    continue;
                if (!any)
                {
                    box = b;
                    any = true;
                    continue;
                }
                float x0 = Mathf.Min(box.x, b.x), y0 = Mathf.Min(box.y, b.y);
                float x1 = Mathf.Max(box.x + box.w, b.x + b.w), y1 = Mathf.Max(box.y + box.h, b.y + b.h);
                box = new UiImageRect { x = x0, y = y0, w = x1 - x0, h = y1 - y0 };
            }
            return any && RenderCanvas.IsUsableBox(box);
        }

        // The box `anim` covers as this controller draws it, in uiImageRect's space: cells from
        // the footprint's bottom-left. False when there is no usable box (see TryGetAnimBox).
        private static bool TryGetFootprintBox(KBatchedAnimController kbac, KAnim.Anim anim, Vector3 footprint,
            out UiImageRect box)
        {
            box = default(UiImageRect);
            if (!TryGetAnimBox(kbac, anim, out float minX, out float maxX, out float minY, out float maxY))
                return false;
            Vector3 origin = kbac.PositionIncludingOffset;
            box = new UiImageRect
            {
                x = origin.x - footprint.x + minX,
                y = origin.y - footprint.y + minY,
                w = maxX - minX,
                h = maxY - minY,
            };
            return RenderCanvas.IsUsableBox(box);
        }

        // The box every frame of `anim` covers as this controller draws it, in cells about the
        // anim origin (PositionIncludingOffset), +y up. Computed from the frames the way the GPU
        // places their vertices: each visible element's symbol-frame box through the element's
        // transform, then the controller's scale and flips.
        //
        // The game also stores an authored extent, KAnim.Anim.unScaledSize, but it is a
        // half-extent (symmetric about the origin, so as deep below a floor building as it is
        // tall) and some kanims carry garbage there: NaN in every anim of the Wall Pumps mod,
        // 536870 cells in wind_tunnel's working anims. It is the fallback, used only when no
        // element resolves. Offline, the frame box matched the authored extent of the base game's
        // building anims to within about 0.1 cell wherever the latter was sane.
        private static bool TryGetAnimBox(KBatchedAnimController kbac, KAnim.Anim anim,
            out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;
            if (kbac == null || anim == null) return false;

            Vector3 scale = kbac.transform.lossyScale;
            float sx = kbac.animScale * kbac.animWidth * scale.x * (kbac.FlipX ? -1f : 1f);
            // Kanim pixels run y-down; the controller flips them (GetTransformMatrix).
            float sy = -kbac.animScale * kbac.animHeight * scale.y * (kbac.FlipY ? -1f : 1f);

            bool any = false;
            try
            {
                KBatchGroupData data = anim.animFile != null
                    ? KAnimBatchManager.Instance().GetBatchGroupData(anim.animFile.animBatchTag)
                    : null;
                int elementCount = data != null ? data.frameElements.Count : 0;
                for (int f = 0; data != null && f < anim.numFrames; f++)
                {
                    if (!data.TryGetFrame(anim.firstFrameIdx + f, out KAnim.Anim.Frame frame))
                        continue;
                    int end = Mathf.Min(frame.firstElementIdx + frame.numElements, elementCount);
                    for (int i = frame.firstElementIdx; i < end; i++)
                    {
                        KAnim.Anim.FrameElement element = data.GetFrameElement(i);
                        if (element.multAlpha <= 0f)
                            continue;
                        KAnim.Build.Symbol symbol = OwnSymbol(kbac, data, element.symbol);
                        if (symbol == null || symbol.frameLookup == null)
                            continue;
                        // A layered symbol is hidden on this controller because its layer
                        // controller draws it (KAnimLayering), so it is still in the picture.
                        if (!kbac.GetSymbolVisiblity(element.symbol) && (symbol.flags & LayeredSymbolFlags) == 0)
                            continue;
                        KAnim.Build.SymbolFrameInstance symbolFrame = symbol.GetFrame(element.frame);
                        if (symbolFrame.symbolIdx < 0)
                            continue;
                        Vector2 lo = symbolFrame.bboxMin, hi = symbolFrame.bboxMax;
                        Include(element.transform, lo.x, lo.y, sx, sy, ref minX, ref maxX, ref minY, ref maxY);
                        Include(element.transform, hi.x, lo.y, sx, sy, ref minX, ref maxX, ref minY, ref maxY);
                        Include(element.transform, lo.x, hi.y, sx, sy, ref minX, ref maxX, ref minY, ref maxY);
                        Include(element.transform, hi.x, hi.y, sx, sy, ref minX, ref maxX, ref minY, ref maxY);
                        any = true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("OniExtract: could not measure " + anim.name + "'s frames: " + e.Message);
                any = false;
            }

            if (!any)
            {
                minX = minY = float.MaxValue;
                maxX = maxY = float.MinValue;
                float halfW = Mathf.Abs(anim.unScaledSize.x * sx), halfH = Mathf.Abs(anim.unScaledSize.y * sy);
                if (!RenderCanvas.IsUsableHalfExtent(halfW, halfH))
                    return false;
                minX = -halfW; maxX = halfW; minY = -halfH; maxY = halfH;
            }

            // A rotated controller can reach its box's farthest corner in any direction.
            if (kbac.Rotation != 0f)
            {
                float r = Mathf.Sqrt(Mathf.Max(minX * minX, maxX * maxX) + Mathf.Max(minY * minY, maxY * maxY));
                minX = minY = -r;
                maxX = maxY = r;
            }
            return true;
        }

        // Symbols a controller with a foreground (or shadow) layer hands to a child controller:
        // KAnimLayering hides them on the parent with SetSymbolVisiblity and the child draws
        // them. A building with a ForegroundLayer, such as the Wall Pumps mod's, can have its
        // whole body there, so measuring only what the parent shows missed it.
        private const int LayeredSymbolFlags = (int)(KAnim.SymbolFlags.FG | KAnim.SymbolFlags.SH);

        // The symbol this controller draws for `hash`: its own build's. A batch group can hold
        // several builds, and the group's own lookup (KBatchGroupData.GetSymbol) returns the
        // first build that has the name. The group's lookup is the last resort, for a symbol
        // none of the controller's files carries.
        private static KAnim.Build.Symbol OwnSymbol(KBatchedAnimController kbac, KBatchGroupData data, KAnimHashedString hash)
        {
            KAnim.Build.Symbol symbol = kbac.curBuild != null ? kbac.curBuild.GetSymbol(hash) : null;
            if (symbol != null) return symbol;
            if (kbac.AnimFiles != null)
            {
                foreach (KAnimFile file in kbac.AnimFiles)
                {
                    KAnim.Build build = file != null && file.GetData() != null ? file.GetData().build : null;
                    symbol = build != null ? build.GetSymbol(hash) : null;
                    if (symbol != null) return symbol;
                }
            }
            return data.GetSymbol(hash);
        }

        private static void Include(Matrix2x3 m, float x, float y, float sx, float sy,
            ref float minX, ref float maxX, ref float minY, ref float maxY)
        {
            Vector3 p = m.MultiplyPoint(new Vector3(x, y, 0f));
            float wx = p.x * sx, wy = p.y * sy;
            if (wx < minX) minX = wx;
            if (wx > maxX) maxX = wx;
            if (wy < minY) minY = wy;
            if (wy > maxY) maxY = wy;
        }

        // Pose the building's main kanim in the most "active" state. Checks
        // BuildingPoseOverrides first; falls back to ChooseActiveAnim.
        // ONLY the root controller is posed — driving child controllers stamps
        // a full extra copy of the building at each child's transform offset.
        // cellW/cellH as for StartExport. Returns where the pose came from, and for an auto
        // pose what was weighed, for the report.
        private string PoseActive(KBatchedAnimController rootKbac, string prefabId, int cellW, int cellH,
            out SortedDictionary<string, PoseCandidate> considered)
        {
            considered = null;
            if (BuildingPoseOverrides.TryGet(prefabId, out var pose))
            {
                PoseController(rootKbac, pose.Anim, pose.Frame);
                return "override";
            }
            string anim = ChooseActiveAnim(rootKbac, cellW, cellH, out considered);
            PoseController(rootKbac, anim, -1);
            if (anim == null) considered = null;
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
        // active state whose measured extent is close to the footprint's, so effects art does
        // not decide the pose), or null to leave the spawned default untouched. cellW/cellH
        // give the footprint; pass -1 to read it from the Building. Exposed internal so the
        // inspector can seed its anim picker with the same default the exporter would choose.
        internal static string ChooseActiveAnim(KBatchedAnimController kbac, int cellW = -1, int cellH = -1) =>
            ChooseActiveAnim(kbac, cellW, cellH, out _);

        // As above; `considered` gets every anim the scorer accepted, for the report.
        private static string ChooseActiveAnim(KBatchedAnimController kbac, int cellW, int cellH,
            out SortedDictionary<string, PoseCandidate> considered)
        {
            considered = null;
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
                // ActivePose compares half-extents about the anim origin, the measure the game
                // stores as unScaledSize; take them from the frame box.
                KAnim.Anim anim = kbac.GetAnim(name);
                float halfW = float.NaN, halfH = float.NaN;
                if (TryGetAnimBox(kbac, anim, out float minX, out float maxX, out float minY, out float maxY))
                {
                    halfW = Mathf.Max(-minX, maxX);
                    halfH = Mathf.Max(-minY, maxY);
                }
                candidates.Add(new AnimCandidate(name, halfW, halfH));
            }

            considered = new SortedDictionary<string, PoseCandidate>(StringComparer.Ordinal);
            foreach (var c in candidates)
            {
                int score = ActivePose.Score(c.name);
                if (score <= 0) continue;
                bool measured = !float.IsNaN(c.halfW) && !float.IsNaN(c.halfH);
                considered[c.name] = new PoseCandidate
                {
                    score = score,
                    halfW = measured ? Round(c.halfW) : (float?)null,
                    halfH = measured ? Round(c.halfH) : (float?)null,
                };
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
