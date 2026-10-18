using System.Collections.Generic;
using OniExtract2024.connection;

namespace OniExtract2024.building
{
    /// <summary>
    /// How one building image was produced: one entry per prefab in
    /// <c>export/building_image_report.json</c>, written by the building-image sweep beside
    /// <c>ui_image_rects.json</c>. Diagnostic, not read by the website: it says why an image
    /// changed between two exports ("pose went from working_loop to on") without a pixel diff,
    /// and lets Test-Export check clipping and containment without decoding images.
    /// Fields that do not apply are null and omitted from the file.
    /// </summary>
    public class BuildingImageReportEntry
    {
        /// <summary>The anim the root controller showed; null if it had none.</summary>
        public string anim;

        /// <summary>The 0-based frame of <see cref="anim"/> rendered, and how many it has.</summary>
        public int frame;
        public int frameCount;

        /// <summary>
        /// Where the pose came from: <c>override</c> (pose_overrides.json or
        /// BuildingPoseOverrides), <c>auto</c> (BuildingImageSnapshotter.ChooseActiveAnim) or
        /// <c>default</c> (nothing to choose from, so the anim the object spawned with).
        /// </summary>
        public string pose;

        /// <summary>
        /// <c>spawned</c> (a real instance of the building or terrain feature) or
        /// <c>artOnly</c> (its kanim on an effect entity; see ExportBuildingImages.CreateArtOnly).
        /// </summary>
        public string render;

        /// <summary>The size of the canvas the image was cropped from.</summary>
        public PixelSize canvasPx;

        /// <summary>
        /// The box the drawn controllers' anims cover over every frame, in cells, in
        /// uiImageRect's space (footprint-relative, bottom-left origin, +y up): measured from
        /// the frames' geometry for the root and its children, falling back to the game's
        /// authored half-extent for a controller where nothing resolves. Null when nothing gives
        /// a usable box.
        /// </summary>
        public UiImageRect? animBBoxCells;

        /// <summary>True when the opaque crop still touched the canvas edge after every retry.</summary>
        public bool clipped;

        /// <summary>The sides touched, when <see cref="clipped"/>.</summary>
        public string[] clippedEdges;

        /// <summary>
        /// Sides whose margin was widened and the building re-rendered because the art reached
        /// them; null when the first render fitted.
        /// </summary>
        public string[] canvasGrownEdges;

        /// <summary>
        /// For an <c>auto</c> pose: every anim the name scorer accepted, with what
        /// ActivePose.Choose weighed, so a pick can be explained from the file. Null otherwise.
        /// </summary>
        public SortedDictionary<string, PoseCandidate> candidates;
    }

    /// <summary>One anim ActivePose.Choose considered.</summary>
    public class PoseCandidate
    {
        /// <summary>ActivePose.Score of the name.</summary>
        public int score;

        /// <summary>
        /// Half-extents about the anim origin in cells, as measured from the frames; null when
        /// the anim could not be measured, which leaves it out of the choice.
        /// </summary>
        public float? halfW;
        public float? halfH;
    }
}
