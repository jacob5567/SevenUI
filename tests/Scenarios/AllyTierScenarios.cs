using System.Collections;
using System.Collections.Generic;
using ModTestRunner;
using RoR2;

namespace SevenUI.Tests
{
    // Drones of different upgrade tiers must not share an ally card.
    internal static class AllyTierScenarios
    {
        // Long enough for the game to rename the cards and the mod to regroup them.
        private const float SettleSeconds = 0.5f;
        private const int DroneCount = 2;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "ally_tiers_split_kind", Stages.TitanicPlains, SplitKind);
        }

        // Two Gunner Drones share one card; one is given a drone upgrade, which must split them into two cards, and the
        // upgrade is taken back to merge them again. The upgrade is a hidden item on the same body, so the name cached
        // for the body has to follow it.
        private static IEnumerator SplitKind(Harness h)
        {
            List<CharacterMaster> drones = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, DroneCount, drones);
            yield return h.Wait(SettleSeconds);
            int together = AllyCardChecks.Measure(h).Kinds;
            Inventory inventory = drones[0].GetBody().inventory;
            inventory.GiveItemPermanent(DLC3Content.Items.DroneUpgradeHidden, 1);
            yield return h.Wait(SettleSeconds);
            AllyCardChecks.Layout split = AllyCardChecks.Measure(h);
            inventory.RemoveItemPermanent(DLC3Content.Items.DroneUpgradeHidden, 1);
            yield return h.Wait(SettleSeconds);
            int merged = AllyCardChecks.Measure(h).Kinds;
            if (split.Kinds != together + 1)
            {
                throw h.Fail($"an upgraded drone should split its kind: {together} kinds before, {split.Kinds} after");
            }
            if (split.Problem != null) throw h.Fail($"cards wrong with two tiers: {split.Problem}");
            if (merged != together) throw h.Fail($"the kinds should merge again: {together} before, {merged} after");
        }
    }
}
