using RoR2;
using UnityEngine;

namespace SevenUI
{
    // A drone's name as SevenUI shows it, rising in color like item tiers: the base name in the tier's color, then the
    // tier as a short tag ("T2") on a badge tinted with that color, in place of the game's "(Tier 2)" suffix. Tier 1 is
    // white, 2 green, 3 red, 4 and up yellow, in SevenUI's item tier shades (I1, Jacob's picks, 2026-11-22). Allies that
    // aren't drones keep their plain name.
    internal static class DroneTier
    {
        private const string TierSuffixToken = "DRONE_TIER_SUFFIX";
        // The badge's tint: the tier's color at this opacity behind the tag.
        private const string BadgeAlpha = "40";
        // Space either side of the tag, so the badge reaches a little past it (the font has no thin space), and the tag
        // a little smaller than the name.
        public const string BadgePad = "<space=0.2em>";
        private const string BadgeSize = "85%";

        // The drone's tier: one more than its upgrades (DroneUpgradeHidden), as DroneCombinerController counts it.
        public static int Of(int upgrades) => upgrades + 1;

        public static Color ColorOf(int tier) =>
            tier <= 1 ? Palette.Text : tier == 2 ? Palette.CountUncommon : tier == 3 ? Palette.CountLegendary : Palette.CountBoss;

        // Whether the drone has a tier: the Drone Combiner can upgrade it (DroneDef.canCombine, the flag
        // DroneCombinerController checks). A Gunner Turret has none, so it keeps its plain name.
        public static bool HasTier(DroneDef drone) => drone && drone.canCombine;

        public static bool HasTier(CharacterBody body) =>
            body && HasTier(DroneCatalog.GetDroneDef(DroneCatalog.GetDroneIndexFromBodyIndex(body.bodyIndex)));

        // The drone's name without the game's tier suffix, from 'gameName' (Util.GetBestBodyName, with the suffix from
        // the first upgrade): the body's own name when the game's is exactly that name with the suffix, as in every
        // language; otherwise (an elite drone, a renamed one) the game's whole name.
        public static string BaseName(string gameName, string baseNameToken, int upgrades)
        {
            if (upgrades > 0 && !string.IsNullOrEmpty(baseNameToken))
            {
                string baseName = Language.GetString(baseNameToken);
                if (Language.GetStringFormatted(TierSuffixToken, baseName, Of(upgrades)) == gameName) return baseName;
            }
            return gameName ?? "";
        }

        // The tier's tag on its tinted badge, as rich text.
        public static string Badge(int upgrades)
        {
            int tier = Of(upgrades);
            string hex = ColorUtility.ToHtmlStringRGB(ColorOf(tier));
            return "<mark=#" + hex + BadgeAlpha + "><size=" + BadgeSize + "><color=#" + hex + "><b>" + BadgePad + "T" + tier + BadgePad +
                   "</b></color></size></mark>";
        }

        // The base name in the tier's color, then the badge.
        public static string Name(string gameName, string baseNameToken, int upgrades) =>
            "<color=#" + ColorUtility.ToHtmlStringRGB(ColorOf(Of(upgrades))) + ">" + BaseName(gameName, baseNameToken, upgrades) +
            "</color> " + Badge(upgrades);
    }
}
