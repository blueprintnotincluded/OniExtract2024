using OniExtract2024.building;
using Xunit;

namespace OniExtract2024.Core.Tests
{
    public class RenderCanvasTests
    {
        private const int Ppc = 200;

        [Fact]
        public void Default_IsTheOldTwoCellMargin()
        {
            var c = RenderCanvas.Default(5, 3, Ppc);

            Assert.Equal(5 * 200 + 800, c.Width);
            Assert.Equal(3 * 200 + 800, c.Height);
            Assert.Equal(2.5f, c.CentreX, 3);
            Assert.Equal(1.5f, c.CentreY, 3);
        }

        private static UiImageRect Box(float x0, float x1, float y0, float y1) =>
            new UiImageRect { x = x0, y = y0, w = x1 - x0, h = y1 - y0 };

        [Fact]
        public void ForBox_ArtInsideTheMargin_KeepsTheDefault()
        {
            // VerticalWindTunnel's on anim (5x6): -0.21..5.24 by -1.71..7.66. With the 0.25-cell
            // allowance nothing passes the two-cell margin, so the canvas is what it always was.
            var c = RenderCanvas.ForBox(5, 6, Ppc, Box(-0.21f, 5.24f, -1.71f, 7.66f));

            Assert.Equal(RenderCanvas.Default(5, 6, Ppc).Width, c.Width);
            Assert.Equal(RenderCanvas.Default(5, 6, Ppc).Height, c.Height);
        }

        [Fact]
        public void ForBox_MonumentMiddle_WidensOnlyTheSidesTheArtReaches()
        {
            // option_a reaches 2.39 cells past each side of a 5-wide footprint, more than the
            // 2-cell margin that clipped it, and 1.24 above it.
            var c = RenderCanvas.ForBox(5, 5, Ppc, Box(-2.39f, 7.39f, -0.07f, 6.24f));

            Assert.Equal(528, c.padLeft);   // (2.39 + 0.25) * 200, rounded up
            Assert.Equal(528, c.padRight);
            Assert.Equal(400, c.padBottom);
            Assert.Equal(400, c.padTop);    // 1.24 + 0.25 = 1.49 < 2
        }

        [Fact]
        public void ForBox_SteamFarBelow_WidensTheBottom()
        {
            // SteamTurbine2's working_loop: steam 4.36 cells below the footprint.
            var c = RenderCanvas.ForBox(5, 3, Ppc, Box(-0.26f, 5.32f, -4.36f, 4.54f));

            Assert.Equal(922, c.padBottom); // (4.36 + 0.25) * 200, rounded up
            Assert.Equal(400, c.padTop);
        }

        [Fact]
        public void ForBox_UnusableBox_FallsBackToDefault()
        {
            var expected = RenderCanvas.Default(5, 6, Ppc);
            var bad = new[]
            {
                Box(float.NaN, 1f, 0f, 1f), Box(0f, float.PositiveInfinity, 0f, 1f),
                Box(-536868f, 536873f, 0f, 1f), Box(1f, 1f, 0f, 1f), Box(0f, 1f, 2f, 1f),
            };
            foreach (var box in bad)
            {
                var c = RenderCanvas.ForBox(5, 6, Ppc, box);
                Assert.Equal(expected.Width, c.Width);
                Assert.Equal(expected.Height, c.Height);
                Assert.False(RenderCanvas.IsUsableBox(box));
            }
        }

        [Fact]
        public void IsUsableHalfExtent_RejectsGarbage()
        {
            foreach (float bad in new[] { 536870.92f, float.NaN, float.PositiveInfinity, 0f, -1f })
                Assert.False(RenderCanvas.IsUsableHalfExtent(bad, 7.59f));
            Assert.True(RenderCanvas.IsUsableHalfExtent(2.66f, 7.59f));
        }

        [Fact]
        public void ForBox_CapsTheMargin()
        {
            var c = RenderCanvas.ForBox(1, 1, Ppc, Box(-19f, 20f, -19f, 20f));

            Assert.Equal((int)(RenderCanvas.MaxPaddingCells * Ppc), c.padLeft);
            Assert.Equal((int)(RenderCanvas.MaxPaddingCells * Ppc), c.padBottom);
        }

        [Fact]
        public void Grow_WidensOnlyTheTouchedSides_UpToTheCap()
        {
            var c = RenderCanvas.Default(1, 3, Ppc).Grow(CanvasEdge.Bottom, 4f);

            Assert.Equal(400 + 800, c.padBottom);
            Assert.Equal(400, c.padTop);
            Assert.Equal(400, c.padLeft);
            Assert.False(c.AtMaximum(CanvasEdge.Bottom));

            for (int i = 0; i < 5; i++) c = c.Grow(CanvasEdge.Bottom, 4f);
            Assert.Equal((int)(RenderCanvas.MaxPaddingCells * Ppc), c.padBottom);
            Assert.True(c.AtMaximum(CanvasEdge.Bottom));
            Assert.False(c.AtMaximum(CanvasEdge.Bottom | CanvasEdge.Top));
        }

        [Fact]
        public void TouchedBy_ReportsEachEdgeTheCropReaches()
        {
            var c = RenderCanvas.Default(1, 3, Ppc); // 1000 x 1400

            Assert.Equal(CanvasEdge.None, c.TouchedBy(10, 10, 100, 100));
            Assert.Equal(CanvasEdge.Bottom, c.TouchedBy(10, 0, 100, 100));
            Assert.Equal(CanvasEdge.Left | CanvasEdge.Top, c.TouchedBy(0, 1300, 100, 100));
            Assert.Equal(CanvasEdge.Right, c.TouchedBy(900, 10, 100, 100));
            Assert.Equal(new[] { "left", "top" }, RenderCanvas.EdgeNames(CanvasEdge.Left | CanvasEdge.Top));
        }

        [Fact]
        public void RectOf_DefaultCanvas_MatchesFromCrop()
        {
            var c = RenderCanvas.Default(5, 3, Ppc);
            var a = c.RectOf(400, 152, 1000, 848);
            var b = UiImageRect.FromCrop(400, 152, 1000, 848, c.Width, c.Height, 5, 3, Ppc);

            Assert.Equal(b.x, a.x, 4);
            Assert.Equal(b.y, a.y, 4);
            Assert.Equal(b.w, a.w, 4);
            Assert.Equal(b.h, a.h, 4);
        }

        [Fact]
        public void RectOf_UnevenMargins_MeasuresFromTheFootprintCorner()
        {
            // Bottom margin widened to 1200 px: art 248 px below the footprint now starts at
            // pixel 952, and must still come out at y = -1.24 cells.
            var c = RenderCanvas.Default(5, 3, Ppc).Grow(CanvasEdge.Bottom, 4f);
            var r = c.RectOf(400, 1200 - 248, 1000, 848);

            Assert.Equal(0f, r.x, 4);
            Assert.Equal(-1.24f, r.y, 4);
            Assert.Equal(4.24f, r.h, 4);
            // The camera centre moves down with the larger bottom margin.
            Assert.Equal((c.Height / 2f - 1200) / Ppc, c.CentreY, 4);
        }
    }
}
