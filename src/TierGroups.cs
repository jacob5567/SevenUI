using RoR2;
using UnityEngine;

namespace SevenUI
{
    // The scoreboard's item tiers in the canvas's order, each in its color: the counts under a player's name, and the
    // runs the scoreboard sorts their items into. Items of any other tier (untiered, food, hallowed, hex, pages, modded
    // tiers) sort last, together, and aren't counted; each still shows its own tier's color (GameColors.OfTier).
    internal static class TierGroups
    {
        public static readonly (ItemTier[] tiers, Color color)[] Main =
        {
            (new[] { ItemTier.Tier1 }, Palette.Text),
            (new[] { ItemTier.Tier2 }, Palette.CountUncommon),
            (new[] { ItemTier.Tier3 }, Palette.CountLegendary),
            (new[] { ItemTier.Boss }, Palette.CountBoss),
            (new[] { ItemTier.Lunar }, Palette.CountLunar),
            (new[] { ItemTier.VoidTier1, ItemTier.VoidTier2, ItemTier.VoidTier3, ItemTier.VoidBoss }, Palette.CountVoid),
        };

        // The main groups and the one for every other tier.
        public static readonly int Count = Main.Length + 1;

        public static int Of(ItemIndex item)
        {
            ItemDef def = ItemCatalog.GetItemDef(item);
            if (!def) return Main.Length;
            for (int group = 0; group < Main.Length; group++)
            {
                if (System.Array.IndexOf(Main[group].tiers, def.tier) >= 0) return group;
            }
            return Main.Length;
        }
    }
}
