using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine.UI;

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
            yield return new Scenario(1, "ally_tiers_combined_then_new", Stages.TitanicPlains, CombinedThenNew);
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

        // The ticket's case, through the game's own Drone Combiner: two Gunner Drones are combined into one of tier 2,
        // then new tier 1 Gunner Drones join, one and then a second; the two tier 1s are combined into a second tier 2,
        // which must share the first's card, and the two tier 2s into a tier 3. A new tier 1 also joins beside the two
        // tier 2s and beside the tier 3, where it must keep a card of its own. After each step every tier present has
        // exactly one card, counting that tier's drones, and while a combine runs no card of theirs shows the dead mark.
        private static IEnumerator CombinedThenNew(Harness h)
        {
            List<CharacterMaster> drones = new List<CharacterMaster>();
            System.Action noDeadMark = () => CheckNoDeadMark(h, drones, "while combining");
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 2, drones);
            yield return Settled(h, drones, "two tier 1");
            yield return DroneCombining.Combine(h, drones, noDeadMark);
            yield return Settled(h, drones, "one tier 2");
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 1, drones);
            yield return Settled(h, drones, "tier 2 and a new tier 1", "new_tier_one");
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 1, drones);
            yield return Settled(h, drones, "tier 2 and two tier 1", "two_tier_one");
            yield return DroneCombining.Combine(h, drones, noDeadMark);
            yield return Settled(h, drones, "two tier 2", "two_tier_two");
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 1, drones);
            yield return Settled(h, drones, "two tier 2 and a new tier 1", "two_tier_two_new_tier_one");
            // The new tier 1 has no partner, so the combiner pairs the two tier 2s.
            yield return DroneCombining.Combine(h, drones, noDeadMark);
            yield return Settled(h, drones, "tier 3 and tier 1", "tier_three");
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 1, drones);
            yield return Settled(h, drones, "tier 3 and two tier 1", "tier_three_two_tier_one");
        }

        private static IEnumerator Settled(Harness h, List<CharacterMaster> drones, string when, string screenshot = null)
        {
            yield return h.Wait(SettleSeconds);
            if (screenshot != null)
            {
                yield return h.Screenshot(screenshot);
            }
            CheckTiers(h, drones, when);
        }

        // One shown card per tier among the living drones, reading "×N" for N of that tier (nothing for one), with no dead
        // mark, and the stack's layout as the checks work it out.
        private static void CheckTiers(Harness h, List<CharacterMaster> drones, string when)
        {
            List<CharacterMaster> living = drones.Where(Alive).ToList();
            List<AllyCardController> shown = Shown(h, drones);
            List<string> seen = new List<string>();
            foreach (IGrouping<int, CharacterMaster> tier in living.GroupBy(DroneCombining.Tier).OrderBy(group => group.Key))
            {
                List<AllyCardController> cards = shown.Where(card => DroneCombining.Tier(card.sourceMaster) == tier.Key).ToList();
                if (cards.Count != 1)
                {
                    throw h.Fail($"{when}: {cards.Count} cards for the {tier.Count()} tier {tier.Key + 1} drones, not 1 {h.Diagnosis()}");
                }
                TMP_Text count = cards[0].nameLabel ? cards[0].nameLabel.transform.Find(AllyKindParts.CountName)?.GetComponent<TMP_Text>() : null;
                string text = count && count.gameObject.activeInHierarchy ? count.text : null;
                string expected = tier.Count() > 1 ? "×" + tier.Count() : null;
                if (text != expected)
                {
                    throw h.Fail($"{when}: the tier {tier.Key + 1} card \"{cards[0].nameLabel.text}\" reads '{text}', not '{expected}' {h.Diagnosis()}");
                }
                seen.Add($"{cards[0].nameLabel.text} {text}".Trim());
            }
            if (shown.Count != seen.Count) throw h.Fail($"{when}: {shown.Count} cards for {seen.Count} tiers {h.Diagnosis()}");
            CheckNoDeadMark(h, drones, when);
            AllyCardChecks.Layout layout = AllyCardChecks.Measure(h);
            if (layout.Problem != null) throw h.Fail($"{when}: {layout.Problem}");
            h.Log($"[ally_tiers] {when}: {string.Join(" | ", seen)}");
        }

        private static void CheckNoDeadMark(Harness h, List<CharacterMaster> drones, string when)
        {
            // The frame a drone is destroyed, the game's cards still name it until it hands them on in the next Update,
            // and its bar, with no source, shows the mark; nothing else can show then.
            if (AllyCardChecks.Cards(h).Any(card => card.gameObject.activeSelf && (object)card.sourceMaster != null && !card.sourceMaster)) return;
            foreach (AllyCardController card in Shown(h, drones))
            {
                Image dead = card.healthBar ? card.healthBar.deadImage : null;
                if (dead && dead.enabled && dead.gameObject.activeInHierarchy && Alive(card.sourceMaster))
                {
                    throw h.Fail($"{when}: {card.nameLabel.text}'s card shows the dead mark over a living drone");
                }
            }
        }

        private static List<AllyCardController> Shown(Harness h, List<CharacterMaster> drones) =>
            AllyCardChecks.Cards(h).Where(card => card.gameObject.activeSelf && card.sourceMaster && drones.Contains(card.sourceMaster)).ToList();

        private static bool Alive(CharacterMaster master) =>
            master && master.GetBody() && master.GetBody().healthComponent.alive;
    }
}
