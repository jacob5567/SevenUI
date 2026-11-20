using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SevenUI.Tests
{
    // Each player's allies on the scoreboard (ScoreboardAllies): the summary line under a row's name, the panel under
    // the board that clicking it opens, and nothing while no player has an ally or the setting is off. Drones are
    // summoned straight in as the player's (Allies.SummonDrones); stand-in players (ScoreboardSetUp.AddPlayers) have
    // their own.
    internal static class ScoreboardAlliesScenarios
    {
        private const int GunnerDrones = 3;
        private const int HealingDrones = 1;
        private const float SettleSeconds = 0.5f;
        // Half a drone's health, for the health segment.
        private const float HurtFraction = 0.5f;
        private const float FillTolerance = 0.05f;
        // In the board's units: a pixel or two, for the panel's width and center.
        private const float Tolerance = 2f;
        private const int StandInItems = 5;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "scoreboard_allies_summary_and_panel", Stages.TitanicPlains, SummaryAndPanel);
            yield return new Scenario(1, "scoreboard_allies_none_shows_nothing", Stages.TitanicPlains, NoneShowsNothing);
            yield return new Scenario(1, "scoreboard_allies_of_another_player", Stages.TitanicPlains, OfAnotherPlayer);
            yield return new Scenario(1, "scoreboard_allies_minion_of_minion", Stages.TitanicPlains, MinionOfMinion);
            yield return new Scenario(1, "scoreboard_allies_setting_off", Stages.TitanicPlains, SettingOff);
            yield return new Scenario(1, "scoreboard_allies_close_with_scoreboard", Stages.TitanicPlains, CloseWithScoreboard);
        }

        // Three Gunner Drones and a Healing Drone: two kinds stacked in the summary with "4 allies", the rows taller to
        // hold it; a click opens the panel under the board, 60% of its width and centered, with a tile per kind and a
        // health segment per drone; a second click closes it.
        private static IEnumerator SummaryAndPanel(Harness h)
        {
            List<CharacterMaster> drones = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, GunnerDrones, drones);
            yield return Allies.SummonDrones(h, Allies.HealingDroneMaster, HealingDrones, drones);
            CharacterBody hurt = drones[0].GetBody();
            hurt.healthComponent.Networkhealth = hurt.healthComponent.fullHealth * HurtFraction;
            ScoreboardSetUp.GiveDifferentItems(h, 3);
            yield return ScoreboardSetUp.Open(h);
            ScoreboardItemsBoard board = Board(h);
            ScoreboardAlliesSummary summary = null;
            yield return h.Until(() => (summary = OwnSummary(board)) && summary.Clickable && summary.Stacked == 2 &&
                                       summary.Text.Contains((GunnerDrones + HealingDrones).ToString()),
                () => $"the viewer's summary with 2 kinds and {GunnerDrones + HealingDrones} allies ({Describe(summary)})");
            yield return ExpectRowHeight(h, board, ScoreboardStyler.AlliesRowHeight);
            if (board.Allies.Open || Shown(board.Allies.Panel))
            {
                throw h.Fail("the allies panel was open before any click");
            }

            yield return Click(h, summary);
            CharacterMaster viewer = Harness.LocalBody.master;
            yield return h.Until(() => board.Allies.Open == viewer && Shown(board.Allies.Panel) && board.Allies.TileCount == 2 &&
                                       summary.Lit,
                () => $"the viewer's allies in the panel, the summary lit (open {Name(board.Allies.Open)}, " +
                      $"panel {(Shown(board.Allies.Panel) ? "shown" : "hidden")}, tiles {board.Allies.TileCount}, lit {summary.Lit})");
            string gunner = board.Allies.TileText(0);
            string healing = board.Allies.TileText(1);
            if (!gunner.Contains("×" + GunnerDrones) || !healing.Contains("×" + HealingDrones))
            {
                throw h.Fail($"expected tiles counting ×{GunnerDrones} and ×{HealingDrones}, got \"{gunner}\" and \"{healing}\"");
            }
            yield return h.Until(() => Mathf.Abs(board.Allies.SegmentFill(0, 0) - HurtFraction) < FillTolerance &&
                                       board.Allies.SegmentFill(0, 1) > 1f - FillTolerance,
                () => $"the hurt drone's segment at half, the next full ({board.Allies.SegmentFill(0, 0):0.00}, " +
                      $"{board.Allies.SegmentFill(0, 1):0.00})");
            ExpectUnderBoard(h, board);
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("scoreboard_allies_panel");

            yield return Click(h, summary);
            yield return h.Until(() => !board.Allies.Open && !Shown(board.Allies.Panel) && !summary.Lit,
                "the panel closed by a second click, the summary no longer lit");
        }

        // Without allies the rows have no summary line and keep their plain height.
        private static IEnumerator NoneShowsNothing(Harness h)
        {
            ScoreboardSetUp.GiveDifferentItems(h, 3);
            yield return ScoreboardSetUp.Open(h);
            ScoreboardItemsBoard board = Board(h);
            yield return h.Until(() => board.Grids.Count > 0, "the viewer's row");
            yield return h.Wait(SettleSeconds);
            ScoreboardAlliesSummary summary = OwnSummary(board);
            if (board.Allies.LineShown || summary && summary.gameObject.activeSelf)
            {
                throw h.Fail($"a summary line shown with no allies ({Describe(summary)})");
            }
            yield return ExpectRowHeight(h, board, ScoreboardStyler.RowHeight);
        }

        // A stand-in player's drones: their row's summary counts them and opens their panel, named for them; the
        // viewer's row says it has none, and can't be clicked.
        private static IEnumerator OfAnotherPlayer(Harness h)
        {
            List<CharacterMaster> players = new List<CharacterMaster>();
            yield return ScoreboardSetUp.AddPlayers(h, 1, StandInItems, players);
            List<CharacterMaster> drones = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 2, drones, players[0].GetBody());
            yield return ScoreboardSetUp.Open(h);
            ScoreboardItemsBoard board = Board(h);
            ScoreboardAlliesSummary theirs = null;
            ScoreboardAlliesSummary own = null;
            yield return h.Until(() => (theirs = SummaryOf(board, players[0])) && theirs.Clickable && theirs.Text.Contains("2") &&
                                       (own = OwnSummary(board)) && own.gameObject.activeSelf && !own.Clickable &&
                                       own.Text == Texts.NoAllies,
                () => $"the stand-in's summary with 2 allies ({Describe(theirs)}), the viewer's with none ({Describe(own)})");
            yield return Click(h, theirs);
            string name = board.Grids.First(grid => grid.Strip.master == players[0]).Strip.nameLabel.text;
            yield return h.Until(() => board.Allies.Open == players[0] && Shown(board.Allies.Panel) &&
                                       board.Allies.HeaderText.Contains(name),
                () => $"the stand-in's allies in the panel, named {name} (header \"{board.Allies.HeaderText}\")");
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("scoreboard_allies_other_player");
        }

        // A Healing Drone following the player's Gunner Drone is still the player's: two kinds, two allies.
        private static IEnumerator MinionOfMinion(Harness h)
        {
            List<CharacterMaster> drones = new List<CharacterMaster>();
            yield return Allies.SummonLoneAllies(h, 1, drones);
            yield return ScoreboardSetUp.Open(h);
            ScoreboardItemsBoard board = Board(h);
            CharacterMaster viewer = Harness.LocalBody.master;
            yield return h.Until(() => board.Allies.AlliesOf(viewer) is ScoreboardAllies.PlayerAllies allies &&
                                       allies.Total == 2 && allies.Kinds.Count == 2 &&
                                       allies.Kinds.Any(kind => kind.Leader == drones[0]),
                () => $"the drone's drone among the viewer's allies ({Count(board, viewer)})");
        }

        // Off, no summary and no panel, and the rows back to their plain height; on again, the summary is back.
        private static IEnumerator SettingOff(Harness h)
        {
            List<CharacterMaster> drones = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 1, drones);
            yield return ScoreboardSetUp.Open(h);
            ScoreboardItemsBoard board = Board(h);
            ScoreboardAlliesSummary summary = null;
            yield return h.Until(() => (summary = OwnSummary(board)) && summary.Clickable, "the viewer's summary");
            yield return Click(h, summary);
            yield return h.Until(() => Shown(board.Allies.Panel), "the allies panel");
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardAllies, false);
            yield return h.Until(() => !board.Allies.LineShown && !summary.gameObject.activeSelf && !Shown(board.Allies.Panel),
                () => $"no summary and no panel with the setting off ({Describe(summary)})");
            yield return ExpectRowHeight(h, board, ScoreboardStyler.RowHeight);
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardAllies, true);
            yield return h.Until(() => summary.Clickable && !Shown(board.Allies.Panel),
                "the summary back with the setting on, the panel still closed");
        }

        // The panel closes with the scoreboard, and isn't open the next time it opens.
        private static IEnumerator CloseWithScoreboard(Harness h)
        {
            List<CharacterMaster> drones = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 1, drones);
            yield return ScoreboardSetUp.Open(h);
            ScoreboardItemsBoard board = Board(h);
            ScoreboardAlliesSummary summary = null;
            yield return h.Until(() => (summary = OwnSummary(board)) && summary.Clickable, "the viewer's summary");
            yield return Click(h, summary);
            yield return h.Until(() => Shown(board.Allies.Panel), "the allies panel");
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            HUD.SetScoreboardActive(user, false, EScoreboardDeactivateReturnContext.Game);
            yield return h.Until(() => !HUD.IsScoreboardActive(user), "the scoreboard closed");
            yield return ScoreboardSetUp.Open(h);
            yield return h.Wait(SettleSeconds);
            if (board.Allies.Open || Shown(board.Allies.Panel))
            {
                throw h.Fail("the allies panel was still open when the scoreboard opened again");
            }
        }

        // A left click on the summary, as the pointer gives it.
        private static IEnumerator Click(Harness h, ScoreboardAlliesSummary summary)
        {
            PointerEventData click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(summary.gameObject, click, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        // Every row at least this tall, and the board's least row height this.
        private static IEnumerator ExpectRowHeight(Harness h, ScoreboardItemsBoard board, float height)
        {
            yield return h.Until(() => Mathf.Approximately(board.PlainRowHeight, height) &&
                                       board.Grids.All(grid => ((RectTransform)grid.Strip.transform).rect.height >= height - 0.5f),
                () => $"rows at least {height:0.#} tall (least {board.PlainRowHeight:0.#}, rows " +
                      $"{string.Join(", ", board.Grids.Select(grid => ((RectTransform)grid.Strip.transform).rect.height.ToString("0.#")))})");
        }

        // The panel's face under the board, 60% of its width and centered on it.
        private static void ExpectUnderBoard(Harness h, ScoreboardItemsBoard board)
        {
            RectTransform strips = (RectTransform)board.transform;
            RectTransform face = board.Allies.Panel;
            Rect boardRect = InParent(strips, strips);
            Rect faceRect = InParent(face, strips);
            float width = boardRect.width * ScoreboardInspect.WidthShare;
            if (Mathf.Abs(faceRect.width - width) > Tolerance || Mathf.Abs(faceRect.center.x - boardRect.center.x) > Tolerance ||
                faceRect.yMax > boardRect.yMin + Tolerance)
            {
                throw h.Fail($"expected the allies panel {width:0} wide, centered under the board ({boardRect}), got {faceRect}");
            }
        }

        // The rect in the board's own space.
        private static Rect InParent(RectTransform rect, RectTransform space)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = space.InverseTransformPoint(corners[0]);
            Vector3 max = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static ScoreboardItemsBoard Board(Harness h) =>
            SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardItemsBoard>();

        private static ScoreboardAlliesSummary OwnSummary(ScoreboardItemsBoard board) =>
            SummaryOf(board, Harness.LocalBody.master);

        private static ScoreboardAlliesSummary SummaryOf(ScoreboardItemsBoard board, CharacterMaster player)
        {
            ScoreboardItemGrid grid = board ? board.Grids.FirstOrDefault(each => each.Strip && each.Strip.master == player) : null;
            return grid != null ? board.Allies.SummaryOf(grid.Strip) : null;
        }

        private static bool Shown(RectTransform panel) => panel && panel.gameObject.activeInHierarchy;

        private static string Name(CharacterMaster master) => master ? master.name : "none";

        private static string Describe(ScoreboardAlliesSummary summary) => summary
            ? $"\"{summary.Text}\", {summary.Stacked} stacked, {(summary.gameObject.activeSelf ? "shown" : "hidden")}, " +
              $"{(summary.Clickable ? "clickable" : "not clickable")}"
            : "none";

        private static string Count(ScoreboardItemsBoard board, CharacterMaster player)
        {
            ScoreboardAllies.PlayerAllies allies = board.Allies.AlliesOf(player);
            return allies == null
                ? "none"
                : $"{allies.Total} allies in {allies.Kinds.Count} kinds: " +
                  string.Join(", ", allies.Kinds.Select(kind => $"{kind.Name} ×{kind.Members.Count} led by {Name(kind.Leader)}"));
        }
    }
}
