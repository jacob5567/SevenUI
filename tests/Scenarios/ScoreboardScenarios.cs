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
    // The scoreboard: checks that a row's items are laid out as the canvas's grid in each layout, that pointing at an
    // item, the equipment or the key item describes it in the scoreboard's pane, that the tabs setting keeps the rows
    // while there is room and collapses them when crowded, and that the layout can be switched while the scoreboard is
    // open, and screenshots of the scoreboard in SevenUI's look and the game's, with a spread of items and with from a
    // few to a hundred different items, each in both layouts. In the tabs setting the board is crowded with stand-in
    // players (ScoreboardSetUp.AddPlayers), so its rows collapse. Together they make the scoreboard suite, and each
    // layout's its own suite (SevenUITests.Suites); the checks are also among the panels' checks, and the spread's
    // screenshot among the screenshots.
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
        // Other players, each with this many different items: more than the rows fit at 32px slots, so in the tabs
        // setting they collapse.
        private const int CrowdPlayers = 3;
        private const int CrowdItems = 100;
        // The size the rows keep before the tabs setting collapses them.
        private const float SmallestRowsSlot = 32f * HudUnits.PerCanvasPixel;

        public static IEnumerable<Scenario> All() => Checks().Concat(Screenshots());

        // One layout's checks and screenshots, and the switch between the layouts, which both suites share.
        public static IEnumerable<Scenario> All(ScoreboardLayout layout) =>
            Checks(layout).Append(LayoutSwitchScenario()).Concat(Screenshots(layout));

        public static IEnumerable<Scenario> Checks() =>
            Checks(ScoreboardLayout.Rows).Concat(Checks(ScoreboardLayout.Tabs)).Append(LayoutSwitchScenario());

        // Each layout's checks, named "scoreboard_..." in the rows layout and "scoreboard_tabs_..." in the tabs layout.
        public static IEnumerable<Scenario> Checks(ScoreboardLayout layout)
        {
            string prefix = Prefix(layout);
            yield return new Scenario(1, prefix + "items_in_slots", Stages.TitanicPlains, h => ItemsInSlots(h, layout));
            yield return new Scenario(1, prefix + "centered_without_setting", Stages.TitanicPlains,
                h => CenteredWithoutSetting(h, layout));
            yield return new Scenario(1, prefix + "item_pane", Stages.TitanicPlains, h => ItemPane(h, layout));
            yield return new Scenario(1, prefix + "equipment_pane", Stages.TitanicPlains, h => LoadoutPane(h, layout, false));
            yield return new Scenario(1, prefix + "key_item_pane", Stages.TitanicPlains, h => LoadoutPane(h, layout, true));
            yield return new Scenario(1, layout == ScoreboardLayout.Rows ? "item_bar_item_tooltip" : "item_bar_item_tooltip_over_tabs",
                Stages.TitanicPlains, h => ItemBarTooltip(h, layout));
            if (layout == ScoreboardLayout.Rows)
            {
                yield return new Scenario(1, "scoreboard_shrinks_when_crowded", Stages.TitanicPlains, ShrinksWhenCrowded);
            }
            else
            {
                yield return new Scenario(1, "scoreboard_tabs_rows_while_roomy", Stages.TitanicPlains, RowsWhileRoomy);
                yield return new Scenario(1, "scoreboard_tabs_shows_pointed_player", Stages.TitanicPlains, ShowsPointedPlayer);
            }
        }

        // The layout, and in the tabs setting the board crowded with other players so its rows collapse.
        private static IEnumerator UseLayout(Harness h, ScoreboardLayout layout)
        {
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, layout);
            if (layout == ScoreboardLayout.Tabs)
            {
                yield return ScoreboardSetUp.AddPlayers(h, CrowdPlayers, CrowdItems);
            }
        }

        private static bool Collapses(ScoreboardLayout layout) => layout == ScoreboardLayout.Tabs;

        // In the rows setting, the crowded board keeps its rows, their slots shrunk below the size the tabs setting
        // keeps: the same crowd collapses the tabs setting's rows.
        private static IEnumerator ShrinksWhenCrowded(Harness h)
        {
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, ScoreboardLayout.Rows);
            yield return ScoreboardSetUp.AddPlayers(h, CrowdPlayers, CrowdItems);
            ScoreboardSetUp.GiveDifferentItems(h, ManyItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(false);
            ScoreboardItemsBoard board = SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardItemsBoard>();
            yield return h.Until(() => board && board.Grids.Count > CrowdPlayers &&
                                       board.Grids.All(grid => grid.SlotSize < SmallestRowsSlot),
                () => $"every row's slots under {SmallestRowsSlot:0.#} units (" +
                      $"{(board ? string.Join(", ", board.Grids.Select(grid => grid.SlotSize.ToString("0.#"))) : "no board")})");
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("scoreboard_crowded");
        }

        // In the tabs setting, a board with room for the rows keeps them, at full size.
        private static IEnumerator RowsWhileRoomy(Harness h)
        {
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, ScoreboardLayout.Tabs);
            ScoreboardSetUp.GiveDifferentItems(h, ManyItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(false);
        }

        // With the rows collapsed, the pointer on another player's row shows their items in place of the viewer's, and
        // moves the tab to their row. The last row's: its hundred items run past the rows' bottom, so the tab reaches
        // up from it.
        private static IEnumerator ShowsPointedPlayer(Harness h)
        {
            yield return UseLayout(h, ScoreboardLayout.Tabs);
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

        private static Scenario LayoutSwitchScenario() =>
            new Scenario(1, "scoreboard_layout_switch", Stages.TitanicPlains, LayoutSwitch);

        private static string Prefix(ScoreboardLayout layout) =>
            layout == ScoreboardLayout.Rows ? "scoreboard_" : "scoreboard_tabs_";

        // With the scoreboard open in each layout, the pointer on an item in the item bar over it opens the item's
        // tooltip, in the flat look: the board's pane doesn't take it.
        private static IEnumerator ItemBarTooltip(Harness h, ScoreboardLayout layout)
        {
            yield return UseLayout(h, layout);
            ScoreboardSetUp.GiveDifferentItems(h, TooltipItems);
            yield return ScoreboardSetUp.Open(h);
            // The scoreboard under the item bar as the layout draws it, its rows collapsed in the tabs setting.
            yield return h.ExpectScoreboardAsCanvas(Collapses(layout));
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
            yield return h.Screenshot(layout == ScoreboardLayout.Rows ? "tooltip" : "tooltip_over_tabs");
        }

        // In each layout, the pointer on an item in the scoreboard describes it in the pane instead of a tooltip, lights
        // it, and in the rows layout dims the rest; moved off, the light goes out and the pane stays on the item.
        private static IEnumerator ItemPane(Harness h, ScoreboardLayout layout)
        {
            yield return UseLayout(h, layout);
            ScoreboardSetUp.GiveDifferentItems(h, TooltipItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(Collapses(layout));
            ScoreboardRow row = SevenUIChecks.HudRoot(h).GetComponentsInChildren<ScoreboardRow>()
                .FirstOrDefault(each => each.Strip && each.Strip.master == Harness.LocalBody.master);
            ItemIcon icon = row ? FirstIcon(row.Strip.itemInventoryDisplay.transform) : null;
            if (!icon) throw h.Fail("no item in the viewer's row of the scoreboard");
            yield return h.Wait(SettleSeconds);
            PointerEventData pointer = null;
            PointAt(h, icon.rectTransform, "an item in the scoreboard", made => pointer = made);
            yield return h.ExpectItemPane(icon);
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot(layout == ScoreboardLayout.Rows ? "scoreboard_pane" : "scoreboard_tabs_pane");

            ExecuteEvents.ExecuteHierarchy(icon.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            ScoreboardItemsBoard board = icon.GetComponentInParent<ScoreboardItemsBoard>();
            yield return h.Until(() => board && board.Highlighted == ItemIndex.None && board.Pane.Shown &&
                                       icon.transform.parent.GetComponentsInChildren<ItemIcon>()
                                           .All(each => !each.GetComponent<CanvasGroup>() || each.GetComponent<CanvasGroup>().alpha >= 1f),
                () => $"the light off and the pane still on the item once the pointer left it (lit " +
                      $"{(board ? board.Highlighted.ToString() : "no board")}, pane on {(board && board.Pane.Shown ? board.Pane.Title : "nothing")})");
        }

        // In each layout, the pointer on the viewer's equipment (Foreign Fruit), or key item (the N'tormat), describes it
        // in the pane instead of a tooltip, and lights no item. The key item is skipped without Hallowed Concepts.
        private static IEnumerator LoadoutPane(Harness h, ScoreboardLayout layout, bool keyItem)
        {
            yield return UseLayout(h, layout);
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
            yield return h.ExpectScoreboardAsCanvas(Collapses(layout));
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
            string tabs = layout == ScoreboardLayout.Tabs ? "_tabs" : "";
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

        public static Scenario SpreadScreenshot() => Shot("scoreboard", h => Spread(h, ScoreboardLayout.Rows));

        public static IEnumerable<Scenario> Screenshots() =>
            Screenshots(ScoreboardLayout.Rows).Concat(Screenshots(ScoreboardLayout.Tabs));

        public static IEnumerable<Scenario> Screenshots(ScoreboardLayout layout)
        {
            yield return layout == ScoreboardLayout.Rows ? SpreadScreenshot() : Shot("scoreboard_tabs", h => Spread(h, layout));
            foreach (int count in ItemCounts)
            {
                yield return Shot($"{Prefix(layout)}items_{count}", h => WithItems(h, count, layout));
            }
        }

        private static Scenario Shot(string name, System.Func<Harness, IEnumerator> run) =>
            new Scenario(1, name, Stages.TitanicPlains, run);

        // Many different items in the viewer's row, more than fit one line: the game would shrink them to fit its cell,
        // and lay them out in the order they were picked up.
        private static IEnumerator ItemsInSlots(Harness h, ScoreboardLayout layout)
        {
            yield return UseLayout(h, layout);
            ScoreboardSetUp.GiveDifferentItems(h, ManyItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(Collapses(layout));
        }

        // On a crowded board, switched while the scoreboard is open, the layout changes in place: rows, collapsed tabs,
        // and rows again. Then, with the flat look off from the collapsed tabs, the game's layout is back: no items
        // board, the icons' pivots the game's, the rows' container laying them out, and the headers shown.
        private static IEnumerator LayoutSwitch(Harness h)
        {
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, ScoreboardLayout.Rows);
            yield return ScoreboardSetUp.AddPlayers(h, CrowdPlayers, CrowdItems);
            ScoreboardSetUp.GiveDifferentItems(h, ManyItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(false);
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, ScoreboardLayout.Tabs);
            yield return h.ExpectScoreboardAsCanvas(true);
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, ScoreboardLayout.Rows);
            yield return h.ExpectScoreboardAsCanvas(false);

            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, ScoreboardLayout.Tabs);
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

        // In each layout, ScoreboardBesideRunInfo moves the board beside the run info; off, it is centered on the screen,
        // as the game centers it, still the canvas's width.
        private static IEnumerator CenteredWithoutSetting(Harness h, ScoreboardLayout layout)
        {
            yield return UseLayout(h, layout);
            ScreenshotScenarios.GiveItemSpread(h);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(Collapses(layout));
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardBesideRunInfo, false);
            yield return h.ExpectScoreboardAsCanvas(Collapses(layout));
        }

        // The tab screen with one item of each main tier and the equipment, in the given layout whatever the player's
        // setting.
        private static IEnumerator Spread(Harness h, ScoreboardLayout layout)
        {
            yield return UseLayout(h, layout);
            ScreenshotScenarios.GiveItemSpread(h);
            h.Cmd($"{Commands.GiveEquipment} {GameNames.Fruit}");
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(Collapses(layout));
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }

        // The tab screen with that many different items in the player's row, from one line of slots to several, in the
        // given layout whatever the player's setting, so the screenshots compare from run to run.
        private static IEnumerator WithItems(Harness h, int count, ScoreboardLayout layout)
        {
            yield return UseLayout(h, layout);
            ScoreboardSetUp.GiveDifferentItems(h, count);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(Collapses(layout));
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }
    }
}
