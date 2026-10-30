using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Keeps one scoreboard row as on the design canvas while it shows: tinted in the accent if it is the viewer's own,
    // the player's survivor and level ("Commando · Lv 17") under their name, the row's item count (LookingGlass's count
    // per tier, when it is installed) under that, and the gamepad's focus outlines shown only while they are lit. The
    // game writes the count into the hidden text it shows under the money; it is copied when it changes.
    internal sealed class ScoreboardRow : MonoBehaviour
    {
        private const string Separator = " · ";
        private const string LevelToken = "HUD_LEVEL_PREFIX";

        public ScoreboardStrip Strip;
        public HUD Hud;
        public Image Background;
        public TMP_Text Survivor;
        public TMP_Text Counts;
        public Image[] FocusOutlines;

        private CharacterMaster shownMaster;
        private CharacterMaster shownViewer;
        private CharacterBody shownBody;
        private int shownLevel = -1;
        private string shownCounts;

        // A few reads and compares when nothing changed.
        private void LateUpdate()
        {
            if (!Strip) return;
            CharacterMaster master = Strip.master;
            CharacterMaster viewer = Hud && Hud.localUserViewer != null ? Hud.localUserViewer.cachedMaster : null;
            if (master != shownMaster || viewer != shownViewer)
            {
                shownMaster = master;
                shownViewer = viewer;
                Background.color = master && master == viewer ? Palette.ScoreboardOwnRow : Color.clear;
            }

            CharacterBody body = master ? master.GetBody() : null;
            int level = body ? Mathf.FloorToInt(body.level) : -1;
            if (body != shownBody || level != shownLevel)
            {
                shownBody = body;
                shownLevel = level;
                Survivor.text = body ? body.GetDisplayName() + Separator + LevelPrefix() + " " + level : "";
            }

            string counts = Strip.itemCountText ? Strip.itemCountText.text : null;
            if (!ReferenceEquals(counts, shownCounts))
            {
                shownCounts = counts;
                Counts.text = counts ?? "";
            }

            // The game lights an outline yellow while the gamepad focuses its part, and darkens it after.
            foreach (Image outline in FocusOutlines)
            {
                if (!outline) continue;
                bool lit = outline.color.r > 0.5f;
                if (outline.enabled != lit)
                {
                    outline.enabled = lit;
                }
            }
        }

        // "Lv", the game's level header without its colon.
        private static string LevelPrefix()
        {
            string prefix = Language.GetString(LevelToken);
            return prefix.TrimEnd(':', '：');
        }
    }
}
