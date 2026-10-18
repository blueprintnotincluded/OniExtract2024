using OniExtract2024.connection;
using Xunit;

namespace OniExtract2024.Core.Tests
{
    public class ConnectionSpriteCellTests
    {
        [Fact]
        public void ForCentredSquare_TransitTube_CellSitsInTheMiddle()
        {
            // The tube's all-connected state reaches 1.2 cells from the centre, so the crop is
            // a 240 px square around a 100 px cell.
            var g = ConnectionSpriteCell.ForCentredSquare(240, 1, 1, 100);

            Assert.Equal(240, g.canvas.w);
            Assert.Equal(240, g.canvas.h);
            Assert.Equal(70f, g.cell.x, 3);
            Assert.Equal(70f, g.cell.y, 3);
            Assert.Equal(100f, g.cell.w, 3);
            Assert.Equal(100f, g.cell.h, 3);
        }

        [Fact]
        public void ForCentredSquare_WireWithThinOverhang_CellInsetByTheOverhang()
        {
            var g = ConnectionSpriteCell.ForCentredSquare(104, 1, 1, 100);

            Assert.Equal(2f, g.cell.x, 3);
            Assert.Equal(2f, g.cell.y, 3);
        }

        [Fact]
        public void ForTile_Standard128PxAtlas_CellAt32()
        {
            // 1.5-cell frame at 128 px/cell: 0.25-cell margin on every side.
            var g = ConnectionSpriteCell.ForTile(192, 128, 0.25f);

            Assert.Equal(192, g.canvas.w);
            Assert.Equal(192, g.canvas.h);
            Assert.Equal(32f, g.cell.x, 3);
            Assert.Equal(32f, g.cell.y, 3);
            Assert.Equal(128f, g.cell.w, 3);
            Assert.Equal(128f, g.cell.h, 3);
        }

        [Fact]
        public void ForTile_FrameRoundedDown_TopMarginIsWhatIsLeft()
        {
            // 1.5 * 127 = 190.5 rounds to 190, so the bottom margin keeps its 31.75 px and the
            // top gets the rest. Rows are y-down, so the cell starts at the top margin.
            var g = ConnectionSpriteCell.ForTile(190, 127, 0.25f);

            Assert.Equal(31.75f, g.cell.x, 3);
            Assert.Equal(190f - 1.25f * 127f, g.cell.y, 3);
        }
    }
}
