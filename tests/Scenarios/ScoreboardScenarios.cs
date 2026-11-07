using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // The scoreboard: checks that a row's items are laid out as the canvas's grid, that pointing at an item, the
    // equipment or the key item describes it in the scoreboard's pane, that several players' rows stay rows while
    // there is room and collapse into tabs when crowded, also while the scoreboard is open, and screenshots of the
    // scoreboard in SevenUI's look and the game's, with a spread of items and with from a few to a hundred different
    // items. Each runs on both boards: as rows, the viewer alone, and as tabs, the board crowded with stand-in players
    // (ScoreboardSetUp.AddPlayers) so its rows collapse. Together they make the scoreboard suite, and each board's its
    // own suite (SevenUITests.Suites); the checks are also among the panels' checks, and the spread's screenshot among
    // the screenshots.
    internal static class ScoreboardScenarios
    {
        // A few items to hover, and the header band the flat look adds to an item's tooltip (TooltipStyler).
        private const int TooltipItems = 3;
        private const string TooltipBandPath = "TooltipOffset/Panel/TitleRect/SevenUIHeaderBand";
        // More different items than fit one row of the scoreboard's slots.
        private const int ManyItems = 40;
        // Different items in the screenshots' rows, from a few to more than two rows' worth of full slots.
        private static readonly int[] ItemCounts = { 3, 10, 20, 30, 45, 50, 70, 75, 100 };
        // Long enough for the switch between looks to settle.
        private const float SettleSeconds = 0.5f;
        // Other players, each with this many different items: more than the rows fit at 32px slots, so they collapse.
        // With the fewer, the rows have room at full size.
        private const int CrowdPlayers = 3;
        private const int CrowdItems = 100;
        private const int RoomyItems = 10;

        // The two boards the scenarios run on, set up from the players and their items alone.
        internal enum Board
        {
            // The viewer alone, whose row never collapses.
            Rows,
            // The viewer and a crowd of stand-in players with many items, so the rows collapse.
            Tabs,
        }

        public static IEnumerable<Scenario> All() => Checks().Concat(Screenshots());

        // One board's checks and screenshots, and the collapse while open, which both suites share.
        public static IEnumerable<Scenario> All(Board board) =>
            Checks(board).Append(CollapseWhileOpenScenario()).Concat(Screenshots(board));

        public static IEnumerable<Scenario> Checks() =>
            Checks(Board.Rows).Concat(Checks(Board.Tabs)).Append(CollapseWhileOpenScenario());

        // Each board's checks, named "scoreboard_..." on the rows and "scoreboard_tabs_..." on the tabs.
        public static IEnumerable<Scenario> Checks(Board board)
        {
            string prefix = Prefix(board);
            yield return new Scenario(1, prefix + "items_in_slots", Stages.TitanicPlains, h => ItemsInSlots(h, board));
            yield return new Scenario(1, prefix + "centered_without_setting", Stages.TitanicPlains,
                h => CenteredWithoutSetting(h, board));
            yield return new Scenario(1, prefix + "item_pane", Stages.TitanicPlains, h => ItemPane(h, board));
            yield return new Scenario(1, prefix + "equipment_pane", Stages.TitanicPlains, h => LoadoutPane(h, board, false));
            yield return new Scenario(1, prefix + "key_item_pane", Stages.TitanicPlains, h => LoadoutPane(h, board, true));
            yield return new Scenario(1, board == Board.Rows ? "item_bar_item_tooltip" : "item_bar_item_tooltip_over_tabs",
                Stages.TitanicPlains, h => ItemBarTooltip(h, board));
            if (board == Board.Rows)
            {
                yield return new Scenario(1, "scoreboard_players_rows_while_roomy", Stages.TitanicPlains, RowsWhileRoomy);
            }
            else
            {
                yield return new Scenario(1, "scoreboard_tabs_shows_pointed_player", Stages.TitanicPlains, ShowsPointedPlayer);
            }
        }

        // On the tabs board, a crowd of other players with many items, so the rows collapse.
        private static IEnumerator UseBoard(Harness h, Board board)
        {
            if (board == Board.Tabs)
            {
                yield return ScoreboardSetUp.AddPlayers(h, CrowdPlayers, CrowdItems);
            }
        }

        private static bool Collapses(Board board) => board == Board.Tabs;

        // Several players whose items have room at full size keep their rows, every row's slots full size.
        private static IEnumerator RowsWhileRoomy(Harness h)
        {
            yield return ScoreboardSetUp.AddPlayers(h, CrowdPlayers, RoomyItems);
            ScoreboardSetUp.GiveDifferentItems(h, ManyItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(false);
            ScoreboardItemsBoard board = SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardItemsBoard>();
            yield return h.Until(() => board && board.Grids.Count > CrowdPlayers &&
                                       board.Grids.All(grid => Mathf.Approximately(grid.SlotSize, ScoreboardItemGrid.FullSlot)),
                () => $"{CrowdPlayers + 1} rows, every row's slots full size (" +
                      $"{(board ? string.Join(", ", board.Grids.Select(grid => grid.SlotSize.ToString("0.#"))) : "no board")})");
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("scoreboard_players");
        }

        // With the rows collapsed, the pointer on another player's row shows their items in place of the viewer's, and
        // moves the tab to their row. The last row's: its hundred items run past the rows' bottom, so the tab reaches
        // up from it.
        private static IEnumerator ShowsPointedPlayer(Harness h)
        {
            yield return UseBoard(h, Board.Tabs);
            // A few items of the viewer's too, which the scoreboard's checks look at.
            ScoreboardSetUp.GiveDifferentItems(h, TooltipItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(true);
            ScoreboardItemsBoard board = SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardItemsBoard>();
            ScoreboardItemGrid other = board.Grids.LastOrDefault(grid => grid.Strip.master != Harness.LocalBody.master);
            if (other == null) throw h.Fail("no other player's row on the scoreboard");
            Transform background = other.Strip.transform.Find(ScoreboardStyler.RowBackgroundName);
            yield return h.Wait(SettleSeconds);
            PointAt(h, (RectTransform)background.Find("NameObject"), "another player's name", owner: other.Strip.transform);
            yield return h.Until(() => board.ShownStrip == other.Strip && board.Grids.All(grid => (grid == other) == Shown(grid.Cell)),
                () => $"the other player's items shown (shown {(board.ShownStrip ? board.ShownStrip.name : "none")}, " +
                      $"items showing in {board.Grids.Count(grid => Shown(grid.Cell))} rows)");
            yield return h.ExpectScoreboardTab(other.Strip);
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("scoreboard_tabs_other_player");
        }

        private static bool Shown(Transform part) =>
            !part.GetComponent<CanvasGroup>() || part.GetComponent<CanvasGroup>().alpha > 0f;

        private static Scenario CollapseWhileOpenScenario() =>
            new Scenario(1, "scoreboard_collapse_while_open", Stages.TitanicPlains, CollapseWhileOpen);

        private static string Prefix(Board board) =>
            board == Board.Rows ? "scoreboard_" : "scoreboard_tabs_";

        // With the scoreboard open on each board, the pointer on an item in the item bar over it opens the item's
        // tooltip, in the flat look: the board's pane doesn't take it.
        private static IEnumerator ItemBarTooltip(Harness h, Board board)
        {
            yield return UseBoard(h, board);
            ScoreboardSetUp.GiveDifferentItems(h, TooltipItems);
            yield return ScoreboardSetUp.Open(h);
            // The scoreboard under the item bar as the board draws it, its rows collapsed on the tabs.
            yield return h.ExpectScoreboardAsCanvas(Collapses(board));
            HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
            ItemIcon icon = null;
            yield return h.Until(() => (icon = FirstIcon(hud.itemInventoryDisplay.transform)) != null, "an item in the item bar");
            yield return h.Wait(SettleSeconds);
            MPEventSystem events = PointAt(h, icon.rectTransform, "an item in the item bar");
            yield return h.Until(() => events.currentTooltipProvider == icon.tooltipProvider && events.currentTooltip,
                () => $"the pointer on an item in the item bar opened " +
                      $"{(events.currentTooltipProvider ? events.currentTooltipProvider.name + "'s tooltip" : "no tooltip")}");
            yield return h.Until(() => events.currentTooltip && events.currentTooltip.transform.Find(TooltipBandPath),
                $"the item's tooltip in the flat look");
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot(board == Board.Rows ? "tooltip" : "tooltip_over_tabs");
        }

        // On each board, the pointer on an item in the scoreboard describes it in the pane instead of a tooltip, lights
        // it, and on the rows dims the rest; moved off, the light goes out and the pane stays on the item.
        private static IEnumerator ItemPane(Harness h, Board board)
        {
            yield return UseBoard(h, board);
            ScoreboardSetUp.GiveDifferentItems(h, TooltipItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(Collapses(board));
            ScoreboardRow row = SevenUIChecks.HudRoot(h).GetComponentsInChildren<ScoreboardRow>()
                .FirstOrDefault(each => each.Strip && each.Strip.master == Harness.LocalBody.master);
            ItemIcon icon = row ? FirstIcon(row.Strip.itemInventoryDisplay.transform) : null;
            if (!icon) throw h.Fail("no item in the viewer's row of the scoreboard");
            yield return h.Wait(SettleSeconds);
            PointerEventData pointer = null;
            PointAt(h, icon.rectTransform, "an item in the scoreboard", made => pointer = made);
            yield return h.ExpectItemPane(icon);
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot(board == Board.Rows ? "scoreboard_pane" : "scoreboard_tabs_pane");

            ExecuteEvents.ExecuteHierarchy(icon.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            ScoreboardItemsBoard itemsBoard = icon.GetComponentInParent<ScoreboardItemsBoard>();
            yield return h.Until(() => itemsBoard && itemsBoard.Highlighted == ItemIndex.None && itemsBoard.Pane.Shown &&
                                       icon.transform.parent.GetComponentsInChildren<ItemIcon>()
                                           .All(each => !each.GetComponent<CanvasGroup>() || each.GetComponent<CanvasGroup>().alpha >= 1f),
                () => $"the light off and the pane still on the item once the pointer left it (lit " +
                      $"{(itemsBoard ? itemsBoard.Highlighted.ToString() : "no board")}, pane on {(itemsBoard && itemsBoard.Pane.Shown ? itemsBoard.Pane.Title : "nothing")})");
        }

        // On each board, the pointer on the viewer's equipment (Foreign Fruit), or key item (the N'tormat), describes it
        // in the pane instead of a tooltip, and lights no item. The key item is skipped without Hallowed Concepts.
        private static IEnumerator LoadoutPane(Harness h, Board board, bool keyItem)
        {
            yield return UseBoard(h, board);
            // A few items too, which the scoreboard's checks look at.
            ScoreboardSetUp.GiveDifferentItems(h, TooltipItems);
            if (keyItem)
            {
                yield return h.GiveNtormat();
            }
            else
            {
                ScoreboardSetUp.GiveEquipment(h, RoR2Content.Equipment.Fruit);
            }
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(Collapses(board));
            ScoreboardRow row = SevenUIChecks.HudRoot(h).GetComponentsInChildren<ScoreboardRow>()
                .FirstOrDefault(each => each.Strip && each.Strip.master == Harness.LocalBody.master);
            ScoreboardStrip strip = row ? row.Strip : null;
            TooltipProvider provider = !strip ? null :
                keyItem ? (strip.keyItemIcon ? strip.keyItemIcon.tooltipProvider : null) :
                strip.equipmentIcon ? strip.equipmentIcon.tooltipProvider : null;
            string what = keyItem ? "key item" : "equipment";
            if (!provider) throw h.Fail($"no {what} tooltip in the viewer's row of the scoreboard");
            yield return h.Wait(SettleSeconds);
            PointAt(h, (RectTransform)provider.transform, $"the {what} in the scoreboard");
            yield return h.ExpectLoadoutPane(strip, keyItem);
            yield return h.Wait(SettleSeconds);
            string tabs = board == Board.Tabs ? "_tabs" : "";
            yield return h.Screenshot($"scoreboard{tabs}_{(keyItem ? "key_item" : "equipment")}_pane");
        }

        // The first item shown under the given part of the HUD.
        private static ItemIcon FirstIcon(Transform under) =>
            under.GetComponentsInChildren<ItemIcon>().FirstOrDefault(each => each.isActiveAndEnabled);

        // The pointer can't be moved, so the game's raycast is made at the target's middle: it must hit the target (or
        // the given part it is in) rather than anything drawn over it, and the hover goes to what it hit. The pointer
        // leaves it at cleanup.
        private static MPEventSystem PointAt(Harness h, RectTransform target, string what, System.Action<PointerEventData> made = null,
            Transform owner = null)
        {
            MPEventSystem events = EventSystem.current as MPEventSystem;
            if (!events) throw h.Fail("there is no event system to send the pointer's events with");
            Canvas canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 middle = RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(target.rect.center));
            PointerEventData pointer = new PointerEventData(events) { position = middle };
            List<RaycastResult> hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            GameObject hit = hits.Count > 0 ? hits[0].gameObject : null;
            if (!hit || !hit.transform.IsChildOf(owner ? owner : target))
            {
                throw h.Fail($"the pointer on {what} reaches {(hit ? hit.name : "nothing")}, not {target.name}");
            }
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerEnterHandler);
            h.OnCleanup(() =>
            {
                if (target)
                {
                    ExecuteEvents.ExecuteHierarchy(target.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                }
            });
            made?.Invoke(pointer);
            return events;
        }

        public static Scenario SpreadScreenshot() => Shot("scoreboard", h => Spread(h, Board.Rows));

        public static IEnumerable<Scenario> Screenshots() =>
            Screenshots(Board.Rows).Concat(Screenshots(Board.Tabs));

        public static IEnumerable<Scenario> Screenshots(Board board)
        {
            yield return board == Board.Rows ? SpreadScreenshot() : Shot("scoreboard_tabs", h => Spread(h, board));
            foreach (int count in ItemCounts)
            {
                yield return Shot($"{Prefix(board)}items_{count}", h => WithItems(h, count, board));
            }
        }

        private static Scenario Shot(string name, System.Func<Harness, IEnumerator> run) =>
            new Scenario(1, name, Stages.TitanicPlains, run);

        // Many different items in the viewer's row, more than fit one line: the game would shrink them to fit its cell,
        // and lay them out in the order they were picked up.
        private static IEnumerator ItemsInSlots(Harness h, Board board)
        {
            yield return UseBoard(h, board);
            ScoreboardSetUp.GiveDifferentItems(h, ManyItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(Collapses(board));
        }

        // With the scoreboard open, the rows collapse in place as the other players pick up many items, and come back
        // as rows once they drop them, then collapse again. Then, with the flat look off from the collapsed rows, the
        // game's layout is back: no items board, the icons' pivots the game's, the rows' container laying them out, and
        // the headers shown.
        private static IEnumerator CollapseWhileOpen(Harness h)
        {
            List<CharacterMaster> players = new List<CharacterMaster>();
            yield return ScoreboardSetUp.AddPlayers(h, CrowdPlayers, RoomyItems, players);
            ScoreboardSetUp.GiveDifferentItems(h, ManyItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(false);
            List<(Inventory inventory, List<ItemDef> items)> picked = players
                .Select(player => (player.inventory, ScoreboardSetUp.GiveDifferentItems(h, CrowdItems - RoomyItems, player.inventory)))
                .ToList();
            yield return h.ExpectScoreboardAsCanvas(true);
            foreach ((Inventory inventory, List<ItemDef> items) in picked)
            {
                ScoreboardSetUp.TakeItems(inventory, items);
            }
            yield return h.ExpectScoreboardAsCanvas(false);
            foreach ((Inventory inventory, List<ItemDef> items) in picked)
            {
                ScoreboardSetUp.GiveItems(inventory, items);
            }
            yield return h.ExpectScoreboardAsCanvas(true);
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            string look = null;
            yield return h.Until(() =>
                {
                    ScoreboardController board = SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardController>();
                    if (!board) return false;
                    Transform strips = board.transform.Find("Container/StripContainer");
                    Transform headers = board.transform.Find("Container/HeaderGroup");
                    ScoreboardStrip strip = board.GetComponentsInChildren<ScoreboardStrip>().FirstOrDefault();
                    ItemInventoryDisplay display = strip ? strip.itemInventoryDisplay : null;
                    Vector2 pivot = display && display.itemIconPrefab ? ((RectTransform)display.itemIconPrefab.transform).pivot : Vector2.zero;
                    bool pivots = display && display.itemIcons.All(icon => !icon || icon.rectTransform.pivot == pivot);
                    bool laidOut = strips && strips.GetComponent<VerticalLayoutGroup>().enabled;
                    look = $"items board {(bool)board.GetComponentInChildren<ScoreboardItemsBoard>(true)}, " +
                           $"game's pivots {pivots}, container laying out {laidOut}, headers shown {headers && headers.gameObject.activeSelf}";
                    return !board.GetComponentInChildren<ScoreboardItemsBoard>(true) && pivots && laidOut &&
                           headers && headers.gameObject.activeSelf;
                },
                () => $"the scoreboard back in the game's layout ({look}) {h.Diagnosis()}");
        }

        // On each board, ScoreboardBesideRunInfo moves the board beside the run info; off, it is centered on the screen,
        // as the game centers it, still the canvas's width.
        private static IEnumerator CenteredWithoutSetting(Harness h, Board board)
        {
            yield return UseBoard(h, board);
            ScreenshotScenarios.GiveItemSpread(h);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(Collapses(board));
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardBesideRunInfo, false);
            yield return h.ExpectScoreboardAsCanvas(Collapses(board));
        }

        // The tab screen with one item of each main tier and the equipment, on the given board.
        private static IEnumerator Spread(Harness h, Board board)
        {
            yield return UseBoard(h, board);
            ScreenshotScenarios.GiveItemSpread(h);
            h.Cmd($"{Commands.GiveEquipment} {GameNames.Fruit}");
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(Collapses(board));
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }

        // The tab screen with that many different items in the player's row, from one line of slots to several, on the
        // given board, so the screenshots compare from run to run.
        private static IEnumerator WithItems(Harness h, int count, Board board)
        {
            yield return UseBoard(h, board);
            ScoreboardSetUp.GiveDifferentItems(h, count);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(Collapses(board));
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }
    }
}
