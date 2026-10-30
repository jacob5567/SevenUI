using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;

namespace SevenUI
{
    // The player's health numbers above the health bar, as on the design canvas: "1,240 / 1,520" at the left, the
    // current health in bold and the rest in grey, and the health regeneration, "+12.4/s", at the right. The numbers
    // are the ones the game writes in the bar (its combined health and full health, rounded up), with thousands
    // separators. While health is low the numbers turn pink and the bar switches to a copy of its style with the
    // health in red (FlatHealthStyles.LowHealth).
    internal sealed class PlayerHealthReadout : MonoBehaviour
    {
        private const string RegenFormat = "+0.0;-0.0;+0.0";
        private const string RegenSuffix = "/s";
        private const float RegenSteps = 10f;

        public HealthBar Bar;
        public TMP_Text Health;
        public TMP_Text Regen;

        private string secondaryColor;
        private long shownCurrent = -1;
        private long shownFull = -1;
        private int shownRegen = int.MinValue;
        private bool shownLow;
        private bool shownEmpty;
        private HealthBarStyle normal;
        private HealthBarStyle low;
        private HealthBarStyle normalAlly;
        private HealthBarStyle lowAlly;

        public bool ShownLow => shownLow;

        private void Awake()
        {
            secondaryColor = "#" + ColorUtility.ToHtmlStringRGB(Palette.HeaderText);
        }

        // A few reads and compares when nothing changed.
        private void Update()
        {
            if (!Bar) return;
            HealthComponent source = Bar.source;
            if (!source)
            {
                if (!shownEmpty)
                {
                    shownEmpty = true;
                    Health.text = "";
                    Regen.text = "";
                    shownCurrent = shownFull = -1;
                    shownRegen = int.MinValue;
                }
                return;
            }
            shownEmpty = false;

            bool isLow = source.alive && source.isHealthLow;
            long current = (long)Mathf.Ceil(source.combinedHealth);
            long full = (long)Mathf.Ceil(source.fullHealth);
            if (current != shownCurrent || full != shownFull || isLow != shownLow)
            {
                shownCurrent = current;
                shownFull = full;
                shownLow = isLow;
                Health.text = $"<b>{current:N0}</b> <color={secondaryColor}>/ {full:N0}</color>";
                Health.color = isLow ? Palette.LowHealthText : Palette.Text;
            }

            CharacterBody body = source.body;
            int regen = body ? Mathf.RoundToInt(body.regen * RegenSteps) : 0;
            if (regen != shownRegen)
            {
                shownRegen = regen;
                Regen.text = (regen / RegenSteps).ToString(RegenFormat) + RegenSuffix;
                Regen.color = regen < 0 ? Palette.LowHealth : Palette.Regen;
            }

            KeepStyle(ref Bar._style, ref normal, ref low, isLow);
            KeepStyle(ref Bar._allyStyle, ref normalAlly, ref lowAlly, isLow);
        }

        // The player's own bar uses its ally style, being on the viewer's team, so both are switched.
        private static void KeepStyle(ref HealthBarStyle style, ref HealthBarStyle normalStyle, ref HealthBarStyle lowStyle,
            bool isLow)
        {
            if (!style) return;
            if (style != normalStyle && style != lowStyle)
            {
                normalStyle = style;
                lowStyle = FlatHealthStyles.LowHealth(style);
            }
            HealthBarStyle wanted = isLow ? lowStyle : normalStyle;
            if (style != wanted)
            {
                style = wanted;
            }
        }
    }
}
