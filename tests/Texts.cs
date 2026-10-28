namespace SevenUI.Tests
{
    // What the mod is expected to show.
    internal static class Texts
    {
        // The baked font assets are named "Barlow Medium" and "Barlow SemiBold" (FontLibrary).
        public const string BarlowFontPrefix = "Barlow";
        // The run timer's font (FontLibrary.Condensed).
        public const string CondensedFontPrefix = "Barlow Condensed";
        // The mod's copy of the health bar style is named after the game's with this suffix (PlayerBarsStyler).
        public const string FlatStyleSuffix = " (SevenUI)";
        // The plain sprite the mod draws flat bars with (FlatSprite.White).
        public const string FlatSpriteName = "SevenUIFlat";
        // The strip the mod draws the barrier with, along the top of the health bar (FlatSprite.TopStrip).
        public const string BarrierStripName = "SevenUITopStrip";
        // The panel the mod puts behind the run info (RunInfoStyler).
        public const string RunInfoBackgroundName = "SevenUIRunInfoPanel";
        // The strip the mod adds behind the objective panel's title (RightInfoBarStyler).
        public const string HeaderStripName = "SevenUIHeaderStrip";
    }
}
