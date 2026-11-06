using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.CharacterAI;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace SevenUI.Tests
{
    // The scoreboard open, with a number of different items in the viewer's row, and other players beside the viewer.
    // The items, the players and the open scoreboard go at cleanup. Server methods; the runner is the host.
    internal static class ScoreboardSetUp
    {
        // Each tier the scoreboard draws, so every tier's color shows.
        private static readonly ItemTier[] Tiers =
        {
            ItemTier.Tier1, ItemTier.Tier2, ItemTier.Tier3, ItemTier.Boss, ItemTier.Lunar, ItemTier.VoidTier1,
            ItemTier.VoidTier2, ItemTier.VoidTier3, ItemTier.VoidBoss,
        };

        // The survivor the stand-in players are, and how far apart they stand beside the viewer.
        private const string StandInBody = "CommandoBody";
        private const float StandInSpacing = 3f;

        // One each of that many items the player (the viewer, or the given inventory's) doesn't have yet, taken in turn
        // from each tier, none that changes the player or the others (TestItems). Skips when the catalog has too few, as
        // without the expansions.
        public static void GiveDifferentItems(Harness h, int count, Inventory inventory = null)
        {
            inventory = inventory ? inventory : Harness.LocalBody.inventory;
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

        // Stand-in players, each with that many different items, so the scoreboard has a row per player. The game has
        // no command for another player, and DebugToolkit's allies (spawn_ai) are AI, which the scoreboard doesn't list:
        // it lists masters with a player's controller and a body. So each is Commando's AI master on the player's team,
        // its AI off so it stands still, given a player's controller without a network user, which the scoreboard names
        // by the survivor.
        public static IEnumerator AddPlayers(Harness h, int count, int items)
        {
            GameObject prefab = MasterCatalog.GetMasterPrefab(
                MasterCatalog.FindAiMasterIndexForBody(BodyCatalog.FindBodyIndex(StandInBody)));
            if (!prefab) throw h.Fail($"no AI master for {StandInBody} to stand in for a player");
            CharacterBody viewer = Harness.LocalBody;
            List<CharacterMaster> players = new List<CharacterMaster>();
            h.OnCleanup(() =>
            {
                foreach (CharacterMaster player in players.Where(player => player))
                {
                    player.DestroyBody();
                    NetworkServer.Destroy(player.gameObject);
                }
            });
            for (int i = 0; i < count; i++)
            {
                CharacterMaster player = new MasterSummon
                {
                    masterPrefab = prefab,
                    position = viewer.footPosition + viewer.transform.right * (StandInSpacing * (i + 1)),
                    rotation = viewer.transform.rotation,
                    teamIndexOverride = TeamIndex.Player,
                    ignoreTeamMemberLimit = true,
                    preSpawnSetupCallback = StandIn,
                }.Perform();
                if (!player) throw h.Fail("the stand-in player wasn't made");
                players.Add(player);
                GiveDifferentItems(h, items, player.inventory);
            }
            yield return h.Until(() => players.All(player => player && player.GetBody()) &&
                                       PlayerCharacterMasterController.instances.Count > count,
                $"{count} stand-in players with bodies");
        }

        // Before the master is spawned, so its controller is among its networked parts.
        private static void StandIn(CharacterMaster master)
        {
            foreach (BaseAI ai in master.GetComponents<BaseAI>())
            {
                ai.enabled = false;
            }
            master.gameObject.AddComponent<PlayerCharacterMasterController>();
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
