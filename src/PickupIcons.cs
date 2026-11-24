using System.Collections.Generic;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine;

namespace SevenUI
{
    // Pickup icons for the mod's own images (the scrappers' exchange strip), loaded as the game loads them: from the
    // pickup's icon reference, or its sprite when it has one, in the background. Kept once loaded; the strip shows only
    // the few scraps.
    internal static class PickupIcons
    {
        private static readonly Dictionary<PickupIndex, AssetOrDirectReference<Sprite>> Loading =
            new Dictionary<PickupIndex, AssetOrDirectReference<Sprite>>();

        // The icon, or none while it loads.
        public static Sprite Get(PickupDef pickup)
        {
            if (pickup == null) return null;
            if (!Loading.TryGetValue(pickup.pickupIndex, out AssetOrDirectReference<Sprite> icon))
            {
                icon = AssetOrDirectReference<Sprite>.GetNewInstance(pickup.GetIconReference(), true, pickup.GetIcon());
                Loading.Add(pickup.pickupIndex, icon);
            }
            return icon.Result;
        }
    }
}
