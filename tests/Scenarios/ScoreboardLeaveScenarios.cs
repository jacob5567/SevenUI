using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace SevenUI.Tests
{
    // Players leaving a scoreboard whose rows they had collapsed: with the board closed, with it open, and as
    // ScoreboardTabs turns back on, then the lone viewer's rows. Every step logs the board's state and where the
    // viewer's row's parts sit, in the row's own units, so a row left out of place can be traced to the step.
    internal static class ScoreboardLeaveScenarios
    {
        private const int CrowdPlayers = 3;
        private const int CrowdItems = 100;
        private const int ViewerItems = 3;
        private const int SettleFrames = 3;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "scoreboard_leave_while_closed", Stages.TitanicPlains, LeaveWhileClosed);
            yield return new Scenario(1, "scoreboard_leave_while_open", Stages.TitanicPlains, LeaveWhileOpen);
            yield return new Scenario(1, "scoreboard_leave_as_tabs_turn_on", Stages.TitanicPlains, LeaveAsTabsTurnOn);
            yield return new Scenario(1, "scoreboard_rows_afterwards", Stages.TitanicPlains, RowsAfterwards);
        }

        // Alone, then a crowd that collapses the rows, then the board closed and the crowd gone in one frame, as a
        // scenario's cleanup does, then open again.
        private static IEnumerator LeaveWhileClosed(Harness h)
        {
            ScoreboardSetUp.GiveDifferentItems(h, ViewerItems);
            yield return OpenAndLog(h, "alone");
            Close();
            List<CharacterMaster> crowd = new List<CharacterMaster>();
            yield return ScoreboardSetUp.AddPlayers(h, CrowdPlayers, CrowdItems, crowd);
            yield return OpenAndLog(h, "crowd");
            Close();
            Remove(crowd);
            yield return LogFrames(h, "closed, crowd gone");
            yield return OpenAndLog(h, "reopened alone");
            yield return h.Screenshot("reopened");
            yield return h.ExpectScoreboardAsCanvas(false);
        }

        // The crowd leaves with the board open.
        private static IEnumerator LeaveWhileOpen(Harness h)
        {
            ScoreboardSetUp.GiveDifferentItems(h, ViewerItems);
            List<CharacterMaster> crowd = new List<CharacterMaster>();
            yield return ScoreboardSetUp.AddPlayers(h, CrowdPlayers, CrowdItems, crowd);
            yield return OpenAndLog(h, "crowd");
            Remove(crowd);
            yield return LogFrames(h, "open, crowd gone");
            yield return h.Screenshot("crowd_gone");
            yield return h.ExpectScoreboardAsCanvas(false);
        }

        // As scoreboard_tabs_off ends: the crowd's rows open with ScoreboardTabs off, then in one frame the board
        // closed, the crowd gone and the setting back on, as its cleanup does. Left so for the next scenario.
        private static IEnumerator LeaveAsTabsTurnOn(Harness h)
        {
            PluginConfig settings = SevenUIPlugin.Settings;
            ScoreboardSetUp.GiveDifferentItems(h, ViewerItems);
            h.SetConfig(settings.ScoreboardTabs, false);
            List<CharacterMaster> crowd = new List<CharacterMaster>();
            yield return ScoreboardSetUp.AddPlayers(h, CrowdPlayers, CrowdItems, crowd);
            yield return OpenAndLog(h, "crowd, tabs off");
            Close();
            Remove(crowd);
            settings.ScoreboardTabs.Value = true;
            yield return LogFrames(h, "closed, crowd gone, tabs on");
            yield return OpenAndLog(h, "reopened alone");
            yield return h.Screenshot("reopened");
            yield return h.ExpectScoreboardAsCanvas(false);
        }

        private static IEnumerator RowsAfterwards(Harness h)
        {
            ScoreboardSetUp.GiveDifferentItems(h, ViewerItems);
            yield return OpenAndLog(h, "alone");
            yield return h.Screenshot("alone");
            yield return h.ExpectScoreboardAsCanvas(false);
        }

        private static IEnumerator OpenAndLog(Harness h, string step)
        {
            yield return ScoreboardSetUp.Open(h);
            yield return LogFrames(h, step);
        }

        // The board on this frame and the next few, since it lays out in LateUpdate, after a check reads it.
        private static IEnumerator LogFrames(Harness h, string step)
        {
            for (int frame = 0; frame <= SettleFrames; frame++)
            {
                Log(h, $"{step}, frame {frame}");
                yield return null;
            }
        }

        private static void Close()
        {
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            HUD.SetScoreboardActive(user, false, EScoreboardDeactivateReturnContext.Game);
        }

        // As ScoreboardSetUp's cleanup removes them.
        private static void Remove(List<CharacterMaster> players)
        {
            foreach (CharacterMaster player in players.Where(player => player))
            {
                player.DestroyBody();
                NetworkServer.Destroy(player.gameObject);
            }
            players.Clear();
        }

        private static void Log(Harness h, string step)
        {
            ScoreboardItemsBoard board = SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardItemsBoard>(true);
            if (!board)
            {
                h.Log($"[scoreboard_leave] {step}: no items board");
                return;
            }
            IReadOnlyList<ScoreboardItemGrid> grids = board.Grids;
            int dead = grids.Count(grid => !grid.Alive);
            string text = $"{step}: open {board.isActiveAndEnabled}, collapsed {board.Collapsed}, {grids.Count} grids ({dead} dead), " +
                          $"tabs setting {SevenUIPlugin.Settings.ScoreboardTabs.Value}, panel setting " +
                          $"{SevenUIPlugin.Settings.ScoreboardDetailPanel.Value}, board width {((RectTransform)board.transform).rect.width:0.#}, " +
                          $"padding {Padding(board)}";
            ScoreboardRow row = board.GetComponentsInChildren<ScoreboardRow>(true)
                .FirstOrDefault(r => r.Strip && r.Strip.master == Harness.LocalBody.master);
            ScoreboardItemGrid own = grids.FirstOrDefault(grid => grid.Alive && row && grid.Strip == row.Strip);
            if (row)
            {
                RectTransform strip = (RectTransform)row.Strip.transform;
                Transform background = row.Background ? row.Background.transform : null;
                text += $"; viewer row {Local(strip, strip)}, in tab {row.InTab}, background {Local(strip, background)}, " +
                        $"equipment {Local(strip, background ? background.Find("EquipmentBackground") : null)}, " +
                        $"name {Local(strip, background ? background.Find("NameObject") : null)}, " +
                        $"cell {Local(strip, row.Strip.itemInventoryDisplay ? row.Strip.itemInventoryDisplay.transform : null)}";
                text += $", key item {Local(strip, background ? background.Find("KeyItemBackground") : null)}, " +
                        $"background {Placement(background)}, equipment {Placement(background ? background.Find("EquipmentBackground") : null)}, " +
                        $"children [{string.Join(" ", background ? background.Cast<Transform>().Select(child => child.name + (child.gameObject.activeSelf ? "" : "(off)")) : new string[0])}]";
                if (own != null)
                {
                    RectTransform cell = own.Cell;
                    text += $", cell anchors {cell.anchorMin}-{cell.anchorMax} pivot {cell.pivot} position {cell.anchoredPosition} " +
                            $"size {cell.sizeDelta}, slot {own.SlotSize}";
                }
                else
                {
                    text += ", not among the grids";
                }
            }
            else
            {
                text += "; no viewer row";
            }
            h.Log("[scoreboard_leave] " + text);
        }

        private static string Padding(ScoreboardItemsBoard board)
        {
            UnityEngine.UI.VerticalLayoutGroup group = board.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            return group ? $"{group.padding.left}/{group.padding.right}" : "none";
        }

        private static string Placement(Transform part)
        {
            if (!(part is RectTransform rect)) return "none";
            return $"anchors {rect.anchorMin}-{rect.anchorMax} pivot {rect.pivot} position {rect.anchoredPosition} size {rect.sizeDelta}";
        }

        // A part's box in the row's units.
        private static string Local(RectTransform strip, Transform part)
        {
            if (!(part is RectTransform rect)) return "none";
            Vector2 min = strip.InverseTransformPoint(rect.TransformPoint(rect.rect.min));
            Vector2 max = strip.InverseTransformPoint(rect.TransformPoint(rect.rect.max));
            return $"x {min.x:0.#}-{max.x:0.#} y {min.y:0.#}-{max.y:0.#}";
        }
    }
}
