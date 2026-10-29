using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the run info in the top right as one flat panel, laid out as on the design canvas: the timer at the left;
    // the difficulty's name, and the stage and level under it, at the right, with the Karma icon beside them; and below
    // them a thin accent bar with the current difficulty segment's name, in place of the game's scrolling segments. The
    // column it heads, which holds the objective panel, is widened to 300 of the canvas's pixels. The game's objects
    // keep running; only their look and places change.
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
        private const string DifficultyIconPath = "SetDifficultyPanel/DifficultyIcon";
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
        public const string BackgroundName = "SevenUIRunInfoPanel";
        private const string ReadoutName = "SevenUIDifficulty";
        private const string RunTextName = "SevenUIRunText";

        // The design canvas's sizes, in its pixels, scaled to the run info's units.
        private const float Canvas = RunInfoUnits.PerCanvasPixel;
        // Narrower than the canvas's 360px, which read as too wide in game; the game's own is about 250.
        private const float ColumnWidth = 300f * Canvas;
        private const float TimerFontSize = 34f * Canvas;
        private const float DifficultyFontSize = 15f * Canvas;
        private const float StageFontSize = 13f * Canvas;
        // How far the rows' parts stay inside the panel's sides.
        private const float RowInset = 14f * Canvas;
        private const float BarHeight = 6f * Canvas;
        private const float BarNameGap = 10f * Canvas;
        private const float BarNameFontSize = 12f * Canvas;
        // The game's timer format sets every character, the colon too, 0.5em apart (<mspace>), which leaves Barlow
        // Condensed's narrow digits loose; this pulls them a little closer, in hundredths of an em.
        private const float TimerTracking = -4f;
        // Above and below the rows, in the run info's units.
        private const float PanelPadding = 6f;
        // The most of the bar's row the segment's name may take before it shrinks.
        private const float MaxNameShare = 0.6f;
        private const float NameMinFontSize = 4f;

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
            // The separate dark boxes, the scrolling gears and the outline give way to one flat panel, and the
            // difficulty's icon to its name.
            Disable<Image>(panel, TimerPanelPath);
            Disable<RawImage>(panel, TimerGearPath);
            Disable<Image>(panel, SetDifficultyPanelPath);
            Disable<Image>(panel, DifficultyIconPath);
            Disable<Image>(panel, OutlinePath);
            PlaceRows(panel, (RectTransform)bar.transform);
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
                timer.alignment = TextAlignmentOptions.Left;
            }
            StyleRunText(panel);
            StyleDifficultyBar(bar);
        }

        // The column takes its new width, growing to the left so it keeps the game's right edge; the timer then
        // goes to its left side, the stage and level to its right, and the difficulty bar across it. Each part keeps
        // its height and the game's place in its row. The parts are drawn larger than the panel (1.6 times, in the
        // game's prefab), and the canvas's sizes are in the parts' own units, so they are scaled into the panel's.
        private void PlaceRows(Transform panel, RectTransform difficultyBar)
        {
            RectTransform column = panel.Find(RightInfoBarPath) as RectTransform;
            RectTransform timer = panel.Find(TimerPanelPath) as RectTransform;
            RectTransform stage = panel.Find(StageAndLevelPath) as RectTransform;
            if (!column || !timer || !stage || recorder.HasRect(column)) return;

            Vector2 range = XRange(panel, column);
            float right = range.y;
            float left = Mathf.Min(range.x, right - ColumnWidth * column.localScale.x);
            SetXRange(panel, column, left, right);

            Vector2 timerRange = XRange(panel, timer);
            float timerLeft = left + RowInset * timer.localScale.x;
            SetXRange(panel, timer, timerLeft, timerLeft + (timerRange.y - timerRange.x));
            SetXRange(panel, stage, (left + right) / 2f, right - RowInset * stage.localScale.x);
            SetXRange(panel, difficultyBar, left, right);
        }

        // The rect's left and right sides in the panel's units.
        private Vector2 XRange(Transform panel, RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            return new Vector2(panel.InverseTransformPoint(corners[0]).x, panel.InverseTransformPoint(corners[2]).x);
        }

        // For a rect whose parent is the panel; its size is in its own units, its position in the panel's.
        private void SetXRange(Transform panel, RectTransform rect, float left, float right)
        {
            if (rect.localScale.x <= 0f) return;
            recorder.RecordRect(rect);
            Vector2 range = XRange(panel, rect);
            rect.sizeDelta += new Vector2((right - left - (range.y - range.x)) / rect.localScale.x, 0f);
            range = XRange(panel, rect);
            rect.anchoredPosition += new Vector2(left - range.x, 0f);
        }

        // The game's stage and level labels are hidden and copied into two lines of the mod's own (RunInfoText).
        private void StyleRunText(Transform panel)
        {
            Transform stageRoot = panel.Find(StageAndLevelPath);
            TMP_Text stage = Find<TMP_Text>(panel, StageTextPath);
            TMP_Text level = Find<TMP_Text>(panel, LevelTextPath);
            if (!stageRoot || !stage || !level) return;
            recorder.Disable(stage);
            recorder.Disable(level);
            if (stageRoot.Find(RunTextName)) return;

            GameObject holder = NewUIObject(RunTextName, stageRoot);
            Stretch((RectTransform)holder.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RunInfoText text = holder.AddComponent<RunInfoText>();
            text.GameStage = stage;
            text.GameLevel = level;
            text.Difficulty = AddLabel(holder.transform, "Difficulty", new Vector2(0f, 0.5f), Vector2.one, stage);
            text.Difficulty.fontSize = DifficultyFontSize;
            text.Difficulty.fontStyle = FontStyles.Bold;
            text.Difficulty.color = Palette.Accent;
            text.StageAndLevel = AddLabel(holder.transform, "StageAndLevel", Vector2.zero, new Vector2(1f, 0.5f), stage);
            text.StageAndLevel.fontSize = StageFontSize;
            text.StageAndLevel.fontStyle = FontStyles.Normal;
            text.StageAndLevel.color = Palette.HeaderText;
            KarmaIcon karma = panel.GetComponentInChildren<KarmaIcon>(true);
            if (karma)
            {
                text.Karma = (RectTransform)karma.transform;
                recorder.RecordRect(text.Karma);
            }
            recorder.Add(holder);
        }

        // A right-aligned line in the given part of its parent, in the game label's font so the font swap gives it
        // Barlow like the rest of the HUD.
        private static TMP_Text AddLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, TMP_Text fontFrom)
        {
            GameObject line = NewUIObject(name, parent);
            Stretch((RectTransform)line.transform, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            TextMeshProUGUI label = line.AddComponent<TextMeshProUGUI>();
            label.font = fontFrom.font;
            label.fontSharedMaterial = fontFrom.fontSharedMaterial;
            label.alignment = TextAlignmentOptions.Right;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
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
                Vector2 range = XRange(panel, bar);
                min.x = range.x;
                max.x = range.y;
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
            Stretch((RectTransform)readout.transform, Vector2.zero, Vector2.one, new Vector2(RowInset, 0f),
                new Vector2(-RowInset, 0f));

            GameObject trough = NewUIObject("Trough", readout.transform);
            Image troughImage = trough.AddComponent<Image>();
            troughImage.color = Palette.Trough;
            troughImage.raycastTarget = false;

            GameObject fill = NewUIObject("Fill", trough.transform);
            RectTransform fillRect = (RectTransform)fill.transform;
            Stretch(fillRect, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = Palette.Accent;
            fillImage.raycastTarget = false;

            // DifficultyReadout places the name and the trough once it knows the name.
            GameObject name = NewUIObject("Name", readout.transform);
            TextMeshProUGUI label = name.AddComponent<TextMeshProUGUI>();
            // Starts in the game's font, so the font swap gives it Barlow like the rest of the HUD.
            TMP_Text gameLabel = bar.labels != null && bar.labels.Length > 0 ? bar.labels[0] : null;
            if (gameLabel)
            {
                label.font = gameLabel.font;
                label.fontSharedMaterial = gameLabel.fontSharedMaterial;
            }
            HeaderStyle.Apply(label);
            label.fontSize = BarNameFontSize;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.fontSizeMin = NameMinFontSize;
            label.fontSizeMax = BarNameFontSize;
            label.raycastTarget = false;

            DifficultyReadout display = readout.AddComponent<DifficultyReadout>();
            display.Bar = bar;
            display.Trough = (RectTransform)trough.transform;
            display.Fill = fillRect;
            display.Label = label;
            display.BarHeight = BarHeight;
            display.NameGap = BarNameGap;
            display.MaxNameShare = MaxNameShare;
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
            foreach (RunInfoText text in cluster.GetComponentsInChildren<RunInfoText>(true))
            {
                output.AppendLine($"  run text: '{text.ShownDifficulty}', '{(text.StageAndLevel ? text.StageAndLevel.text : "")}'");
            }
        }
    }
}
