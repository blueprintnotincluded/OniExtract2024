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

        [Fact]
        public void ForArt_SmallArt_KeepsTheDefaultMargin()
        {
            // A 2x2 building whose anim reaches 1.1 cells either side of its bottom-centre and
            // 1.7 up and down: with the 0.25-cell allowance nothing passes the two-cell margin,
            // so the canvas is what it always was.
            var c = RenderCanvas.ForArt(2, 2, Ppc, 1f, 0f, 1.1f, 1.7f);

            Assert.Equal(RenderCanvas.Default(2, 2, Ppc).Width, c.Width);
            Assert.Equal(RenderCanvas.Default(2, 2, Ppc).Height, c.Height);
        }

        [Fact]
        public void ForArt_MonumentMiddle_WidensBothSides()
        {
            // option_a reaches 4.89 cells either side of the centre of a 5-wide footprint: 2.39
            // past each edge, more than the 2-cell margin that clipped it.
            var c = RenderCanvas.ForArt(5, 5, Ppc, 2.5f, 0f, 4.89f, 6.24f);

            Assert.Equal(528, c.padLeft);   // (2.39 + 0.25) * 200, rounded up
            Assert.Equal(528, c.padRight);
            Assert.Equal(1298, c.padBottom); // symmetric box: 6.24 below the origin too
            Assert.Equal(400, c.padTop);     // 6.24 - 5 + 0.25 = 1.49 < 2
        }

        [Fact]
        public void ForArt_UnusableExtent_FallsBackToDefault()
        {
            var expected = RenderCanvas.Default(5, 6, Ppc);
            foreach (float bad in new[] { 536870.92f, float.NaN, float.PositiveInfinity, 0f, -1f })
            {
                var c = RenderCanvas.ForArt(5, 6, Ppc, 2.5f, 0f, bad, 7.59f);
                Assert.Equal(expected.Width, c.Width);
                Assert.Equal(expected.Height, c.Height);
                Assert.False(RenderCanvas.IsUsableHalfExtent(bad, 7.59f));
            }
        }

        [Fact]
        public void ForArt_CapsTheMargin()
        {
            var c = RenderCanvas.ForArt(1, 1, Ppc, 0.5f, 0f, 19.5f, 19.5f);

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
