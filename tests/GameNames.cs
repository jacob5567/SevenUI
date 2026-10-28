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

        // A spawn_interactable argument: a small chest, for an interaction prompt.
        public const string SmallChest = "iscChest1";

        // The damage number particle's start size is this many characters wide by one tall.
        public const float DamageNumberCharacters = 4f;

        // The HUD's corner clusters, which the game turns 6 degrees about the vertical axis.
        public static readonly string[] TiltedClusters =
        {
            "MainContainer/MainUIArea/SpringCanvas/UpperLeftCluster",
            "MainContainer/MainUIArea/SpringCanvas/UpperRightCluster",
            "MainContainer/MainUIArea/SpringCanvas/BottomLeftCluster",
            "MainContainer/MainUIArea/SpringCanvas/BottomRightCluster",
        };

        // The money cluster in the top left, and the strip behind each currency in it.
        public const string MoneyCluster = "MainContainer/MainUIArea/SpringCanvas/UpperLeftCluster";
        public const string CurrencyBackground = "BackgroundPanel";

        // The scoreboard's board, under its panel, and the chat's input field, under its chat box.
        public const string ScoreboardBoard = "Container/StripContainer";
        public const string ChatInputField = "PermanentBG/Input Field";

        // The run info's timer box, and the part of the difficulty bar that holds its scrolling segments.
        public const string TimerPanel = "TimerPanel";
        public const string StageAndLevel = "StageAndLevelRoot";
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
