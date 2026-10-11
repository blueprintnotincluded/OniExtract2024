using System.Collections;
using System.Collections.Generic;
using System.IO;
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

            int tileBuildings = 0, tileSprites = 0;
            int utilityBuildings = 0;

            try
            {
                // --- 1) Tiles: extracted straight from BlockTileAtlas (no placement) -----
                foreach (var def in Assets.BuildingDefs)
                {
                    if (def == null || !def.isKAnimTile)
                        continue;
                    int files = TileConnectionExtractor.Export(def);
                    if (files > 0)
                    {
                        tileBuildings++;
                        tileSprites += files;
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
                        utilityBuildings++;
                }
                Debug.Log("OniExtract: utilities exported - " + utilityBuildings + " buildings.");

                // There is no third pass for bridges. A wire or pipe bridge has one fixed
                // sprite: it carries no KAnimGraphTileVisualizer and does not redraw to match
                // its neighbours. Every config that adds that component also sets isUtility,
                // so pass 2 already covers everything that has connection states.

                Debug.Log("OniExtract: connection-sprite export complete -> " + RootDir);
                summary = "Connection sprites exported: " + tileBuildings + " tiles and "
                    + utilityBuildings + " utilities.\n" + RootDir;
            }
            finally
            {
                InGameExport.End(summary);
            }
        }
    }
}
