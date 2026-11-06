using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;

namespace SevenUI.Tests
{
    // The scoreboard open, with a number of different items in the viewer's row. The items and the open scoreboard go
    // at cleanup. Server methods; the runner is the host.
    internal static class ScoreboardSetUp
    {
        // Each tier the scoreboard draws, so every tier's color shows.
        private static readonly ItemTier[] Tiers =
        {
            ItemTier.Tier1, ItemTier.Tier2, ItemTier.Tier3, ItemTier.Boss, ItemTier.Lunar, ItemTier.VoidTier1,
            ItemTier.VoidTier2, ItemTier.VoidTier3, ItemTier.VoidBoss,
        };

        // One each of that many items the player doesn't have yet, taken in turn from each tier, none that changes the
        // player or the others (TestItems). Skips when the catalog has too few, as without the expansions.
        public static void GiveDifferentItems(Harness h, int count)
        {
            Inventory inventory = Harness.LocalBody.inventory;
            List<Queue<ItemDef>> byTier = Tiers.Select(tier => new Queue<ItemDef>(ItemCatalog.allItemDefs.Where(item =>
                item && item.tier == tier && !item.hidden && !item.isConsumed && item.canRemove &&
                (!item.requiredExpansion || Run.instance.IsExpansionEnabled(item.requiredExpansion)) &&
                inventory.GetItemCountPermanent(item) == 0))).ToList();
            List<ItemDef> items = new List<ItemDef>();
            Func<ItemDef, bool> held = TestItems.HeldBy(inventory, items);
            while (items.Count < count && byTier.Any(queue => queue.Count > 0))
            {
                foreach (Queue<ItemDef> queue in byTier.Where(queue => queue.Count > 0))
                {
                    if (items.Count == count) break;
                    ItemDef item = queue.Dequeue();
                    if (!TestItems.ChangesOthers(item, held))
                    {
                        items.Add(item);
                    }
                }
            }
            if (items.Count < count) throw h.Skip($"only {items.Count} different items to give, not {count}");
            h.OnCleanup(() =>
            {
                if (!inventory) return;
                foreach (ItemDef item in items)
                {
                    inventory.RemoveItemPermanent(item);
                }
            });
            foreach (ItemDef item in items)
            {
                inventory.GiveItemPermanent(item);
            }
        }

        // The equipment in the player's active slot, and the one before it back at cleanup.
        public static void GiveEquipment(Harness h, EquipmentDef equipment)
        {
            Inventory inventory = Harness.LocalBody.inventory;
            EquipmentLocation slot = inventory.activeEquipmentLocation;
            EquipmentIndex before = inventory.currentEquipmentIndex;
            h.OnCleanup(() =>
            {
                if (inventory) inventory.SetEquipmentIndex(before, in slot);
            });
            inventory.SetEquipmentIndex(equipment.equipmentIndex, in slot);
        }

        // The HUD closes the scoreboard every frame unless the info key is held or it was opened as a toggle, as the
        // pause menu's button opens it; closing it ends the toggle.
        public static IEnumerator Open(Harness h)
        {
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            h.OnCleanup(() => HUD.SetScoreboardActive(user, false, EScoreboardDeactivateReturnContext.Game));
            HUD.SetScoreboardActive(user, true, EScoreboardDeactivateReturnContext.Game, shouldForceScoreboardToggle: true);
            yield return h.Until(() => HUD.IsScoreboardActive(user), "the scoreboard open");
        }
    }
}
