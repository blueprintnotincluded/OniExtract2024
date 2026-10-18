using System.Collections.Generic;
using UnityEngine;

namespace OniExtract2024.building
{
    /// <summary>
    /// Picks the cell the in-game exports spawn their temporary buildings at: the
    /// <see cref="SpawnSite"/> in the active world nearest the spot the exports always used,
    /// 12 cells down and left of the camera's centre. The player does not have to prepare
    /// anything; the exports only look for a bit of natural or built floor with revealed air
    /// above it and no building within reach of the canvas. Plants, debris and critters do not
    /// count: they never share a render batch with a building. When the world has no such spot,
    /// the old one is used and the caller is told why.
    /// </summary>
    public static class SpawnArea
    {
        // Returns the spawn cell. `note` says where it is relative to the old spot, or what is
        // wrong with the old spot when no clear one was found; `clear` is false in that case.
        public static int Find(out string note, out bool clear)
        {
            int preferred = Grid.PosToCell(Camera.main.transform.position);
            for (int i = 0; i < 12; i++)
                preferred = Grid.CellDownLeft(preferred);

            WorldContainer world = ClusterManager.Instance.activeWorld;
            Vector2I offset = world.WorldOffset, size = world.WorldSize;
            int w = size.x, h = size.y;
            var solid = new bool[w * h];
            var air = new bool[w * h];
            var revealedEmpty = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int cell = Grid.XYToCell(offset.x + x, offset.y + y);
                    if (!Grid.IsValidCell(cell)) continue;
                    int i = y * w + x;
                    solid[i] = Grid.Solid[cell];
                    air[i] = !Grid.Solid[cell] && !Grid.IsLiquid(cell);
                    revealedEmpty[i] = Grid.IsVisible(cell);
                }
            }

            // A building or geyser drawn by kanim could share a render batch with a temporary
            // one, or be joined by it (AnimTileable). One pass over the layers marks them all.
            foreach (Dictionary<int, GameObject> layer in Grid.ObjectLayers)
            {
                if (layer == null) continue;
                foreach (var kv in layer)
                {
                    GameObject go = kv.Value;
                    if (go == null || go.GetComponent<KBatchedAnimController>() == null) continue;
                    if (go.GetComponent<Building>() == null && go.GetComponent<Geyser>() == null) continue;
                    Grid.CellToXY(kv.Key, out int cx, out int cy);
                    cx -= offset.x;
                    cy -= offset.y;
                    if (cx >= 0 && cy >= 0 && cx < w && cy < h)
                        revealedEmpty[cy * w + cx] = false;
                }
            }

            Grid.CellToXY(preferred, out int px, out int py);
            bool found = SpawnSite.TryFindNearest(w, h, solid, air, revealedEmpty, px - offset.x, py - offset.y,
                out int sx, out int sy, out SpawnSite.Survey survey);
            string counts = survey.Floor + " cells stand on a floor, " + survey.FloorAndOpen + " of them with "
                + (2 * SpawnSite.OpenHalfWidth + 1) + "x" + SpawnSite.OpenHeight + " cells of gas above, "
                + survey.Sites + " of those revealed with no building within " + SpawnSite.ClearSide + " cells";
            if (found)
            {
                clear = true;
                note = "spawning at (" + (offset.x + sx) + ", " + (offset.y + sy) + "), "
                    + Mathf.RoundToInt(Mathf.Sqrt((offset.x + sx - px) * (offset.x + sx - px) + (offset.y + sy - py) * (offset.y + sy - py)))
                    + " cells from the view's spot (" + counts + ")";
                return Grid.XYToCell(offset.x + sx, offset.y + sy);
            }

            clear = false;
            note = "no clear spot in this world (" + counts + "), so spawning at the view's spot;"
                + " images may show its surroundings";
            return preferred;
        }
    }
}
