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
    // Narrows down when a compact ally card's portrait grows past its row: each scenario walks one sequence of adding and
    // removing allies or turning settings off and on, and after every step logs which compact portraits are wider than
    // they ask to be, with the card's layout, and takes a screenshot at the steps worth seeing. A scenario runs every
    // step whatever it finds and fails at the end with the steps that went wrong, so one run shows which changes do it.
    internal static class AllyPortraitScenarios
    {
        private const float Tolerance = 0.5f;
        // Long enough for the game to add or remove a card and the mod to lay it out.
        private const float SettleSeconds = 0.5f;
        private const float DeathStyleSeconds = 0.1f;
        // The game destroys a dead drone's master a second after its death; well past that.
        private const float WatchTimeout = 5f;
        private static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();
        private const int MaxLoneAllies = 20;
        private const int KindSize = 5;
        private const int StandInPlayers = 2;
        // The share of an ally's health given to it as barrier.
        private const float BarrierShare = 0.5f;
        private const int DronesPerStandIn = 2;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "ally_portraits_growing", Stages.TitanicPlains, Growing);
            yield return new Scenario(1, "ally_portraits_more_row_and_back", Stages.TitanicPlains, MoreRowAndBack);
            yield return new Scenario(1, "ally_portraits_kind_shrinks", Stages.TitanicPlains, KindShrinks);
            yield return new Scenario(1, "ally_portraits_kind_lead_removed", Stages.TitanicPlains, KindLeadRemoved);
            yield return new Scenario(1, "ally_portraits_kind_dead_member", Stages.TitanicPlains, KindDeadMember);
            yield return new Scenario(1, "ally_portraits_other_kind_dies", Stages.TitanicPlains, OtherKindDies);
            yield return new Scenario(1, "ally_portraits_kind_member_dies", Stages.TitanicPlains, KindMemberDies);
            yield return new Scenario(1, "ally_portraits_drone_scrapped", Stages.TitanicPlains, DroneScrapped);
            yield return new Scenario(1, "ally_portraits_players_stay_full", Stages.TitanicPlains, PlayersStayFull);
            yield return new Scenario(1, "ally_portraits_compact_barrier", Stages.TitanicPlains, CompactBarrier);
            yield return new Scenario(1, "ally_portraits_group_toggle", Stages.TitanicPlains, GroupToggle);
            yield return new Scenario(1, "ally_portraits_compact_toggle", Stages.TitanicPlains, CompactToggle);
            yield return new Scenario(1, "ally_portraits_flat_panels_toggle", Stages.TitanicPlains, FlatPanelsToggle);
        }

        // What each step found, kept for the scenario's end.
        private sealed class Trace
        {
            private readonly Harness h;
            private readonly List<string> bad = new List<string>();

            public Trace(Harness h)
            {
                this.h = h;
            }

            // Waits for the cards to settle, then logs the step and, if asked, takes a screenshot named after it.
            public IEnumerator Step(string step, bool screenshot = false)
            {
                yield return h.Wait(SettleSeconds);
                List<AllyCardController> cards = AllyCardChecks.Cards(h);
                List<AllyCardController> shown = cards.Where(card => card.gameObject.activeSelf).ToList();
                int compact = shown.Count(IsCompact);
                int more = AllyCardChecks.More(h).Count;
                List<AllyCardController> wrong = shown.Where(card => IsCompact(card) && PortraitOff(card)).ToList();
                string summary = $"{step}: {cards.Count} cards, {shown.Count} shown, {compact} compact, " +
                                 $"{more} more rows, " +
                                 (wrong.Count == 0 ? "portraits right" : $"{wrong.Count} compact portraits too wide");
                h.Log("[ally_portraits] " + summary);
                if (wrong.Count > 0)
                {
                    bad.Add(summary);
                    foreach (AllyCardController card in wrong)
                    {
                        h.Log($"[ally_portraits]   {Name(card)}: {AllyCardChecks.RowLayout(card)}");
                    }
                }
                if (screenshot)
                {
                    yield return h.Screenshot(step.Replace(' ', '_'));
                }
            }

            public void Finish()
            {
                if (bad.Count > 0) throw h.Fail($"compact portraits too wide after: {string.Join(" | ", bad)}");
            }
        }

        // Lone allies added one at a time until the cards turn compact, then until the more row shows.
        private static IEnumerator Growing(Harness h)
        {
            Trace trace = new Trace(h);
            List<CharacterMaster> allies = new List<CharacterMaster>();
            yield return trace.Step("start");
            yield return GrowUntil(h, trace, allies, () => AnyCompact(h), "compact");
            yield return trace.Step("first compact", true);
            yield return Allies.SummonLoneAllies(h, 1, allies);
            yield return trace.Step("one more while compact", true);
            yield return GrowUntil(h, trace, allies, () => MoreShown(h), "more row");
            yield return trace.Step("first more row", true);
            yield return Allies.SummonLoneAllies(h, 1, allies);
            yield return trace.Step("one more under the more row", true);
            trace.Finish();
        }

        // Up to the more row, then allies removed one pair at a time, newest first, until the cards are full size again.
        private static IEnumerator MoreRowAndBack(Harness h)
        {
            Trace trace = new Trace(h);
            List<CharacterMaster> allies = new List<CharacterMaster>();
            yield return GrowUntil(h, trace, allies, () => MoreShown(h), "more row");
            yield return trace.Step("more row", true);
            int removed = 0;
            while (allies.Count >= 2 && AnyCompact(h))
            {
                RemoveLast(allies, 2);
                removed++;
                yield return trace.Step($"removed pair {removed}", true);
            }
            yield return trace.Step("full size again", true);
            trace.Finish();
        }

        // A kind of five Healing Drones shares a card while lone allies make the cards compact; its members are removed
        // one at a time until it is a single ally's card, which then turns compact.
        private static IEnumerator KindShrinks(Harness h)
        {
            Trace trace = new Trace(h);
            List<CharacterMaster> kind = new List<CharacterMaster>();
            List<CharacterMaster> allies = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.HealingDroneMaster, KindSize, kind);
            yield return trace.Step("kind of five");
            yield return GrowUntil(h, trace, allies, () => AnyCompact(h), "compact");
            yield return trace.Step("compact with the kind", true);
            while (kind.Count > 1)
            {
                RemoveLast(kind, 1);
                yield return trace.Step($"kind of {kind.Count}", kind.Count == 1);
            }
            trace.Finish();
        }

        // As above, but the kind's first member goes each time, so a card that was hidden as the kind's spare becomes the
        // one the kind shares, or a compact single card.
        private static IEnumerator KindLeadRemoved(Harness h)
        {
            Trace trace = new Trace(h);
            List<CharacterMaster> kind = new List<CharacterMaster>();
            List<CharacterMaster> allies = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.HealingDroneMaster, KindSize, kind);
            yield return GrowUntil(h, trace, allies, () => AnyCompact(h), "compact");
            yield return trace.Step("compact with the kind", true);
            while (kind.Count > 1)
            {
                Allies.Remove(kind.Take(1));
                kind.RemoveAt(0);
                yield return trace.Step($"lead removed, kind of {kind.Count}", kind.Count == 1);
            }
            trace.Finish();
        }

        // Three Gunner Drones share a card. The first one dies, its body lingering: the card must not show the game's dead
        // mark while two live, and must count two. Then a Healing Drone, of another kind, is removed, and the same holds.
        private static IEnumerator KindDeadMember(Harness h)
        {
            const int Drones = 3;
            List<CharacterMaster> kind = new List<CharacterMaster>();
            List<CharacterMaster> other = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, Drones, kind);
            yield return Allies.SummonDrones(h, Allies.HealingDroneMaster, 1, other);
            yield return h.Wait(SettleSeconds);
            CheckSharedCard(h, kind, "before a death");
            // The game destroys a dead drone's master a second after its death (CharacterMaster.OnBodyDeath); the two
            // checks after the death fall inside that second, while its body lingers.
            kind[0].GetBody().healthComponent.Suicide();
            yield return h.Until(() => !kind[0].GetBody() || !kind[0].GetBody().healthComponent.alive, "the first drone's death");
            // A moment for the cards to be styled, before the dead drone's body goes.
            yield return h.Wait(DeathStyleSeconds);
            yield return h.Screenshot("right_after_death");
            CheckSharedCard(h, kind, "right after the death");
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("death_settled");
            CheckSharedCard(h, kind, "after the death settled");
            Allies.Remove(other);
            other.Clear();
            yield return h.Until(() => !kind[0], "the dead drone's master to be destroyed");
            yield return h.Wait(SettleSeconds);
            CheckSharedCard(h, kind, "after another kind's drone was removed");
        }

        // A Healing Drone on its own card above two Gunner Drones sharing one, as in play. The Healing Drone dies: its card
        // shows the game's dead mark, and when the game destroys its master it hands the cards on down the list, so the
        // card with the mark becomes a Gunner Drone's. The shared card must never show the mark.
        private static IEnumerator OtherKindDies(Harness h)
        {
            const int Drones = 2;
            List<CharacterMaster> other = new List<CharacterMaster>();
            List<CharacterMaster> kind = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.HealingDroneMaster, 1, other);
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, Drones, kind);
            yield return h.Wait(SettleSeconds);
            CheckSharedCard(h, kind, "before the other drone's death");
            other[0].GetBody().healthComponent.Suicide();
            yield return h.Until(() => !other[0].GetBody() || !other[0].GetBody().healthComponent.alive, "the Healing Drone's death");
            yield return h.Wait(DeathStyleSeconds);
            CheckSharedCard(h, kind, "right after the other drone's death");
            yield return h.Until(() => !other[0], "the Healing Drone's master to be destroyed");
            // Checked every frame while the game hands the cards on, then once settled.
            float until = Time.time + SettleSeconds;
            while (Time.time < until)
            {
                yield return EndOfFrame;
                CheckSharedCard(h, kind, "while the cards are handed on");
            }
            CheckSharedCard(h, kind, "after the cards settled");
        }

        // Three Gunner Drones share a card and the last, not the one whose card is shared, dies: its own hidden card shows
        // the game's dead mark until its master goes, and the shared card must never show it, checked every frame from
        // the death until the cards have settled after the master is destroyed.
        private static IEnumerator KindMemberDies(Harness h)
        {
            const int Drones = 3;
            List<CharacterMaster> kind = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, Drones, kind);
            yield return h.Wait(SettleSeconds);
            CheckSharedCard(h, kind, "before a member's death");
            CharacterMaster member = kind[Drones - 1];
            member.GetBody().healthComponent.Suicide();
            yield return WatchSharedCard(h, kind, () => !member, "while the dead member lingers");
            yield return h.Screenshot("member_died");
            yield return WatchSharedCard(h, kind, null, "while the cards are handed on");
            CheckSharedCard(h, kind, "after the cards settled");
        }

        // Scrapping, as a player does at a Drone Scrapper, destroys a drone at once with no death. A Healing Drone on its
        // own card above two Gunner Drones sharing one is scrapped, so its card goes and the cards below are handed on;
        // then, with a third Gunner Drone, the kind's lead is scrapped, so the shared card passes to another member. The
        // shared card must never show the dead mark and must count the living, checked every frame after each scrap.
        private static IEnumerator DroneScrapped(Harness h)
        {
            List<CharacterMaster> other = new List<CharacterMaster>();
            List<CharacterMaster> kind = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.HealingDroneMaster, 1, other);
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 2, kind);
            yield return h.Wait(SettleSeconds);
            CheckSharedCard(h, kind, "before the Healing Drone is scrapped");
            yield return DroneScrapping.Scrap(h, other[0]);
            yield return WatchSharedCard(h, kind, () => !other[0], "while the Healing Drone is scrapped");
            yield return WatchSharedCard(h, kind, null, "while the cards are handed on after the Healing Drone's scrap");

            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 1, kind);
            yield return h.Wait(SettleSeconds);
            CheckSharedCard(h, kind, "with a third Gunner Drone");
            CharacterMaster lead = AllyCardChecks.Cards(h).Where(card => card.gameObject.activeSelf && kind.Contains(card.sourceMaster))
                .Select(card => card.sourceMaster).FirstOrDefault();
            if (!lead) throw h.Fail($"no shown card for the Gunner Drones {h.Diagnosis()}");
            yield return DroneScrapping.Scrap(h, lead);
            yield return WatchSharedCard(h, kind, () => !lead, "while the kind's lead is scrapped");
            yield return h.Screenshot("lead_scrapped");
            yield return WatchSharedCard(h, kind, null, "while the cards are handed on after the lead's scrap");
            CheckSharedCard(h, kind, "after the cards settled");
        }

        // Checks the kind's shared card at the end of every frame, as drawn, until done holds, or for SettleSeconds without
        // one. Not straight after Update: the game hands its cards on in Update and the mod restyles them in LateUpdate.
        private static IEnumerator WatchSharedCard(Harness h, List<CharacterMaster> kind, System.Func<bool> done, string when)
        {
            float until = Time.time + (done == null ? SettleSeconds : WatchTimeout);
            while (Time.time < until && (done == null || !done()))
            {
                yield return EndOfFrame;
                // The frame a drone is destroyed, its card still names it until the game hands the cards on in the next
                // Update; only the dead mark can be checked then.
                if (AllyCardChecks.Cards(h).Any(card => card.gameObject.activeSelf && (object)card.sourceMaster != null && !card.sourceMaster))
                {
                    CheckNoDeadMark(h, when);
                    continue;
                }
                CheckSharedCard(h, kind, when);
            }
            if (done != null && !done()) throw h.Fail($"{when}: not done after {WatchTimeout:0} s {h.Diagnosis()}");
        }

        private static void CheckNoDeadMark(Harness h, string when)
        {
            foreach (AllyCardController card in AllyCardChecks.Cards(h).Where(card => card.gameObject.activeSelf))
            {
                Image dead = card.healthBar ? card.healthBar.deadImage : null;
                Transform count = card.nameLabel ? card.nameLabel.transform.Find(AllyKindParts.CountName) : null;
                if (dead && dead.enabled && dead.gameObject.activeInHierarchy && count && count.gameObject.activeInHierarchy)
                {
                    throw h.Fail($"{when}: the shared card {card.nameLabel.text} shows the dead mark");
                }
            }
        }

        // The card of the kind's living allies shows no dead mark and counts them; the dead drone's card is hidden.
        private static void CheckSharedCard(Harness h, List<CharacterMaster> kind, string when)
        {
            List<CharacterMaster> living = kind.Where(master => master && master.GetBody() &&
                master.GetBody().healthComponent.alive).ToList();
            List<AllyCardController> cards = AllyCardChecks.Cards(h).Where(card => card.gameObject.activeSelf &&
                card.sourceMaster && kind.Contains(card.sourceMaster)).ToList();
            if (cards.Count != 1) throw h.Fail($"{when}: {cards.Count} cards shown for the kind, not 1 {h.Diagnosis()}");
            AllyCardController card = cards[0];
            if (!living.Contains(card.sourceMaster)) throw h.Fail($"{when}: the shared card belongs to a dead drone");
            Image dead = card.healthBar ? card.healthBar.deadImage : null;
            if (dead && dead.enabled && dead.gameObject.activeInHierarchy) throw h.Fail($"{when}: the shared card shows the dead mark");
            TMP_Text count = card.nameLabel ? card.nameLabel.transform.Find(AllyKindParts.CountName)?.GetComponent<TMP_Text>() : null;
            string expected = living.Count > 1 ? "×" + living.Count : null;
            if ((count ? count.text : null) != expected)
            {
                throw h.Fail($"{when}: the shared card reads '{(count ? count.text : "")}', not '{expected}'");
            }
            h.Log($"[ally_portraits] {when}: shared card of {living.Count} living, {(count ? count.text : "no count")}");
        }

        // Lone allies until the cards turn compact, then each shown ally given a barrier of half its health: a screenshot
        // of the compact rows' barriers (the style check is in AllyCardChecks, as a barrier decays too fast to measure).
        private static IEnumerator CompactBarrier(Harness h)
        {
            Trace trace = new Trace(h);
            List<CharacterMaster> allies = new List<CharacterMaster>();
            yield return GrowUntil(h, trace, allies, () => AnyCompact(h), "compact");
            foreach (CharacterMaster ally in allies)
            {
                HealthComponent health = ally && ally.GetBody() ? ally.GetBody().healthComponent : null;
                if (health) health.AddBarrier(health.fullCombinedHealth * BarrierShare);
            }
            yield return h.Wait(DeathStyleSeconds);
            yield return h.Screenshot("barriers");
        }

        // Two stand-in players, each with drones of their own, and enough lone allies of the viewer's for compact cards and
        // the more row: every player's card must stay full size, shown and above the chat, in the game's player order, with
        // the player's own allies and more row between that card and the next section's, and the viewer's own allies
        // below every player's section. Then each stand-in gets a second
        // kind, and lone allies are added until at least two sections fold, each into a more row counting its own allies.
        private static IEnumerator PlayersStayFull(Harness h)
        {
            Trace trace = new Trace(h);
            List<CharacterMaster> players = new List<CharacterMaster>();
            yield return ScoreboardSetUp.AddPlayers(h, StandInPlayers, 0, players, true);
            List<CharacterMaster> drones = new List<CharacterMaster>();
            foreach (CharacterMaster player in players)
            {
                yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, DronesPerStandIn, drones, player.GetBody());
            }
            // A minion of one of those drones, which the game doesn't make yet: it stays in that player's section,
            // indented under its drone's card as the game indents any minion whose leader has a card.
            yield return Allies.SummonDrones(h, Allies.HealingDroneMaster, 1, drones, drones[0].GetBody());
            List<CharacterMaster> allies = new List<CharacterMaster>();
            yield return trace.Step("players with drones", true);
            CheckPlayers(h, players, "players with drones");
            yield return GrowUntil(h, trace, allies, () => AnyCompact(h), "compact");
            yield return trace.Step("compact", true);
            CheckPlayers(h, players, "compact");
            yield return GrowUntil(h, trace, allies, () => MoreShown(h), "more row");
            yield return trace.Step("more row", true);
            CheckPlayers(h, players, "more row");
            yield return Allies.SummonLoneAllies(h, 1, allies);
            yield return trace.Step("one more under the more row");
            CheckPlayers(h, players, "one more under the more row");
            foreach (CharacterMaster player in players)
            {
                yield return Allies.SummonDrones(h, Allies.HealingDroneMaster, 1, drones, player.GetBody());
            }
            yield return GrowUntil(h, trace, allies, () => FoldedSections(h) >= 2, "two folded sections");
            yield return trace.Step("crowded", true);
            CheckPlayers(h, players, "crowded");
            if (FoldedSections(h) < 2) throw h.Fail($"crowded: {FoldedSections(h)} sections fold, not 2 or more {h.Diagnosis()}");
            trace.Finish();
        }

        private static int FoldedSections(Harness h)
        {
            AllyCardChecks.Layout layout = AllyCardChecks.Measure(h);
            return layout.Problem == null ? layout.Sections.Count(section => section.Hidden > 0) : 0;
        }

        // Each stand-in's card is shown at the game's height, fully above the chat's top and in the game's order; no card
        // or more row overlaps another; each player's shown allies and more row lie between that player's card and the
        // next section's; the viewer's own allies lie below the last player's. The mod's own layout check agrees, which also compares each more row's count with its section.
        private static void CheckPlayers(Harness h, List<CharacterMaster> players, string when)
        {
            RectTransform cluster = AllyCardChecks.Cluster(h);
            float room = AllyCardChecks.Room(h);
            AllyCardChecks.Layout layout = AllyCardChecks.Measure(h);
            if (layout.Problem != null) throw h.Fail($"{when}: {layout.Problem}");
            List<AllyCardChecks.Section> sections = layout.Sections;
            List<CharacterMaster> inOrder = CharacterMaster.readOnlyInstancesList.Where(players.Contains).ToList();
            List<CharacterMaster> cardOrder = sections.Where(section => section.Player != null && players.Contains(section.Player.Master))
                .Select(section => section.Player.Master).ToList();
            if (cardOrder.Count != inOrder.Count) throw h.Fail($"{when}: {cardOrder.Count} cards for {inOrder.Count} stand-in players {h.Diagnosis()}");
            if (!cardOrder.SequenceEqual(inOrder)) throw h.Fail($"{when}: the players' cards are not in the game's order");
            foreach (AllyCardChecks.Section section in sections.Where(section => section.Player != null))
            {
                AllyCardController card = section.Player.Lead;
                if (!card.gameObject.activeSelf) throw h.Fail($"{when}: a player's card is hidden or folded away");
                float tall = card.rectTransform.rect.height;
                if (Mathf.Abs(tall - AllyCardChecks.GameHeight) > Tolerance)
                {
                    throw h.Fail($"{when}: a player's card is {tall:0.#} units tall, not {AllyCardChecks.GameHeight:0.#}");
                }
                float depth = AllyCardChecks.Depth(cluster, card.rectTransform);
                if (depth > room + Tolerance) throw h.Fail($"{when}: a player's card reaches {depth:0.#} units down, past the {room:0.#} above the chat");
            }
            // Nothing overlaps: every card and row shown, from the top, starts where the one before ends.
            List<(string Name, float Top, float Bottom)> shown = AllyCardChecks.Cards(h).Where(card => card.gameObject.activeSelf)
                .Select(card => Extent(cluster, card.rectTransform, Name(card)))
                .Concat(AllyCardChecks.More(h).Select(row => Extent(cluster, row, row.name))).OrderBy(extent => extent.Top).ToList();
            for (int i = 1; i < shown.Count; i++)
            {
                if (shown[i].Top < shown[i - 1].Bottom - Tolerance)
                {
                    throw h.Fail($"{when}: {shown[i].Name} starts {shown[i].Top:0.#} units down, over {shown[i - 1].Name}, which ends at {shown[i - 1].Bottom:0.#}");
                }
            }
            // The viewer's own allies, which have no player card, come after every player's section.
            for (int i = 0; i < sections.Count - 1; i++)
            {
                if (sections[i].Player == null) throw h.Fail($"{when}: the viewer's own allies are in section {i + 1} of {sections.Count}, above a player's {h.Diagnosis()}");
            }
            for (int i = 0; i < sections.Count; i++)
            {
                AllyCardChecks.Section section = sections[i];
                float lower = 0f;
                if (section.Player != null)
                {
                    lower = AllyCardChecks.Depth(cluster, section.Player.Lead.rectTransform);
                }
                else if (i > 0)
                {
                    lower = Items(cluster, sections[i - 1]).Max(item => item.Bottom);
                }
                float upper = i + 1 < sections.Count ? Items(cluster, sections[i + 1]).Min(item => item.Top) : float.MaxValue;
                List<(string Name, float Top, float Bottom)> own = section.ShownKinds
                    .Select(kind => Extent(cluster, kind.Lead.rectTransform, Name(kind.Lead))).ToList();
                if (section.Row) own.Add(Extent(cluster, section.Row, section.Row.name));
                foreach ((string name, float top, float bottom) in own)
                {
                    if (top < lower - Tolerance || bottom > upper + Tolerance)
                    {
                        throw h.Fail($"{when}: {name} lies {top:0.#} to {bottom:0.#} units down, not between {lower:0.#} and {upper:0.#}");
                    }
                }
            }
            h.Log($"[ally_portraits] {when}: {players.Count} player cards full size in {sections.Count} sections, " +
                  $"{(layout.Compact ? "compact" : "full size")}, hidden per section: " +
                  string.Join(", ", sections.Select(section => section.Hidden)));
        }

        // The player's card, the allies shown and the more row of a section.
        private static List<(string Name, float Top, float Bottom)> Items(RectTransform cluster, AllyCardChecks.Section section)
        {
            List<(string Name, float Top, float Bottom)> items = section.ShownKinds
                .Select(kind => Extent(cluster, kind.Lead.rectTransform, Name(kind.Lead))).ToList();
            if (section.Player != null)
            {
                items.Add(Extent(cluster, section.Player.Lead.rectTransform, Name(section.Player.Lead)));
            }
            if (section.Row)
            {
                items.Add(Extent(cluster, section.Row, section.Row.name));
            }
            return items;
        }

        private static (string Name, float Top, float Bottom) Extent(RectTransform cluster, RectTransform rect, string name)
        {
            float bottom = AllyCardChecks.Depth(cluster, rect);
            return (name, bottom - rect.rect.height, bottom);
        }

        // A kind of five shown as one card while the cards are compact, then GroupAllyCards off and on, twice.
        private static IEnumerator GroupToggle(Harness h)
        {
            Trace trace = new Trace(h);
            PluginConfig settings = SevenUIPlugin.Settings;
            h.OnCleanup(() => h.SetConfig(settings.GroupAllyCards, true));
            List<CharacterMaster> kind = new List<CharacterMaster>();
            List<CharacterMaster> allies = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.HealingDroneMaster, KindSize, kind);
            yield return GrowUntil(h, trace, allies, () => AnyCompact(h), "compact");
            yield return trace.Step("grouped", true);
            for (int round = 1; round <= 2; round++)
            {
                h.SetConfig(settings.GroupAllyCards, false);
                yield return trace.Step($"grouping off {round}", true);
                h.SetConfig(settings.GroupAllyCards, true);
                yield return trace.Step($"grouping on {round}", true);
            }
            trace.Finish();
        }

        // Compact cards, then CompactAllyCards off and on, twice.
        private static IEnumerator CompactToggle(Harness h)
        {
            Trace trace = new Trace(h);
            PluginConfig settings = SevenUIPlugin.Settings;
            h.OnCleanup(() => h.SetConfig(settings.CompactAllyCards, true));
            List<CharacterMaster> allies = new List<CharacterMaster>();
            yield return GrowUntil(h, trace, allies, () => AnyCompact(h), "compact");
            yield return trace.Step("compact", true);
            for (int round = 1; round <= 2; round++)
            {
                h.SetConfig(settings.CompactAllyCards, false);
                yield return trace.Step($"compact off {round}");
                h.SetConfig(settings.CompactAllyCards, true);
                yield return trace.Step($"compact on {round}", true);
            }
            trace.Finish();
        }

        // Compact cards, then FlatPanels off and on, twice: the whole look restored and applied again.
        private static IEnumerator FlatPanelsToggle(Harness h)
        {
            Trace trace = new Trace(h);
            PluginConfig settings = SevenUIPlugin.Settings;
            h.OnCleanup(() => h.SetConfig(settings.FlatPanels, true));
            List<CharacterMaster> allies = new List<CharacterMaster>();
            yield return GrowUntil(h, trace, allies, () => AnyCompact(h), "compact");
            yield return trace.Step("compact", true);
            for (int round = 1; round <= 2; round++)
            {
                h.SetConfig(settings.FlatPanels, false);
                yield return trace.Step($"panels off {round}");
                h.SetConfig(settings.FlatPanels, true);
                yield return trace.Step($"panels on {round}", true);
            }
            trace.Finish();
        }

        // Lone allies one at a time, a step logged after each, until the condition holds.
        private static IEnumerator GrowUntil(Harness h, Trace trace, List<CharacterMaster> allies, System.Func<bool> done,
            string what)
        {
            for (int added = 0; added < MaxLoneAllies && !done(); added++)
            {
                yield return Allies.SummonLoneAllies(h, 1, allies);
                yield return trace.Step($"added lone ally {added + 1}");
            }
            if (!done()) throw h.Fail($"no {what} after {MaxLoneAllies} lone allies {h.Diagnosis()}");
        }

        private static void RemoveLast(List<CharacterMaster> allies, int count)
        {
            List<CharacterMaster> last = allies.Skip(allies.Count - count).ToList();
            Allies.Remove(last);
            allies.RemoveAll(last.Contains);
        }

        private static bool IsCompact(AllyCardController card) =>
            Mathf.Abs(card.rectTransform.rect.height - AllyCompactCard.RowHeight) < Tolerance;

        private static bool PortraitOff(AllyCardController card)
        {
            LayoutElement portrait = card.portraitIconImage ? card.portraitIconImage.GetComponent<LayoutElement>() : null;
            return portrait && Mathf.Abs(card.portraitIconImage.rectTransform.rect.width - portrait.preferredWidth) > Tolerance;
        }

        private static bool AnyCompact(Harness h) =>
            AllyCardChecks.Cards(h).Any(card => card.gameObject.activeSelf && IsCompact(card));

        private static bool MoreShown(Harness h)
        {
            return AllyCardChecks.More(h).Count > 0;
        }

        private static string Name(AllyCardController card) => card.nameLabel ? card.nameLabel.text : card.name;
    }
}
