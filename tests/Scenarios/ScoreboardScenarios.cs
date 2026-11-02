using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;

namespace SevenUI.Tests
{
    // The scoreboard: a check that a row's items are laid out as the canvas's slots, and screenshots of the
    // scoreboard in SevenUI's look and the game's, with a spread of items and with from a few to a hundred different
    // items. Together they make the scoreboard suite (SevenUITests.Suites); the check is also among the panels' checks,
    // and the spread's screenshot among the screenshots.
    internal static class ScoreboardScenarios
    {
        // More different items than fit one row of the scoreboard's slots.
        private const int ManyItems = 40;
        // Different items in the screenshots' rows, from a few to more than two rows' worth of full slots.
        private static readonly int[] ItemCounts = { 3, 10, 20, 30, 45, 50, 70, 75, 100 };
        // Long enough for the switch between looks to settle.
        private const float SettleSeconds = 0.5f;

        public static IEnumerable<Scenario> All() => Checks().Concat(Screenshots());

        public static IEnumerable<Scenario> Checks()
        {
            yield return new Scenario(1, "scoreboard_items_in_slots", Stages.TitanicPlains, ItemsInSlots);
            yield return new Scenario(1, "scoreboard_centered_without_setting", Stages.TitanicPlains, CenteredWithoutSetting);
        }

        public static Scenario SpreadScreenshot() => Shot("scoreboard", Spread);

        public static IEnumerable<Scenario> Screenshots()
        {
            yield return SpreadScreenshot();
            foreach (int count in ItemCounts)
            {
                yield return Shot($"scoreboard_items_{count}", h => WithItems(h, count));
            }
        }

        private static Scenario Shot(string name, System.Func<Harness, IEnumerator> run) =>
            new Scenario(1, name, Stages.TitanicPlains, run);

        // Many different items in the viewer's row: the game would step them closer than its icons are wide, so they
        // overlap, start them lower than the slots beside them, and center the rows.
        private static IEnumerator ItemsInSlots(Harness h)
        {
            ScoreboardSetUp.GiveDifferentItems(h, ManyItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas();
        }

        // ScoreboardBesideRunInfo moves the board beside the run info; off, it is centered on the screen, as the game
        // centers it, still the canvas's width.
        private static IEnumerator CenteredWithoutSetting(Harness h)
        {
            ScreenshotScenarios.GiveItemSpread(h);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas();
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardBesideRunInfo, false);
            yield return h.ExpectScoreboardAsCanvas();
        }

        // The tab screen with one item of each main tier and the equipment.
        private static IEnumerator Spread(Harness h)
        {
            ScreenshotScenarios.GiveItemSpread(h);
            h.Cmd($"{Commands.GiveEquipment} {GameNames.Fruit}");
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas();
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }

        // The tab screen with that many different items in the player's row, from one row of full slots to several
        // rows of smaller ones.
        private static IEnumerator WithItems(Harness h, int count)
        {
            ScoreboardSetUp.GiveDifferentItems(h, count);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas();
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }
    }
}
