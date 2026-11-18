using System.Collections.Generic;
using System.Reflection;
using System.Text;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // One countdown panel (the moon's escape, a Winnowing trial, Champ Rush's doom timer) as a flat panel: the game's
    // own label, timer and images stay in place, switched off, and the game keeps writing the time into its timer text;
    // the mod's panel draws a spaced-capital label, the time as "02:05" and a thin bar under it that drains with the
    // time left.
    internal sealed class CountdownPanel
    {
        public const string HolderName = "SevenUICountdown";
        // In the top cluster's units, which match the design canvas's pixels.
        public const float Width = 200f;
        public const float Height = 80f;
        private const float SidePadding = 14f;
        private const float LabelTop = 8f;
        private const float LabelHeight = 14f;
        private const float TimeTop = 24f;
        private const float TimeHeight = 38f;
        private const float TimeFontSize = 34f;
        private const float BarTop = 68f;
        private const float BarHeight = 4f;
        // The bar and the time turn red at this many seconds left.
        public const float LowSeconds = 10f;
        private const string FieldName = "_totalDuration";

        private static readonly FieldInfo DoomTotal = typeof(DoomTimer).GetField(FieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly RectTransform root;
        private readonly TimerText timer;
        private readonly TrialTimerController trial;
        private readonly List<TMP_Text> gameLabels = new List<TMP_Text>();
        private readonly List<AnimateFontSize> animations = new List<AnimateFontSize>();
        private readonly StyleRecorder recorder;
        private readonly TMP_Text label;
        private readonly TMP_Text time;
        private readonly RectTransform fill;
        private readonly Image fillImage;
        private readonly StringBuilder builder = new StringBuilder();
        private double total;
        private double shownSeconds = double.NaN;
        private TimerStringFormatter shownFormat;
        private string shownLabel;
        private float shownShare = -1f;

        private CountdownPanel(RectTransform root, TimerText timer, StyleRecorder recorder, TMP_Text label, TMP_Text time,
            RectTransform fill, Image fillImage)
        {
            this.root = root;
            this.timer = timer;
            this.recorder = recorder;
            this.label = label;
            this.time = time;
            this.fill = fill;
            this.fillImage = fillImage;
            trial = timer.GetComponent<TrialTimerController>();
        }

        public bool Alive => root && timer && time;
        public RectTransform Root => root;
        public double Total => total;
        public string ShownText => time ? time.text : "";
        // The tests read what the panel draws.
        public TMP_Text Label => label;
        public TMP_Text Time => time;
        public RectTransform Fill => fill;
        public Image FillImage => fillImage;

        // Null when the panel has no text for the timer to write, which the mod can't read the time from.
        public static CountdownPanel Style(RectTransform root, TimerText timer, StyleRecorder recorder)
        {
            TMP_Text gameTime = timer.targetLabel;
            if (!gameTime) return null;

            List<Graphic> graphics = new List<Graphic>();
            root.GetComponentsInChildren(true, graphics);
            List<TMP_Text> labels = new List<TMP_Text>();
            foreach (Graphic graphic in graphics)
            {
                recorder.Disable(graphic);
                if (graphic is TMP_Text text && text != gameTime)
                {
                    labels.Add(text);
                }
            }

            // The panel's own size and place, out of its parent's layout, which would otherwise size it.
            recorder.RecordRect(root);
            recorder.SetLocalScale(root, Vector3.one);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(Width, Height);
            recorder.Add(root.gameObject.AddComponent<LayoutIgnorer>());
            // The game types the panel's texts out, turning each label on as its first letter shows and setting it
            // every frame, which would draw the game's texts over these; paused while restyled, the restore resumes it.
            foreach (TypewriteTextController typewriter in root.GetComponentsInChildren<TypewriteTextController>(true))
            {
                recorder.Disable(typewriter);
            }
            foreach (ContentSizeFitter fitter in root.GetComponents<ContentSizeFitter>())
            {
                recorder.Disable(fitter);
            }
            foreach (LayoutGroup group in root.GetComponents<LayoutGroup>())
            {
                recorder.Disable(group);
            }

            // The game's timer writes its text through the format, so the format is where the text becomes mm:ss.
            TimerStringFormatter original = timer.format;
            recorder.OnRestore(() =>
            {
                if (!timer) return;
                timer.format = original;
            });
            timer.format = TightTimerFormat.MinutesSeconds();

            RectTransform holder = UiFactory.NewObject(HolderName, root);
            UiFactory.Stretch(holder);
            holder.SetAsFirstSibling();
            recorder.Add(holder.gameObject);
            UiFactory.Stretch(UiFactory.NewImage("Background", holder, Palette.Panel).rectTransform);

            TMP_Text label = UiFactory.NewText("Label", holder, gameTime, HeaderStyle.FontSize, Palette.HeaderText);
            // The game's time may already be in Barlow when the panel is restyled again.
            HudFontSwapper.StartInGameFont(label);
            HeaderStyle.Apply(label);
            label.alignment = TextAlignmentOptions.Center;
            UiFactory.KeepOnOneLine(label);
            Row(label.rectTransform, LabelTop, LabelHeight, 0f);

            TMP_Text time = UiFactory.NewText("Time", holder, gameTime, TimeFontSize, Palette.Text);
            HudFontSwapper.StartInGameFont(time);
            time.alignment = TextAlignmentOptions.Center;
            UiFactory.KeepOnOneLine(time);
            time.gameObject.AddComponent<CondensedText>();
            Row(time.rectTransform, TimeTop, TimeHeight, 0f);

            Image track = UiFactory.NewImage("Track", holder, Palette.Trough);
            Row(track.rectTransform, BarTop, BarHeight, SidePadding);
            Image fillImage = UiFactory.NewImage("Fill", track.transform, Palette.Accent);
            RectTransform fill = fillImage.rectTransform;
            UiFactory.Stretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CountdownPanel panel = new CountdownPanel(root, timer, recorder, label, time, fill, fillImage);
            panel.gameLabels.AddRange(labels);
            root.GetComponentsInChildren(true, panel.animations);
            panel.total = OwnerDuration(panel.trial);
            panel.Update();
            return panel;
        }

        // A row across the panel, given from its top, inset from the sides.
        private static void Row(RectTransform rect, float top, float height, float inset)
        {
            UiFactory.Stretch(rect, new Vector2(0f, 1f), Vector2.one, new Vector2(inset, -(top + height)),
                new Vector2(-inset, -top));
        }

        // How long the countdown was set for, read from what owns it (none of these calls changes anything), or 0 when
        // it can't be found: the largest time seen then stands in.
        private static float OwnerDuration(TrialTimerController trial)
        {
            // Champ Rush's doom timer is the only one in its stage, and its duration is synced to clients. Its panel
            // can be the trial's, so it is asked first.
            DoomTimer doom = Object.FindObjectOfType<DoomTimer>();
            if (doom && DoomTotal != null && DoomTotal.GetValue(doom) is float doomTotal) return doomTotal;
            EscapeSequenceController escape = Object.FindObjectOfType<EscapeSequenceController>();
            if (escape) return escape.countdownDuration;
            // The trials' share of the run: what the game scales the countdown's sound by.
            return trial ? DLC4TrialSelector.totalAllotedSecondsForTrials : 0f;
        }

        // Every frame: two compares when nothing changed. The trial's timer sets the colors and font size of its own
        // (hidden) text and images every frame; its warning colors are copied, its font size changes are turned off.
        public void Update()
        {
            foreach (AnimateFontSize animation in animations)
            {
                if (animation && animation.enabled)
                {
                    recorder.Disable(animation);
                }
            }

            double seconds = timer.seconds;
            if (seconds > total)
            {
                total = seconds;
            }
            TimerStringFormatter format = timer.format;
            double whole = System.Math.Floor(seconds);
            if (whole != shownSeconds || format != shownFormat)
            {
                shownSeconds = whole;
                shownFormat = format;
                builder.Clear();
                if (format)
                {
                    format.AppendToStringBuilder(seconds, builder);
                }
                time.text = builder.ToString();
            }

            string game = GameLabel();
            if (!string.Equals(game, shownLabel, System.StringComparison.Ordinal))
            {
                shownLabel = game;
                label.text = game;
            }

            float share = total > 0.0 ? Mathf.Clamp01((float)(seconds / total)) : 0f;
            if (!Mathf.Approximately(share, shownShare))
            {
                shownShare = share;
                fill.anchorMax = new Vector2(share, 1f);
            }
            bool low = total > 0.0 && seconds <= LowSeconds;
            Color barColor = low ? Palette.LowHealth : Palette.Accent;
            if (fillImage.color != barColor)
            {
                fillImage.color = barColor;
            }
            // The trial's own warning colors (red, then flashing) from its warning time on.
            bool warning = trial && seconds <= trial.warningThreshold;
            Color timeColor = warning ? timer.targetLabel.color : low ? Palette.LowHealth : Palette.Text;
            if (time.color != timeColor)
            {
                time.color = timeColor;
            }
        }

        // The first text the game's panel shows besides the time.
        private string GameLabel()
        {
            foreach (TMP_Text text in gameLabels)
            {
                if (text && !string.IsNullOrWhiteSpace(text.text)) return text.text;
            }
            return "";
        }
    }
}
