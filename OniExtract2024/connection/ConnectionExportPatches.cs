using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.Events;

namespace OniExtract2024.connection
{
    /// <summary>
    /// Adds an "Export Connection Sprites" button to the in-game pause screen so the
    /// connection-sprite exporter can be triggered manually from inside a loaded
    /// game/sandbox. Kept separate from the rest of OniExtract2024's patches.
    /// </summary>
    public class ConnectionExportPatches
    {
        [HarmonyPatch(typeof(PauseScreen), "ConfigureButtonInfos")]
        public static class PauseScreen_ConfigureButtonInfos_Patch
        {
            public static void Postfix(ref KButtonMenu.ButtonInfo[] ___buttons)
            {
                var list = new List<KButtonMenu.ButtonInfo>(___buttons);
                var button = new KButtonMenu.ButtonInfo(
                    "Export Connection Sprites",
                    global::Action.NumActions,
                    new UnityAction(ExportConnectionSprites.Start));
                button.isEnabled = true;
                // Insert just before the last entry (typically "Desktop"/quit).
                int index = list.Count > 0 ? list.Count - 1 : 0;
                list.Insert(index, button);
                ___buttons = list.ToArray();
            }
        }
    }
}
