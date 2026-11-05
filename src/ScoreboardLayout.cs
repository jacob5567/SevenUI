namespace SevenUI
{
    // How the scoreboard shows each player's items (PluginConfig.ScoreboardLayout).
    internal enum ScoreboardLayout
    {
        // A row per player, their items after their equipment, wrapping under themselves.
        Rows,
        // One player at a time, picked from tabs along the board's top.
        Tabs,
    }
}
