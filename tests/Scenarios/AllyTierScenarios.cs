using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // Drones of different upgrade tiers must not share an ally card.
    internal static class AllyTierScenarios
    {
        // Long enough for the game to rename the cards and the mod to regroup them.
        private const float SettleSeconds = 0.5f;
        private const int DroneCount = 2;
        private const int MarksReported = 5;

        // The AI master of a Gunner Turret, the purchasable turret.
        private const string TurretMaster = "Turret1Master";

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "ally_tiers_split_kind", Stages.TitanicPlains, SplitKind);
            yield return new Scenario(1, "ally_tiers_combined_then_new", Stages.TitanicPlains, CombinedThenNew);
            yield return new Scenario(1, "ally_tiers_tagged", Stages.TitanicPlains, Tagged);
            yield return new Scenario(1, "ally_tiers_turret_untagged", Stages.TitanicPlains, TurretUntagged);
        }

        // I1: four Gunner Drones of tiers 1 to 4 (0 to 3 upgrades) each show their base name in the tier's color and
        // the tier's badge ("T3", none at tier 1), never the game's "(Tier 3)" suffix, on their card and on the scoreboard's ally tile;
        // with FlatPanels off the cards read as the game writes them again.
        private static IEnumerator Tagged(Harness h)
        {
            List<CharacterMaster> drones = new List<CharacterMaster>();
            DroneCombining.RemoveOtherDrones(h, drones);
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 4, drones);
            yield return h.Until(() => drones.All(master => master && master.GetBody()), "the four drones' bodies");
            for (int i = 0; i < drones.Count; i++)
            {
                if (i > 0) drones[i].inventory.GiveItemPermanent(DLC3Content.Items.DroneUpgradeHidden, i);
            }
            yield return h.Wait(SettleSeconds * 2f);
            string baseName = Language.GetString(drones[0].GetBody().baseNameToken);
            string problem = null;
            yield return h.Until(() => (problem = TagProblem(h, drones, baseName)) == null, () => $"{problem} {h.Diagnosis()}");
            yield return h.Until(() => (problem = drones.Skip(1).Select(master => BadgeProblem(CardOf(h, master).nameLabel))
                                           .FirstOrDefault(found => found != null)) == null, () => $"on an ally card, {problem}");
            yield return h.Screenshot("tiers");

            yield return ScoreboardSetUp.Open(h);
            ScoreboardItemsBoard board = null;
            ScoreboardAlliesSummary summary = null;
            yield return h.Until(() => (board = SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardItemsBoard>()) &&
                                       (summary = ScoreboardAlliesScenarios.OwnSummary(board)) && summary.Clickable, "the viewer's allies line");
            yield return ScoreboardAlliesScenarios.Click(h, summary);
            string tiles = null;
            yield return h.Until(() =>
                {
                    tiles = string.Join(" | ", Enumerable.Range(0, board.Allies.TileCount).Select(board.Allies.TileText));
                    return board.Allies.TileCount >= 4 && Enumerable.Range(2, 3).All(tier => tiles.Contains(DroneTier.Tag(tier) + "</link>")) &&
                           !tiles.Contains("T1") && !tiles.Contains("(Tier");
                },
                () => $"the scoreboard's ally tiles read \"{tiles}\", not a badge per tier 2 to 4 and none on tier 1 {h.Diagnosis()}");
            yield return h.Until(() => (problem = Enumerable.Range(0, board.Allies.TileCount)
                    .Select(board.Allies.TileLabel).Where(label => DroneTier.HasBadge(label.text)).Select(BadgeProblem)
                    .FirstOrDefault(found => found != null)) == null, () => $"on a scoreboard tile, {problem}");
            yield return h.Screenshot("tiles");
            HUD.SetScoreboardActive(LocalUserManager.GetFirstLocalUser(), false, EScoreboardDeactivateReturnContext.Game);

            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            h.OnCleanup(() => h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true));
            yield return h.Until(() => drones.All(master =>
                {
                    AllyCardController card = CardOf(h, master);
                    return card && card.nameLabel.text == Util.GetBestBodyName(master.GetBody().gameObject);
                }), "the game's names back with FlatPanels off");
        }

        // A Gunner Turret, which the Drone Combiner can't upgrade (its body lacks the Drone flag), keeps the game's plain name on
        // its card and its scoreboard tile: no tier color, no badge.
        private static IEnumerator TurretUntagged(Harness h)
        {
            List<CharacterMaster> turrets = new List<CharacterMaster>();
            DroneCombining.RemoveOtherDrones(h, turrets);
            yield return Allies.SummonDrones(h, TurretMaster, 1, turrets);
            yield return h.Until(() => turrets[0] && turrets[0].GetBody(), "the turret's body");
            if (DroneTier.HasTier(turrets[0].GetBody())) throw h.Fail($"the Gunner Turret has a tier (body flags {turrets[0].GetBody().bodyFlags})");
            yield return h.Wait(SettleSeconds * 2f);
            string gameName = Util.GetBestBodyName(turrets[0].GetBody().gameObject);
            AllyCardController card = null;
            yield return h.Until(() => (card = CardOf(h, turrets[0])) && card.nameLabel.text == gameName,
                () => $"the turret's card reads \"{(card ? card.nameLabel.text : "no card")}\", not \"{gameName}\" {h.Diagnosis()}");

            yield return ScoreboardSetUp.Open(h);
            ScoreboardItemsBoard board = null;
            ScoreboardAlliesSummary summary = null;
            yield return h.Until(() => (board = SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardItemsBoard>()) &&
                                       (summary = ScoreboardAlliesScenarios.OwnSummary(board)) && summary.Clickable, "the viewer's allies line");
            yield return ScoreboardAlliesScenarios.Click(h, summary);
            string tile = null;
            yield return h.Until(() => board.Allies.TileCount >= 1 && (tile = board.Allies.TileText(0)).StartsWith(gameName + " ") &&
                                       !tile.Contains("<mark") && !tile.Contains("T1"),
                () => $"the turret's tile reads \"{tile}\", not the plain \"{gameName}\" and its count {h.Diagnosis()}");
            HUD.SetScoreboardActive(LocalUserManager.GetFirstLocalUser(), false, EScoreboardDeactivateReturnContext.Game);
        }

        // Design B: the badge's outline and fill, drawn before the label so under its text, centered on the tag's letters
        // both ways to within a screen pixel, and reaching past them on every side.
        private const float CenterTolerance = 1.01f;

        private static string BadgeProblem(TMP_Text label)
        {
            if (!label) return "no label";
            Transform parent = label.transform.parent;
            Image outline = parent.Find(TierBadge.OutlineName)?.GetComponent<Image>();
            Image fill = parent.Find(TierBadge.FillName)?.GetComponent<Image>();
            if (!outline || !fill || !outline.enabled || !fill.enabled) return $"\"{label.text}\" has no badge drawn";
            int index = label.transform.GetSiblingIndex();
            if (outline.transform.GetSiblingIndex() >= index || fill.transform.GetSiblingIndex() >= index ||
                fill.transform.GetSiblingIndex() < outline.transform.GetSiblingIndex())
                return $"\"{label.text}\"'s badge draws over its text";
            TMP_TextInfo info = label.textInfo;
            TMP_LinkInfo link = info.linkInfo.Take(info.linkCount).FirstOrDefault(found => DroneTier.IsBadgeLink(found.GetLinkID(), out _));
            if (link.linkTextLength == 0) return $"\"{label.text}\" has no tag in its layout";
            TMP_CharacterInfo first = info.characterInfo[link.linkTextfirstCharacterIndex];
            TMP_CharacterInfo last = info.characterInfo[link.linkTextfirstCharacterIndex + link.linkTextLength - 1];
            Vector2 letters = Screen(label.transform, (first.bottomLeft + last.topRight) / 2f);
            Rect box = ScreenRect(outline.rectTransform);
            Vector2 offset = box.center - letters;
            if (Mathf.Abs(offset.x) > CenterTolerance || Mathf.Abs(offset.y) > CenterTolerance)
                return $"\"{label.text}\"'s badge is {offset} screen pixels off its letters' middle";
            Rect inner = ScreenRect(fill.rectTransform);
            if (!box.Contains(inner.min) || !box.Contains(inner.max) || inner.width >= box.width)
                return $"\"{label.text}\"'s badge fill {inner} isn't inside its outline {box}";
            Vector2 left = Screen(label.transform, new Vector3(first.origin, first.baseLine));
            Vector2 right = Screen(label.transform, new Vector3(last.xAdvance, last.baseLine));
            if (box.xMin >= left.x || box.xMax <= right.x || box.yMin >= left.y)
                return $"\"{label.text}\"'s badge {box} doesn't reach past its letters ({left} to {right})";
            return null;
        }

        private static Vector2 Screen(Transform transform, Vector3 local) =>
            RectTransformUtility.WorldToScreenPoint(null, transform.TransformPoint(local));

        private static Rect ScreenRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static AllyCardController CardOf(Harness h, CharacterMaster master) =>
            SevenUIChecks.HudRoot(h).GetComponentsInChildren<AllyCardController>(true).FirstOrDefault(card => card.sourceMaster == master);

        private static string TagProblem(Harness h, List<CharacterMaster> drones, string baseName)
        {
            foreach (CharacterMaster master in drones)
            {
                int upgrades = master.inventory.GetItemCountEffective(DLC3Content.Items.DroneUpgradeHidden);
                AllyCardController card = CardOf(h, master);
                if (!card || !card.nameLabel) return $"no card for the drone with {upgrades} upgrades";
                string text = card.nameLabel.text;
                string hex = UnityEngine.ColorUtility.ToHtmlStringRGB(DroneTier.ColorOf(upgrades + 1));
                if (!text.StartsWith("<color=#" + hex + ">" + baseName + "</color>") || text.Contains("T" + (upgrades + 1)) != (upgrades > 0) || text.Contains("("))
                {
                    return $"the tier {upgrades + 1} card reads \"{text}\", not {baseName} in #{hex} {(upgrades > 0 ? $"with a T{upgrades + 1} badge" : "with no badge")}";
                }
            }
            return null;
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
            yield return h.Screenshot("upgraded");
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
            List<string> marks = new List<string>();
            System.Action noDeadMark = () => RecordDeadMarks(h, drones, marks);
            DroneCombining.RemoveOtherDrones(h, drones);
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
            if (marks.Count > 0)
            {
                throw h.Fail($"the dead mark showed over a living drone's card in {marks.Count} frames while combining: " +
                             string.Join("; ", marks.Take(MarksReported)));
            }
        }

        // Every frame a living drone's card shows the dead mark, with what its bar follows, for the scenario's end.
        private static void RecordDeadMarks(Harness h, List<CharacterMaster> drones, List<string> marks)
        {
            if (AllyCardChecks.Cards(h).Any(card => card.gameObject.activeSelf && (object)card.sourceMaster != null && !card.sourceMaster)) return;
            foreach (AllyCardController card in Shown(h, drones))
            {
                Image dead = card.healthBar ? card.healthBar.deadImage : null;
                if (!(dead && dead.enabled && dead.gameObject.activeInHierarchy && Alive(card.sourceMaster))) continue;
                HealthComponent source = card.healthBar.source;
                CharacterBody body = card.sourceMaster.GetBody();
                string mark = $"frame {UnityEngine.Time.frameCount}: {card.nameLabel.text} (bar follows " +
                              $"{(source ? source.name + (source.alive ? ", alive" : ", dead") : (object)source != null ? "a destroyed body" : "nothing")}; " +
                              $"its drone {(body.currentVehicle ? "seated" : "free")}, tier {DroneCombining.Tier(card.sourceMaster) + 1})";
                marks.Add(mark);
                h.Log($"[ally_tiers] dead mark: {mark}");
            }
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
