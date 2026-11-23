using RoR2;
using UnityEngine;

namespace SevenUI
{
    // A drone's name as SevenUI shows it, rising in color like item tiers: the base name in the tier's color, then, from
    // tier 2, the tier as a short tag ("T2") on a rounded badge in a dark shade of that color (TierBadge), in place of the
    // game's "(Tier 2)" suffix. Tier 1 is white, 2 green, 3 red, 4 and up yellow, in SevenUI's item tier shades (I1,
    // Jacob's picks, 2026-11-22). Allies that aren't drones keep their plain name.
    internal static class DroneTier
    {
        private const string TierSuffixToken = "DRONE_TIER_SUFFIX";
        // The link that marks the tag for TierBadge, which draws the badge behind it, with the tier's color in its ID.
        private const string BadgeLinkPrefix = "SevenUITier:";
        // The tag a little smaller than the name, with room either side for the badge TierBadge draws around it.
        private const string BadgeSize = "72%";
        private static readonly string BadgeRoom =
            "<space=" + (TierBadge.PadXEm + TierBadge.BorderEm).ToString(System.Globalization.CultureInfo.InvariantCulture) + "em>";

        // The drone's tier: one more than its upgrades (DroneUpgradeHidden), as DroneCombinerController counts it.
        public static int Of(int upgrades) => upgrades + 1;

        public static Color ColorOf(int tier) =>
            tier <= 1 ? Palette.Text : tier == 2 ? Palette.CountUncommon : tier == 3 ? Palette.CountLegendary : Palette.CountBoss;

        // Whether the drone has a tier: the Drone Combiner can upgrade it. DroneCombinerController.TryGetCombinableDrones
        // takes only bodies with the Drone body flag whose DroneDef allows combining. A Gunner Turret's DroneDef allows
        // it, but its body isn't flagged a drone, so it keeps its plain name.
        public static bool HasTier(DroneDef drone) =>
            drone && drone.canCombine && drone.bodyPrefab && drone.bodyPrefab.GetComponent<CharacterBody>() is CharacterBody body &&
            (body.bodyFlags & CharacterBody.BodyFlags.Drone) != 0;

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

        // The tier's tag ("T2") as rich text, in the tier's color and marked for TierBadge to draw its badge behind; none
        // for tier 1, which is the plain drone. A label showing it needs TierBadge.Attach.
        public static string Badge(int upgrades)
        {
            int tier = Of(upgrades);
            if (tier <= 1) return "";
            string hex = ColorUtility.ToHtmlStringRGB(ColorOf(tier));
            return "<size=" + BadgeSize + ">" + BadgeRoom + "<color=#" + hex + "><b><link=\"" + BadgeLinkPrefix + hex + "\">" + Tag(tier) +
                   "</link></b></color>" + BadgeRoom + "</size>";
        }

        // The tag itself, as Badge shows it.
        public static string Tag(int tier) => "T" + tier;

        // Whether a text holds a badge.
        public static bool HasBadge(string text) => text != null && text.Contains(BadgeLinkPrefix);

        // Whether a link in a label is a badge's, and the tier color it carries.
        public static bool IsBadgeLink(string id, out Color color)
        {
            color = Color.clear;
            return id != null && id.StartsWith(BadgeLinkPrefix) &&
                   ColorUtility.TryParseHtmlString("#" + id.Substring(BadgeLinkPrefix.Length), out color);
        }

        // The base name in the tier's color, then the badge (none at tier 1).
        public static string Name(string gameName, string baseNameToken, int upgrades) =>
            WithBadge("<color=#" + ColorUtility.ToHtmlStringRGB(ColorOf(Of(upgrades))) + ">" + BaseName(gameName, baseNameToken, upgrades) +
                      "</color>", upgrades);

        // 'name', then a space and the badge, or just 'name' at tier 1.
        public static string WithBadge(string name, int upgrades) => Of(upgrades) <= 1 ? name : name + " " + Badge(upgrades);
    }
}
