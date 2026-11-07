using System.Collections.Generic;

namespace OniExtract2024.building
{
    /// <summary>A grid cell offset: whole cells from an origin cell, +x right, +y up.</summary>
    public struct Cell
    {
        public int x;
        public int y;

        public Cell(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    /// <summary>
    /// A footprint as a box of whole cells: <see cref="x"/>, <see cref="y"/> is its bottom-left
    /// cell relative to the object's own cell (the one its position is in), <see cref="w"/> and
    /// <see cref="h"/> its size. A building's box is <see cref="Standard"/>: the bottom row
    /// at column floor((w-1)/2), so the object's cell is bottom-centre for an odd width. A
    /// hanging plant's box reaches down from its cell instead (y = 1 - h), and a tree's
    /// takes in its branches.
    /// </summary>
    public struct CellBox
    {
        public int x;
        public int y;
        public int w;
        public int h;

        /// <summary>The box the game gives a w x h building or placed entity standing on its cell.</summary>
        public static CellBox Standard(int w, int h) =>
            new CellBox { x = -((w - 1) / 2), y = 0, w = w, h = h };

        /// <summary>True when this box is where <see cref="Standard"/> would put one of its size.</summary>
        public bool IsStandard => x == Standard(w, h).x && y == 0;

        /// <summary>
        /// The smallest box holding every cell; false for no cells. For a plant these are its
        /// OccupyArea offsets, plus the branch offsets of a tree drawn with its canopy.
        /// </summary>
        public static bool TryFromCells(IEnumerable<Cell> cells, out CellBox box)
        {
            box = default(CellBox);
            bool any = false;
            int minX = 0, minY = 0, maxX = 0, maxY = 0;
            foreach (Cell c in cells)
            {
                if (!any)
                {
                    minX = maxX = c.x;
                    minY = maxY = c.y;
                    any = true;
                    continue;
                }
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.y > maxY) maxY = c.y;
            }
            if (!any) return false;
            box = new CellBox { x = minX, y = minY, w = maxX - minX + 1, h = maxY - minY + 1 };
            return true;
        }
    }
}
