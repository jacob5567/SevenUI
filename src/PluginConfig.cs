using BepInEx.Configuration;
using UnityEngine;

namespace SevenUI
{
    // All config entries, bound from the plugin's config file.
    internal sealed class PluginConfig
    {
        public readonly ConfigEntry<bool> BarlowFont;
        public readonly ConfigEntry<bool> FlatPanels;

        public readonly ConfigEntry<bool> DebugLogging;
        public readonly ConfigEntry<bool> DebugAlwaysShow;
        public readonly ConfigEntry<KeyboardShortcut> DumpKey;

        public PluginConfig(ConfigFile config)
        {
            BarlowFont = config.Bind("Text", "BarlowFont", true,
                "Draw HUD text in Barlow instead of the game's font. Languages the game draws in Noto Sans keep it.");
            FlatPanels = config.Bind("Panels", "FlatPanels", true,
                "Draw the run info, objective, artifact and LookingGlass stats panels in the top right as flat " +
                "translucent panels, with a header strip on the objective panel and a thin bar for the difficulty.");

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
