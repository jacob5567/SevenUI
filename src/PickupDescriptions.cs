using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using RoR2;
using RoR2.Items;
using RoR2.UI;
using UnityEngine;

namespace SevenUI
{
    // The description a restyled picker shows for an option: the game's short pickup text, or, when LookingGlass is set
    // to show full item descriptions in the HUD, the full one as its own picker tooltip has it (with its stack
    // calculations and corruption note). Its setting is read through BepInEx's config and its description builder
    // through reflection, so no LookingGlass type is touched and the mod loads without it.
    internal static class PickupDescriptions
    {
        private const string LookingGlassGUID = "droppod.lookingglass";
        private const string FullDescriptionSection = "Misc";
        private const string FullDescriptionKey = "Full Description in Hud";
        private const string ItemStatsType = "LookingGlass.ItemStatsNameSpace.ItemStats";
        private const string ItemDescriptionMethod = "GetItemDescription";
        // Above the stats on a panel that takes the item, in LookingGlass's words for its own captions.
        public const string LoseCaption = "With one less stack, you will have:";
        private static readonly Type[] ItemDescriptionParameters =
        {
            typeof(ItemDef), typeof(int), typeof(CharacterMaster), typeof(bool), typeof(bool),
        };

        // LookingGlass's ItemStats.GetItemDescription, found once; null without LookingGlass or that method.
        private static MethodInfo itemDescription;
        private static bool itemDescriptionLooked;
        private static bool itemDescriptionFailed;

        // Whether LookingGlass is loaded, whatever its settings.
        public static bool LookingGlass() =>
            Chainloader.PluginInfos.TryGetValue(LookingGlassGUID, out PluginInfo info) && info.Instance;

        // Read each time a panel is styled, so a change in Risk Of Options shows on the next picker.
        public static bool Full()
        {
            if (!Chainloader.PluginInfos.TryGetValue(LookingGlassGUID, out PluginInfo info) || !info.Instance) return false;
            return info.Instance.Config.TryGetEntry(FullDescriptionSection, FullDescriptionKey, out ConfigEntry<bool> entry) &&
                entry.Value;
        }

        // LookingGlass writes its tooltip when the game sets the options, before the panel is styled; without its
        // tooltip (its Command Tooltips setting off) the full description is the catalog's. An item's stats are worked
        // out again for what the pick does to the stack (Counted).
        public static string For(PickupDef pickup, MPButton button, bool full, StackChange change, bool permanent)
        {
            if (pickup == null) return "";
            if (!full) return Language.GetString(pickup.pickupToken);
            TooltipProvider tooltip = button ? button.GetComponent<TooltipProvider>() : null;
            string written = tooltip && !string.IsNullOrEmpty(tooltip.overrideBodyText) ? tooltip.overrideBodyText : null;
            string counted = Counted(pickup, written, change, permanent);
            if (counted != null) return counted;
            if (written != null) return written;
            string token = DescriptionToken(pickup);
            return Language.GetString(string.IsNullOrEmpty(token) ? pickup.pickupToken : token);
        }

        // LookingGlass counts one more only on Command's and the Void Potential's panels, the stack as held on every
        // other (the scrapper's, the Lemurian egg's, the other potentials'), and works its picker text out without the
        // player's master, which some stats need. So an item's text is asked for again here, with the local player's
        // master: one more on a panel that gives the item, one less on one that takes one, and the stack as held where
        // the pick changes nothing or takes the whole stack (the scrapper, whose strip says how many it takes). Null when
        // there is nothing to work out (not an item, no LookingGlass) or LookingGlass's own text is the better one: its
        // note that a held void item would corrupt the item, which it writes only while none of it is held, and a
        // temporary or rented stack, whose count isn't the permanent one it counts.
        private static string Counted(PickupDef pickup, string written, StackChange change, bool permanent)
        {
            ItemDef item = ItemCatalog.GetItemDef(pickup.itemIndex);
            CharacterMaster master = LocalUserManager.GetFirstLocalUser()?.cachedMaster;
            if (!item || !master || !master.inventory || ItemDescription() == null) return null;
            Inventory inventory = master.inventory;
            int held = inventory.GetItemCountPermanent(item);
            switch (change)
            {
                case StackChange.Gain:
                    if (written != null && held == 0 && Contagious(item.itemIndex, inventory)) return null;
                    return WithStats(item, held, master, true);
                case StackChange.Lose:
                    if (!permanent) return null;
                    // The last one taken leaves no stack to show stats for.
                    if (held <= 1) return Language.GetString(item.descriptionToken);
                    string text = WithStats(item, held - 1, master, false);
                    return text != null ? ItemStatsText.WithCaption(text, LoseCaption) : null;
                default:
                    // A temporary or rented stack's own count isn't the permanent one LookingGlass counts.
                    if (!permanent) return null;
                    return WithStats(item, held, master, false);
            }
        }

        // Whether a void item held would corrupt the item, or the item is a void one corrupting items held.
        private static bool Contagious(ItemIndex item, Inventory inventory)
        {
            ItemIndex transformed = ContagiousItemManager.GetTransformedItemIndex(item);
            if (transformed != ItemIndex.None && inventory.GetItemCountPermanent(transformed) > 0) return true;
            foreach (ContagiousItemManager.TransformationInfo info in ContagiousItemManager.transformationInfos)
            {
                if (info.transformedItem == item && inventory.GetItemCountPermanent(info.originalItem) > 0) return true;
            }
            return false;
        }

        // LookingGlass's full description of an item with its stats for a stack of 'count', or of one more with its
        // caption ("With another stack, you will have:", or "With this item" for the first) when 'oneMore'; null without
        // LookingGlass. Its ItemStats.GetItemDescription only builds the string, and is called by reflection so no
        // LookingGlass type is touched at compile time.
        public static string WithStats(ItemDef item, int count, CharacterMaster master, bool oneMore)
        {
            MethodInfo method = ItemDescription();
            if (method == null || !item) return null;
            try
            {
                return method.Invoke(null, new object[] { item, count, master, oneMore, false }) as string;
            }
            catch (Exception e)
            {
                if (!itemDescriptionFailed)
                {
                    itemDescriptionFailed = true;
                    SevenUIPlugin.Log.LogWarning($"LookingGlass's item description failed, so its stats aren't shown apart: {e.InnerException ?? e}");
                }
                return null;
            }
        }

        private static MethodInfo ItemDescription()
        {
            if (itemDescriptionLooked) return itemDescription;
            if (!Chainloader.PluginInfos.TryGetValue(LookingGlassGUID, out PluginInfo info) || !info.Instance) return null;
            itemDescriptionLooked = true;
            Type type = info.Instance.GetType().Assembly.GetType(ItemStatsType);
            itemDescription = type?.GetMethod(ItemDescriptionMethod, BindingFlags.Public | BindingFlags.Static, null,
                ItemDescriptionParameters, null);
            if (itemDescription == null)
            {
                SevenUIPlugin.Log.LogWarning($"LookingGlass has no {ItemStatsType}.{ItemDescriptionMethod}, so its stats aren't shown apart");
            }
            return itemDescription;
        }

        private static string DescriptionToken(PickupDef pickup)
        {
            ItemDef item = ItemCatalog.GetItemDef(pickup.itemIndex);
            if (item) return item.descriptionToken;
            EquipmentDef equipment = EquipmentCatalog.GetEquipmentDef(pickup.equipmentIndex);
            if (equipment) return equipment.descriptionToken;
            DroneDef drone = DroneCatalog.GetDroneDef(pickup.droneIndex);
            return drone ? drone.descriptionToken : null;
        }
    }
}
