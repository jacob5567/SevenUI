using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using RoR2;
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
        private static readonly Type[] ItemDescriptionParameters =
        {
            typeof(ItemDef), typeof(int), typeof(CharacterMaster), typeof(bool), typeof(bool),
        };

        // LookingGlass's ItemStats.GetItemDescription, found once; null without LookingGlass or that method.
        private static MethodInfo itemDescription;
        private static bool itemDescriptionLooked;
        private static bool itemDescriptionFailed;

        // Read each time a panel is styled, so a change in Risk Of Options shows on the next picker.
        public static bool Full()
        {
            if (!Chainloader.PluginInfos.TryGetValue(LookingGlassGUID, out PluginInfo info) || !info.Instance) return false;
            return info.Instance.Config.TryGetEntry(FullDescriptionSection, FullDescriptionKey, out ConfigEntry<bool> entry) &&
                entry.Value;
        }

        // LookingGlass writes its tooltip when the game sets the options, before the panel is styled; without its
        // tooltip (its Command Tooltips setting off) the full description is the catalog's.
        public static string For(PickupDef pickup, MPButton button, bool full)
        {
            if (pickup == null) return "";
            if (!full) return Language.GetString(pickup.pickupToken);
            TooltipProvider tooltip = button ? button.GetComponent<TooltipProvider>() : null;
            if (tooltip && !string.IsNullOrEmpty(tooltip.overrideBodyText))
            {
                return tooltip.overrideBodyText;
            }
            string token = DescriptionToken(pickup);
            return Language.GetString(string.IsNullOrEmpty(token) ? pickup.pickupToken : token);
        }

        // LookingGlass's full description of an item with its stats for the master holding that many of it and one
        // more, as its Command tooltips write it ("With another stack, you will have:" and a line a stat); null without
        // LookingGlass. Its ItemStats.GetItemDescription only builds the string, and is called by reflection so no
        // LookingGlass type is touched at compile time.
        public static string WithStats(ItemDef item, int held, CharacterMaster master)
        {
            MethodInfo method = ItemDescription();
            if (method == null || !item) return null;
            try
            {
                return method.Invoke(null, new object[] { item, held, master, true, false }) as string;
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
