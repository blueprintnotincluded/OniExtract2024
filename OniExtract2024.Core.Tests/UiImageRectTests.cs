using OniExtract2024.building;
using Xunit;

namespace OniExtract2024.Core.Tests
{
    public class UiImageRectTests
    {
        // Mirrors BuildingImageSnapshotter's geometry: PixelsPerCell = 200, PaddingPx = 400,
        // so a W×H building renders to a (W*200 + 800) × (H*200 + 800) texture with the
        // footprint centred. Footprint pixel bounds are therefore [400 .. 400 + size].
        private const float Ppc = 200f;
        private static int TexDim(int cells) => cells * 200 + 800; // 2*400 padding

        [Fact]
        public void FromCrop_FootprintFillingArt_IsZeroOriginFootprintSize()
        {
            // A 2x2 building whose art exactly fills the footprint: crop = [400..800] in both axes.
            int w = 2, h = 2;
            var r = UiImageRect.FromCrop(400, 400, 400, 400, TexDim(w), TexDim(h), w, h, Ppc);

            Assert.Equal(0f, r.x, 3);
            Assert.Equal(0f, r.y, 3);
            Assert.Equal(2f, r.w, 3);
            Assert.Equal(2f, r.h, 3);
        }

        [Fact]
        public void FromCrop_OverhangBelowFootprint_GivesNegativeY()
        {
            // SteamTurbine2: 5x3 footprint, exhaust hangs ~1.24 cells below.
            // Footprint bottom pixel = 400; art bottom = 400 - 1.24*200 = 152.
            // Art is footprint-wide (1000 px) and reaches the footprint top (pixel 1000),
            // so crop height = 1000 - 152 = 848.
            int w = 5, h = 3;
            var r = UiImageRect.FromCrop(400, 152, 1000, 848, TexDim(w), TexDim(h), w, h, Ppc);

            Assert.Equal(0f, r.x, 3);
            Assert.Equal(-1.24f, r.y, 3);
            Assert.Equal(5f, r.w, 3);
            Assert.Equal(4.24f, r.h, 3);
        }

        [Fact]
        public void FromCrop_SideOverhang_GivesNegativeX()
        {
            // Art extends half a cell left of the footprint: art left pixel = 400 - 100 = 300.
            int w = 3, h = 1;
            var r = UiImageRect.FromCrop(300, 400, 800, 200, TexDim(w), TexDim(h), w, h, Ppc);

            Assert.Equal(-0.5f, r.x, 3);
            Assert.Equal(0f, r.y, 3);
            Assert.Equal(4f, r.w, 3); // 800 px / 200 = 4 cells (3-wide footprint + 0.5 each side)
            Assert.Equal(1f, r.h, 3);
        }
    }
}
