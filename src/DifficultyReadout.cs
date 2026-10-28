using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;

namespace SevenUI
{
    // The mod's difficulty bar, in place of the game's scrolling segments: a thin bar filled by the progress through
    // the current difficulty, and that difficulty's name. It reads what the game's DifficultyBarController shows, so
    // it agrees with it on host and client.
    internal sealed class DifficultyReadout : MonoBehaviour
    {
        // Below this change in progress the fill isn't moved, so the layout isn't dirtied every frame.
        private const float FillEpsilon = 0.001f;

        public DifficultyBarController Bar;
        public RectTransform Fill;
        public TMP_Text Label;

        private float shownProgress = -1f;
        private string shownName;

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
            if (name != shownName)
            {
                shownName = name;
                Label.text = name ?? "";
            }
        }
    }
}
