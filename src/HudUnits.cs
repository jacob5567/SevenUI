namespace SevenUI
{
    // The HUD's perspective camera draws the HUD's parts, the corner clusters as well as the notifications, a little
    // smaller than its canvas's units: one unit shows as about 0.977 of the design canvas's pixels (the player's bars,
    // laid out at 8/7 units per pixel, measured 1.117 times the canvas's sizes on a 1440-pixel-high screen, and a
    // 500-unit notification 488 pixels). Sizes taken from the canvas are scaled by this.
    internal static class HudUnits
    {
        public const float PerCanvasPixel = 1f / 0.977f;
        // The design canvas's height, in its pixels.
        public const float CanvasHeight = 1080f;
        // The outline sprite's edge, in its pixels (FlatSprite.Outline).
        public const float OutlineEdgePixels = 2f;
        // The thin lines between a panel's parts.
        public const float DividerHeight = 1f * PerCanvasPixel;
    }
}
