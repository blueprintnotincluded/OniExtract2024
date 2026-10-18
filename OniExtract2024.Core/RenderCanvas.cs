using System;
using System.Collections.Generic;

namespace OniExtract2024.building
{
    /// <summary>
    /// Sides of a render canvas, for reporting which ones a crop touched.
    /// </summary>
    [Flags]
    public enum CanvasEdge
    {
        None = 0,
        Left = 1,
        Right = 2,
        Bottom = 4,
        Top = 8,
    }

    /// <summary>
    /// The pixel layout of one building render: the footprint at <see cref="PixelsPerCell"/>
    /// with a margin of empty canvas on each side. Pixels count from the bottom-left, as
    /// Texture2D.GetPixels returns them. Every margin is a whole number of pixels, so the
    /// footprint's edges always fall on pixel boundaries and a render is the same image
    /// whatever the margins are, only with more or less empty space around it.
    ///
    /// The margins come from the art: the box the posed anim's frames cover, over the whole anim
    /// (BuildingImageSnapshotter computes it from the frames' geometry). Before this the margin
    /// was a fixed two cells, and art past it was cut off with the crop edge landing on the
    /// canvas edge (SteamTurbine2's steam, VerticalWindTunnel, FishFeeder, MonumentMiddle).
    /// </summary>
    public struct RenderCanvas
    {
        /// <summary>The margin every render had before, and still the least it gets.</summary>
        public const float DefaultPaddingCells = 2f;

        /// <summary>Room past the anim's box, so the outermost pixels cannot sit on the edge.</summary>
        public const float MarginCells = 0.25f;

        /// <summary>
        /// Extents above this are not art but garbage. The authored extents some anims carry
        /// are: wind_tunnel's working anims report 536870 cells, MicrobeMusher's working_loop
        /// 37003, MiniFridge's working and every anim of the Wall Pumps mod NaN. An anim like
        /// that is sized with the default margin instead.
        /// </summary>
        public const float MaxAuthoredHalfExtentCells = 20f;

        /// <summary>No margin grows past this, so one bad anim cannot allocate a huge texture.</summary>
        public const float MaxPaddingCells = 16f;

        /// <summary>
        /// Growing stops before a canvas passes this many pixels (128 MB of Color32, as much
        /// again for the render texture). Authored extents in the shipped game need at most
        /// about 7 million.
        /// </summary>
        public const long MaxPixels = 32000000;

        public int cellW;
        public int cellH;
        public int PixelsPerCell;
        public int padLeft;
        public int padRight;
        public int padBottom;
        public int padTop;

        public int Width => cellW * PixelsPerCell + padLeft + padRight;
        public int Height => cellH * PixelsPerCell + padBottom + padTop;
        public long Pixels => (long)Width * Height;

        /// <summary>The canvas centre, in cells from the footprint's bottom-left corner.</summary>
        public float CentreX => (Width / 2f - padLeft) / PixelsPerCell;
        public float CentreY => (Height / 2f - padBottom) / PixelsPerCell;

        /// <summary>The fixed two-cell margin on every side: the layout every render used to have.</summary>
        public static RenderCanvas Default(int cellW, int cellH, int pixelsPerCell)
        {
            int pad = Px(DefaultPaddingCells, pixelsPerCell);
            return new RenderCanvas
            {
                cellW = cellW, cellH = cellH, PixelsPerCell = pixelsPerCell,
                padLeft = pad, padRight = pad, padBottom = pad, padTop = pad,
            };
        }

        /// <summary>
        /// True when an authored half-extent describes art: finite, positive and not one of the
        /// garbage values some anims carry (see <see cref="MaxAuthoredHalfExtentCells"/>).
        /// </summary>
        public static bool IsUsableHalfExtent(float halfW, float halfH) =>
            IsUsable(halfW) && IsUsable(halfH);

        private static bool IsUsable(float v) =>
            !float.IsNaN(v) && !float.IsInfinity(v) && v > 0f && v <= MaxAuthoredHalfExtentCells;

        /// <summary>
        /// True when <paramref name="box"/> (cells, footprint-relative, as
        /// <see cref="UiImageRect"/>) describes art: finite, non-empty, and no edge further than
        /// <see cref="MaxAuthoredHalfExtentCells"/> from the footprint.
        /// </summary>
        public static bool IsUsableBox(UiImageRect box) =>
            Finite(box.x) && Finite(box.y) && Finite(box.w) && Finite(box.h) && box.w > 0f && box.h > 0f
            && box.x >= -MaxAuthoredHalfExtentCells && box.y >= -MaxAuthoredHalfExtentCells
            && box.x + box.w <= MaxAuthoredHalfExtentCells * 2 && box.y + box.h <= MaxAuthoredHalfExtentCells * 2;

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>
        /// The layout for art that stays inside <paramref name="box"/>: cells, footprint-relative,
        /// bottom-left origin, as <see cref="UiImageRect"/>. Each side gets the default margin or
        /// what the art needs plus <see cref="MarginCells"/>, whichever is larger, so no canvas
        /// is smaller than it was. An unusable box yields <see cref="Default"/>.
        /// </summary>
        public static RenderCanvas ForBox(int cellW, int cellH, int pixelsPerCell, UiImageRect box)
        {
            var c = Default(cellW, cellH, pixelsPerCell);
            if (!IsUsableBox(box))
                return c;
            c.padLeft = Need(-box.x, pixelsPerCell);
            c.padRight = Need(box.x + box.w - cellW, pixelsPerCell);
            c.padBottom = Need(-box.y, pixelsPerCell);
            c.padTop = Need(box.y + box.h - cellH, pixelsPerCell);
            return c;
        }

        /// <summary>
        /// This layout with <paramref name="cells"/> more margin on each side in
        /// <paramref name="edges"/>, each capped at <see cref="MaxPaddingCells"/>.
        /// </summary>
        public RenderCanvas Grow(CanvasEdge edges, float cells)
        {
            var c = this;
            int add = Px(cells, PixelsPerCell);
            int max = Px(MaxPaddingCells, PixelsPerCell);
            if ((edges & CanvasEdge.Left) != 0) c.padLeft = Math.Min(max, c.padLeft + add);
            if ((edges & CanvasEdge.Right) != 0) c.padRight = Math.Min(max, c.padRight + add);
            if ((edges & CanvasEdge.Bottom) != 0) c.padBottom = Math.Min(max, c.padBottom + add);
            if ((edges & CanvasEdge.Top) != 0) c.padTop = Math.Min(max, c.padTop + add);
            return c;
        }

        /// <summary>True when every side in <paramref name="edges"/> is already at the cap.</summary>
        public bool AtMaximum(CanvasEdge edges)
        {
            int max = Px(MaxPaddingCells, PixelsPerCell);
            return ((edges & CanvasEdge.Left) == 0 || padLeft >= max)
                && ((edges & CanvasEdge.Right) == 0 || padRight >= max)
                && ((edges & CanvasEdge.Bottom) == 0 || padBottom >= max)
                && ((edges & CanvasEdge.Top) == 0 || padTop >= max);
        }

        /// <summary>
        /// The sides of the canvas an opaque crop reaches: art that touches an edge almost
        /// certainly continued past it and was cut off.
        /// </summary>
        public CanvasEdge TouchedBy(int minX, int minY, int cropW, int cropH)
        {
            var e = CanvasEdge.None;
            if (minX <= 0) e |= CanvasEdge.Left;
            if (minX + cropW >= Width) e |= CanvasEdge.Right;
            if (minY <= 0) e |= CanvasEdge.Bottom;
            if (minY + cropH >= Height) e |= CanvasEdge.Top;
            return e;
        }

        /// <summary>
        /// The footprint-relative rectangle of an opaque crop on this canvas: see
        /// <see cref="UiImageRect"/>. Equal to <see cref="UiImageRect.FromCrop"/> when the
        /// margins are equal on opposite sides, which is what that method assumes.
        /// </summary>
        public UiImageRect RectOf(int minX, int minY, int cropW, int cropH) => new UiImageRect
        {
            x = (minX - padLeft) / (float)PixelsPerCell,
            y = (minY - padBottom) / (float)PixelsPerCell,
            w = cropW / (float)PixelsPerCell,
            h = cropH / (float)PixelsPerCell,
        };

        /// <summary>Lower-case names of <paramref name="edges"/>, in left, right, bottom, top order.</summary>
        public static string[] EdgeNames(CanvasEdge edges)
        {
            var names = new List<string>();
            if ((edges & CanvasEdge.Left) != 0) names.Add("left");
            if ((edges & CanvasEdge.Right) != 0) names.Add("right");
            if ((edges & CanvasEdge.Bottom) != 0) names.Add("bottom");
            if ((edges & CanvasEdge.Top) != 0) names.Add("top");
            return names.ToArray();
        }

        private static int Need(float artCells, int pixelsPerCell) =>
            Px(Math.Min(MaxPaddingCells, Math.Max(DefaultPaddingCells, artCells + MarginCells)), pixelsPerCell);

        private static int Px(float cells, int pixelsPerCell) =>
            (int)Math.Ceiling(cells * pixelsPerCell - 1e-3f);
    }
}
