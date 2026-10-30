namespace SevenUI
{
    // The corner clusters sit on the HUD's main area, which the HUD's perspective camera draws behind the canvas's
    // plane, so one of their units shows as about 0.875 of the design canvas's pixels (the game's 48-unit buff icons
    // are 42 pixels tall on screen). Sizes taken from the canvas are scaled by this.
    internal static class HudUnits
    {
        public const float PerCanvasPixel = 8f / 7f;
    }
}
