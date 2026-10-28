using UnityEngine;

namespace SevenUI
{
    // The colors of the design canvas, shared by every restyled part of the HUD.
    internal static class Palette
    {
        // Panels are flat 45% black, with a 6% white strip behind their header.
        public static readonly Color Panel = new Color(0f, 0f, 0f, 0.45f);
        public static readonly Color HeaderStrip = new Color(1f, 1f, 1f, 0.06f);
        // #a9aebb, for header strip text and secondary labels.
        public static readonly Color HeaderText = new Color(0.663f, 0.682f, 0.733f, 1f);
        // #e7e9f0, for primary text such as the timer.
        public static readonly Color Text = new Color(0.906f, 0.914f, 0.941f, 1f);
        // #b8c4ff, the accent, for bar fills.
        public static readonly Color Accent = new Color(0.722f, 0.769f, 1f, 1f);
        // #e8a25a, the equipment tier's orange, for the equipment slot's ready outline.
        public static readonly Color Equipment = new Color(0.910f, 0.635f, 0.353f, 1f);
        // rgba(10,12,16,0.72), the cover over a skill that is recharging.
        public static readonly Color CooldownCover = new Color(0.039f, 0.047f, 0.063f, 0.72f);
        // #d9645a and #6a3a3a, the boss bar's health and the health it just lost.
        public static readonly Color BossHealth = new Color(0.851f, 0.392f, 0.353f, 1f);
        public static readonly Color BossHealthLost = new Color(0.416f, 0.227f, 0.227f, 1f);
        // #3a3f4c, the empty part of a bar.
        public static readonly Color Trough = new Color(0.227f, 0.247f, 0.298f, 1f);
    }
}
