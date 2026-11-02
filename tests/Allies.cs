using System.Collections;
using ModTestRunner;
using RoR2;

namespace SevenUI.Tests
{
    // Gunner Drones bought for the player, so ally cards show. The runner cleans them up with the scenario's spawns.
    internal static class Allies
    {
        // Enough for the drones at any difficulty or stage count the tests reach.
        private const int Money = 2000;

        public static IEnumerator BuyDrones(Harness h, int count)
        {
            h.Cmd($"{Commands.GiveMoney} {Money}");
            for (int i = 0; i < count; i++)
            {
                PurchaseInteraction drone = null;
                yield return h.Spawned<PurchaseInteraction>($"{Commands.SpawnInteractable} {GameNames.BrokenDrone}",
                    found => drone = found);
                yield return h.Interact(drone.gameObject);
            }
        }
    }
}
