using System.Text.RegularExpressions;
using RoR2;
using TMPro;
using UnityEngine;

namespace SevenUI
{
    // The run info's right-hand text as on the design canvas: the run's difficulty by name in the accent, where the
    // game draws its icon (an Eclipse run shows its level there, "Eclipse 3" without the game's brackets), and under it
    // the stage and the monsters' level on one line. The game's stage and level labels stay in place, hidden, and the
    // game keeps writing them; this copies their text when it changes, so it is translated and agrees with them on host
    // and client.
    internal sealed class RunInfoText : MonoBehaviour
    {
        private const string Separator = " · ";
        // A level in brackets at the end of a difficulty's name, as in the Eclipse levels' "Eclipse (3)".
        private static readonly Regex BracketedLevel = new Regex(@"\s*\((?<level>\d+)\)\s*$");

        public TMP_Text GameStage;
        public TMP_Text GameLevel;
        public TMP_Text Difficulty;
        public TMP_Text StageAndLevel;

        private string shownStage;
        private string shownLevel;
        private DifficultyIndex shownDifficulty = DifficultyIndex.Invalid;

        public string ShownDifficulty => shownDifficulty != DifficultyIndex.Invalid ? Difficulty.text : null;

        // An enum and two string compares when nothing changed; the game writes its labels only when they change.
        private void Update()
        {
            // Chosen in the lobby, so fixed for the run in the game itself; read again in case anything changes it.
            DifficultyIndex difficulty = Run.instance ? Run.instance.selectedDifficulty : DifficultyIndex.Invalid;
            if (difficulty != shownDifficulty)
            {
                shownDifficulty = difficulty;
                DifficultyDef def = DifficultyCatalog.GetDifficultyDef(difficulty);
                Difficulty.text = Name(def);
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
            }
        }

        // The difficulty's name as the run info shows it, with an Eclipse level's brackets taken off.
        public static string Name(DifficultyDef def)
        {
            if (def == null) return "";
            return BracketedLevel.Replace(Language.GetString(def.nameToken), " ${level}");
        }
    }
}
