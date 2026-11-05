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
    // item describes it in the scoreboard's pane, and that the layout can be switched while the scoreboard is open, and
    // screenshots of the scoreboard in SevenUI's look and the game's, with a spread of items and with from a few to a
    // hundred different items. Together they make the scoreboard suite (SevenUITests.Suites); the checks are also among
    // the panels' checks, and the spread's screenshot among the screenshots.
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

        public static IEnumerable<Scenario> All() => Checks().Concat(Screenshots());

        public static IEnumerable<Scenario> Checks()
        {
            yield return new Scenario(1, "scoreboard_items_in_slots", Stages.TitanicPlains, ItemsInSlots);
            yield return new Scenario(1, "scoreboard_tabs_items_in_slots", Stages.TitanicPlains,
                h => ItemsInSlots(h, ScoreboardLayout.Tabs));
            yield return new Scenario(1, "scoreboard_centered_without_setting", Stages.TitanicPlains, CenteredWithoutSetting);
            yield return new Scenario(1, "scoreboard_layout_switch", Stages.TitanicPlains, LayoutSwitch);
            yield return new Scenario(1, "scoreboard_item_pane", Stages.TitanicPlains, h => ItemPane(h, ScoreboardLayout.Rows));
            yield return new Scenario(1, "scoreboard_tabs_item_pane", Stages.TitanicPlains, h => ItemPane(h, ScoreboardLayout.Tabs));
            yield return new Scenario(1, "item_bar_item_tooltip", Stages.TitanicPlains, ItemBarTooltip);
        }

        // With the scoreboard open, the pointer on an item in the item bar over it opens the item's tooltip, in the flat
        // look.
        private static IEnumerator ItemBarTooltip(Harness h)
        {
            ScoreboardSetUp.GiveDifferentItems(h, TooltipItems);
            yield return ScoreboardSetUp.Open(h);
            HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
            ItemIcon icon = null;
            yield return h.Until(() => (icon = FirstIcon(hud.itemInventoryDisplay.transform)) != null, "an item in the item bar");
            yield return h.Wait(SettleSeconds);
            MPEventSystem events = PointAt(h, icon, "the item bar");
            yield return h.Until(() => events.currentTooltipProvider == icon.tooltipProvider && events.currentTooltip,
                () => $"the pointer on an item in the item bar opened " +
                      $"{(events.currentTooltipProvider ? events.currentTooltipProvider.name + "'s tooltip" : "no tooltip")}");
            yield return h.Until(() => events.currentTooltip && events.currentTooltip.transform.Find(TooltipBandPath),
                $"the item's tooltip in the flat look");
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("tooltip");
        }

        // In each layout, the pointer on an item in the scoreboard describes it in the pane instead of a tooltip, lights
        // it, and in the rows layout dims the rest; moved off, the light goes out and the pane stays on the item.
        private static IEnumerator ItemPane(Harness h, ScoreboardLayout layout)
        {
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, layout);
            ScoreboardSetUp.GiveDifferentItems(h, TooltipItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas();
            ScoreboardRow row = SevenUIChecks.HudRoot(h).GetComponentsInChildren<ScoreboardRow>()
                .FirstOrDefault(each => each.Strip && each.Strip.master == Harness.LocalBody.master);
            ItemIcon icon = row ? FirstIcon(row.Strip.itemInventoryDisplay.transform) : null;
            if (!icon) throw h.Fail("no item in the viewer's row of the scoreboard");
            yield return h.Wait(SettleSeconds);
            PointerEventData pointer = null;
            PointAt(h, icon, "the scoreboard", made => pointer = made);
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

        // The first item shown under the given part of the HUD.
        private static ItemIcon FirstIcon(Transform under) =>
            under.GetComponentsInChildren<ItemIcon>().FirstOrDefault(each => each.isActiveAndEnabled);

        // The pointer can't be moved, so the game's raycast is made at the item's middle: it must hit the item rather
        // than anything drawn over it, and the hover goes to what it hit. The pointer leaves the item at cleanup.
        private static MPEventSystem PointAt(Harness h, ItemIcon icon, string where, System.Action<PointerEventData> made = null)
        {
            MPEventSystem events = EventSystem.current as MPEventSystem;
            if (!events) throw h.Fail("there is no event system to send the pointer's events with");
            Canvas canvas = icon.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 middle = RectTransformUtility.WorldToScreenPoint(camera, icon.rectTransform.TransformPoint(icon.rectTransform.rect.center));
            PointerEventData pointer = new PointerEventData(events) { position = middle };
            List<RaycastResult> hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            GameObject hit = hits.Count > 0 ? hits[0].gameObject : null;
            if (!hit || !hit.transform.IsChildOf(icon.transform))
            {
                throw h.Fail($"the pointer on an item in {where} reaches {(hit ? hit.name : "nothing")}, not the item");
            }
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerEnterHandler);
            h.OnCleanup(() =>
            {
                if (icon)
                {
                    ExecuteEvents.ExecuteHierarchy(icon.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                }
            });
            made?.Invoke(pointer);
            return events;
        }

        public static Scenario SpreadScreenshot() => Shot("scoreboard", Spread);

        public static IEnumerable<Scenario> Screenshots()
        {
            yield return SpreadScreenshot();
            yield return Shot("scoreboard_tabs", h => Spread(h, ScoreboardLayout.Tabs));
            foreach (int count in ItemCounts)
            {
                yield return Shot($"scoreboard_items_{count}", h => WithItems(h, count));
            }
        }

        private static Scenario Shot(string name, System.Func<Harness, IEnumerator> run) =>
            new Scenario(1, name, Stages.TitanicPlains, run);

        // Many different items in the viewer's row, more than fit one line: the game would shrink them to fit its cell,
        // and lay them out in the order they were picked up.
        private static IEnumerator ItemsInSlots(Harness h) => ItemsInSlots(h, ScoreboardLayout.Rows);

        private static IEnumerator ItemsInSlots(Harness h, ScoreboardLayout layout)
        {
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, layout);
            ScoreboardSetUp.GiveDifferentItems(h, ManyItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas();
        }

        // Switched while the scoreboard is open, the layout changes in place: rows, tabs, and rows again. Then, with the
        // flat look off, the game's layout is back: no items board, the icons' pivots the game's, the
        // rows' container laying them out, and the headers shown.
        private static IEnumerator LayoutSwitch(Harness h)
        {
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, ScoreboardLayout.Rows);
            ScoreboardSetUp.GiveDifferentItems(h, ManyItems);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas();
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, ScoreboardLayout.Tabs);
            yield return h.ExpectScoreboardAsCanvas();
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, ScoreboardLayout.Rows);
            yield return h.ExpectScoreboardAsCanvas();

            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, ScoreboardLayout.Tabs);
            yield return h.ExpectScoreboardAsCanvas();
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
        private static IEnumerator Spread(Harness h) => Spread(h, ScoreboardLayout.Rows);

        private static IEnumerator Spread(Harness h, ScoreboardLayout layout)
        {
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, layout);
            ScreenshotScenarios.GiveItemSpread(h);
            h.Cmd($"{Commands.GiveEquipment} {GameNames.Fruit}");
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas();
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }

        // The tab screen with that many different items in the player's row, from one line of slots to several. In
        // the rows layout whatever the player's setting, so the screenshots compare from run to run.
        private static IEnumerator WithItems(Harness h, int count)
        {
            h.SetConfig(SevenUIPlugin.Settings.ScoreboardLayout, ScoreboardLayout.Rows);
            ScoreboardSetUp.GiveDifferentItems(h, count);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas();
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }
    }
}
