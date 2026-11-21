using BepInEx.Configuration;
using UnityEngine;

namespace SevenUI
{
    // All config entries, bound from the plugin's config file.
    internal sealed class PluginConfig
    {
        public readonly ConfigEntry<bool> BarlowFont;
        public readonly ConfigEntry<bool> MonospaceDamageNumbers;
        public readonly ConfigEntry<bool> FlatPanels;
        public readonly ConfigEntry<bool> TightMargins;
        public readonly ConfigEntry<bool> ScoreboardBesideRunInfo;
        public readonly ConfigEntry<bool> EquipmentDeck;
        public readonly ConfigEntry<bool> RestyleItemPickers;
        public readonly ConfigEntry<bool> CompactAllyCards;
        public readonly ConfigEntry<bool> GroupAllyCards;
        public readonly ConfigEntry<bool> LevelHud;
        public readonly ConfigEntry<bool> ScoreboardDetailPanel;
        public readonly ConfigEntry<bool> ScoreboardTabs;
        public readonly ConfigEntry<bool> ScoreboardAllies;
        public readonly ConfigEntry<bool> RestylePauseMenu;
        public readonly ConfigEntry<bool> RestyleTooltips;
        public readonly ConfigEntry<bool> TimerHundredths;
        public readonly ConfigEntry<bool> ShowHealthRegen;

        public readonly ConfigEntry<bool> DebugLogging;
        public readonly ConfigEntry<KeyboardShortcut> DumpKey;

        public PluginConfig(ConfigFile config)
        {
            BarlowFont = config.Bind("Text", "BarlowFont", true,
                "Draw HUD text in Barlow instead of the game's font. Languages the game draws in Noto Sans keep it.");
            MonospaceDamageNumbers = config.Bind("Text", "MonospaceDamageNumbers", true,
                "Draw the floating damage numbers in JetBrains Mono, a monospace font, instead of the game's pixel digits.");
            FlatPanels = config.Bind("Panels", "FlatPanels", true,
                "Draw the HUD flat: facing the screen squarely instead of leaning in, with flat translucent panels, flat " +
                "bars and slots, and the health numbers as text. Off brings back the game's look.");
            TightMargins = config.Bind("Panels", "TightMargins", true,
                "Draw the HUD's corners near the screen's edges, as on SevenUI's design: 2.6% of the screen's height " +
                "in (2.2% at the top), and the ally cards 2.6% in and 28% down. Off keeps the game's margins, 5% of the screen. Only with FlatPanels on.");
            ScoreboardBesideRunInfo = config.Bind("Panels", "ScoreboardBesideRunInfo", true,
                "Place the scoreboard as on SevenUI's design: near the top of the screen, centered between its left edge " +
                "and the run info. Off centers it on the screen, where the game puts it. Only with FlatPanels on.");
            EquipmentDeck = config.Bind("Panels", "EquipmentDeck", true,
                "Show the next equipment as a deck of icons above the equipment slot with a Functional Coupler. Off shows no deck, " +
                "only the slot. Only with FlatPanels on.");
            RestyleItemPickers = config.Bind("Panels", "RestyleItemPickers", true,
                "Restyle the panels where you pick an item, such as Command and Void Potentials. Off keeps the game's look for " +
                "them. Only with FlatPanels on.");
            CompactAllyCards = config.Bind("Panels", "CompactAllyCards", true,
                "Shrink the ally cards to compact rows and fold the ones that don't fit into \"+N more\" above the chat. Off keeps " +
                "the cards at full size and never folds them. Only with FlatPanels on.");
            GroupAllyCards = config.Bind("Panels", "GroupAllyCards", true,
                "Group allies of one kind into a single card with a count and a health segment each. Off shows one card per " +
                "ally, as the game has it, with its own name and health bar. Only with FlatPanels on.");
            LevelHud = config.Bind("Panels", "LevelHud", true,
                "Keeps the game's inward lean of the HUD's corners, with everything else flat. Only with FlatPanels on.");
            ScoreboardDetailPanel = config.Bind("Panels", "ScoreboardDetailPanel", true,
                "Describes the item, equipment or key item you point at in the scoreboard in a panel docked at the board's right, " +
                "which widens the board. Off shows the game's tooltip over the pointed-at item instead. Only with FlatPanels on.");
            ScoreboardTabs = config.Bind("Panels", "ScoreboardTabs", true,
                "Collapses the scoreboard's rows to tabs when the items would not fit at a readable size. Off keeps every row " +
                "and shrinks the items to fit. Only with FlatPanels on.");
            ScoreboardAllies = config.Bind("Panels", "ScoreboardAllies", true,
                "Shows each player's drones and other allies on the scoreboard: a summary under the player's name, which opens " +
                "their allies in a panel under the board when clicked. Off shows none. Only with FlatPanels on.");
            RestylePauseMenu = config.Bind("Panels", "RestylePauseMenu", true,
                "Restyle the pause menu with a title, the run's line and outlined buttons. Off keeps the game's look for it. " +
                "Only with FlatPanels on.");
            RestyleTooltips = config.Bind("Panels", "RestyleTooltips", true,
                "Restyle the tooltips of items, equipment and skills in the HUD. Off keeps the game's look for them. " +
                "Only with FlatPanels on.");
            TimerHundredths = config.Bind("Panels", "TimerHundredths", false,
                "On shows the run timer's hundredths of a second, which the restyled run info removes. " +
                "Only with FlatPanels on.");
            ShowHealthRegen = config.Bind("Panels", "ShowHealthRegen", true,
                "Off hides the health regeneration text, such as +12.4/s, beside the health numbers. " +
                "The game's HUD has no such text. Only with FlatPanels on.");

            // Risk Of Options uses the section name as the menu header, so keep it exactly "Debug".
            DebugLogging = config.Bind("Debug", "DebugLogging", false,
                "Log extra detail, and enable the dump key.");
            DumpKey = config.Bind("Debug", "DumpKey", new KeyboardShortcut(KeyCode.F8),
                "With DebugLogging on, press to log each HUD's fonts and what the mod restyled.");
        }
    }
}
