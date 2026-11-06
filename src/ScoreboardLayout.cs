namespace SevenUI
{
    // How the scoreboard shows each player's items (PluginConfig.ScoreboardLayout).
    internal enum ScoreboardLayout
    {
        // A row per player, their items after their equipment, wrapping under themselves.
        Rows,
        // One player's items at a time, picked from a list of the players down the board's left.
        Tabs,
    }
}
