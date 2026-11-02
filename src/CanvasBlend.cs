using UnityEngine;

namespace SevenUI
{
    // The design canvas blends translucent colors as a browser does, in sRGB, while the game blends in linear light (its
    // color space is linear), where a faint light tint over a dark panel shows several times stronger: the canvas's 6%
    // white header strip came out near a quarter. For a tint the canvas lays over a known color, Over gives the alpha
    // that draws the same color over it in linear light, matched by luminance.
    internal static class CanvasBlend
    {
        public static Color Over(Color tint, Color backdrop)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear || tint.a <= 0f || tint.a >= 1f) return tint;
            Color opaque = new Color(tint.r, tint.g, tint.b, 1f);
            Color canvas = Color.Lerp(backdrop, opaque, tint.a);
            float under = Luminance(backdrop.linear);
            float over = Luminance(opaque.linear);
            if (Mathf.Approximately(over, under)) return tint;
            float alpha = Mathf.Clamp01((Luminance(canvas.linear) - under) / (over - under));
            return new Color(tint.r, tint.g, tint.b, alpha);
        }

        private static float Luminance(Color color) => 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
    }
}
