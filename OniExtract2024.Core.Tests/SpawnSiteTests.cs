using OniExtract2024.building;
using Xunit;

namespace OniExtract2024.Core.Tests
{
    public class SpawnSiteTests
    {
        // A world of open, revealed, empty air over a solid floor at row `floor`.
        private sealed class World
        {
            public readonly int W, H;
            public readonly bool[] Solid, Air, Clear;

            public World(int w, int h, int floor)
            {
                W = w; H = h;
                Solid = new bool[w * h]; Air = new bool[w * h]; Clear = new bool[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Solid[y * w + x] = y <= floor;
                        Air[y * w + x] = y > floor;
                        Clear[y * w + x] = true;
                    }
            }

            public bool Find(int fromX, int fromY, out int x, out int y) =>
                SpawnSite.TryFindNearest(W, H, Solid, Air, Clear, fromX, fromY, out x, out y);
        }

        [Fact]
        public void OpenFloor_SpawnsOnTheFloorNearestThePreferredSpot()
        {
            var w = new World(80, 60, 20);

            Assert.True(w.Find(40, 45, out int x, out int y));
            Assert.Equal(40, x);
            Assert.Equal(21, y); // the first air row, standing on the floor
        }

        [Fact]
        public void BuildingNearby_MovesTheSiteAwayFromIt()
        {
            var w = new World(80, 60, 20);
            w.Clear[25 * 80 + 40] = false; // something built at (40, 25)

            Assert.True(w.Find(40, 21, out int x, out int y));
            Assert.True(System.Math.Abs(x - 40) > SpawnSite.ClearSide || y - 25 > SpawnSite.ClearBelow
                || 25 - y > SpawnSite.ClearAbove);
        }

        [Fact]
        public void GapInTheFloor_IsNotASite()
        {
            // A one-cell shaft under (40, 21): the pitcher pump would hang a pipe into it.
            var w = new World(80, 60, 20);
            w.Solid[20 * 80 + 40] = false;
            w.Air[20 * 80 + 40] = true;

            Assert.True(w.Find(40, 21, out int x, out _));
            Assert.True(System.Math.Abs(x - 40) > SpawnSite.FloorHalfWidth);
        }

        [Fact]
        public void LiquidOrRockAbove_IsNotASite()
        {
            var w = new World(80, 60, 20);
            for (int x = 0; x < 80; x++)
                w.Air[23 * 80 + x] = false; // a liquid layer three rows up, everywhere

            Assert.False(w.Find(40, 21, out _, out _));
        }

        [Fact]
        public void UnrevealedOrCrowdedWorld_HasNoSite()
        {
            var w = new World(80, 60, 20);
            for (int i = 0; i < w.Clear.Length; i++)
                w.Clear[i] = i % 7 != 0;

            Assert.False(w.Find(40, 21, out _, out _));
        }

        [Fact]
        public void WorldTooSmall_HasNoSite()
        {
            var w = new World(20, 20, 5);

            Assert.False(w.Find(10, 6, out _, out _));
        }
    }
}
