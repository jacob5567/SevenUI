using RoR2;
using TMPro;
using UnityEngine;

namespace SevenUI
{
    // The run info's right-hand text as on the design canvas: the run's difficulty by name in the accent, where the
    // game draws its icon, and under it the stage and the monsters' level on one line. The game's stage and level
    // labels stay in place, hidden, and the game keeps writing them; this copies their text when it changes, so it is
    // translated and agrees with them on host and client.
    internal sealed class RunInfoText : MonoBehaviour
    {
        private const string Separator = " · ";

        public TMP_Text GameStage;
        public TMP_Text GameLevel;
        public TMP_Text Difficulty;
        public TMP_Text StageAndLevel;

        private string shownStage;
        private string shownLevel;
        private bool named;

        public string ShownDifficulty => named ? Difficulty.text : null;

        // Two string compares when nothing changed; the game writes its labels only when they change.
        private void Update()
        {
            if (!named && Run.instance)
            {
                // Chosen in the lobby and fixed for the run.
                DifficultyDef difficulty = DifficultyCatalog.GetDifficultyDef(Run.instance.selectedDifficulty);
                Difficulty.text = difficulty != null ? Language.GetString(difficulty.nameToken) : "";
                named = true;
            }
            string stage = GameStage ? GameStage.text : null;
            string level = GameLevel ? GameLevel.text : null;
            if (string.Equals(stage, shownStage, System.StringComparison.Ordinal) &&
                string.Equals(level, shownLevel, System.StringComparison.Ordinal)) return;
            shownStage = stage;
            shownLevel = level;
            StageAndLevel.text = string.IsNullOrEmpty(stage) || string.IsNullOrEmpty(level)
                ? stage ?? level ?? ""
                : stage + Separator + level;
        }
    }
}
