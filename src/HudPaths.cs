namespace SevenUI
{
    // Paths of the vanilla HUD's objects, from the HUD's root, that more than one class finds.
    internal static class HudPaths
    {
        public const string MainUIArea = "MainContainer/MainUIArea";
        public const string SpringCanvas = MainUIArea + "/SpringCanvas";
        public const string UpperLeftCluster = SpringCanvas + "/UpperLeftCluster";
        public const string TopCenterCluster = SpringCanvas + "/TopCenterCluster";
        public const string UpperRightCluster = SpringCanvas + "/UpperRightCluster";
        public const string BottomRightCluster = SpringCanvas + "/BottomRightCluster";
        public const string LeftCluster = SpringCanvas + "/LeftCluster";
        public const string BarRoots = SpringCanvas + "/BottomLeftCluster/BarRoots";
    }
}
