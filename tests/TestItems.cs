using System;
using System.Collections.Generic;
using System.Linq;
using RoR2;

namespace SevenUI.Tests
{
    // Which items the tests may give without changing the player or their other items: not the Heresy items, which
    // each replace a skill and together turn the survivor into the Heretic; not Egocentrism, which turns other items
    // into itself over time; not Benthic Bloom, which upgrades other items each stage; and no void item along with an
    // item it corrupts (Newly Hatched Zoea corrupts every boss item), either way round.
    internal static class TestItems
    {
        private static HashSet<ItemDef> changing;

        // Held: whether the player has the item, or is given it with this one.
        public static bool ChangesOthers(ItemDef item, Func<ItemDef, bool> held)
        {
            if (changing == null)
            {
                changing = new HashSet<ItemDef>
                {
                    RoR2Content.Items.LunarPrimaryReplacement, RoR2Content.Items.LunarSecondaryReplacement,
                    RoR2Content.Items.LunarUtilityReplacement, RoR2Content.Items.LunarSpecialReplacement,
                    DLC1Content.Items.LunarSun, DLC1Content.Items.CloverVoid,
                };
            }
            if (changing.Contains(item)) return true;
            // Each pair is an item and the void item that corrupts it.
            return ItemCatalog.GetItemPairsForRelationship(DLC1Content.ItemRelationshipTypes.ContagiousItem).Any(pair =>
                pair.itemDef1 == item && held(pair.itemDef2) || pair.itemDef2 == item && held(pair.itemDef1));
        }

        // Whether the player has any of the item.
        public static Func<ItemDef, bool> HeldBy(Inventory inventory, ICollection<ItemDef> given) =>
            other => other && (given.Contains(other) || inventory.GetItemCountPermanent(other) > 0);
    }
}
