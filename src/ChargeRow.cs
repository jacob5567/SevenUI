using System.Text.RegularExpressions;
using RoR2;
using TMPro;
using UnityEngine;

namespace SevenUI
{
    // The teleporter's charge objective as on the design canvas: "Charge the Teleporter" with its percentage at the
    // right in the accent, and a thin bar under them filled to the charge. The game writes the percentage into the
    // objective's text, "Charge the Teleporter (64%)", every time it changes; that text is hidden, and copied without
    // the percentage into a label over it. The percentage is the game's own, from the zone's synced charge.
    internal sealed class ChargeRow : MonoBehaviour
    {
        private const string PercentSuffix = "%";

        public HoldoutZoneController Zone;
        public TMP_Text GameLabel;
        public TMP_Text Label;
        public TMP_Text Percent;
        public RectTransform Fill;

        private string shownSource;
        private int shownPercent = -1;

        public int ShownPercent => shownPercent;

        // LateUpdate, after the objective panel has written this frame's text. A string and an int compared when
        // nothing changed.
        private void LateUpdate()
        {
            if (!Zone || !GameLabel) return;
            int percent = Zone.displayChargePercent;
            string text = GameLabel.text;
            if (!ReferenceEquals(text, shownSource) || percent != shownPercent)
            {
                shownSource = text;
                Label.text = WithoutPercent(text, percent);
                Label.fontStyle = GameLabel.fontStyle;
            }
            if (percent != shownPercent)
            {
                shownPercent = percent;
                Percent.text = percent + PercentSuffix;
                Fill.anchorMax = new Vector2(percent / 100f, 1f);
            }
            // The game sets the text's color as it writes it (grey once the objective is done); the copy takes it,
            // and the game's text stays clear.
            Color color = GameLabel.color;
            if (color.a > 0f)
            {
                Label.color = new Color(color.r, color.g, color.b, 1f);
                GameLabel.color = new Color(color.r, color.g, color.b, 0f);
            }
        }

        // The percentage as the game's English and most other languages write it, in brackets after the text; left in
        // when a language writes it some other way.
        private static string WithoutPercent(string text, int percent)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return Regex.Replace(text, @"\s*[(（]\s*" + percent + @"\s*[%％]\s*[)）]", "");
        }
    }
}
