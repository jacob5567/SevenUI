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
    }
}
