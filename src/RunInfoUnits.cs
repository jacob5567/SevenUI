namespace SevenUI
{
    // The run info's parts and the column of panels under it (the objective panel, LookingGlass's stats) are drawn 1.6
    // times the size of the rest of the HUD, so one of their own units is about one and a half of the design canvas's
    // pixels, the canvas's 34px timer being 22 there. Sizes taken from the canvas are scaled by this.
    internal static class RunInfoUnits
    {
        public const float PerCanvasPixel = 22f / 34f;
    }
}
