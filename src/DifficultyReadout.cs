using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;

namespace SevenUI
{
    // The mod's difficulty bar, in place of the game's scrolling segments: a thin bar filled by the progress through
    // the current difficulty, and that difficulty's name at its right, the bar taking the room the name leaves. It
    // reads what the game's DifficultyBarController shows, so it agrees with it on host and client.
    internal sealed class DifficultyReadout : MonoBehaviour
    {
        // Below this change in progress the fill isn't moved, so the layout isn't dirtied every frame.
        private const float FillEpsilon = 0.001f;

        public DifficultyBarController Bar;
        public RectTransform Trough;
        public RectTransform Fill;
        public TMP_Text Label;
        public float BarHeight;
        public float NameGap;
        // The most of the row the name may take; a longer name shrinks to fit (the later difficulties have long
        // names, "I'M COMING FOR YOU").
        public float MaxNameShare;

        private float shownProgress = -1f;
        private string shownName;
        private TMP_FontAsset shownFont;

        public string ShownName => shownName;
        public float ShownProgress => shownProgress;

        private void Update()
        {
            if (!Bar || !Run.instance || Bar.segmentDefs == null || Bar.segmentDefs.Length == 0) return;

            // The same position the game's bar scrolls to (DifficultyBarController.DoBarUpdates). ambientLevel comes
            // from the synced run timer, so a client's bar matches the host's.
            float scroll = Mathf.Max(0f, (Run.instance.ambientLevel - 1f) / Bar.levelsPerSegment);
            int last = Bar.segmentDefs.Length - 1;
            int index = Mathf.Min(Mathf.FloorToInt(scroll), last);
            // The last difficulty never ends, so its bar stays full.
            float progress = index == last ? 1f : scroll - index;

            if (Mathf.Abs(progress - shownProgress) > FillEpsilon)
            {
                shownProgress = progress;
                Fill.anchorMax = new Vector2(progress, 1f);
            }

            // The game's own label for the segment, so it is translated, and changed when the game changes it.
            string name = Bar.labels != null && index < Bar.labels.Length && Bar.labels[index]
                ? Bar.labels[index].text
                : null;
            // Refitted when the font changes too, since the font setting can change it.
            if (name != shownName || Label.font != shownFont)
            {
                shownName = name;
                shownFont = Label.font;
                Label.text = name ?? "";
                FitName();
            }
        }

        private void FitName()
        {
            float row = ((RectTransform)transform).rect.width;
            float maxWidth = row * MaxNameShare;
            Label.enableAutoSizing = false;
            Label.fontSize = Label.fontSizeMax;
            float width = Label.GetPreferredValues(Label.text, float.PositiveInfinity, float.PositiveInfinity).x;
            if (width > maxWidth)
            {
                Label.enableAutoSizing = true;
                width = maxWidth;
            }
            RectTransform name = (RectTransform)Label.transform;
            name.anchorMin = new Vector2(1f, 0f);
            name.anchorMax = Vector2.one;
            name.pivot = new Vector2(1f, 0.5f);
            name.anchoredPosition = Vector2.zero;
            name.sizeDelta = new Vector2(width, 0f);
            Trough.anchorMin = new Vector2(0f, 0.5f);
            Trough.anchorMax = new Vector2(1f, 0.5f);
            Trough.offsetMin = new Vector2(0f, -BarHeight / 2f);
            Trough.offsetMax = new Vector2(-(width + NameGap), BarHeight / 2f);
        }
    }
}
