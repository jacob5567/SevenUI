using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // The scoreboard's inspect panel (what Tab shows for the pickup or interactable beside the player), restyled in place
    // as the canvas's design A: 60% of the board's width centered under it, 6px under it (the top padding
    // of the panel's own layout, not the container's spacing), the board's flat face, a 2px strip along its top in the title's color, the 64px icon on a
    // dark slot with a 3px strip of that color along its foot, the name in bold 21 over the description in 17 (I3), in the
    // body color, wrapping beside the icon.
    // The panel is shown through the game's own controller (Show), which is what the interact prompt's Inspect does,
    // with an item (Alien Head) and with a small chest's own inspect info. FlatPanels off puts the game's panel back as
    // it was (compared with a look taken before the mod touched it), and on again styles it again.
    internal static class ScoreboardInspectScenarios
    {
        private const string PanelPath = "Container/InspectPanel";
        private const string BoardPath = "Container/StripContainer";
        private const string InspectionPath = "Container/InspectPanel/InspectPanelArea/InspectionPanel";
        private const string IconPath = InspectionPath + "/HorizontalBox/InspectIconContainer/InspectVisualBackground";
        private const string BoxPath = InspectionPath + "/HorizontalBox";
        private const string TextsPath = InspectionPath + "/HorizontalBox/InspectDescriptionContainer";
        // The parts whose layout the restyle sets, which FlatPanels off must put back as the game had them.
        private static readonly string[] LayoutPaths =
        {
            PanelPath, PanelPath + "/InspectPanelArea", InspectionPath, InspectionPath + "/HorizontalBox",
            InspectionPath + "/HorizontalBox/InspectIconContainer", IconPath, TextsPath,
        };
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        // In the canvas's pixels, which Box measures in.
        private const float BoardGap = 6f;
        private const float StripHeight = 2f;
        private const float FootStripHeight = 3f;
        private const float IconSize = 64f;
        private const float PaddingX = 14f;
        private const float PaddingY = 12f;
        private const float IconGap = 12f;
        private const float NameSize = 21f;
        private const float DescriptionSize = 17f;
        private const float LineHeight = 1.4f;
        private const float Tolerance = 1.5f;
        private const float LineTolerance = 0.05f;
        private const float OpaqueAlpha = 0.99f;
        private const float SettleSeconds = 0.5f;
        // The tooltip's tier strip (TooltipStyler) and the docked panel's (ScoreboardDetailPane).
        private const string TooltipStripPath = "TooltipOffset/Panel/TitleRect/SevenUITierStrip";
        private const string PaneStripName = "TierStrip";
        // The stats column: its width, the line before it, the gaps, in the canvas's pixels.
        private const float StatsWidth = 260f;
        private const float DividerWidth = 1f;
        private const float StatsSpacing = 14f;
        private const float StatsTop = 2f;
        private const float StatsRowGap = 6f;
        private const float StatsLabelGap = 16f;
        private const float StatsSize = 17f;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "scoreboard_inspect_item_as_canvas", Stages.TitanicPlains, ItemAsCanvas);
            yield return new Scenario(1, "scoreboard_inspect_chest_as_canvas", Stages.TitanicPlains, ChestAsCanvas);
            yield return new Scenario(1, "scoreboard_inspect_flat_panels_off_and_on", Stages.TitanicPlains, FlatPanelsOffAndOn);
            yield return new Scenario(1, "scoreboard_inspect_stats_column", Stages.TitanicPlains, StatsColumn);
            yield return new Scenario(1, "scoreboard_inspect_stats_none_without_stats", Stages.TitanicPlains, NoStatsNoColumn);
            yield return new Scenario(1, "scoreboard_inspect_stats_flat_panels_off", Stages.TitanicPlains, StatsFlatPanelsOff);
            yield return new Scenario(1, "scoreboard_inspect_stats_board_item", Stages.TitanicPlains, StatsBoardItem);
            yield return new Scenario(1, "tier_colors_match_everywhere", Stages.TitanicPlains, TierColorsMatch);
            yield return new Scenario(1, "scoreboard_inspect_corruption_line", Stages.TitanicPlains, CorruptionLine);
        }

        // With the scoreboard open, the panel showing Alien Head: laid out and drawn as the canvas, in the item's tier color.
        private static IEnumerator ItemAsCanvas(Harness h)
        {
            WithoutStats(h);
            h.SetConfig(SevenUIPlugin.Settings.DebugLogging, true);
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return ScoreboardSetUp.Open(h);
            InspectInfo info = RoR2Content.Items.AlienHead;
            yield return Inspect(h, info);
            yield return h.Wait(SettleSeconds);
            yield return Expect(h, info.TitleColor, "Alien Head");
            yield return h.Screenshot("scoreboard_inspect");
        }

        // One item of each tier in the item bar's slot, its tooltip, the scoreboard's docked panel and the inspect panel,
        // all at once: every strip in the same color, SevenUI's shade of the tier's (GameColors.ToCanvas). Every tier
        // with a color has a shade, Hallowed Concepts' too, so none shows the game's raw color or the grey of no tier.
        private static IEnumerator TierColorsMatch(Harness h)
        {
            WithoutStats(h);
            ItemDef[] items = TierSamples(h);
            yield return ScoreboardSetUp.Open(h);
            yield return h.Wait(SettleSeconds);
            HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
            ScoreboardItemsBoard itemsBoard = SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardItemsBoard>();
            List<string> wrong = new List<string>();
            foreach (ItemDef item in items)
            {
                Color32 game = ColorCatalog.GetColor(ItemTierCatalog.GetItemTierDef(item.tier).colorIndex);
                Color expected = GameColors.ToCanvas(game);
                if (item.tier != ItemTier.Tier1 && SevenUIChecks.Near(expected, (Color)game))
                {
                    wrong.Add($"{item.name}'s tier {item.tier} has no SevenUI shade (#{ColorUtility.ToHtmlStringRGB(expected)})");
                }
                ItemIcon onBoard = SevenUIChecks.HudRoot(h).GetComponentsInChildren<ScoreboardRow>()
                    .Where(row => row.Strip && row.Strip.master == Harness.LocalBody.master)
                    .SelectMany(row => row.Strip.itemInventoryDisplay.GetComponentsInChildren<ItemIcon>())
                    .FirstOrDefault(icon => icon.itemIndex == item.itemIndex);
                ItemIcon inBar = hud.itemInventoryDisplay.GetComponentsInChildren<ItemIcon>()
                    .FirstOrDefault(icon => icon.itemIndex == item.itemIndex);
                if (!onBoard || !inBar) throw h.Fail($"no {item.name} in the scoreboard row or the item bar");

                UnityEngine.EventSystems.PointerEventData boardPointer = null;
                ScoreboardScenarios.PointAt(h, onBoard.rectTransform, item.name + " on the scoreboard", made => boardPointer = made);
                yield return h.Until(() => itemsBoard && itemsBoard.Pane.Shown && itemsBoard.Pane.Item == item.itemIndex,
                    $"the docked panel on {item.name}");
                UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(onBoard.gameObject, boardPointer,
                    UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
                yield return Inspect(h, item);
                UnityEngine.EventSystems.PointerEventData barPointer = null;
                MPEventSystem events = ScoreboardScenarios.PointAt(h, inBar.rectTransform, item.name + " in the item bar",
                    made => barPointer = made);
                yield return h.Until(() => events.currentTooltip && events.currentTooltip.transform.Find(TooltipStripPath),
                    $"the restyled tooltip of {item.name}");
                yield return h.Wait(SettleSeconds);

                Image tooltipStrip = events.currentTooltip.transform.Find(TooltipStripPath).GetComponent<Image>();
                Transform paneStrip = itemsBoard.Pane.Root.Find(PaneStripName);
                ItemSlot slot = inBar.GetComponentInChildren<ItemSlot>(true);
                (string place, Image image)[] strips =
                {
                    ("item slot", slot ? slot.TierStrip : null),
                    ("tooltip", tooltipStrip),
                    ("docked panel", paneStrip ? paneStrip.GetComponent<Image>() : null),
                    ("inspect panel", Strip(h)),
                };
                foreach ((string place, Image image) in strips)
                {
                    if (!image || !SevenUIChecks.Near(image.color, expected))
                    {
                        wrong.Add($"{item.name}'s {place} strip {(image ? "#" + ColorUtility.ToHtmlStringRGB(image.color) : "missing")}");
                    }
                }
                UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(inBar.gameObject, barPointer,
                    UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
                h.Log($"{item.name}: expected #{ColorUtility.ToHtmlStringRGB(expected)}");
            }
            if (wrong.Count > 0) throw h.Fail("tier colors differ from SevenUI's shades: " + string.Join("; ", wrong));
        }

        // A chest has no tier: its inspect info's own title color names it and colors both strips.
        // I2: with a Polylute held, the inspect panel on an Ukulele says "Corrupted by Polylute" under its description, in
        // the void color; on an Alien Head, which nothing held ties to, it says nothing.
        private static IEnumerator CorruptionLine(Harness h)
        {
            WithoutStats(h);
            Inventory inventory = Harness.LocalBody.inventory;
            ItemDef voidItem = DLC1Content.Items.ChainLightningVoid;
            if (!voidItem) throw h.Skip("Polylute needs Survivors of the Void");
            inventory.GiveItemPermanent(voidItem, 1);
            h.OnCleanup(() =>
            {
                if (inventory) inventory.RemoveItemPermanent(voidItem, 1);
            });
            yield return ScoreboardSetUp.Open(h);
            yield return Inspect(h, RoR2Content.Items.ChainLightning);
            string expected = "Corrupted by " + Language.GetString(voidItem.nameToken);
            TMP_Text line = null;
            yield return h.Until(() => (line = Line(h)) && line.gameObject.activeInHierarchy && line.text.Contains(expected) &&
                                       line.text.Contains(ColorUtility.ToHtmlStringRGB(Palette.CountVoid)),
                () => $"the inspect panel's corruption line \"{(line ? line.text : "none")}\", not \"{expected}\" in the void color");
            yield return h.Screenshot("corruption_line");
            yield return Inspect(h, RoR2Content.Items.AlienHead);
            yield return h.Until(() => !(line = Line(h)) || !line.gameObject.activeInHierarchy,
                () => $"a corruption line \"{line.text}\" on Alien Head");
        }

        private static TMP_Text Line(Harness h) => SevenUIChecks.HudRoot(h).GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(text => text.name == ScoreboardInspect.CorruptionLineName);

        // One visible item of each tier with a color: the main six by name, and the first of each other tier in the
        // catalog (food, hallowed, hex, pages), which are picked by tier since their names change between updates. Each
        // is given to the viewer and taken back at the end.
        internal static ItemDef[] TierSamples(Harness h)
        {
            List<ItemDef> items = new List<ItemDef>
            {
                RoR2Content.Items.Syringe, RoR2Content.Items.Feather, RoR2Content.Items.Behemoth,
                RoR2Content.Items.Knurl, RoR2Content.Items.LunarDagger, DLC1Content.Items.BleedOnHitVoid,
            };
            foreach (ItemTier tier in new[] { ItemTier.FoodTier, ItemTier.Cursed, ItemTier.Hex, ItemTier.Page })
            {
                ItemDef sample = ItemCatalog.allItemDefs
                    .Where(item => item && item.tier == tier && !item.hidden)
                    .OrderBy(item => item.itemIndex)
                    .FirstOrDefault();
                if (!sample) throw h.Fail($"no visible {tier} item in the catalog");
                items.Add(sample);
            }
            Inventory inventory = Harness.LocalBody.inventory;
            foreach (ItemDef item in items)
            {
                inventory.GiveItemPermanent(item.itemIndex);
                ItemDef given = item;
                h.OnCleanup(() =>
                {
                    if (inventory) inventory.RemoveItemPermanent(given.itemIndex);
                });
            }
            h.Log("tier samples: " + string.Join(", ", items.Select(item => $"{item.name} ({item.tier})")));
            return items.ToArray();
        }

        private static IEnumerator ChestAsCanvas(Harness h)
        {
            WithoutStats(h);
            h.SetConfig(SevenUIPlugin.Settings.DebugLogging, true);
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            PurchaseInteraction chest = null;
            yield return h.Spawned<PurchaseInteraction>($"{Commands.SpawnInteractable} {GameNames.SmallChest}", found => chest = found);
            IInspectInfoProvider provider = chest ? chest.GetComponent<IInspectInfoProvider>() : null;
            InspectInfo info = provider?.GetInfo();
            if (info == null) throw h.Skip("the small chest has no inspect info");
            yield return ScoreboardSetUp.Open(h);
            yield return Inspect(h, info);
            yield return h.Wait(SettleSeconds);
            yield return Expect(h, info.TitleColor, "the chest");
            yield return h.Screenshot("scoreboard_inspect_chest");
        }

        // The game's panel as it draws it with FlatPanels off, taken before the mod has touched it; on, it is styled; off
        // again, every value is the game's again and the strip is gone; and on once more it is styled again.
        private static IEnumerator FlatPanelsOffAndOn(Harness h)
        {
            WithoutStats(h);
            PluginConfig settings = SevenUIPlugin.Settings;
            h.SetConfig(settings.DebugLogging, true);
            h.SetConfig(settings.FlatPanels, false);
            yield return h.Wait(SevenUIChecks.LookSwitchSeconds);
            yield return ScoreboardSetUp.Open(h);
            InspectInfo info = RoR2Content.Items.AlienHead;
            yield return Inspect(h, info);
            yield return h.Wait(SettleSeconds);
            string vanilla = Look(h);
            if (Strip(h) || FootStrip(h)) throw h.Fail($"the mod's strips on the panel with FlatPanels off {h.Diagnosis()}");
            yield return h.Screenshot("scoreboard_inspect_off");

            h.SetConfig(settings.FlatPanels, true);
            yield return h.Wait(SevenUIChecks.LookSwitchSeconds);
            yield return Expect(h, info.TitleColor, "Alien Head with FlatPanels on");

            h.SetConfig(settings.FlatPanels, false);
            string restored = null;
            yield return h.Until(() => !Strip(h) && !FootStrip(h) && (restored = Look(h)) == vanilla,
                () => $"the game's panel back with FlatPanels off (strips {(Strip(h) || FootStrip(h) ? "still there" : "gone")}; " +
                      $"before: {vanilla}; now: {restored}) {h.Diagnosis()}");

            h.SetConfig(settings.FlatPanels, true);
            yield return h.Wait(SevenUIChecks.LookSwitchSeconds);
            yield return Expect(h, info.TitleColor, "Alien Head with FlatPanels on again");
        }

        // With LookingGlass's full descriptions on, Alien Head's stats as LookingGlass works them out for the viewer's
        // stack and one more (its GetItemDescription, as its Command tooltips have them) leave the description for a 200px
        // column after a 1px line, a row a stat, names left in the header color and values right in the accent color,
        // their right edges on the column's. Given one Alien Head, the rows follow the new stack.
        private static IEnumerator StatsColumn(Harness h)
        {
            ConfigEntry<bool> full = LookingGlassSettings(h);
            h.SetConfig(SevenUIPlugin.Settings.DebugLogging, true);
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            h.SetConfig(full, true);
            yield return ScoreboardSetUp.Open(h);
            ItemDef item = RoR2Content.Items.AlienHead;
            yield return Inspect(h, Discovered(item));
            string problem = null;
            yield return h.Until(() => (problem = StatsProblem(h, item)) == null,
                () => $"the stats column beside the description: {problem} {h.Diagnosis()}");
            yield return h.Screenshot("scoreboard_inspect_stats");

            Inventory inventory = Viewer().inventory;
            h.OnCleanup(() =>
            {
                if (inventory && inventory.GetItemCountPermanent(item) > 0) inventory.RemoveItemPermanent(item);
            });
            int before = inventory.GetItemCountPermanent(item);
            string firstValues = string.Join(", ", LookingGlassParts(h, item).Values);
            inventory.GiveItemPermanent(item);
            string values = string.Join(", ", LookingGlassParts(h, item).Values);
            if (values == firstValues) throw h.Fail($"LookingGlass's stats are the same for one more Alien Head ({values})");
            yield return h.Until(() => (problem = StatsProblem(h, item)) == null,
                () => $"the stats column for {before + 1} Alien Heads held: {problem} {h.Diagnosis()}");
        }

        // An item clicked on the board is looked at, not picked up: with two Alien Heads held, the column has the stats
        // for the two the row's icon shows, without one more and without LookingGlass's caption, as the icon's tooltip
        // has them. Shown from beside a pickup again (the panel closed and shown with its sidecar, as Tab does, then
        // the sidecar's button after another click on the board), the stats are for one more again.
        private static IEnumerator StatsBoardItem(Harness h)
        {
            ConfigEntry<bool> full = LookingGlassSettings(h);
            h.SetConfig(SevenUIPlugin.Settings.DebugLogging, true);
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            h.SetConfig(full, true);
            ItemDef item = RoR2Content.Items.AlienHead;
            Inventory inventory = Viewer().inventory;
            const int Given = 2;
            for (int i = 0; i < Given; i++)
            {
                inventory.GiveItemPermanent(item);
            }
            h.OnCleanup(() =>
            {
                for (int i = 0; i < Given && inventory && inventory.GetItemCountPermanent(item) > 0; i++) inventory.RemoveItemPermanent(item);
            });
            // A click opens the panel only with the game's inspect feature on, a profile setting.
            UserProfile profile = LocalUserManager.GetFirstLocalUser().userProfile;
            bool inspectFeature = profile.useInspectFeature;
            profile.useInspectFeature = true;
            h.OnCleanup(() => profile.useInspectFeature = inspectFeature);
            yield return ScoreboardSetUp.Open(h);
            InspectPanelController controller = null;
            yield return h.Until(() => (controller = Controller(h)) != null, "the scoreboard's inspect panel");
            NoAutopause(h, controller);
            ItemIcon icon = null;
            yield return h.Until(() => (icon = OwnIcon(h, item)) != null, "Alien Head's icon on your row");

            icon.ItemClicked();
            string problem = null;
            yield return h.Until(() => (problem = StatsProblem(h, item, false)) == null,
                () => $"the column for the {Given} Alien Heads on the board: {problem} {h.Diagnosis()}");
            if (LookingGlassParts(h, item, false).Caption.Length > 0) throw h.Fail("LookingGlass captioned the stack as held");
            yield return h.Screenshot("scoreboard_inspect_stats_board_item");

            // Closed a moment, as the scoreboard is between two presses of Tab.
            controller.OnCloseButtonClicked();
            yield return h.Wait(SettleSeconds);
            controller.Show(Discovered(item), true);
            yield return h.Until(() => (problem = StatsProblem(h, item)) == null,
                () => $"the column for one more, shown from beside a pickup: {problem} {h.Diagnosis()}");
            yield return h.Screenshot("scoreboard_inspect_stats_beside_pickup");

            icon.ItemClicked();
            yield return h.Until(() => (problem = StatsProblem(h, item, false)) == null,
                () => $"the column for the stack on the board after another click: {problem} {h.Diagnosis()}");
            // The sidecar's button shows the pickup again, when the game keeps one for this panel.
            FieldInfo sidecarField = typeof(InspectPanelController).GetField("Sidecar", Fields);
            FieldInfo storedField = typeof(InspectPanelController).GetField("SidecarStoredInfo", Fields);
            GameObject sidecar = sidecarField?.GetValue(controller) as GameObject;
            HGButton sidecarButton = sidecar ? sidecar.GetComponentInChildren<HGButton>(true) : null;
            if (!sidecarButton || storedField?.GetValue(controller) == null)
            {
                h.Log("the scoreboard's inspect panel keeps no sidecar, so its button isn't checked");
                yield break;
            }
            sidecarButton.onClick.Invoke();
            yield return h.Until(() => (problem = StatsProblem(h, item)) == null,
                () => $"the column for one more, from the sidecar's button: {problem} {h.Diagnosis()}");
        }

        // Your row's icon for the item on the board.
        private static ItemIcon OwnIcon(Harness h, ItemDef item)
        {
            Transform board = Board(h);
            CharacterMaster master = Viewer();
            ScoreboardStrip row = board ? board.GetComponentsInChildren<ScoreboardStrip>().FirstOrDefault(strip => strip.master == master) : null;
            return row && row.itemInventoryDisplay
                ? row.itemInventoryDisplay.itemIcons.FirstOrDefault(icon => icon && icon.itemIndex == item.itemIndex)
                : null;
        }

        // A chest, an equipment, or Alien Head with LookingGlass's setting off leave the panel as it was: no line, no
        // column, the icon's gap, the game's text. With the setting on again, Alien Head's column shows.
        private static IEnumerator NoStatsNoColumn(Harness h)
        {
            ConfigEntry<bool> full = LookingGlassSettings(h);
            h.SetConfig(SevenUIPlugin.Settings.DebugLogging, true);
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            h.SetConfig(full, true);
            PurchaseInteraction chest = null;
            yield return h.Spawned<PurchaseInteraction>($"{Commands.SpawnInteractable} {GameNames.SmallChest}", found => chest = found);
            IInspectInfoProvider provider = chest ? chest.GetComponent<IInspectInfoProvider>() : null;
            InspectInfo info = provider?.GetInfo();
            if (info == null) throw h.Skip("the small chest has no inspect info");
            yield return ScoreboardSetUp.Open(h);
            yield return Inspect(h, info);
            yield return NoColumn(h, "the chest");

            EquipmentDef equipment = RoR2Content.Equipment.Fruit;
            yield return Inspect(h, Discovered(equipment));
            yield return NoColumn(h, "an equipment");

            h.SetConfig(full, false);
            ItemDef item = RoR2Content.Items.AlienHead;
            yield return Inspect(h, Discovered(item));
            yield return NoColumn(h, "Alien Head with Full Description in Hud off");
            TMP_Text description = Description(Controller(h));
            string game = Language.GetString(item.descriptionToken);
            if (description.text != game) throw h.Fail($"the description \"{description.text}\" not the game's \"{game}\" with the setting off");
            yield return h.Screenshot("scoreboard_inspect_stats_none");

            h.SetConfig(full, true);
            string problem = null;
            yield return h.Until(() => (problem = StatsProblem(h, item)) == null,
                () => $"the stats column with Full Description in Hud on again: {problem} {h.Diagnosis()}");
        }

        // The panel as the game draws it for Alien Head, taken before the mod touched it; styled, the column is there;
        // with FlatPanels off, the text, the gap and the objects are the game's again.
        private static IEnumerator StatsFlatPanelsOff(Harness h)
        {
            PluginConfig settings = SevenUIPlugin.Settings;
            ConfigEntry<bool> full = LookingGlassSettings(h);
            h.SetConfig(settings.DebugLogging, true);
            h.SetConfig(settings.FlatPanels, false);
            h.SetConfig(full, true);
            yield return h.Wait(SevenUIChecks.LookSwitchSeconds);
            yield return ScoreboardSetUp.Open(h);
            ItemDef item = RoR2Content.Items.AlienHead;
            yield return Inspect(h, Discovered(item));
            yield return h.Wait(SettleSeconds);
            string vanilla = Look(h);

            h.SetConfig(settings.FlatPanels, true);
            string problem = null;
            yield return h.Until(() => (problem = StatsProblem(h, item)) == null,
                () => $"the stats column with FlatPanels on: {problem} {h.Diagnosis()}");

            h.SetConfig(settings.FlatPanels, false);
            string restored = null;
            yield return h.Until(() => Column(h, false) == null && (restored = Look(h)) == vanilla,
                () => $"the game's panel back with FlatPanels off (column: {Column(h, false) ?? "gone"}; " +
                      $"before: {vanilla}; now: {restored}) {h.Diagnosis()}");
        }

        // E1's layout without the stats column: LookingGlass's full descriptions off, when it is installed.
        private static void WithoutStats(Harness h)
        {
            if (Chainloader.PluginInfos.TryGetValue(GameNames.LookingGlassGUID, out BepInEx.PluginInfo info) && info.Instance &&
                info.Instance.Config.TryGetEntry(GameNames.LookingGlassSection, GameNames.LookingGlassFullDescription,
                    out ConfigEntry<bool> full))
            {
                h.SetConfig(full, false);
            }
        }

        // LookingGlass's Full Description in Hud setting, which the mod reads through BepInEx's config, with its stats
        // calculations turned on for the scenario; without LookingGlass there is none.
        private static ConfigEntry<bool> LookingGlassSettings(Harness h)
        {
            ConfigEntry<bool> full = null;
            ConfigEntry<bool> calculations = null;
            if (Chainloader.PluginInfos.TryGetValue(GameNames.LookingGlassGUID, out BepInEx.PluginInfo info) && info.Instance)
            {
                info.Instance.Config.TryGetEntry(GameNames.LookingGlassSection, GameNames.LookingGlassFullDescription, out full);
                info.Instance.Config.TryGetEntry(GameNames.LookingGlassSection, GameNames.LookingGlassStatsCalculations, out calculations);
            }
            if (full == null) throw h.Skip("LookingGlass isn't installed, or has no Full Description in Hud setting");
            if (calculations != null)
            {
                h.SetConfig(calculations, true);
            }
            return full;
        }

        // The panel shows the unknown item for a pickup the profile hasn't discovered; the scenarios want the real one.
        private static InspectInfo Discovered(InspectInfo info)
        {
            info.MarkForceShowInfo();
            return info;
        }

        // The HUD's target's master, whose stack the mod counts, as SevenUI's ScoreboardInspect does.
        private static CharacterMaster Viewer()
        {
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            HUD hud = user?.cameraRigController ? user.cameraRigController.hud : null;
            return hud && hud.targetMaster ? hud.targetMaster : user?.cachedMaster;
        }

        // LookingGlass's own text for the item, with stats for the viewer's stack and one more, split as the mod splits
        // it. Called by reflection, as the mod does, so the tests build without LookingGlass.
        private static ItemStatsText.Parts LookingGlassParts(Harness h, ItemDef item, bool oneMore = true)
        {
            CharacterMaster master = Viewer();
            if (!master || !master.inventory) throw h.Fail("no viewer's master to count Alien Heads for");
            int held = master.inventory.GetItemCountPermanent(item);
            string text = LookingGlassText(h, item, held, master, oneMore);
            ItemStatsText.Parts parts = ItemStatsText.Split(text);
            if (!parts.HasStats) throw h.Fail($"LookingGlass gave no stats for {item.name}: \"{text}\"");
            return parts;
        }

        // LookingGlass's own description of the item with its stats for a stack of 'count', or one more with its caption.
        // Called by reflection, as the mod does, so the tests build without LookingGlass.
        internal static string LookingGlassText(Harness h, ItemDef item, int count, CharacterMaster master, bool oneMore)
        {
            Chainloader.PluginInfos.TryGetValue(GameNames.LookingGlassGUID, out BepInEx.PluginInfo info);
            System.Type type = info?.Instance ? info.Instance.GetType().Assembly.GetType(GameNames.LookingGlassItemStats) : null;
            MethodInfo method = type?.GetMethod(GameNames.LookingGlassItemDescription, BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(ItemDef), typeof(int), typeof(CharacterMaster), typeof(bool), typeof(bool) }, null);
            if (method == null) throw h.Fail($"LookingGlass has no {GameNames.LookingGlassItemStats}.{GameNames.LookingGlassItemDescription}");
            return method.Invoke(null, new object[] { item, count, master, oneMore, false }) as string;
        }

        // Waits a moment, then fails if the line or column shows.
        private static IEnumerator NoColumn(Harness h, string what)
        {
            yield return h.Wait(SettleSeconds);
            string problem = Column(h, false);
            if (problem != null) throw h.Fail($"a stats column on {what}: {problem} {h.Diagnosis()}");
        }

        // Null when the column is as it should be (shown, or not when expected not to be), else what is wrong.
        private static string Column(Harness h, bool shown)
        {
            Transform box = Board(h) ? Board(h).Find(BoxPath) : null;
            if (!box) return "no box";
            Transform divider = box.Find(ScoreboardInspect.DividerName);
            Transform stats = box.Find(ScoreboardInspect.StatsName);
            bool active = (divider && divider.gameObject.activeSelf) || (stats && stats.gameObject.activeSelf);
            return active == shown ? null : shown ? "no column" : "a column or line is showing";
        }

        // Null when the column, the line and the description are as design B draws them, with LookingGlass's stats for
        // the item and the description without them, else what differs.
        private static string StatsProblem(Harness h, ItemDef item, bool oneMore = true)
        {
            ItemStatsText.Parts expected = LookingGlassParts(h, item, oneMore);
            string problem = Column(h, true);
            if (problem != null) return problem;
            Transform box = Board(h).Find(BoxPath);
            Transform divider = box.Find(ScoreboardInspect.DividerName);
            Transform stats = box.Find(ScoreboardInspect.StatsName);
            Transform texts = box.Find("InspectDescriptionContainer");
            if (!divider.gameObject.activeSelf || !stats.gameObject.activeSelf) return "the line or the column is hidden";
            Rect boxRect = CanvasSizeChecks.Box(h, (RectTransform)box);
            Rect textRect = CanvasSizeChecks.Box(h, (RectTransform)texts);
            Rect lineRect = CanvasSizeChecks.Box(h, (RectTransform)divider);
            Rect column = CanvasSizeChecks.Box(h, (RectTransform)stats);
            if (Mathf.Abs(lineRect.xMin - textRect.xMax - StatsSpacing) > Tolerance)
            {
                return $"line {lineRect.xMin - textRect.xMax:0.#}px after the text, not {StatsSpacing}";
            }
            if (Mathf.Abs(lineRect.width - DividerWidth) > Tolerance) return $"line {lineRect.width:0.#}px wide, not {DividerWidth}";
            Image line = divider.GetComponent<Image>();
            if (!line || !SevenUIChecks.Near(line.color, Palette.Divider)) return "the line is not the divider color";
            // The panel is its tallest content plus the padding, and the line is as tall as that content.
            Rect slot = CanvasSizeChecks.Box(h, (RectTransform)box.Find("InspectIconContainer"));
            float content = Mathf.Max(slot.height, Mathf.Max(textRect.height, column.height));
            if (Mathf.Abs(boxRect.height - (content + 2f * PaddingY)) > Tolerance)
            {
                return $"panel {boxRect.height:0.#}px tall, not its content's {content:0.#} plus the padding {2f * PaddingY:0.#}";
            }
            if (Mathf.Abs(lineRect.height - content) > Tolerance) return $"line {lineRect.height:0.#}px tall, not the content's {content:0.#}";
            if (Mathf.Abs(column.xMin - lineRect.xMax - StatsSpacing) > Tolerance) return $"column {column.xMin - lineRect.xMax:0.#}px after the line, not {StatsSpacing}";
            if (Mathf.Abs(column.width - StatsWidth) > Tolerance) return $"column {column.width:0.#}px wide, not {StatsWidth}";
            List<TMP_Text> labels = Rows(stats, ScoreboardInspect.LabelName);
            List<TMP_Text> values = Rows(stats, ScoreboardInspect.ValueName);
            if (labels.Count != expected.Labels.Count || values.Count != expected.Labels.Count)
            {
                return $"{labels.Count} names and {values.Count} values for {expected.Labels.Count} stats";
            }
            float unit = HudUnits.PerCanvasPixel;
            float top = column.yMax - StatsTop;
            for (int i = 0; i < labels.Count; i++)
            {
                if (labels[i].text != expected.Labels[i] || values[i].text != expected.Values[i])
                {
                    return $"row {i} \"{labels[i].text}\" \"{values[i].text}\", not LookingGlass's \"{expected.Labels[i]}\" \"{expected.Values[i]}\"";
                }
                if (!SevenUIChecks.Near(labels[i].color, Palette.HeaderText) || (labels[i].fontStyle & FontStyles.Bold) != 0) return $"row {i} name {labels[i].color} {labels[i].fontStyle}";
                if (!SevenUIChecks.Near(values[i].color, Palette.Accent) || (values[i].fontStyle & FontStyles.Bold) == 0) return $"row {i} value {values[i].color} {values[i].fontStyle}";
                if (!SevenUIChecks.IsBarlow(labels[i]) || !SevenUIChecks.IsBarlow(values[i])) return $"row {i} not in Barlow";
                if (Mathf.Abs(labels[i].fontSize - StatsSize * unit) > 0.01f || Mathf.Abs(values[i].fontSize - StatsSize * unit) > 0.01f) return $"row {i} not {StatsSize} in size";
                Rect name = CanvasSizeChecks.Box(h, labels[i].rectTransform);
                Rect value = CanvasSizeChecks.Box(h, values[i].rectTransform);
                if (Mathf.Abs(value.xMax - column.xMax) > Tolerance) return $"row {i} value ends {column.xMax - value.xMax:0.#}px short of the column's edge";
                if (Mathf.Abs(name.xMin - column.xMin) > Tolerance) return $"row {i} name not at the column's left";
                if (value.xMin - name.xMax < StatsLabelGap - Tolerance) return $"row {i} {value.xMin - name.xMax:0.#}px between name and value, under {StatsLabelGap}";
                if (Mathf.Abs(top - Mathf.Max(name.yMax, value.yMax)) > Tolerance) return $"row {i} starts {top - name.yMax:0.#}px off its place";
                top -= Mathf.Max(name.height, value.height) + StatsRowGap;
            }
            TMP_Text description = Description(Controller(h));
            if (description.text != expected.Description)
            {
                return $"description \"{description.text}\" not LookingGlass's without the stats \"{expected.Description}\"";
            }
            return null;
        }

        private static List<TMP_Text> Rows(Transform column, string name) =>
            column.Cast<Transform>().Where(row => row.name == name && row.gameObject.activeSelf)
                .Select(row => row.GetComponent<TMP_Text>()).ToList();

        // Shows what the interact prompt's Inspect would, through the game's controller. Autopause, which the panel asks
        // for while it is up in a single-player run, would stop the game, so it is off for the scenario.
        internal static IEnumerator Inspect(Harness h, InspectInfo info)
        {
            InspectPanelController controller = null;
            yield return h.Until(() => (controller = Controller(h)) != null, "the scoreboard's inspect panel");
            NoAutopause(h, controller);
            controller.Show(info);
            yield return null;
        }

        private static void NoAutopause(Harness h, InspectPanelController controller)
        {
            FieldInfo autopause = typeof(InspectPanelController).GetField("isAutopauseCandidate", Fields);
            if (autopause != null)
            {
                bool before = (bool)autopause.GetValue(controller);
                autopause.SetValue(controller, false);
                h.OnCleanup(() =>
                {
                    if (controller) autopause.SetValue(controller, before);
                });
            }
            h.OnCleanup(() =>
            {
                if (controller) controller.OnCloseButtonClicked();
            });
        }

        private static InspectPanelController Controller(Harness h)
        {
            ScoreboardController board = SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardController>(true);
            return board ? board.GetComponentInChildren<InspectPanelController>(true) : null;
        }

        private static Transform Board(Harness h)
        {
            ScoreboardController board = SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardController>(true);
            return board ? board.transform : null;
        }

        private static Image Strip(Harness h)
        {
            Transform inspection = Board(h) ? Board(h).Find(InspectionPath) : null;
            Transform strip = inspection ? inspection.Find(ScoreboardInspect.StripName) : null;
            return strip ? strip.GetComponent<Image>() : null;
        }

        private static Image FootStrip(Harness h)
        {
            Transform slot = Board(h) ? Board(h).Find(IconPath) : null;
            Transform strip = slot ? slot.Find(ScoreboardInspect.FootStripName) : null;
            return strip ? strip.GetComponent<Image>() : null;
        }

        // The game's parts of the panel, found by what the controller points at.
        private static TMP_Text Title(InspectPanelController controller) =>
            typeof(InspectPanelController).GetField("InspectTitleText", Fields).GetValue(controller) as TMP_Text;

        private static TMP_Text Description(InspectPanelController controller)
        {
            Component text = typeof(InspectPanelController).GetField("InspectDescription", Fields).GetValue(controller) as Component;
            return text ? text.GetComponent<TMP_Text>() : null;
        }

        private static GameObject Flavor(InspectPanelController controller)
        {
            Component text = typeof(InspectPanelController).GetField("InspectFlavor", Fields).GetValue(controller) as Component;
            return text ? text.gameObject : null;
        }

        // Every value the restyle touches, as text, to compare the game's look before and after.
        private static string Look(Harness h)
        {
            Transform board = Board(h);
            InspectPanelController controller = Controller(h);
            if (!board || !controller) return "no panel";
            TMP_Text title = Title(controller);
            TMP_Text description = Description(controller);
            GameObject flavor = Flavor(controller);
            Image face = board.Find(InspectionPath).GetComponent<Image>();
            Image slot = board.Find(IconPath).GetComponent<Image>();
            VerticalLayoutGroup container = board.Find("Container").GetComponent<VerticalLayoutGroup>();
            List<string> parts = new List<string>
            {
                $"panel {Format(CanvasSizeChecks.Box(h, (RectTransform)board.Find(InspectionPath)))}",
                $"icon {Format(CanvasSizeChecks.Box(h, (RectTransform)slot.transform))}",
                $"face {face.color} {(face.sprite ? face.sprite.name : "no sprite")}",
                $"slot {slot.color} {(slot.sprite ? slot.sprite.name : "no sprite")}",
                $"title {title.fontSize:0.##} {title.fontStyle} {title.color} {title.alignment}",
                $"description {description.fontSize:0.##} {description.fontStyle} {description.color} {description.lineSpacing:0.##} " +
                $"{description.richText} {description.alignment}",
                $"flavor {(flavor ? flavor.activeSelf.ToString() : "none")}",
                $"text {description.text}",
                $"box {board.Find(BoxPath).GetComponent<HorizontalOrVerticalLayoutGroup>().spacing:0.##} {board.Find(BoxPath).childCount}",
                $"container {container.spacing:0.##} {container.padding.left} {container.padding.top} {container.childControlWidth}",
            };
            foreach (string path in LayoutPaths)
            {
                Transform part = board.Find(path);
                HorizontalOrVerticalLayoutGroup group = part ? part.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;
                LayoutElement element = part ? part.GetComponent<LayoutElement>() : null;
                parts.Add($"{path} {(group ? $"{group.padding.left},{group.padding.top},{group.padding.right},{group.padding.bottom} " +
                    $"{group.spacing:0.##} {group.childAlignment} {group.childControlWidth}{group.childControlHeight} " +
                    $"{group.childForceExpandWidth}{group.childForceExpandHeight}" : "no group")} " +
                    $"{(element ? $"{element.minWidth}/{element.preferredWidth}/{element.flexibleWidth} {element.minHeight}/{element.preferredHeight}/{element.flexibleHeight}" : "no element")}");
            }
            return string.Join("; ", parts);
        }

        private static string Format(Rect rect) => $"{rect.x:0.#},{rect.y:0.#} {rect.width:0.#}x{rect.height:0.#}";

        // Waits for the panel to be laid out as the canvas draws it, with a title in the given color.
        private static IEnumerator Expect(Harness h, Color titleColor, string what)
        {
            string problem = null;
            yield return h.Until(() => (problem = Problem(h, titleColor)) == null,
                () => $"the inspect panel on {what} as the canvas: {problem} {h.Diagnosis()}");
        }

        // Null when the panel is as the canvas draws it, else what differs.
        private static string Problem(Harness h, Color titleColor)
        {
            Transform board = Board(h);
            InspectPanelController controller = Controller(h);
            if (!board || !controller) return "no scoreboard or inspect panel";
            Transform inspection = board.Find(InspectionPath);
            Transform boardFace = board.Find(BoardPath);
            Transform slotObject = board.Find(IconPath);
            Transform texts = board.Find(TextsPath);
            Image strip = Strip(h);
            TMP_Text title = Title(controller);
            TMP_Text description = Description(controller);
            if (!inspection || !boardFace || !slotObject || !texts || !title || !description) return "a part of the panel is missing";
            if (!inspection.gameObject.activeInHierarchy) return "the panel is not showing";
            Rect panel = CanvasSizeChecks.Box(h, (RectTransform)inspection);
            Rect boardBox = CanvasSizeChecks.Box(h, (RectTransform)boardFace);
            float width = Mathf.Round(boardBox.width * ScoreboardInspect.WidthShare);
            if (Mathf.Abs(panel.center.x - boardBox.center.x) > Tolerance || Mathf.Abs(panel.width - width) > Tolerance)
            {
                return $"panel {Format(panel)} not {ScoreboardInspect.WidthShare:P0} of the board's width, centered under it {Format(boardBox)}";
            }
            // Screen y grows upward: the face's top is the board's bottom less the gap, which the panel's own top padding
            // holds (the container's spacing is the game's and not counted on).
            if (Mathf.Abs(boardBox.yMin - panel.yMax - BoardGap) > Tolerance)
            {
                return $"{boardBox.yMin - panel.yMax:0.#}px between the board's bottom and the panel's face, not {BoardGap}";
            }
            Image face = inspection.GetComponent<Image>();
            if (!face || face.sprite || !SevenUIChecks.Near(face.color, Palette.ScoreboardPanel))
            {
                return $"face {(face ? face.color.ToString() : "missing")} not the board's";
            }
            if (!strip) return "no strip along the top";
            Rect stripBox = CanvasSizeChecks.Box(h, strip.rectTransform);
            if (Mathf.Abs(stripBox.height - StripHeight) > Tolerance || Mathf.Abs(stripBox.width - panel.width) > Tolerance ||
                Mathf.Abs(stripBox.yMax - panel.yMax) > Tolerance)
            {
                return $"strip {Format(stripBox)} not {StripHeight}px along the panel's top {Format(panel)}";
            }
            // The game's title color in SevenUI's shade, as everywhere a tier color is drawn.
            Color expected = GameColors.ToCanvas(titleColor);
            Color expectedTitle = new Color(expected.r, expected.g, expected.b, titleColor.a);
            if (!SevenUIChecks.Near(strip.color, expected)) return $"strip {strip.color} not the shade of the game's title color {expected}";
            if (!SevenUIChecks.Near(title.color, expectedTitle)) return $"name {title.color} not the shade of the game's {titleColor}: {expectedTitle}";
            Image slot = slotObject.GetComponent<Image>();
            Rect slotBox = CanvasSizeChecks.Box(h, (RectTransform)slotObject);
            if (!slot || !SevenUIChecks.Near(slot.color, Palette.ItemSlot)) return "the icon's slot is not the item slot color";
            if (Mathf.Abs(slotBox.width - IconSize) > Tolerance || Mathf.Abs(slotBox.height - IconSize) > Tolerance)
            {
                return $"icon slot {Format(slotBox)} not {IconSize}x{IconSize}";
            }
            if (Mathf.Abs(slotBox.xMin - panel.xMin - PaddingX) > Tolerance || Mathf.Abs(panel.yMax - slotBox.yMax - PaddingY) > Tolerance)
            {
                return $"icon slot {Format(slotBox)} not {PaddingX}px in and {PaddingY}px down in the panel {Format(panel)}";
            }
            string outside = IconOutside(h, controller.InspectVisual, slotBox);
            if (outside != null) return outside;
            Image foot = FootStrip(h);
            if (!foot) return "no strip along the icon slot's foot";
            Rect footBox = CanvasSizeChecks.Box(h, foot.rectTransform);
            if (Mathf.Abs(footBox.height - FootStripHeight) > Tolerance || Mathf.Abs(footBox.width - slotBox.width) > Tolerance ||
                Mathf.Abs(footBox.xMin - slotBox.xMin) > Tolerance || Mathf.Abs(footBox.yMin - slotBox.yMin) > Tolerance)
            {
                return $"foot strip {Format(footBox)} not {FootStripHeight}px along the foot of the icon slot {Format(slotBox)}";
            }
            if (!SevenUIChecks.Near(foot.color, expected)) return $"foot strip {foot.color} not the shade of the title's {expected}";
            Rect textBox = CanvasSizeChecks.Box(h, (RectTransform)texts);
            if (Mathf.Abs(textBox.xMin - slotBox.xMax - IconGap) > Tolerance)
            {
                return $"texts start {textBox.xMin - slotBox.xMax:0.#}px from the icon, not {IconGap}";
            }
            // The texts wrap in what the icon and the padding leave, one under the other, and the panel holds them.
            Rect titleBox = CanvasSizeChecks.Box(h, title.rectTransform);
            Rect descriptionBox = CanvasSizeChecks.Box(h, description.rectTransform);
            if (titleBox.xMin < slotBox.xMax + IconGap - Tolerance || descriptionBox.xMin < slotBox.xMax + IconGap - Tolerance ||
                titleBox.xMax > panel.xMax - PaddingX + Tolerance || descriptionBox.xMax > panel.xMax - PaddingX + Tolerance)
            {
                return $"texts {Format(titleBox)} and {Format(descriptionBox)} not beside the icon {Format(slotBox)} " +
                       $"and {PaddingX}px inside the panel {Format(panel)}";
            }
            if (Mathf.Min(titleBox.yMax, descriptionBox.yMax) - Mathf.Max(titleBox.yMin, descriptionBox.yMin) > Tolerance)
            {
                return $"name {Format(titleBox)} and description {Format(descriptionBox)} overlap";
            }
            float content = Mathf.Min(Mathf.Min(titleBox.yMin, descriptionBox.yMin), slotBox.yMin);
            if (content - panel.yMin < PaddingY - Tolerance)
            {
                return $"the panel {Format(panel)} does not hold its content {PaddingY}px in (lowest at {content:0.#})";
            }
            if (Mathf.Abs(title.fontSize - NameSize * HudUnits.PerCanvasPixel) > 0.01f || (title.fontStyle & FontStyles.Bold) == 0 ||
                !SevenUIChecks.IsBarlow(title))
            {
                return $"name {title.fontSize:0.##} {title.fontStyle} in {title.font.name}, not Barlow bold {NameSize}";
            }
            if (Mathf.Abs(description.fontSize - DescriptionSize * HudUnits.PerCanvasPixel) > 0.01f || !description.richText ||
                !SevenUIChecks.IsBarlow(description))
            {
                return $"description {description.fontSize:0.##} rich {description.richText} in {description.font.name}, not Barlow {DescriptionSize}";
            }
            if (!SevenUIChecks.Near(description.color, Palette.TooltipBody)) return $"description {description.color} not the body color";
            // TextMeshPro adds the line spacing, in percent of the font size, to the font's own line height.
            float lines = description.font.faceInfo.lineHeight / description.font.faceInfo.pointSize + description.lineSpacing / 100f;
            if (Mathf.Abs(lines - LineHeight) > LineTolerance) return $"description line height {lines:0.##}, not {LineHeight}";
            GameObject flavor = Flavor(controller);
            if (flavor && flavor.activeInHierarchy) return "the lore still shows";
            if (description.text.Length == 0 || title.text.Length == 0) return "the name or description is empty";
            return Visible(inspection, title, description, slotObject);
        }

        // Null when the icon and every image drawn under it lie inside the slot, within its inset (it was seen about twice the
        // slot's size, spilling over the name and description).
        private static string IconOutside(Harness h, Image visual, Rect slotBox)
        {
            if (!visual) return "the panel has no icon image";
            foreach (Image image in visual.GetComponentsInChildren<Image>(false))
            {
                if (!image.enabled || image.color.a <= 0f || (image != visual && !image.sprite)) continue;
                Rect box = CanvasSizeChecks.Box(h, image.rectTransform);
                if (box.xMin < slotBox.xMin - Tolerance || box.yMin < slotBox.yMin - Tolerance ||
                    box.xMax > slotBox.xMax + Tolerance || box.yMax > slotBox.yMax + Tolerance)
                {
                    return $"icon '{image.name}' {Format(box)} not inside its slot {Format(slotBox)}: {Describe(image)}";
                }
            }
            return null;
        }

        private static string Describe(Image image)
        {
            RectTransform rect = image.rectTransform;
            RectTransform parent = rect.parent as RectTransform;
            string types = string.Join(",", image.GetComponents<Component>().Select(c => c ? c.GetType().Name : "missing"));
            return $"localScale {rect.localScale}, anchors {rect.anchorMin}-{rect.anchorMax}, sizeDelta {rect.sizeDelta}, " +
                   $"anchoredPosition {rect.anchoredPosition}, parent '{(parent ? parent.name : "none")}' " +
                   $"{(parent ? parent.rect.ToString() : "")}, components [{types}]";
        }

        // Null when the panel is drawn at full strength: the groups up its hierarchy and each text's own color leave it
        // opaque (it was seen at a tenth of that, as if under a faded group), and nothing but the mod's strips is drawn
        // after the texts over their rects.
        private static string Visible(Transform inspection, TMP_Text title, TMP_Text description, Transform slot)
        {
            float panelAlpha = ScoreboardInspect.EffectiveAlpha(inspection);
            if (panelAlpha < OpaqueAlpha) return $"the panel is under CanvasGroups at alpha {panelAlpha:0.##}, not 1";
            foreach (TMP_Text text in new[] { title, description })
            {
                float alpha = ScoreboardInspect.EffectiveAlpha(text.transform) * text.color.a;
                if (alpha < OpaqueAlpha) return $"'{text.name}' draws at alpha {alpha:0.##} (color {text.color}), not 1";
                List<Graphic> over = ScoreboardInspect.DrawnOver(text);
                if (over.Count > 0) return $"'{text.name}' is drawn over by '{over[0].name}' ({over[0].GetType().Name} {over[0].color})";
            }
            Image visual = slot.GetComponentInChildren<Image>(true);
            if (visual && ScoreboardInspect.EffectiveAlpha(visual.transform) < OpaqueAlpha)
            {
                return $"the icon is under CanvasGroups at alpha {ScoreboardInspect.EffectiveAlpha(visual.transform):0.##}, not 1";
            }
            return null;
        }
    }
}
