using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the run info in the top right (timer, stage and level, difficulty icon and difficulty bar) as one flat
    // panel, with the timer first in its row, and the difficulty shown as a thin accent bar and the difficulty's name
    // in place of the game's scrolling segments. The game's objects keep running; only their look and order change.
    internal sealed class RunInfoStyler
    {
        private const string UpperRightClusterPath = "MainContainer/MainUIArea/SpringCanvas/UpperRightCluster";
        // Vanilla object names under the run info panel (ClassicRunInfoHudPanel).
        private const string TimerPanelPath = "TimerPanel";
        private const string TimerGearPath = "TimerPanel/Wormgear";
        private const string TimerTextPath = "TimerPanel/TimerText";
        private const string StageTextPath = "StageAndLevelRoot/StageCountRoot/Stage Text";
        private const string LevelTextPath = "StageAndLevelRoot/AmbientLevelRoot/Level Text";
        private const string SetDifficultyPanelPath = "SetDifficultyPanel";
        private const string StageAndLevelPath = "StageAndLevelRoot";
        private const string OutlinePath = "OutlineImage";
        private const string RightInfoBarPath = "RightInfoBar";
        // Under DifficultyBar.
        private const string BarGearPath = "Wormgear";
        private const string ScrollViewPath = "Scroll View";
        private const string ScrollBackdropPath = "Scroll View/Backdrop";
        private const string ViewportPath = "Scroll View/Viewport";
        private const string MarkerBackdropPath = "Marker, Backdrop";
        private const string MarkerPath = "Marker, Backdrop/Marker";
        private const string BackgroundName = "SevenUIRunInfoPanel";
        private const string ReadoutName = "SevenUIDifficulty";

        // In HUD canvas units.
        private const float TimerFontSize = 22f;
        // The game's timer format sets every character, the colon too, 0.5em apart (<mspace>), which leaves Barlow
        // Condensed's narrow digits loose; this pulls them a little closer, in hundredths of an em.
        private const float TimerTracking = -4f;
        private const float PanelPadding = 6f;
        // How far the top row's parts stay inside the panel's sides. The stage and level are right-aligned in their
        // box, which the swap puts at the panel's right edge.
        private const float RowInset = 8f;
        private const float BarInset = 8f;
        private const float BarHeight = 4f;
        // The bar takes the left part of the difficulty row, the name the rest.
        private const float BarShare = 0.55f;
        private const float BarNameGap = 8f;
        private const float NameMinFontSize = 6f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<DifficultyBarController> bars = new List<DifficultyBarController>();
        private readonly Vector3[] corners = new Vector3[4];
        // What the panel background covers; the difficulty bar is added by its own transform. Not every child: the
        // difficulty tutorial popup is one, and sits well outside the panel.
        private static readonly string[] CoveredPaths = { TimerPanelPath, SetDifficultyPanelPath, StageAndLevelPath };

        public RunInfoStyler(Transform root)
        {
            this.root = root;
        }

        public void Apply()
        {
            Transform cluster = root.Find(UpperRightClusterPath);
            if (!cluster) return;
            cluster.GetComponentsInChildren(true, bars);
            foreach (DifficultyBarController bar in bars)
            {
                // The difficulty bar's parent is the run info panel; other game modes' panels share its layout.
                StylePanel(bar.transform.parent, bar);
            }
            bars.Clear();
        }

        public void Restore()
        {
            recorder.Restore();
        }

        private void StylePanel(Transform panel, DifficultyBarController bar)
        {
            // The separate dark boxes, the scrolling gears and the outline give way to one flat panel.
            Disable<Image>(panel, TimerPanelPath);
            Disable<RawImage>(panel, TimerGearPath);
            Disable<Image>(panel, SetDifficultyPanelPath);
            Disable<Image>(panel, OutlinePath);
            SwapTopRow(panel);
            Transform background = panel.Find(BackgroundName);
            if (!background)
            {
                background = AddBackground(panel);
            }
            FitBackground(panel, (RectTransform)background, (RectTransform)bar.transform);

            TMP_Text timer = Find<TMP_Text>(panel, TimerTextPath);
            if (timer)
            {
                recorder.Record(timer);
                timer.fontSize = TimerFontSize;
                timer.characterSpacing = TimerTracking;
                timer.color = Palette.Text;
            }
            // The game draws these in bold; the canvas uses the regular face for secondary text.
            foreach (string path in new[] { StageTextPath, LevelTextPath })
            {
                TMP_Text label = Find<TMP_Text>(panel, path);
                if (!label) continue;
                recorder.Record(label);
                label.fontStyle &= ~FontStyles.Bold;
            }

            StyleDifficultyBar(bar);
        }

        private void StyleDifficultyBar(DifficultyBarController bar)
        {
            Transform transform = bar.transform;
            Disable<RawImage>(transform, BarGearPath);
            Disable<Image>(transform, ScrollViewPath);
            Disable<Image>(transform, ScrollBackdropPath);
            Disable<Image>(transform, MarkerBackdropPath);
            Disable<Image>(transform, MarkerPath);
            // The segments themselves are hidden as a group: the game turns each segment's image and label on and
            // off as it scrolls, which would undo turning them off one by one.
            Transform viewport = transform.Find(ViewportPath);
            if (viewport && !viewport.GetComponent<CanvasGroup>())
            {
                CanvasGroup group = viewport.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                recorder.Add(group);
            }
            if (!transform.Find(ReadoutName))
            {
                AddReadout(bar);
            }
        }

        // The design canvas reads timer first: the game's top row (stage and level, difficulty icon, timer) is swapped
        // left to right about its own middle, so it still lines up with the panels below. Each part keeps its size.
        private void SwapTopRow(Transform panel)
        {
            RectTransform first = panel.Find(CoveredPaths[0]) as RectTransform;
            if (!first || recorder.HasPosition(first)) return;

            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (string path in CoveredPaths)
            {
                if (!(panel.Find(path) is RectTransform rect)) continue;
                rect.GetWorldCorners(corners);
                min = Mathf.Min(min, panel.InverseTransformPoint(corners[0]).x);
                max = Mathf.Max(max, panel.InverseTransformPoint(corners[2]).x);
            }
            // The panel's sides are the panels' below it, when there are any (see FitBackground).
            float left = min;
            float right = max;
            if (panel.Find(RightInfoBarPath) is RectTransform bar)
            {
                bar.GetWorldCorners(corners);
                left = panel.InverseTransformPoint(corners[0]).x;
                right = panel.InverseTransformPoint(corners[2]).x;
            }
            foreach (string path in CoveredPaths)
            {
                if (!(panel.Find(path) is RectTransform rect)) continue;
                rect.GetWorldCorners(corners);
                float center = (panel.InverseTransformPoint(corners[0]).x + panel.InverseTransformPoint(corners[2]).x) / 2f;
                // Moving a part's center from c to (min + max - c) mirrors it within the row; then it is kept inside
                // the row's inset.
                float half = (panel.InverseTransformPoint(corners[2]).x - panel.InverseTransformPoint(corners[0]).x) / 2f;
                float mirrored = min + max - center;
                mirrored = Mathf.Clamp(mirrored, left + RowInset + half, Mathf.Max(left + RowInset + half, right - RowInset - half));
                float shift = mirrored - center;
                recorder.SetAnchoredPosition(rect, rect.anchoredPosition + new Vector2(shift, 0f));
            }
        }

        private Transform AddBackground(Transform panel)
        {
            GameObject background = NewUIObject(BackgroundName, panel);
            // First child, so it draws behind the rest of the run info.
            background.transform.SetAsFirstSibling();
            Image image = background.AddComponent<Image>();
            image.color = Palette.Panel;
            image.raycastTarget = false;
            recorder.Add(background);
            return background.transform;
        }

        // Covers the timer, stage, icon and difficulty bar, and lines up with the panels below them.
        private void FitBackground(Transform panel, RectTransform background, RectTransform difficultyBar)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            Cover(panel, difficultyBar, ref min, ref max);
            foreach (string path in CoveredPaths)
            {
                if (panel.Find(path) is RectTransform rect && rect.gameObject.activeSelf)
                {
                    Cover(panel, rect, ref min, ref max);
                }
            }

            RectTransform bar = panel.Find(RightInfoBarPath) as RectTransform;
            if (bar)
            {
                bar.GetWorldCorners(corners);
                min.x = panel.InverseTransformPoint(corners[0]).x;
                max.x = panel.InverseTransformPoint(corners[2]).x;
            }
            else
            {
                min.x -= PanelPadding;
                max.x += PanelPadding;
            }
            min.y -= PanelPadding;
            max.y += PanelPadding;

            background.anchorMin = background.anchorMax = background.pivot = new Vector2(0.5f, 0.5f);
            background.localPosition = (min + max) / 2f;
            background.sizeDelta = max - min;
        }

        private void Cover(Transform panel, RectTransform rect, ref Vector2 min, ref Vector2 max)
        {
            rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 local = panel.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
        }

        private void AddReadout(DifficultyBarController bar)
        {
            GameObject readout = NewUIObject(ReadoutName, bar.transform);
            Stretch((RectTransform)readout.transform, Vector2.zero, Vector2.one, new Vector2(BarInset, 0f),
                new Vector2(-BarInset, 0f));

            GameObject trough = NewUIObject("Trough", readout.transform);
            RectTransform troughRect = (RectTransform)trough.transform;
            Stretch(troughRect, new Vector2(0f, 0.5f), new Vector2(BarShare, 0.5f), new Vector2(0f, -BarHeight / 2f),
                new Vector2(-BarNameGap / 2f, BarHeight / 2f));
            Image troughImage = trough.AddComponent<Image>();
            troughImage.color = Palette.Trough;
            troughImage.raycastTarget = false;

            GameObject fill = NewUIObject("Fill", trough.transform);
            RectTransform fillRect = (RectTransform)fill.transform;
            Stretch(fillRect, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = Palette.Accent;
            fillImage.raycastTarget = false;

            GameObject name = NewUIObject("Name", readout.transform);
            Stretch((RectTransform)name.transform, new Vector2(BarShare, 0f), Vector2.one, new Vector2(BarNameGap / 2f, 0f),
                Vector2.zero);
            TextMeshProUGUI label = name.AddComponent<TextMeshProUGUI>();
            // Starts in the game's font, so the font swap gives it Barlow like the rest of the HUD.
            TMP_Text gameLabel = bar.labels != null && bar.labels.Length > 0 ? bar.labels[0] : null;
            if (gameLabel)
            {
                label.font = gameLabel.font;
                label.fontSharedMaterial = gameLabel.fontSharedMaterial;
            }
            HeaderStyle.Apply(label);
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            // The later difficulties have long names ("I'M COMING FOR YOU"), so they shrink to fit.
            label.enableAutoSizing = true;
            label.fontSizeMin = NameMinFontSize;
            label.fontSizeMax = HeaderStyle.FontSize;
            label.raycastTarget = false;

            DifficultyReadout display = readout.AddComponent<DifficultyReadout>();
            display.Bar = bar;
            display.Fill = fillRect;
            display.Label = label;
            recorder.Add(readout);
        }

        private static GameObject NewUIObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            return created;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void Disable<T>(Transform parent, string path) where T : Behaviour
        {
            recorder.Disable(Find<T>(parent, path));
        }

        private static T Find<T>(Transform parent, string path) where T : Component
        {
            Transform found = parent.Find(path);
            return found ? found.GetComponent<T>() : null;
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled run info values: {recorder.Count}");
            Transform cluster = root.Find(UpperRightClusterPath);
            if (!cluster) return;
            foreach (DifficultyReadout readout in cluster.GetComponentsInChildren<DifficultyReadout>(true))
            {
                output.AppendLine($"  difficulty readout: '{readout.ShownName}' at {readout.ShownProgress:P0}");
            }
        }
    }
}
