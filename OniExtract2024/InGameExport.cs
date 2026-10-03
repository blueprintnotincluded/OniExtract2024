using System;
using PeterHan.PLib.UI;
using UnityEngine;

namespace OniExtract2024
{
    /// <summary>
    /// Shared by the two pause-screen export tools (building images, connection sprites).
    /// Lets only one of them run at a time, and says on screen when one starts, ends, or is
    /// refused — Player.log is otherwise the only sign that anything happened.
    /// </summary>
    public static class InGameExport
    {
        // Display name of the export that is running; null when idle.
        public static string Running { get; private set; }

        // The Game the running coroutine was started on. A coroutine dies with its host
        // without running its finally block, so if the colony is unloaded mid-export nothing
        // would ever clear Running. Comparing hosts lets the next request see that it is stale.
        private static Game s_host;

        private static ConfirmDialogScreen s_dialog;

        // Both tools spawn their temporary buildings at the same off-screen cell and both
        // switch the main camera off around each snapshot, so one must finish before the
        // other starts. Returns false, and says why on screen, when the request is refused.
        public static bool TryBegin(string name)
        {
            if (Game.Instance == null)
            {
                Debug.LogWarning("OniExtract: " + name + " export requires a loaded game.");
                return false;
            }
            if (Running != null && s_host == Game.Instance)
            {
                Debug.LogWarning("OniExtract: " + name + " export not started; " + Running + " export is still running.");
                Show("The " + Running + " export is still running.\n\nWait for its finished message, then try again.");
                return false;
            }

            Running = name;
            s_host = Game.Instance;
            Show("Exporting " + name + "...\n\nThis message is replaced when the export has finished. "
                + "Progress is logged to Player.log on lines starting \"OniExtract:\".");
            return true;
        }

        // Call from the export coroutine's finally block, so it also runs when the export
        // throws part-way; pass a summary that says so in that case.
        public static void End(string summary)
        {
            Running = null;
            s_host = null;
            Show(summary);
        }

        // Replaces the previous message rather than stacking a second dialog on top of it.
        // The export must never fail because a message could not be shown.
        private static void Show(string message)
        {
            try
            {
                if (s_dialog != null)
                    s_dialog.Deactivate();
                s_dialog = null;

                GameObject go = Util.KInstantiateUI(
                    ScreenPrefabs.Instance.ConfirmDialogScreen.gameObject, PDialog.GetParentObject(), false);
                if (!go.TryGetComponent(out ConfirmDialogScreen screen))
                    return;
                // No confirm or cancel action: the dialog shows a single OK button.
                screen.PopupConfirmDialog(message, null, null, null, null, "OniExtract");
                go.SetActive(true);
                s_dialog = screen;
            }
            catch (Exception e)
            {
                Debug.LogWarning("OniExtract: could not show an on-screen message: " + e.Message);
            }
        }
    }
}
