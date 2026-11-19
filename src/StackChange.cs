namespace SevenUI
{
    // What choosing an item does to the chooser's stack of it, which decides the stack LookingGlass's stats are worked
    // out for: one more for a pickup or a panel that gives the item, one less for a panel that takes it from the
    // inventory, and the stack as held where nothing changes (an item looked at on the scoreboard, Delusion's guess).
    internal enum StackChange
    {
        Gain,
        Lose,
        None,
    }
}
