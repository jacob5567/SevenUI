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
        // Personal Shield Generator, for a shield, and Foreign Fruit, an equipment with a cooldown.
        public const string PersonalShield = "PersonalShield";
        public const string Fruit = "Fruit";
        // Infusion, which turns the health bar red.
        public const string Infusion = "Infusion";

        // spawn_interactable and spawn_ai arguments: a small chest, a broken Gunner Drone to buy, and a monster with
        // enough health to take several hits.
        public const string SmallChest = "iscChest1";
        public const string BrokenDrone = "iscBrokenDrone1";
        public const string GunnerDroneBody = "Drone1Body";
        public const string ToughMonster = "BeetleQueen";
        // A monster that can be instakilled. DebugToolkit ranks a name match on the master's name and the body's display
        // name; the Devoted Lemurian's display name is "Lemurian" too, so the plain one's master is named in full.
        public const string ExecutableMonster = "LemurianMaster";
        public const string ExecutableMonsterBody = "LemurianBody";
        // The particle system in Lost Seer's Lenses' execute effect (CritGlassesVoidExecuteEffect) that bursts fake
        // crit numbers, drawn with the damage numbers' material.
        public const string LensesFakeNumbers = "FakeDamageNumbers";

        // The damage number particle's start size is this many characters wide by one tall.
        public const float DamageNumberCharacters = 4f;

        // DebugToolkit's command that turns the player into another body on the spot (master.TransformBody). It also
        // changes the body the player respawns as, so the scenarios switch back.
        public const string SpawnAs = "spawn_as";

        // Survivors that add HUD elements of their own, through HudOverlayManager: Railgunner's backpack charge,
        // Void Fiend's corruption meter, Seeker's, Drone Tech's, Drifter's junk meter and Acolyte's.
        public const string Railgunner = "RailgunnerBody";
        public const string VoidFiend = "VoidSurvivorBody";
        public const string Seeker = "SeekerBody";
        public const string DroneTech = "DroneTechBody";
        public const string Drifter = "DrifterBody";
        public const string Acolyte = "AcolyteBody";

        // The HUD's corner clusters, which the game turns 6 degrees about the vertical axis.
        public static readonly string[] TiltedClusters =
        {
            "MainContainer/MainUIArea/SpringCanvas/UpperLeftCluster",
            "MainContainer/MainUIArea/SpringCanvas/UpperRightCluster",
            "MainContainer/MainUIArea/SpringCanvas/BottomLeftCluster",
            "MainContainer/MainUIArea/SpringCanvas/BottomRightCluster",
        };

        // The inventory and sprint buttons and the key item slot, left of the skills, with their key labels.
        public const string ExtraButtons = "MainContainer/MainUIArea/SpringCanvas/BottomRightCluster/Scaler/ExtraButtonPanels";

        // An empty slot above the skills that the game parents Drone Tech's drones and Seeker's lotus to.
        public const string UtilityArea = "MainContainer/MainUIArea/SpringCanvas/BottomRightCluster/Scaler/UtilityArea";

        // The equipment and key item slots' background, under their display roots.
        public const string SlotBackground = "BGPanel";

        // The key item slot's ready outline, under its display root.
        public const string KeyItemOutline = "IsReadyPanel";

        // The top cluster spans its parent less this, in the game's HUD prefab.
        public const float TopCenterWidthDelta = -576f;

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

        // The player's level and bars in the bottom left, its health bar, and the health bar's sprite digits.
        public const string PlayerBars = "MainContainer/MainUIArea/SpringCanvas/BottomLeftCluster/BarRoots";
        public const string PlayerHealthBar = PlayerBars + "/HealthbarRoot";
        public const string ExpBar = "LevelDisplayCluster/ExpBarRoot";
        public const string LevelRoot = "LevelDisplayCluster/LevelDisplayRoot";
        public const string HealthSpriteDigits = "Managed_Sprite_HP";

        // The objective panel in the run info, and its title.
        public const string ObjectivePanel = "ObjectivePanel";
        public const string ObjectiveTitle = "Label";

        // The pick-an-item interactables: Command's cube, which offers every item of the dropped item's tier, and a Void
        // Potential, which offers the options it is given; and three void items for it, as a Void Potential offers.
        public const string CommandCube = "RoR2/Base/Command/CommandCube.prefab";
        public const string VoidPotential = "RoR2/DLC1/OptionPickup/OptionPickup.prefab";
        public static readonly string[] VoidItems = { "CritGlassesVoid", "ChainLightningVoid", "MissileVoid" };

        // Every other pick-an-item panel the game opens, by what opens it, for a Command cube to open in its place.
        // Panels nothing in the game opens (the cut drone assembly station's, the drone cleanup panel, the first
        // trial blessing panel) aren't tested.
        public static readonly (string Name, string Key)[] PickerPanels =
        {
            ("scrapper", "RoR2/Base/Scrapper/ScrapperPickerPanel.prefab"),
            ("delusion", "RoR2/CU8/DelusionOptionPickerPanel.prefab"),
            ("lemurian_egg", "RoR2/CU8/LemurianEgg/LemurianEggPickerPanel.prefab"),
            ("fragment_potential", "RoR2/DLC2/FragmentPotentialPickerPanel.prefab"),
            ("rebirth", "RoR2/DLC2/RebirthPickerPanel.prefab"),
            ("meal_prep", "RoR2/DLC3/MealPrep/MealPrepPickerPanel.prefab"),
            ("solus_vendor", "RoR2/DLC3/SolusVendorShrine/SolusVendorShrinePickerPanel-Variant.prefab"),
            ("acolyte_unlock", "RoR2/DLC4/champrush/UnlockableAcolytePickerPanel.prefab"),
            ("cursed_book", "RoR2/DLC4/CursedBook/CursedBookPickerPanel.prefab"),
            ("cursed_potential", "RoR2/DLC4/CursedPotentialPickerPanel.prefab"),
            ("page_potential", "RoR2/DLC4/PagePotentialPickerPanel.prefab"),
            ("hallowed_potential", "RoR2/DLC4/UI/HallowedPotentialPickerPanel.prefab"),
            ("trial_initial", "RoR2/DLC4/UI/TrialPotentialInitialPickerPanel.prefab"),
            ("trial_blessing_b", "RoR2/DLC4/UI/TrialPotentialBlessingPickerPanelB.prefab"),
            ("ultima_potential", "RoR2/DLC4/UI/UltimalPotentialPickerPanel.prefab"),
            ("wake_potential", "RoR2/DLC4/Wake/WakePotentialPickerPanel.prefab"),
        };

        public const string LookingGlassGUID = "droppod.lookingglass";
        // LookingGlass's stats panel, a copy of the objective panel it adds next to it.
        public const string LookingGlassPanel = "PlayerStats";
    }
}
