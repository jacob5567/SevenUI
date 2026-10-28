namespace SevenUI.Tests
{
    // Every name the scenarios take from the game or other mods, beyond ModTestRunner's Stages and Commands, in one
    // place so a patch means one edit. Not yet run in game: a wrong one shows up as a scenario that fails or skips with
    // the name in its message.
    internal static class GameNames
    {
        // The Bombardier font assets the game uses for Latin languages (tmpBombDropShadow and its variants).
        public const string GameFontPrefix = "tmpBomb";

        // give_item arguments, for items that add stacked icons to the item bar.
        public const string Syringe = "Syringe";
        public const string Hoof = "Hoof";
        // Titanic Knurl, for more max health.
        public const string Knurl = "Knurl";

        // The run info's timer box, and the part of the difficulty bar that holds its scrolling segments.
        public const string TimerPanel = "TimerPanel";
        public const string DifficultySegments = "Scroll View/Viewport";

        // The player's health bar in the bottom left, and its sprite digits.
        public const string PlayerHealthBar = "MainContainer/MainUIArea/SpringCanvas/BottomLeftCluster/BarRoots/HealthbarRoot";
        public const string HealthSpriteDigits = "Managed_Sprite_HP";

        // The objective panel in the run info, and its title.
        public const string ObjectivePanel = "ObjectivePanel";
        public const string ObjectiveTitle = "Label";

        public const string LookingGlassGUID = "droppod.lookingglass";
        // LookingGlass's stats panel, a copy of the objective panel it adds next to it.
        public const string LookingGlassPanel = "PlayerStats";
    }
}
