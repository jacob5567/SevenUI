using RoR2;
using TMPro;
using UnityEngine;

namespace SevenUI
{
    // The run info's right-hand text as on the design canvas: the run's difficulty by name in the accent, where the
    // game draws its icon (an Eclipse run shows its level there, "Eclipse (3)"), and under it the stage and the
    // monsters' level on one line. The game's stage and level labels stay in place, hidden, and the game keeps writing
    // them; this copies their text when it changes, so it is translated and agrees with them on host and client.
    //
    // The Karma icon, which the game shows beside the difficulty's icon when Karma is on for the run, is kept just left
    // of the text, whose width changes with it.
    internal sealed class RunInfoText : MonoBehaviour
    {
        private const string Separator = " · ";
        // The design canvas's 8px, in the run info parts' units.
        private const float KarmaGap = 8f * RunInfoUnits.PerCanvasPixel;

        public TMP_Text GameStage;
        public TMP_Text GameLevel;
        public TMP_Text Difficulty;
        public TMP_Text StageAndLevel;
        public RectTransform Karma;

        private string shownStage;
        private string shownLevel;
        private DifficultyIndex shownDifficulty = DifficultyIndex.Invalid;
        private TMP_FontAsset shownFont;
        private readonly Vector3[] corners = new Vector3[4];

        public string ShownDifficulty => shownDifficulty != DifficultyIndex.Invalid ? Difficulty.text : null;

        // An enum, two string and a font compare when nothing changed; the game writes its labels only when they
        // change.
        private void Update()
        {
            bool changed = false;
            // Chosen in the lobby, so fixed for the run in the game itself; read again in case anything changes it.
            DifficultyIndex difficulty = Run.instance ? Run.instance.selectedDifficulty : DifficultyIndex.Invalid;
            if (difficulty != shownDifficulty)
            {
                shownDifficulty = difficulty;
                DifficultyDef def = DifficultyCatalog.GetDifficultyDef(difficulty);
                Difficulty.text = def != null ? Language.GetString(def.nameToken) : "";
                changed = true;
            }
            string stage = GameStage ? GameStage.text : null;
            string level = GameLevel ? GameLevel.text : null;
            if (!string.Equals(stage, shownStage, System.StringComparison.Ordinal) ||
                !string.Equals(level, shownLevel, System.StringComparison.Ordinal))
            {
                shownStage = stage;
                shownLevel = level;
                StageAndLevel.text = string.IsNullOrEmpty(stage) || string.IsNullOrEmpty(level)
                    ? stage ?? level ?? ""
                    : stage + Separator + level;
                changed = true;
            }
            if (Difficulty.font != shownFont)
            {
                shownFont = Difficulty.font;
                changed = true;
            }
            if (changed)
            {
                PlaceKarma();
            }
        }

        // Centered on the text's height, a gap left of its wider line. The icon sits in another part of the run info,
        // so it is moved in world space.
        private void PlaceKarma()
        {
            if (!Karma) return;
            RectTransform holder = (RectTransform)transform;
            Rect area = holder.rect;
            Karma.GetWorldCorners(corners);
            Vector3 min = holder.InverseTransformPoint(corners[0]);
            Vector3 max = holder.InverseTransformPoint(corners[2]);
            float textWidth = Mathf.Max(Difficulty.preferredWidth, StageAndLevel.preferredWidth);
            Vector3 target = new Vector3(area.xMax - textWidth - KarmaGap - (max.x - min.x) / 2f, area.center.y,
                (min.z + max.z) / 2f);
            Karma.position += holder.TransformPoint(target) - holder.TransformPoint((min + max) / 2f);
        }
    }
}
