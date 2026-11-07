using System.Collections.Generic;
using OniExtract2024.building;
using Xunit;

namespace OniExtract2024.Core.Tests
{
    public class CellBoxTests
    {
        private static List<Cell> Cells(params int[] xy)
        {
            var list = new List<Cell>();
            for (int i = 0; i < xy.Length; i += 2) list.Add(new Cell(xy[i], xy[i + 1]));
            return list;
        }

        [Theory]
        [InlineData(1, 1, 0)]
        [InlineData(2, 2, 0)]
        [InlineData(3, 4, -1)]
        [InlineData(4, 1, -1)]
        [InlineData(5, 3, -2)]
        public void Standard_OriginColumnIsFloorOfHalfWidthMinusOne(int w, int h, int expectedX)
        {
            var box = CellBox.Standard(w, h);
            Assert.Equal(expectedX, box.x);
            Assert.Equal(0, box.y);
            Assert.Equal(w, box.w);
            Assert.Equal(h, box.h);
            Assert.True(box.IsStandard);
        }

        [Fact]
        public void StandingPlant_MatchesTheGameOffsets()
        {
            // EntityTemplates.GenerateOffsets(3, 4): x from -1 to 1, y from 0 to 3.
            var cells = new List<Cell>();
            for (int y = 0; y <= 3; y++) for (int x = -1; x <= 1; x++) cells.Add(new Cell(x, y));
            Assert.True(CellBox.TryFromCells(cells, out CellBox box));
            Assert.Equal(CellBox.Standard(3, 4), box);
        }

        [Fact]
        public void HangingPlant_ReachesDownFromItsCell()
        {
            // EntityTemplates.GenerateHangingOffsets(1, 3): y from -2 to 0.
            Assert.True(CellBox.TryFromCells(Cells(0, 0, 0, -1, 0, -2), out CellBox box));
            Assert.Equal(0, box.x);
            Assert.Equal(-2, box.y);
            Assert.Equal(1, box.w);
            Assert.Equal(3, box.h);
            Assert.False(box.IsStandard);
        }

        [Fact]
        public void TreeWithBranches_IsTheWholeCanopy()
        {
            // Arbor Tree: a 1x2 trunk and seven branch offsets around it.
            var cells = Cells(0, 0, 0, 1, -1, 0, -1, 1, -1, 2, 0, 2, 1, 2, 1, 1, 1, 0);
            Assert.True(CellBox.TryFromCells(cells, out CellBox box));
            Assert.Equal(CellBox.Standard(3, 3), box);
        }

        [Fact]
        public void NoCells_NoBox()
        {
            Assert.False(CellBox.TryFromCells(new List<Cell>(), out _));
        }
    }
}
