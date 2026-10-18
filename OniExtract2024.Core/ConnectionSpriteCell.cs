namespace OniExtract2024.connection
{
    /// <summary>
    /// Where the building's cell sits on its 16 connection-state PNGs, in pixels: origin at the
    /// top-left of the PNG, +y down, exactly as an image reader indexes it. One entry per
    /// connectable in <c>export/connection_sprite_cells.json</c>, written by the
    /// connection-sprite tool alongside the PNGs, so the website scales and anchors each sprite
    /// from what the mod drew instead of inferring it from the art.
    ///
    /// <c>cell</c> is the building's footprint, which is one cell for every connectable the game
    /// has. Its pixels per cell is <c>cell.w</c>. Art outside the cell (disconnected tile borders,
    /// the transit tube's corner fillets) is overhang into the neighbouring cells.
    /// </summary>
    public class ConnectionSpriteCell
    {
        public PixelSize canvas;
        public PixelRect cell;

        /// <summary>
        /// Utility path (ConnectionSpriteSnapshotter): the snapshot camera centres the footprint
        /// on the frame at <paramref name="pixelsPerCell"/>, and every state is cropped to one
        /// square of side <paramref name="side"/> centred on that same point. The footprint
        /// therefore sits in the middle of the square; the crop is symmetric, so the y-down
        /// position equals the y-up one.
        /// </summary>
        public static ConnectionSpriteCell ForCentredSquare(int side, int cellW, int cellH, int pixelsPerCell)
        {
            float w = cellW * pixelsPerCell;
            float h = cellH * pixelsPerCell;
            return new ConnectionSpriteCell
            {
                canvas = new PixelSize { w = side, h = side },
                cell = new PixelRect { x = side / 2f - w / 2f, y = side / 2f - h / 2f, w = w, h = h },
            };
        }

        /// <summary>
        /// Tile path (TileConnectionExtractor): every state is resampled into a square
        /// <paramref name="frame"/> px wide, mapping world x (cells) to pixel
        /// <c>(x + overhang) * cellPx</c> with rows counted from the bottom. So the cell starts
        /// <c>overhang * cellPx</c> from the left and bottom, and its top edge lies
        /// <c>frame - (1 + overhang) * cellPx</c> below the top of the PNG.
        /// </summary>
        public static ConnectionSpriteCell ForTile(int frame, int cellPx, float overhangCells)
        {
            return new ConnectionSpriteCell
            {
                canvas = new PixelSize { w = frame, h = frame },
                cell = new PixelRect
                {
                    x = overhangCells * cellPx,
                    y = frame - (1f + overhangCells) * cellPx,
                    w = cellPx,
                    h = cellPx,
                },
            };
        }
    }

    public struct PixelSize
    {
        public int w;
        public int h;
    }

    public struct PixelRect
    {
        public float x;
        public float y;
        public float w;
        public float h;
    }
}
