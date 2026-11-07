using System.Collections.Generic;
using UnityEngine;

namespace OniExtract2024.building
{
    /// <summary>
    /// The cells a plant occupies, as a box relative to its own cell: the game's
    /// OccupyArea offsets (EntityTemplates.GenerateOffsets for a standing plant, which puts
    /// the plant's cell in the bottom row; GenerateHangingOffsets for a ceiling plant, whose
    /// cell is the top row), plus the branch offsets of a tree the sweep draws with its canopy
    /// (PlantCanopy). The sweep measures each plant's uiImageRect against this box and the
    /// game-data export writes it onto the plant's entities.json entry, so the two agree.
    /// </summary>
    internal static class PlantFootprint
    {
        public static bool TryMeasure(GameObject prefab, out CellBox box)
        {
            box = default(CellBox);
            if (prefab == null) return false;

            var cells = new List<Cell>();
            var area = prefab.GetComponent<OccupyArea>();
            CellOffset[] occupied = area != null ? area.OccupiedCellsOffsets : null;
            if (occupied != null)
                foreach (CellOffset o in occupied)
                    cells.Add(new Cell(o.x, o.y));
            foreach (CellOffset o in PlantCanopy.BranchOffsets(prefab))
                cells.Add(new Cell(o.x, o.y));
            if (CellBox.TryFromCells(cells, out box))
                return true;

            // No OccupyArea: the collider ConfigPlacedEntity sized to (width, height).
            var collider = prefab.GetComponent<KBoxCollider2D>();
            if (collider == null) return false;
            int w = Mathf.RoundToInt(collider.size.x), h = Mathf.RoundToInt(collider.size.y);
            if (w <= 0 || h <= 0) return false;
            box = CellBox.Standard(w, h);
            return true;
        }
    }
}
