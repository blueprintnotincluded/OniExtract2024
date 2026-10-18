namespace OniExtract2024.building
{
    /// <summary>
    /// Finds where the in-game exports spawn the buildings they render. A spawned building draws
    /// some of its parts from its surroundings: a pitcher pump or a brackene fountain shows a pipe
    /// for each open cell below it, a glass or drywall panel joins the backwalls next to it, and a
    /// colony building in view is drawn into the image when it shares a render batch. An image of
    /// the building alone needs a spot with a solid floor under it, open air where the building
    /// stands, and no building around it. Pure logic over cell flags, so CI can test it.
    /// </summary>
    public static class SpawnSite
    {
        /// <summary>
        /// The floor must be solid this far either side of the spawn cell: the cells the pitcher
        /// pump (0 and +1) and the brackene fountain (0) look down from.
        /// </summary>
        public const int FloorHalfWidth = 1;

        /// <summary>
        /// Gas or vacuum this far either side and this high above the floor, the largest
        /// footprint (7 wide, 6 tall), so nothing spawns into rock or liquid.
        /// </summary>
        public const int OpenHalfWidth = 3, OpenHeight = 6;

        /// <summary>Revealed and free of buildings this far around: canvases reach a few cells past the art.</summary>
        public const int ClearSide = 8, ClearBelow = 4, ClearAbove = 10;

        /// <summary>How many cells passed each test, for saying why no site was found.</summary>
        public struct Survey
        {
            public int Floor;
            public int FloorAndOpen;
            public int Sites;
        }

        /// <summary>
        /// The site nearest (<paramref name="fromX"/>, <paramref name="fromY"/>). The flag arrays
        /// cover a <paramref name="width"/> x <paramref name="height"/> world row by row from its
        /// bottom-left (index = y * width + x): <paramref name="solid"/> cells can carry a
        /// building, <paramref name="air"/> cells are gas or vacuum, <paramref name="clear"/>
        /// cells are revealed and hold no building. Ties go to the lower, then the leftmost
        /// cell. False when the world has no such site; <paramref name="survey"/> says which test
        /// ruled the cells out.
        /// </summary>
        public static bool TryFindNearest(int width, int height, bool[] solid, bool[] air, bool[] clear,
            int fromX, int fromY, out int x, out int y, out Survey survey)
        {
            x = y = -1;
            survey = default(Survey);
            var solidSum = Summed(width, height, solid);
            var airSum = Summed(width, height, air);
            var clearSum = Summed(width, height, clear);

            long best = long.MaxValue;
            int floorCells = 2 * FloorHalfWidth + 1;
            int openCells = (2 * OpenHalfWidth + 1) * OpenHeight;
            int clearCells = (2 * ClearSide + 1) * (ClearBelow + ClearAbove + 1);
            for (int cy = ClearBelow; cy + ClearAbove < height; cy++)
            {
                for (int cx = ClearSide; cx + ClearSide < width; cx++)
                {
                    if (Count(solidSum, width, cx - FloorHalfWidth, cy - 1, cx + FloorHalfWidth, cy - 1) != floorCells) continue;
                    survey.Floor++;
                    if (Count(airSum, width, cx - OpenHalfWidth, cy, cx + OpenHalfWidth, cy + OpenHeight - 1) != openCells) continue;
                    survey.FloorAndOpen++;
                    if (Count(clearSum, width, cx - ClearSide, cy - ClearBelow, cx + ClearSide, cy + ClearAbove) != clearCells) continue;
                    survey.Sites++;
                    long dx = cx - fromX, dy = cy - fromY;
                    long d = dx * dx + dy * dy;
                    if (d >= best) continue;
                    best = d;
                    x = cx;
                    y = cy;
                }
            }
            return x >= 0;
        }

        // Summed-area table with a zero row and column in front: s[(y+1)*(w+1) + (x+1)] is the
        // number of set flags in [0..x] x [0..y].
        private static int[] Summed(int width, int height, bool[] flags)
        {
            int stride = width + 1;
            var s = new int[stride * (height + 1)];
            for (int y = 0; y < height; y++)
            {
                int row = 0;
                for (int x = 0; x < width; x++)
                {
                    if (flags[y * width + x]) row++;
                    s[(y + 1) * stride + x + 1] = s[y * stride + x + 1] + row;
                }
            }
            return s;
        }

        // Set flags in the inclusive box [x0..x1] x [y0..y1], which must lie inside the world.
        private static int Count(int[] s, int width, int x0, int y0, int x1, int y1)
        {
            int stride = width + 1;
            return s[(y1 + 1) * stride + x1 + 1] - s[y0 * stride + x1 + 1] - s[(y1 + 1) * stride + x0] + s[y0 * stride + x0];
        }
    }
}
