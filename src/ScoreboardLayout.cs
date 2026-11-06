namespace SevenUI
{
    // How the scoreboard shows each player's items (PluginConfig.ScoreboardLayout).
    internal enum ScoreboardLayout
    {
        // A row per player, their items after their equipment, wrapping under themselves.
        Rows,
        // The rows while their items fit at 32px or larger; past that, the rows collapse to show one player's items
        // at a time, the player pointed at.
        Tabs,
    }
}
