using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace OniExtract2024.connection
{
    /// <summary>
    /// Separate, in-game connection-sprite exporter. Independent of the game-data
    /// export (ExportGameData) because connectables can
    /// only be rendered inside a loaded game/sandbox: utilities need a placed instance
    /// plus the live KAnimBatchManager and camera; tiles need their texture atlas loaded.
    ///
    /// Triggered from a button on the in-game pause screen (see ConnectionExportPatches).
    /// Writes one PNG per 4-bit bitmask (left=1, right=2, up=4, down=8) to:
    ///     {RootFolder}/export/connection_sprites/{prefabId}/{bitmask}.png
    ///
    /// Two rendering paths, by building type:
    ///   - isUtility  : ConnectionSpriteSnapshotter (kanim camera snapshot, then a
    ///                  cell-centred crop to remove the snapshot's whitespace).
    ///   - isKAnimTile: TileConnectionExtractor (resamples the BlockTileAtlas into a
    ///                  cell-anchored frame with the game's trim/overhang geometry so
    ///                  tiles join seamlessly).
    ///
    /// TODO (future): expose progress/results in an in-game panel (the user wants this
    /// to grow into a debugging UI), and reinstate the decor "tops"/corner layer for
    /// tiles (dropped for now - its placement is diagonal-dependent and can't be
    /// represented in the website's 4-bit/16-state model; see TileConnectionExtractor).
    /// </summary>
    public static class ExportConnectionSprites
    {
        private const string ExportName = "connection sprites";

        public static bool IsRunning => InGameExport.Running == ExportName;

        public static string RootDir =>
            Path.Combine(Util.RootFolder(), "export", "connection_sprites");

        public static string OutputDir(string prefabId) => Path.Combine(RootDir, prefabId);

        // Where each connectable's cell sits on its PNGs (ConnectionSpriteCell). Beside the
        // folder rather than in database/, because this tool writes it and the game-data export
        // must not be able to drop it; the same reason ui_image_rects.json lives there.
        public static string CellsPath =>
            Path.Combine(Util.RootFolder(), "export", "connection_sprite_cells.json");

        public static void Start()
        {
            if (InGameExport.TryBegin(ExportName))
                Game.Instance.StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            // Replaced on success; what the user sees if the export throws part-way.
            string summary = "The connection sprites export stopped early. See Player.log for the error.";
            Debug.Log("OniExtract: connection-sprite export started -> " + RootDir);

            // Everything under RootDir is rewritten below, so start from nothing: a directory
            // left by an earlier run for a connectable that is no longer loaded (a mod since
            // disabled) would otherwise ship to the website as current. A connectable this run
            // fails to write is then missing rather than stale, which the website notices.
            // If the folder cannot be cleared (open in another program, say), writing into it
            // would produce exactly the stale mix this exists to prevent, so stop here. The
            // cells sidecar describes the folder, so it goes with it and is rewritten at the end.
            try
            {
                if (Directory.Exists(RootDir))
                    Directory.Delete(RootDir, true);
                if (File.Exists(CellsPath))
                    File.Delete(CellsPath);
            }
            catch (Exception e)
            {
                Debug.LogError("OniExtract: could not clear " + RootDir + ": " + e.Message);
                InGameExport.End("Connection sprites not exported: could not clear\n" + RootDir
                    + "\nClose anything using it and run again. See Player.log.");
                yield break;
            }

            int tileBuildings = 0, tileSprites = 0;
            int utilityBuildings = 0;
            // Sorted, so the file diffs cleanly from one run to the next.
            var cells = new SortedDictionary<string, ConnectionSpriteCell>(StringComparer.Ordinal);

            try
            {
                // --- 1) Tiles: extracted straight from BlockTileAtlas (no placement) -----
                foreach (var def in Assets.BuildingDefs)
                {
                    if (def == null || !def.isKAnimTile)
                        continue;
                    int files = TileConnectionExtractor.Export(def, out ConnectionSpriteCell tileCell);
                    if (files > 0)
                    {
                        tileBuildings++;
                        tileSprites += files;
                        cells[def.PrefabID] = tileCell;
                    }
                    yield return null;
                }
                Debug.Log("OniExtract: tiles exported - " + tileBuildings + " buildings, " + tileSprites + " sprites.");

                // --- 2) Utilities: spawn a temp instance, snapshot 16 kanim states -------
                // Place well away from the camera/colony to avoid disturbing the view.
                int cell = Grid.PosToCell(Camera.main.transform.position);
                for (int i = 0; i < 12; i++)
                    cell = Grid.CellDownLeft(cell);
                Vector3 spawnPos = Grid.CellToPos(cell);

                foreach (var def in Assets.BuildingDefs)
                {
                    if (def == null || !def.isUtility)
                        continue;
                    if (def.BuildingComplete == null || !def.BuildingComplete.TryGetComponent<KBatchedAnimController>(out _))
                        continue;

                    GameObject temp = def.Create(spawnPos, null,
                        new List<Tag> { SimHashes.Unobtanium.CreateTag() }, null, 100f, def.BuildingComplete);
                    if (temp == null)
                        continue;

                    var snapshotter = temp.AddOrGet<ConnectionSpriteSnapshotter>();
                    yield return snapshotter.ExportThenDestroy();
                    // Count what was written, not what was attempted: a building with no
                    // usable connection manager is spawned, skipped with a warning, and
                    // leaves no sprites behind.
                    if (snapshotter.WroteSprites)
                    {
                        utilityBuildings++;
                        cells[def.PrefabID] = snapshotter.Cell;
                    }
                }
                Debug.Log("OniExtract: utilities exported - " + utilityBuildings + " buildings.");

                // Written after both passes, so it lists exactly the directories on disk.
                bool cellsWritten = WriteCells(cells);

                // There is no third pass for bridges. A wire or pipe bridge has one fixed
                // sprite: it carries no KAnimGraphTileVisualizer and does not redraw to match
                // its neighbours. Every config that adds that component also sets isUtility,
                // so pass 2 already covers everything that has connection states.

                Debug.Log("OniExtract: connection-sprite export complete -> " + RootDir);
                summary = "Connection sprites exported: " + tileBuildings + " tiles and "
                    + utilityBuildings + " utilities.\n" + RootDir
                    + (cellsWritten ? "" : "\nconnection_sprite_cells.json could not be written. See Player.log.");
            }
            finally
            {
                InGameExport.End(summary);
            }
        }

        private static bool WriteCells(IDictionary<string, ConnectionSpriteCell> cells)
        {
            try
            {
                File.WriteAllText(CellsPath, JsonConvert.SerializeObject(cells, Formatting.Indented));
                Debug.Log("OniExtract: connection sprite cells -> " + CellsPath + " (" + cells.Count + " connectables)");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("OniExtract: could not write " + CellsPath + ": " + e);
                return false;
            }
        }
    }
}
