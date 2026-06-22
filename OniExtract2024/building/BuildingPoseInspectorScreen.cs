// In-game building pose inspector. A pause-screen button opens this screen. You choose a
// building from the built-in list (search box + scrollable list + prev/next), and the tool
// spawns that one building off-screen, lets you cycle animations and scrub frames, and shows
// the paste-ready line to add to BuildingPoseOverrides.Overrides. No in-game persistence —
// this only reveals the correct values; you add them to the source file and iterate.
//
// One building is loaded at a time, so a building that misbehaves on spawn just logs a
// (caught) error — you pick another and keep going; everything already written down is safe.
//
// Rendering goes through BuildingKanimRenderer (same pipeline as export) so what the
// inspector shows is pixel-for-pixel what ExportBuildingImages will produce.

using System;
using System.Collections.Generic;
using System.Linq;
using PeterHan.PLib.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OniExtract2024.building
{
    public static class BuildingPoseInspectorScreen
    {
        // --- static state (single open inspector at a time) ---

        private static BuildingDef s_def;
        private static GameObject s_tempBuilding;
        private static BuildingKanimRenderer s_renderer;
        private static KBatchedAnimController s_kbac;

        private static List<string> s_animNames;
        private static int s_animIndex;
        private static int s_frameIndex;

        // Chooser state.
        private static List<BuildingDef> s_defList;     // all renderable defs, sorted
        private static List<BuildingDef> s_filtered;     // current search result, in order
        private static int s_filteredPos = -1;           // index of s_def within s_filtered

        // UI element refs captured via OnRealize
        private static GameObject s_buildingNameLabelGO;
        private static GameObject s_counterLabelGO;
        private static GameObject s_animNameLabelGO;
        private static GameObject s_frameInfoLabelGO;
        private static GameObject s_pasteLineLabelGO;
        private static Image s_previewImage;
        private static Slider s_frameSlider;
        private static bool s_suppressSliderEvents;

        private static string PrefabId => s_def?.PrefabID ?? "(none)";

        // -------------------------------------------------------------------

        public static void Open()
        {
            Cleanup();

            if (Game.Instance == null)
            {
                Debug.LogWarning("OniExtract: Building pose inspector requires a loaded game.");
                return;
            }

            s_defList = Assets.BuildingDefs
                .Where(BuildingSpawnFilter.IsRenderable)
                .OrderBy(d => d.PrefabID, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Start at the world-selected building if it happens to be renderable, else the
            // first in the list. Selection is just a convenience — not required.
            var selectedDef = SelectTool.Instance?.selected?.GetComponent<Building>()?.Def;
            s_def = (selectedDef != null && s_defList.Contains(selectedDef))
                ? selectedDef
                : s_defList.FirstOrDefault();

            BuildAndShowDialog();

            // Rows are realized during Build/Show above; seed the filter and load.
            ApplyFilter("");
            if (s_def != null)
                SwitchTo(s_def);
        }

        private static void BuildAndShowDialog()
        {
            // Reset UI refs
            s_buildingNameLabelGO = null;
            s_counterLabelGO = null;
            s_animNameLabelGO = null;
            s_frameInfoLabelGO = null;
            s_pasteLineLabelGO = null;
            s_previewImage = null;
            s_frameSlider = null;
            s_suppressSliderEvents = false;

            var dialog = new PDialog("OniExtractPoseInspector")
            {
                Title = "Building Pose Inspector",
                SortKey = 200f,
                Size = new Vector2(560f, 680f),
                DialogClosed = (_) => Cleanup(),
            };
            dialog.AddButton("ok", "Close", (string)null);

            var body = dialog.Body;
            body.Direction = PanelDirection.Vertical;
            body.Spacing = 6;
            body.Margin = new RectOffset(8, 8, 4, 4);

            if (s_defList == null || s_defList.Count == 0)
            {
                body.AddChild(new PLabel("HintLabel")
                {
                    Text = "No renderable buildings found.",
                });
            }
            else
            {
                BuildChooser(body);

                // Currently-loaded building name
                var nameLabel = new PLabel("BuildingNameLabel") { Text = PrefabId };
                nameLabel.OnRealize += (go) => s_buildingNameLabelGO = go;
                body.AddChild(nameLabel);

                // Preview image panel — filled by OnRealize
                var previewPanel = new PPanel("PreviewPanel")
                {
                    BackColor = Color.black,
                };
                previewPanel.OnRealize += (go) =>
                {
                    // The PPanel already owns an Image for its BackColor; reuse it rather than
                    // AddComponent a second one (which returns null and NPEs). We render the
                    // building sprite into this same Image, preserving aspect.
                    var img = go.AddOrGet<Image>();
                    img.preserveAspect = true;
                    s_previewImage = img;

                    var rt = go.GetComponent<RectTransform>();
                    if (rt != null) rt.sizeDelta = new Vector2(260f, 260f);
                };
                body.AddChild(previewPanel);

                BuildAnimAndFrameRows(body);

                // Paste line
                body.AddChild(new PLabel("PasteHeader")
                {
                    Text = "Add to BuildingPoseOverrides.Overrides:",
                });

                var pasteLine = new PLabel("PasteLineLabel")
                {
                    Text = GetPasteLine(),
                    FlexSize = new Vector2(1f, 0f),
                };
                pasteLine.OnRealize += (go) => s_pasteLineLabelGO = go;
                body.AddChild(pasteLine);
            }

            var dialogGO = dialog.Build();

            // Attach a cleanup hook so that if the dialog is destroyed by any means
            // (not just the Close button) we still clean up the temp building + renderer.
            dialogGO.AddComponent<InspectorDialogCleaner>();

            dialogGO.GetComponent<KScreen>()?.Show(true);
        }

        // Search field, prev/next navigation, and a scrollable list of every renderable
        // building. Selecting any of them spawns it for inspection.
        private static void BuildChooser(PPanel body)
        {
            var search = new PTextField("BuildingSearch")
            {
                PlaceholderText = "search buildings…",
                Text = "",
                MinWidth = 240,
                FlexSize = new Vector2(1f, 0f),
            };
            search.OnTextChanged = (_, text) => ApplyFilter(text);
            body.AddChild(search);

            var navRow = new PPanel("NavRow")
            {
                Direction = PanelDirection.Horizontal,
                Spacing = 4,
                FlexSize = new Vector2(1f, 0f),
            };
            var prevB = new PButton("PrevBuilding") { Text = "◄ Building" };
            prevB.OnClick = (_) => Step(-1);
            navRow.AddChild(prevB);

            var counter = new PLabel("CounterLabel")
            {
                Text = "0 / 0",
                FlexSize = new Vector2(1f, 0f),
            };
            counter.OnRealize += (go) => s_counterLabelGO = go;
            navRow.AddChild(counter);

            var nextB = new PButton("NextBuilding") { Text = "Building ►" };
            nextB.OnClick = (_) => Step(1);
            navRow.AddChild(nextB);
            body.AddChild(navRow);

            // No building list: PLib's scroll pane couldn't be bounded reliably and grew the
            // dialog off-screen. Navigation is the search box (narrows the set) plus the
            // prev/next buttons above (step through the current match set).
        }

        private static void BuildAnimAndFrameRows(PPanel body)
        {
            // Anim row
            var animRow = new PPanel("AnimRow")
            {
                Direction = PanelDirection.Horizontal,
                Spacing = 4,
                FlexSize = new Vector2(1f, 0f),
            };

            var prevAnim = new PButton("PrevAnim") { Text = "◄ Anim" };
            prevAnim.OnClick = (_) => CycleAnim(-1);
            animRow.AddChild(prevAnim);

            var animNameLabel = new PLabel("AnimNameLabel")
            {
                Text = "—",
                FlexSize = new Vector2(1f, 0f),
            };
            animNameLabel.OnRealize += (go) => s_animNameLabelGO = go;
            animRow.AddChild(animNameLabel);

            var nextAnim = new PButton("NextAnim") { Text = "Anim ►" };
            nextAnim.OnClick = (_) => CycleAnim(1);
            animRow.AddChild(nextAnim);

            body.AddChild(animRow);

            // Frame row
            var frameRow = new PPanel("FrameRow")
            {
                Direction = PanelDirection.Horizontal,
                Spacing = 4,
                FlexSize = new Vector2(1f, 0f),
            };

            var prevFrame = new PButton("PrevFrame") { Text = "◄" };
            prevFrame.OnClick = (_) => StepFrame(-1);
            frameRow.AddChild(prevFrame);

            var frameSliderUI = new PSliderSingle("FrameSlider")
            {
                MinValue = 0f,
                MaxValue = 1f,
                InitialValue = 0f,
                FlexSize = new Vector2(1f, 0f),
            };
            frameSliderUI.OnValueChanged = (_, val) =>
            {
                if (!s_suppressSliderEvents)
                    OnSliderChanged(val);
            };
            frameSliderUI.OnRealize += (go) =>
                s_frameSlider = go.GetComponentInChildren<Slider>();
            frameRow.AddChild(frameSliderUI);

            var nextFrame = new PButton("NextFrame") { Text = "►" };
            nextFrame.OnClick = (_) => StepFrame(1);
            frameRow.AddChild(nextFrame);

            var frameInfo = new PLabel("FrameInfoLabel")
            {
                Text = "frame 0 / 0",
            };
            frameInfo.OnRealize += (go) => s_frameInfoLabelGO = go;
            frameRow.AddChild(frameInfo);

            body.AddChild(frameRow);
        }

        // -------------------------------------------------------------------
        // Chooser behaviour

        private static void ApplyFilter(string query)
        {
            if (s_defList == null) return;
            query = (query ?? "").Trim();

            s_filtered = new List<BuildingDef>();
            for (int i = 0; i < s_defList.Count; i++)
            {
                var def = s_defList[i];
                if (query.Length == 0
                    || def.PrefabID.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    s_filtered.Add(def);
            }

            s_filteredPos = s_def != null ? s_filtered.IndexOf(s_def) : -1;
            UpdateCounter();
        }

        private static void Step(int dir)
        {
            if (s_filtered == null || s_filtered.Count == 0) return;
            if (s_filteredPos < 0)
                s_filteredPos = dir > 0 ? 0 : s_filtered.Count - 1;
            else
                s_filteredPos = ((s_filteredPos + dir) % s_filtered.Count + s_filtered.Count) % s_filtered.Count;
            SwitchTo(s_filtered[s_filteredPos]);
        }

        // Unload the current building and load the given one. Keeps the dialog open.
        private static void SwitchTo(BuildingDef def)
        {
            if (def == null) return;
            UnloadBuilding();
            s_def = def;
            s_filteredPos = s_filtered != null ? s_filtered.IndexOf(def) : -1;
            SetLabelText(s_buildingNameLabelGO, PrefabId);
            UpdateCounter();
            InitBuilding();
        }

        private static void UpdateCounter()
        {
            int n = s_filtered?.Count ?? 0;
            int pos = s_filteredPos >= 0 ? s_filteredPos + 1 : 0;
            SetLabelText(s_counterLabelGO, pos + " / " + n);
        }

        // -------------------------------------------------------------------

        private static void InitBuilding()
        {
            if (s_def == null) return;

            int cell = Grid.PosToCell(Camera.main.transform.position);
            for (int i = 0; i < 15; i++) cell = Grid.CellDownLeft(cell);
            Vector3 spawnPos = Grid.CellToPos(cell);

            try
            {
                s_tempBuilding = s_def.Create(spawnPos, null,
                    new List<Tag> { SimHashes.Unobtanium.CreateTag() }, null, 100f, s_def.BuildingComplete);
            }
            catch (Exception e)
            {
                // OnSpawn exceptions are normally caught by the engine, but guard against a
                // hard throw in Create so one bad building can't take down the dialog.
                Debug.LogWarning("OniExtract: failed to spawn " + PrefabId + " for inspection: " + e.Message);
                UnloadBuilding();
                SetLabelText(s_buildingNameLabelGO, PrefabId + "  (failed to spawn — skip it)");
                return;
            }
            if (s_tempBuilding == null) return;

            s_kbac = s_tempBuilding.GetComponent<KBatchedAnimController>();
            if (s_kbac == null) return;

            // Build the selectable anim list from the group file.
            var rawNames = BuildingImageSnapshotter.GetAnimNames(s_kbac);
            s_animNames = rawNames != null
                ? rawNames
                    .Select(hs => hs.ToString())
                    .Where(n => !string.IsNullOrEmpty(n) && s_kbac.HasAnimation(n))
                    .ToList()
                : new List<string>();

            // Seed at the same anim the exporter would auto-pick.
            string autoAnim = BuildingImageSnapshotter.ChooseActiveAnim(s_kbac);
            s_animIndex = s_animNames.IndexOf(autoAnim);
            if (s_animIndex < 0) s_animIndex = 0;
            s_frameIndex = 0;

            var building = s_tempBuilding.GetComponent<Building>();
            int w = building?.Def.WidthInCells ?? 1;
            int h = building?.Def.HeightInCells ?? 1;

            s_renderer = new BuildingKanimRenderer();
            s_renderer.Init(w, h, s_tempBuilding.transform.GetPosition());

            ApplyPoseAndRender();
        }

        private static void ApplyPoseAndRender()
        {
            if (s_kbac == null || s_animNames == null || s_animNames.Count == 0) return;

            string anim = s_animNames[s_animIndex];
            s_kbac.Play(anim, KAnim.PlayMode.Paused);

            int numFrames = s_kbac.GetCurrentNumFrames();
            if (numFrames <= 0) numFrames = 1;
            s_frameIndex = Mathf.Clamp(s_frameIndex, 0, numFrames - 1);
            s_kbac.SetPositionPercent(BuildingPoseOverrides.PercentForFrame(s_frameIndex, numFrames));

            // Update slider range without triggering the value-changed callback.
            if (s_frameSlider != null)
            {
                s_suppressSliderEvents = true;
                s_frameSlider.minValue = 0f;
                s_frameSlider.maxValue = numFrames - 1;
                s_frameSlider.value = s_frameIndex;
                s_suppressSliderEvents = false;
            }

            SetLabelText(s_animNameLabelGO, anim);
            SetLabelText(s_frameInfoLabelGO, "frame " + s_frameIndex + " / " + (numFrames - 1));
            SetLabelText(s_pasteLineLabelGO, GetPasteLine());

            RenderPreview();
        }

        private static void RenderPreview()
        {
            if (s_renderer == null || s_tempBuilding == null || s_tempBuilding.IsNullOrDestroyed()) return;

            var kbacs = s_tempBuilding.GetComponentsInChildren<KBatchedAnimController>()
                .OrderBy(k => k.transform.position.z);

            CameraController.Instance.baseCamera.enabled = false;
            s_renderer.Render(kbacs);
            CameraController.Instance.baseCamera.enabled = true;

            if (s_previewImage != null)
            {
                Texture2D tex = s_renderer.ReadPixels();
                if (tex != null)
                {
                    // Discard old sprite texture before replacing.
                    if (s_previewImage.sprite != null)
                    {
                        UnityEngine.Object.Destroy(s_previewImage.sprite.texture);
                        s_previewImage.sprite = null;
                    }
                    s_previewImage.sprite = Sprite.Create(
                        tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
                }
            }
        }

        private static void CycleAnim(int dir)
        {
            if (s_animNames == null || s_animNames.Count == 0) return;
            s_frameIndex = 0;
            s_animIndex = ((s_animIndex + dir) % s_animNames.Count + s_animNames.Count) % s_animNames.Count;
            ApplyPoseAndRender();
        }

        private static void StepFrame(int dir)
        {
            if (s_kbac == null) return;
            int numFrames = s_kbac.GetCurrentNumFrames();
            if (numFrames <= 0) return;
            s_frameIndex = Mathf.Clamp(s_frameIndex + dir, 0, numFrames - 1);
            ApplyPoseAndRender();
        }

        private static void OnSliderChanged(float val)
        {
            if (s_kbac == null) return;
            int numFrames = s_kbac.GetCurrentNumFrames();
            if (numFrames <= 0) return;
            int newFrame = Mathf.RoundToInt(val);
            if (newFrame == s_frameIndex) return;
            s_frameIndex = Mathf.Clamp(newFrame, 0, numFrames - 1);
            ApplyPoseAndRender();
        }

        private static string GetPasteLine()
        {
            if (s_def == null) return "";
            string anim = (s_animNames != null && s_animIndex >= 0 && s_animIndex < s_animNames.Count)
                ? s_animNames[s_animIndex] : "???";
            return "{ \"" + PrefabId + "\", new BuildingPose(\"" + anim + "\", " + s_frameIndex + ") },";
        }

        private static void SetLabelText(GameObject go, string text)
        {
            if (go == null) return;
            go.GetComponentInChildren<LocText>()?.SetText(text);
        }

        // Release the spawned building + renderer + preview texture, but keep the dialog
        // (and its captured UI refs) alive so another building can be loaded.
        private static void UnloadBuilding()
        {
            if (s_tempBuilding != null && !s_tempBuilding.IsNullOrDestroyed())
            {
                Util.KDestroyGameObject(s_tempBuilding);
                s_tempBuilding = null;
            }
            s_renderer?.Cleanup();
            s_renderer = null;
            s_kbac = null;
            s_animNames = null;

            if (s_previewImage != null && s_previewImage.sprite != null)
            {
                UnityEngine.Object.Destroy(s_previewImage.sprite.texture);
                s_previewImage.sprite = null;
            }
        }

        // Called by InspectorDialogCleaner.OnCleanUp() when the dialog GO is destroyed,
        // ensuring everything is released regardless of how the dialog was closed.
        internal static void Cleanup()
        {
            UnloadBuilding();

            s_previewImage = null;
            s_def = null;
            s_defList = null;
            s_filtered = null;
            s_filteredPos = -1;

            s_buildingNameLabelGO = null;
            s_counterLabelGO = null;
            s_animNameLabelGO = null;
            s_frameInfoLabelGO = null;
            s_pasteLineLabelGO = null;
            s_frameSlider = null;
        }
    }

    // Attached to the dialog GameObject. OnCleanUp fires when the GO is destroyed (any cause).
    internal class InspectorDialogCleaner : KMonoBehaviour
    {
        protected override void OnCleanUp() => BuildingPoseInspectorScreen.Cleanup();
    }
}
