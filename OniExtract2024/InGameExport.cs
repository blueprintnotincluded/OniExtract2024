using System;
using System.Collections;
using PeterHan.PLib.UI;
using UnityEngine;
using UnityEngine.UI;

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

        // The message currently on screen, and a counter that lets a pending auto-hide tell
        // whether the message it was started for has since been replaced.
        private static GameObject s_banner;
        private static int s_bannerVersion;

        // How long a finished message and a refusal stay up. The "running" message stays
        // until the export ends.
        private const float SummarySeconds = 10f;
        private const float RefusalSeconds = 4f;

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
                // The running export's own message is still what matters; put it back after
                // the refusal has been read.
                string running = Running;
                Show("Not started: the " + running + " export is still running.", RefusalSeconds,
                    () => { if (Running == running) Show(RunningMessage(running), 0f, null); });
                return false;
            }

            Running = name;
            s_host = Game.Instance;
            Show(RunningMessage(name), 0f, null);
            return true;
        }

        // Call from the export coroutine's finally block, so it also runs when the export
        // throws part-way; pass a summary that says so in that case.
        public static void End(string summary)
        {
            Running = null;
            s_host = null;
            Show(summary, SummarySeconds, null);
        }

        private static string RunningMessage(string name)
        {
            return "Exporting " + name + "...\nProgress is logged to Player.log on lines starting \"OniExtract:\".";
        }

        // A plain banner along the top of the screen. It replaces the previous one, takes no
        // input (the pause menu stays usable underneath it), and stays until replaced when
        // `seconds` is 0, or else removes itself after that long and calls `afterHide` if it
        // was not replaced first. The export must never fail because a message could not be
        // shown.
        private static void Show(string message, float seconds, System.Action afterHide)
        {
            try
            {
                if (s_banner != null)
                    UnityEngine.Object.Destroy(s_banner);
                s_banner = null;
                int version = ++s_bannerVersion;

                GameObject parent = BannerParent();
                if (parent == null)
                    return;

                GameObject banner = PUIElements.CreateUI(parent, "OniExtractStatus");
                var rect = (RectTransform)banner.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);   // top centre
                rect.anchoredPosition = new Vector2(0f, -24f);
                rect.sizeDelta = new Vector2(760f, 110f);
                var background = banner.AddComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0.85f);
                background.raycastTarget = false;

                GameObject textObject = PUIElements.CreateUI(banner, "Text");
                var textRect = (RectTransform)textObject.transform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(14f, 8f);
                textRect.offsetMax = new Vector2(-14f, -8f);
                // LocText applies its style when it is enabled, so the style has to be in
                // place first: add the component to an inactive object, as PLib does.
                textObject.SetActive(false);
                var text = textObject.AddComponent<LocText>();
                text.key = string.Empty;
                text.textStyleSetting = PUITuning.Fonts.TextLightStyle;
                textObject.SetActive(true);
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.textWrappingMode = TMPro.TextWrappingModes.Normal;
                text.raycastTarget = false;
                text.SetText(message);

                banner.transform.SetAsLastSibling();
                s_banner = banner;

                if (seconds > 0f && Game.Instance != null)
                    Game.Instance.StartCoroutine(HideAfter(version, seconds, afterHide));
            }
            catch (Exception e)
            {
                Debug.LogWarning("OniExtract: could not show an on-screen message: " + e.Message);
            }
        }

        // The pause menu is drawn above the general overlay canvas, so a message parented
        // there ends up underneath the menu. The game opens its own pause-menu dialogs under
        // the pause screen's parent and lets them climb to the nearest canvas; start from the
        // same place so the banner shares the menu's canvas and can sit on top of it.
        private static GameObject BannerParent()
        {
            if (PauseScreen.Instance == null || PauseScreen.Instance.transform.parent == null)
                return PDialog.GetParentObject();
            Transform parent = PauseScreen.Instance.transform.parent;
            while (parent.GetComponent<Canvas>() == null && parent.parent != null)
                parent = parent.parent;
            return parent.gameObject;
        }

        // Realtime, because the game is paused while the pause menu is open.
        private static IEnumerator HideAfter(int version, float seconds, System.Action afterHide)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (version != s_bannerVersion)
                yield break;
            if (s_banner != null)
                UnityEngine.Object.Destroy(s_banner);
            s_banner = null;
            if (afterHide != null)
                afterHide();
        }
    }
}
