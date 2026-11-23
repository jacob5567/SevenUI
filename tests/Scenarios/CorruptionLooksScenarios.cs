using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SevenUI.Tests
{
    // Screenshots of every place an item's description can say it is tied to void corruption, in every combination of
    // held items and LookingGlass's settings, to find text said twice (Jacob's audit after I2, 2026-11-22). Ukulele and
    // Polylute stand for every pair. Logs each text shown, and flags one that mentions corruption more than once. They
    // check nothing; run them with --suite looks.
    internal static class CorruptionLooksScenarios
    {
        private const float SettleSeconds = 0.5f;
        private const int HeldOriginals = 2;
        private const float NotificationTimeout = 20f;
        private const string LookingGlassCommand = "Command Settings";
        private const string LookingGlassTooltips = "Command Tooltips";
        private const string LookingGlassMisc = "Misc";
        private const string LookingGlassFull = "Full Description in Hud";
        // Words that say an item is tied to corruption: the game's "Corrupts all Ukuleles.", LookingGlass's "Has been
        // corrupted by:", SevenUI's "Corrupted by Polylute" and "Corrupts your Ukulele x2".
        private static readonly Regex CorruptionWords = new Regex("corrupt", RegexOptions.IgnoreCase);
        private static readonly Regex Tags = new Regex("<[^>]*>");

        private enum Held
        {
            Nothing,
            Polylute,
            Ukuleles,
        }

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "looks_corruption_pickers", Stages.TitanicPlains, Pickers);
            yield return new Scenario(1, "looks_corruption_inspect", Stages.TitanicPlains, InspectPanel);
            yield return new Scenario(1, "looks_corruption_notification", Stages.TitanicPlains, Notification);
            yield return new Scenario(1, "looks_corruption_scoreboard", Stages.TitanicPlains, Scoreboard);
        }

        // Each LookingGlass combination (its picker tooltips and its full descriptions, on and off), each picker layout
        // (cards for a few options, a grid for more), each held state: both options' descriptions, hovered in turn.
        private static IEnumerator Pickers(Harness h)
        {
            if (!DLC1Content.Items.ChainLightningVoid) throw h.Skip("Polylute needs Survivors of the Void");
            ConfigEntry<bool> tooltips = LookingGlassSetting(LookingGlassCommand, LookingGlassTooltips);
            ConfigEntry<bool> full = LookingGlassSetting(LookingGlassMisc, LookingGlassFull);
            foreach ((bool tips, bool desc) in Combinations(tooltips, full))
            {
                if (tooltips != null) h.SetConfig(tooltips, tips);
                if (full != null) h.SetConfig(full, desc);
                string lookingGlass = LookingGlassName(tooltips, full, tips, desc);
                foreach (bool cards in new[] { true, false })
                {
                    foreach (Held held in new[] { Held.Nothing, Held.Polylute, Held.Ukuleles })
                    {
                        yield return Picker(h, lookingGlass, cards, held);
                    }
                }
            }
        }

        private static IEnumerator Picker(Harness h, string lookingGlass, bool cards, Held held)
        {
            Inventory inventory = Harness.LocalBody.master.inventory;
            Hold(h, inventory, held);
            yield return h.Wait(SettleSeconds);
            yield return PickerScenarios.GoHome(h);
            List<ItemDef> offered = new List<ItemDef> { RoR2Content.Items.ChainLightning, DLC1Content.Items.ChainLightningVoid };
            if (!cards)
            {
                offered.AddRange(ItemCatalog.allItemDefs.Where(item => item && item.tier == ItemTier.Tier2 && !item.hidden &&
                                                                       item != RoR2Content.Items.ChainLightning).Take(PickerPanel.CardLimit));
            }
            PickupPickerController picker = h.SpawnPrefab<PickupPickerController>(GameNames.CommandCube);
            picker.SetOptionsServer(offered.Select(item => new PickupPickerController.Option
            {
                pickup = new UniquePickup(PickupCatalog.FindPickupIndex(item.itemIndex)),
                available = true,
            }).ToArray());
            PickerPanel panel = null;
            yield return PickerScenarios.OpenStyled(h, picker, found => panel = found);
            string layout = panel.Cards ? "cards" : "grid";
            foreach (ItemDef item in offered.Take(2))
            {
                PickerButton option = panel.Buttons.FirstOrDefault(button => button.Pickup != null && button.Pickup.itemIndex == item.itemIndex);
                if (option == null) throw h.Fail($"no {item.name} option");
                PointerEventData pointer = new PointerEventData(EventSystem.current);
                GameObject hovered = option.Button.gameObject;
                // The grid's details follow the selected option (a gamepad's, or the pointer's as it enters), which
                // starts on the first.
                option.Button.Select();
                ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerEnterHandler);
                yield return h.Wait(SettleSeconds);
                string shown = panel.Cards ? (option.Description ? option.Description.text : "") :
                    panel.DetailsDescriptionText ? panel.DetailsDescriptionText.text : "";
                Report(h, $"picker {layout}, {lookingGlass}, holding {Holding(held)}, {Language.GetString(item.nameToken)}", shown,
                    $"count \"{(option.Count ? option.Count.text : "")}\", corner {(option.CorruptMark && option.CorruptMark.enabled ? "on" : "off")}");
                yield return h.Screenshot($"picker_{layout}_{Slug(lookingGlass)}_{held}_{item.name}");
                ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerExitHandler);
            }
            PickerScenarios.Close(panel.Panel);
            Object.Destroy(picker.gameObject);
            yield return h.Wait(SettleSeconds);
        }

        // The inspect panel (the interact prompt's Inspect, for an item on the ground, or the scoreboard's): Ukulele
        // and Polylute in each held state, with LookingGlass's full descriptions on and off.
        private static IEnumerator InspectPanel(Harness h)
        {
            if (!DLC1Content.Items.ChainLightningVoid) throw h.Skip("Polylute needs Survivors of the Void");
            ConfigEntry<bool> full = LookingGlassSetting(LookingGlassMisc, LookingGlassFull);
            Inventory inventory = Harness.LocalBody.master.inventory;
            yield return ScoreboardSetUp.Open(h);
            foreach (bool desc in full != null ? new[] { true, false } : new[] { false })
            {
                if (full != null) h.SetConfig(full, desc);
                foreach (Held held in new[] { Held.Nothing, Held.Polylute, Held.Ukuleles })
                {
                    Hold(h, inventory, held);
                    foreach (ItemDef item in new[] { RoR2Content.Items.ChainLightning, DLC1Content.Items.ChainLightningVoid })
                    {
                        yield return ScoreboardInspectScenarios.Inspect(h, item);
                        yield return h.Wait(SettleSeconds);
                        string shown = string.Join(" | ", SevenUIChecks.HudRoot(h).GetComponentsInChildren<InspectPanelController>(true)
                            .SelectMany(panel => panel.GetComponentsInChildren<TMP_Text>())
                            .Where(text => text.isActiveAndEnabled && text.text.Length > 0).Select(text => text.text));
                        Report(h, $"inspect, LookingGlass full descriptions {(full == null ? "absent" : desc ? "on" : "off")}, " +
                                  $"holding {Holding(held)}, {Language.GetString(item.nameToken)}", shown, "");
                        yield return h.Screenshot($"inspect_{(desc ? "full" : "short")}_{held}_{item.name}");
                    }
                }
            }
        }

        // The notification when a picked-up Ukulele is corrupted at once by a held Polylute, with LookingGlass's full
        // descriptions on and off.
        private static IEnumerator Notification(Harness h)
        {
            if (!DLC1Content.Items.ChainLightningVoid) throw h.Skip("Polylute needs Survivors of the Void");
            ConfigEntry<bool> full = LookingGlassSetting(LookingGlassMisc, LookingGlassFull);
            foreach (bool desc in full != null ? new[] { true, false } : new[] { false })
            {
                if (full != null) h.SetConfig(full, desc);
                CharacterMasterNotificationQueue.PushItemTransformNotification(Harness.LocalBody.master, RoR2Content.Items.ChainLightning.itemIndex,
                    DLC1Content.Items.ChainLightningVoid.itemIndex, CharacterMasterNotificationQueue.TransformationType.ContagiousVoid);
                GenericNotification shown = null;
                yield return h.Until(() => (shown = SevenUIChecks.HudRoot(h).GetComponentsInChildren<GenericNotification>()
                        .FirstOrDefault(found => found.titleText && found.titleText.token == DLC1Content.Items.ChainLightningVoid.nameToken)) != null,
                    "the Polylute transformation notification", NotificationTimeout);
                yield return h.Wait(SettleSeconds * 2f);
                string text = string.Join(" | ", shown.GetComponentsInChildren<TMP_Text>().Where(found => found.isActiveAndEnabled)
                    .Select(found => found.text));
                Report(h, $"notification, LookingGlass full descriptions {(full == null ? "absent" : desc ? "on" : "off")}", text, "");
                yield return h.Screenshot($"notification_{(desc ? "full" : "short")}");
                // The next waits its turn in the game's queue, after this one fades.
                yield return h.Until(() => !shown, "the notification fading", NotificationTimeout);
            }
        }

        // A held item pointed at on the scoreboard (described in the docked pane, or in a tooltip with the pane off) and
        // in the item bar: Polylute alone and Ukuleles alone (a player can't hold both; the game corrupts the Ukuleles),
        // with LookingGlass's full descriptions on and off.
        private static IEnumerator Scoreboard(Harness h)
        {
            if (!DLC1Content.Items.ChainLightningVoid) throw h.Skip("Polylute needs Survivors of the Void");
            ConfigEntry<bool> full = LookingGlassSetting(LookingGlassMisc, LookingGlassFull);
            Inventory inventory = Harness.LocalBody.master.inventory;
            HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
            yield return ScoreboardSetUp.Open(h);
            // Off first: LookingGlass writes its full description onto an icon's tooltip and leaves it there when the
            // setting is turned off, so an icon reused for another item would keep the old item's text.
            foreach (bool desc in full != null ? new[] { false, true } : new[] { false })
            {
                if (full != null) h.SetConfig(full, desc);
                string lookingGlass = $"LookingGlass full descriptions {(full == null ? "absent" : desc ? "on" : "off")}";
                foreach (Held held in new[] { Held.Polylute, Held.Ukuleles })
                {
                    Hold(h, inventory, held);
                    ItemDef item = held == Held.Polylute ? DLC1Content.Items.ChainLightningVoid : RoR2Content.Items.ChainLightning;
                    yield return h.Wait(SettleSeconds * 2f);
                    foreach (bool pane in new[] { true, false })
                    {
                        h.SetConfig(SevenUIPlugin.Settings.ScoreboardDetailPanel, pane);
                        yield return h.Wait(SettleSeconds);
                        ItemIcon onBoard = SevenUIChecks.HudRoot(h).GetComponentsInChildren<ScoreboardRow>()
                            .Where(row => row.Strip && row.Strip.master == Harness.LocalBody.master)
                            .SelectMany(row => row.Strip.itemInventoryDisplay.GetComponentsInChildren<ItemIcon>())
                            .FirstOrDefault(icon => icon.itemIndex == item.itemIndex);
                        if (!onBoard) throw h.Fail($"no {item.name} in the viewer's scoreboard row");
                        PointerEventData pointer = null;
                        MPEventSystem events = ScoreboardScenarios.PointAt(h, onBoard.rectTransform, item.name + " on the scoreboard",
                            made => pointer = made);
                        yield return h.Wait(SettleSeconds * 2f);
                        string shown = pane ? Texts(SevenUIChecks.HudRoot(h).GetComponentsInChildren<Transform>()
                                .FirstOrDefault(found => found.name == ScoreboardDetailPane.PaneName))
                            : Texts(events && events.currentTooltip ? events.currentTooltip.transform : null);
                        Report(h, $"scoreboard {(pane ? "pane" : "tooltip")}, {lookingGlass}, holding {Holding(held)}, " +
                                  Language.GetString(item.nameToken), shown, "");
                        yield return h.Screenshot($"board_{(pane ? "pane" : "tooltip")}_{(desc ? "full" : "short")}_{held}");
                        ExecuteEvents.ExecuteHierarchy(onBoard.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                    }

                    ItemIcon inBar = hud.itemInventoryDisplay.GetComponentsInChildren<ItemIcon>()
                        .FirstOrDefault(icon => icon.itemIndex == item.itemIndex);
                    if (!inBar) throw h.Fail($"no {item.name} in the item bar");
                    PointerEventData barPointer = null;
                    MPEventSystem barEvents = ScoreboardScenarios.PointAt(h, inBar.rectTransform, item.name + " in the item bar",
                        made => barPointer = made);
                    yield return h.Until(() => barEvents.currentTooltip, "the item bar's tooltip");
                    yield return h.Wait(SettleSeconds);
                    Report(h, $"item bar tooltip, {lookingGlass}, holding {Holding(held)}, {Language.GetString(item.nameToken)}",
                        Texts(barEvents.currentTooltip.transform), "");
                    yield return h.Screenshot($"bar_tooltip_{(desc ? "full" : "short")}_{held}");
                    ExecuteEvents.ExecuteHierarchy(inBar.gameObject, barPointer, ExecuteEvents.pointerExitHandler);
                    yield return h.Wait(SettleSeconds);
                }
            }
        }

        private static string Texts(Transform root) => root
            ? string.Join(" | ", root.GetComponentsInChildren<TMP_Text>().Where(text => text.isActiveAndEnabled && text.text.Length > 0)
                .Select(text => text.text))
            : "";

        private static void Report(Harness h, string where, string text, string extra)
        {
            string plain = Tags.Replace(text ?? "", "").Replace("\n", " / ");
            int mentions = CorruptionWords.Matches(plain).Count;
            h.Log($"CORRUPTION-LOOK {(mentions > 1 ? "TWICE" : "ok")} [{where}] {extra} :: {plain}");
        }

        // Clears both items, then gives what the state holds; puts the player's own stacks back at the end.
        private static void Hold(Harness h, Inventory inventory, Held held)
        {
            ItemDef original = RoR2Content.Items.ChainLightning;
            ItemDef corrupter = DLC1Content.Items.ChainLightningVoid;
            int originalBefore = inventory.GetItemCountPermanent(original);
            int corrupterBefore = inventory.GetItemCountPermanent(corrupter);
            if (originalBefore > 0) inventory.RemoveItemPermanent(original, originalBefore);
            if (corrupterBefore > 0) inventory.RemoveItemPermanent(corrupter, corrupterBefore);
            if (held == Held.Polylute) inventory.GiveItemPermanent(corrupter, 1);
            if (held == Held.Ukuleles) inventory.GiveItemPermanent(original, HeldOriginals);
            h.OnCleanup(() =>
            {
                if (!inventory) return;
                inventory.RemoveItemPermanent(original, inventory.GetItemCountPermanent(original));
                inventory.RemoveItemPermanent(corrupter, inventory.GetItemCountPermanent(corrupter));
                if (originalBefore > 0) inventory.GiveItemPermanent(original, originalBefore);
                if (corrupterBefore > 0) inventory.GiveItemPermanent(corrupter, corrupterBefore);
            });
        }

        private static IEnumerable<(bool, bool)> Combinations(ConfigEntry<bool> tooltips, ConfigEntry<bool> full)
        {
            if (tooltips == null || full == null)
            {
                yield return (false, false);
                yield break;
            }
            yield return (true, true);
            yield return (true, false);
            yield return (false, true);
            yield return (false, false);
        }

        private static string LookingGlassName(ConfigEntry<bool> tooltips, ConfigEntry<bool> full, bool tips, bool desc) =>
            tooltips == null || full == null
                ? "LookingGlass absent"
                : $"LookingGlass tooltips {(tips ? "on" : "off")} full {(desc ? "on" : "off")}";

        private static string Slug(string text) => Regex.Replace(text.Replace("LookingGlass ", "lg "), "[^a-z0-9]+", "_",
            RegexOptions.IgnoreCase).Trim('_').ToLowerInvariant();

        private static string Holding(Held held) => held == Held.Nothing ? "neither" : held == Held.Polylute ? "1 Polylute" :
            HeldOriginals + " Ukulele";

        private static ConfigEntry<bool> LookingGlassSetting(string section, string key)
        {
            if (!Chainloader.PluginInfos.TryGetValue(GameNames.LookingGlassGUID, out BepInEx.PluginInfo info) || !info.Instance) return null;
            return info.Instance.Config.TryGetEntry(section, key, out ConfigEntry<bool> entry) ? entry : null;
        }
    }
}
