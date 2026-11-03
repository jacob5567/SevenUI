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
    // calculations and corruption note). Its setting is read through BepInEx's config, so no LookingGlass type is
    // touched and the mod loads without it.
    internal static class PickupDescriptions
    {
        private const string LookingGlassGUID = "droppod.lookingglass";
        private const string FullDescriptionSection = "Misc";
        private const string FullDescriptionKey = "Full Description in Hud";

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
