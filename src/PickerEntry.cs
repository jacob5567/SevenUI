using RoR2;
using RoR2.UI;
using UnityEngine;

namespace SevenUI
{
    // What a picker panel shows for one option: its name, its description and its tier's color. An item, equipment or
    // drone pickup's from the pickup catalog, a scrapper's drone from its definition.
    internal sealed class PickerEntry
    {
        public string NameToken;
        public Color Color = Palette.Text;
        // The pickup an item panel's option gives; null for a drone, which gives no item to count.
        public PickupDef Pickup;
        public DroneDef Drone;
        // What picking the option does to the player's stack of it.
        public StackChange Change = StackChange.Gain;

        public string Name => NameToken != null ? Language.GetString(NameToken) : "";

        public static PickerEntry ForPickup(PickupDef pickup, StackChange change = StackChange.Gain) => new PickerEntry
        {
            Pickup = pickup,
            Change = change,
            NameToken = pickup?.nameToken,
            Color = pickup != null ? GameColors.ToCanvas(pickup.baseColor) : Palette.Text,
        };

        public static PickerEntry ForDrone(DroneDef drone) => new PickerEntry
        {
            Drone = drone,
            NameToken = drone ? drone.nameToken : null,
            Color = drone ? GameColors.ToCanvas(ColorCatalog.GetColor(drone.color)) : Palette.Text,
        };

        // The description as the panel shows it: the game's short text, or the full one when LookingGlass is set to.
        public string Description(MPButton button, bool full)
        {
            if (Drone)
            {
                string token = full && !string.IsNullOrEmpty(Drone.descriptionToken) ? Drone.descriptionToken : Drone.pickupToken;
                return string.IsNullOrEmpty(token) ? "" : Language.GetString(token);
            }
            return PickupDescriptions.For(Pickup, button, full, Change);
        }
    }
}
