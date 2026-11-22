using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using RoR2;
using RoR2.UI;

namespace SevenUI
{
    // The name and description the scoreboard's docked panel shows for a row's equipment. The row's equipment icon has
    // them in its tooltip, but with a Functional Coupler that text can describe the wrong equipment: the game updates
    // the icon in its own Update, which may come after the panel reads it in the frame of a swap, and LookingGlass
    // writes its full description into the tooltip only when the row is set up (a hook on ScoreboardStrip.SetMaster),
    // so after a swap it still describes the equipment the row first had. Here the text comes from the equipment
    // itself, and LookingGlass's when it is out of date is built again the way it builds it.
    internal static class EquipmentDescriptions
    {
        private const string LookingGlassGUID = "droppod.lookingglass";
        private const string ItemStatsType = "LookingGlass.ItemStatsNameSpace.ItemStats";
        private const string ExtrasMethod = "GetEquipmentExtras";
        private const string DefinitionsType = "LookingGlass.StatsDisplay.StatsDisplayDefinitions";
        private const string PrecisionField = "floatPrecision";
        // LookingGlass's own default, two decimals.
        private const string DefaultPrecision = "0.##";
        private const string DisabledNameToken = "EQUIPMENT_DISABLED_NAME";
        private const float Percent = 100f;
        private static readonly Type[] ExtrasParameters = { typeof(CharacterMaster), typeof(EquipmentIndex) };

        private static bool looked;
        private static MethodInfo extras;
        private static FieldInfo precision;
        private static bool failed;

        public static string Title(EquipmentDef equipment, TooltipProvider provider) =>
            Current(equipment, provider) ? provider.titleText : Language.GetString(equipment.nameToken);

        // The description, LookingGlass's if it wrote one for the icon.
        public static string Body(EquipmentDef equipment, EquipmentIcon icon)
        {
            TooltipProvider provider = icon.tooltipProvider;
            string description = Language.GetString(equipment.descriptionToken);
            string written = provider ? provider.overrideBodyText : null;
            if (string.IsNullOrEmpty(written)) return description;
            if (written.StartsWith(description, StringComparison.Ordinal)) return written;
            return WithStats(equipment, icon.targetInventory) ?? description;
        }

        // The icon's tooltip is for this equipment, or for a disabled one, whose name the game hides.
        private static bool Current(EquipmentDef equipment, TooltipProvider provider) =>
            provider && (provider.titleToken == equipment.nameToken || provider.titleToken == DisabledNameToken);

        // LookingGlass's text as ItemStats.EquipmentIcon_AddCDProcInfo writes it: the description, the cooldown for the
        // holder's items (with its malformed style tag, copied so the text matches its own), and the equipment's stats
        // from ItemStats.GetEquipmentExtras, which only builds a string. Null without LookingGlass.
        private static string WithStats(EquipmentDef equipment, Inventory inventory)
        {
            if (!inventory || !Find()) return null;
            try
            {
                // Inventory.CalculateEquipmentCooldownScale only reads the inventory and the body's buffs.
                float scale = inventory.CalculateEquipmentCooldownScale();
                string format = precision?.GetValue(null) as string ?? DefaultPrecision;
                StringBuilder text = new StringBuilder(Language.GetString(equipment.descriptionToken));
                text.Append("\n\nCooldown: <style=\"cIsUtility>")
                    .Append((equipment.cooldown * scale).ToString(format, CultureInfo.CurrentCulture)).Append("s</style>");
                if (scale != 1f)
                {
                    text.Append(" <style=\"cStack\">(Base: ").Append(equipment.cooldown).Append(")</style>\nCooldown Reduction: <style=\"cIsUtility>")
                        .Append(((1f - scale) * Percent).ToString(format, CultureInfo.CurrentCulture)).Append("%</style>");
                }
                CharacterMaster master = inventory.GetComponent<CharacterMaster>();
                text.Append(extras.Invoke(null, new object[] { master, equipment.equipmentIndex }) as string);
                return text.ToString();
            }
            catch (Exception e)
            {
                if (!failed)
                {
                    failed = true;
                    SevenUIPlugin.Log.LogWarning($"LookingGlass's equipment stats failed, so the scoreboard shows the plain description: {e.InnerException ?? e}");
                }
                return null;
            }
        }

        private static bool Find()
        {
            if (looked) return extras != null;
            if (!Chainloader.PluginInfos.TryGetValue(LookingGlassGUID, out PluginInfo info) || !info.Instance) return false;
            looked = true;
            Assembly assembly = info.Instance.GetType().Assembly;
            extras = assembly.GetType(ItemStatsType)?.GetMethod(ExtrasMethod, BindingFlags.Public | BindingFlags.Static, null,
                ExtrasParameters, null);
            precision = assembly.GetType(DefinitionsType)?.GetField(PrecisionField, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (extras == null)
            {
                SevenUIPlugin.Log.LogWarning($"LookingGlass has no {ItemStatsType}.{ExtrasMethod}, so the scoreboard shows the plain equipment description");
            }
            return extras != null;
        }
    }
}
