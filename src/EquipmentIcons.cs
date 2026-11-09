using System.Collections.Generic;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine;

namespace SevenUI
{
    // Equipment icons for the mod's own images, loaded as the game loads them for its equipment slots: from the
    // equipment's icon reference, or its sprite when it has one, in the background. Kept once loaded, since a player
    // holds only a few equipment.
    internal static class EquipmentIcons
    {
        private static readonly Dictionary<EquipmentIndex, AssetOrDirectReference<Sprite>> loading =
            new Dictionary<EquipmentIndex, AssetOrDirectReference<Sprite>>();

        // The icon, or none while it loads or for no equipment.
        public static Texture Get(EquipmentIndex index)
        {
            if (!loading.TryGetValue(index, out AssetOrDirectReference<Sprite> icon))
            {
                EquipmentDef equipment = EquipmentCatalog.GetEquipmentDef(index);
                if (!equipment) return null;
                icon = AssetOrDirectReference<Sprite>.GetNewInstance(equipment.GetIconReference(), true, equipment.GetIcon());
                loading.Add(index, icon);
            }
            Sprite sprite = icon.Result;
            return sprite ? sprite.texture : null;
        }
    }
}
