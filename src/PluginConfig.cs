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
        public readonly ConfigEntry<ScoreboardLayout> ScoreboardLayout;

        public readonly ConfigEntry<bool> DebugLogging;
        public readonly ConfigEntry<bool> DebugAlwaysShow;
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
            ScoreboardLayout = config.Bind("Panels", "ScoreboardLayout", SevenUI.ScoreboardLayout.Rows,
                "How the scoreboard shows items. Rows: a row per player, with their items after their equipment, grouped by " +
                "tier. Tabs: one player's items at a time, picked from a list of the players at the left. Either way, the " +
                "item you point at is described in a panel on the scoreboard, with who holds it. Only with FlatPanels on.");

            // Risk Of Options uses the section name as the menu header, so keep it exactly "Debug".
            DebugLogging = config.Bind("Debug", "DebugLogging", false,
                "Log extra detail, and enable the dump key.");
            DebugAlwaysShow = config.Bind("Debug", "DebugAlwaysShow", false,
                "Show the mod's visuals even when they wouldn't normally appear, for testing.");
            DumpKey = config.Bind("Debug", "DumpKey", new KeyboardShortcut(KeyCode.F8),
                "With DebugLogging on, press to log each HUD's fonts and what the mod restyled.");
        }
    }
}
