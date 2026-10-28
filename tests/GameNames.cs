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

        // The objective panel in the run info, and its title.
        public const string ObjectivePanel = "ObjectivePanel";
        public const string ObjectiveTitle = "Label";

        public const string LookingGlassGUID = "droppod.lookingglass";
        // LookingGlass's stats panel, a copy of the objective panel it adds next to it.
        public const string LookingGlassPanel = "PlayerStats";
    }
}
