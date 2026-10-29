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
        // The scoreboard and the chat's input sit on a darker panel, since they cover the game; the scoreboard's rows
        // are a faint lighter band on it.
        public static readonly Color ScoreboardPanel = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color ScoreboardRow = new Color(1f, 1f, 1f, 0.04f);
        // Pickup notifications sit on a slightly darker panel, since they appear over the middle of the screen.
        public static readonly Color NotificationPanel = new Color(0f, 0f, 0f, 0.55f);
        // #2c303a, the slot behind an item icon.
        public static readonly Color ItemSlot = new Color(0.173f, 0.188f, 0.227f, 1f);
        // #3a3f4c, the empty part of a bar.
        public static readonly Color Trough = new Color(0.227f, 0.247f, 0.298f, 1f);
        // #e9d27a and #8fd0e8, the money and lunar coin amounts in the top left.
        public static readonly Color Money = new Color(0.914f, 0.824f, 0.478f, 1f);
        public static readonly Color LunarCoin = new Color(0.561f, 0.816f, 0.910f, 1f);
        // #7fd07f, #e06a5f and #e9d27a, the strip under a buff, a debuff and a cooldown.
        public static readonly Color Buff = new Color(0.498f, 0.816f, 0.498f, 1f);
        public static readonly Color Debuff = new Color(0.878f, 0.416f, 0.373f, 1f);
        public static readonly Color BuffCooldown = new Color(0.914f, 0.824f, 0.478f, 1f);
        // #e9d27a, the barrier, a strip along the top of the health bar.
        public static readonly Color Barrier = new Color(0.914f, 0.824f, 0.478f, 1f);
    }
}
