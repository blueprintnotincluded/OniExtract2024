using System.Collections.Generic;
using HarmonyLib;
using OniExtract2024.building;
using UnityEngine.Events;

namespace OniExtract2024.connection
{
    /// <summary>
    /// Adds export buttons to the in-game pause screen. Kept separate from the
    /// data-collection patches in Patches.cs; every export runs from a loaded game.
    /// </summary>
    public class ConnectionExportPatches
    {
        [HarmonyPatch(typeof(PauseScreen), "ConfigureButtonInfos")]
        public static class PauseScreen_ConfigureButtonInfos_Patch
        {
            public static void Postfix(ref KButtonMenu.ButtonInfo[] ___buttons)
            {
                var list = new List<KButtonMenu.ButtonInfo>(___buttons);
                // Insert just before the last entry (typically "Desktop"/quit), in the order
                // they should be run: the image sweep patches building.json, so data first.
                int index = list.Count > 0 ? list.Count - 1 : 0;
                list.Insert(index++, Button("Export Game Data", ExportGameData.Start));
                list.Insert(index++, Button("Export Building Images", ExportBuildingImages.Start));
                list.Insert(index++, Button("Export Connection Sprites", ExportConnectionSprites.Start));
                list.Insert(index++, Button("Inspect Building Poses", BuildingPoseInspectorScreen.Open));
                ___buttons = list.ToArray();
            }

            private static KButtonMenu.ButtonInfo Button(string text, UnityAction onClick)
            {
                var button = new KButtonMenu.ButtonInfo(text, global::Action.NumActions, onClick);
                button.isEnabled = true;
                return button;
            }
        }
    }
}
