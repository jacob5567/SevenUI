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
    // as the canvas's design A: the board's width, 6px under it, the board's flat face, a 2px strip along its top in the
    // title's color, the 56px icon on a dark slot, the name in bold 16 over the description in 13, in the body color.
    // The panel is shown through the game's own controller (Show), which is what the interact prompt's Inspect does,
    // with an item (Alien Head) and with a small chest's own inspect info. FlatPanels off puts the game's panel back as
    // it was (compared with a look taken before the mod touched it), and on again styles it again.
    internal static class ScoreboardInspectScenarios
    {
        private const string BoardPath = "Container/StripContainer";
        private const string InspectionPath = "Container/InspectPanel/InspectPanelArea/InspectionPanel";
        private const string IconPath = InspectionPath + "/HorizontalBox/InspectIconContainer/InspectVisualBackground";
        private const string BoxPath = InspectionPath + "/HorizontalBox";
        private const string TextsPath = InspectionPath + "/HorizontalBox/InspectDescriptionContainer";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        // In the canvas's pixels, which Box measures in.
        private const float BoardGap = 6f;
        private const float StripHeight = 2f;
        private const float IconSize = 56f;
        private const float PaddingX = 14f;
        private const float PaddingY = 12f;
        private const float IconGap = 12f;
        private const float NameSize = 16f;
        private const float DescriptionSize = 13f;
        private const float LineHeight = 1.4f;
        private const float Tolerance = 1.5f;
        private const float LineTolerance = 0.05f;
        private const float SettleSeconds = 0.5f;
        // The stats column: its width, the line before it, the gaps, in the canvas's pixels.
        private const float StatsWidth = 200f;
        private const float DividerWidth = 1f;
        private const float StatsSpacing = 14f;
        private const float StatsTop = 2f;
        private const float StatsRowGap = 6f;
        private const float StatsLabelGap = 16f;
        private const float StatsSize = 13f;
        private const string LookingGlassSection = "Misc";
        private const string LookingGlassKey = "Full Description in Hud";
        private const string Described = "Gain a little damage for every skill.";
        // What LookingGlass writes for an item with stats: the description, then a line a stat, in a size tag.
        private const string LookingGlassText = "<size=80%>" + Described +
            "\n<color=\"white\">Damage: </color><style=\"cIsDamage\">45%</style>" +
            "\n<color=\"white\">Cooldown reduction: </color><style=\"cIsUtility\">25%</style></size>";
        private static readonly string[] StatLabels = { "Damage: ", "Cooldown reduction: " };
        private static readonly string[] StatValues = { "45%", "25%" };

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "scoreboard_inspect_item_as_canvas", Stages.TitanicPlains, ItemAsCanvas);
            yield return new Scenario(1, "scoreboard_inspect_chest_as_canvas", Stages.TitanicPlains, ChestAsCanvas);
            yield return new Scenario(1, "scoreboard_inspect_flat_panels_off_and_on", Stages.TitanicPlains, FlatPanelsOffAndOn);
            yield return new Scenario(1, "scoreboard_inspect_stats_column", Stages.TitanicPlains, StatsColumn);
            yield return new Scenario(1, "scoreboard_inspect_stats_none_without_stats", Stages.TitanicPlains, NoStatsNoColumn);
            yield return new Scenario(1, "scoreboard_inspect_stats_flat_panels_off", Stages.TitanicPlains, StatsFlatPanelsOff);
        }

        // With the scoreboard open, the panel showing Alien Head: laid out and drawn as the canvas, in the item's tier color.
        private static IEnumerator ItemAsCanvas(Harness h)
        {
            h.SetConfig(SevenUIPlugin.Settings.DebugLogging, true);
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return ScoreboardSetUp.Open(h);
            InspectInfo info = RoR2Content.Items.AlienHead;
            yield return Inspect(h, info);
            yield return h.Wait(SettleSeconds);
            yield return Expect(h, info.TitleColor, "Alien Head");
            yield return h.Screenshot("scoreboard_inspect");
        }

        // A chest has no tier: its inspect info's own title color names it and colors the strip, the slot has no strip.
        private static IEnumerator ChestAsCanvas(Harness h)
        {
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
            PluginConfig settings = SevenUIPlugin.Settings;
            h.SetConfig(settings.DebugLogging, true);
            h.SetConfig(settings.FlatPanels, false);
            yield return h.Wait(SevenUIChecks.LookSwitchSeconds);
            yield return ScoreboardSetUp.Open(h);
            InspectInfo info = RoR2Content.Items.AlienHead;
            yield return Inspect(h, info);
            yield return h.Wait(SettleSeconds);
            string vanilla = Look(h);
            if (Strip(h)) throw h.Fail($"the mod's strip on the panel with FlatPanels off {h.Diagnosis()}");
            yield return h.Screenshot("scoreboard_inspect_off");

            h.SetConfig(settings.FlatPanels, true);
            yield return h.Wait(SevenUIChecks.LookSwitchSeconds);
            yield return Expect(h, info.TitleColor, "Alien Head with FlatPanels on");

            h.SetConfig(settings.FlatPanels, false);
            string restored = null;
            yield return h.Until(() => !Strip(h) && (restored = Look(h)) == vanilla,
                () => $"the game's panel back with FlatPanels off (strip {(Strip(h) ? "still there" : "gone")}; " +
                      $"before: {vanilla}; now: {restored}) {h.Diagnosis()}");

            h.SetConfig(settings.FlatPanels, true);
            yield return h.Wait(SevenUIChecks.LookSwitchSeconds);
            yield return Expect(h, info.TitleColor, "Alien Head with FlatPanels on again");
        }

        // With LookingGlass's full descriptions on and text as it writes it for an item with stats: the stats leave the
        // description for a 200px column after a 1px line, a row a stat, names left in the header color and values right in
        // the accent color, their right edges on the column's.
        private static IEnumerator StatsColumn(Harness h)
        {
            ConfigEntry<bool> full = FullDescription(h);
            h.SetConfig(SevenUIPlugin.Settings.DebugLogging, true);
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            h.SetConfig(full, true);
            yield return ScoreboardSetUp.Open(h);
            InspectInfo info = RoR2Content.Items.AlienHead;
            yield return Inspect(h, info);
            yield return WriteLookingGlassText(h);
            string problem = null;
            yield return h.Until(() => (problem = StatsProblem(h)) == null,
                () => $"the stats column beside the description: {problem} {h.Diagnosis()}");
            yield return h.Screenshot("scoreboard_inspect_stats");
        }

        // Text without stat lines (a chest's), or with the stats off in LookingGlass, leaves the panel as it was: no line,
        // no column, the icon's gap.
        private static IEnumerator NoStatsNoColumn(Harness h)
        {
            ConfigEntry<bool> full = FullDescription(h);
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
            yield return h.Wait(SettleSeconds);
            string problem = Column(h, false);
            if (problem != null) throw h.Fail($"a stats column on the chest: {problem} {h.Diagnosis()}");

            // With stats but LookingGlass's setting off: the game's text, whole, and no column.
            h.SetConfig(full, false);
            TMP_Text description = Description(Controller(h));
            description.text = LookingGlassText;
            yield return h.Wait(SettleSeconds);
            problem = Column(h, false);
            if (problem != null) throw h.Fail($"a stats column with Full Description in Hud off: {problem} {h.Diagnosis()}");
            if (description.text != LookingGlassText) throw h.Fail($"the game's text changed with the setting off: {description.text}");
            yield return h.Screenshot("scoreboard_inspect_stats_none");
        }

        // The panel as the game draws it, with the stats in its text, taken before the mod touched it; styled, the column
        // is there; with FlatPanels off, the text, the gap and the objects are the game's again.
        private static IEnumerator StatsFlatPanelsOff(Harness h)
        {
            PluginConfig settings = SevenUIPlugin.Settings;
            ConfigEntry<bool> full = FullDescription(h);
            h.SetConfig(settings.DebugLogging, true);
            h.SetConfig(settings.FlatPanels, false);
            h.SetConfig(full, true);
            yield return h.Wait(SevenUIChecks.LookSwitchSeconds);
            yield return ScoreboardSetUp.Open(h);
            InspectInfo info = RoR2Content.Items.AlienHead;
            yield return Inspect(h, info);
            yield return WriteLookingGlassText(h);
            yield return h.Wait(SettleSeconds);
            string vanilla = Look(h);

            h.SetConfig(settings.FlatPanels, true);
            string problem = null;
            yield return h.Until(() => (problem = StatsProblem(h)) == null,
                () => $"the stats column with FlatPanels on: {problem} {h.Diagnosis()}");

            h.SetConfig(settings.FlatPanels, false);
            string restored = null;
            yield return h.Until(() => Column(h, false) == null && (restored = Look(h)) == vanilla,
                () => $"the game's panel back with FlatPanels off (column: {Column(h, false) ?? "gone"}; " +
                      $"before: {vanilla}; now: {restored}) {h.Diagnosis()}");
        }

        // LookingGlass's own setting, which the mod reads through BepInEx's config; without LookingGlass there is none.
        private static ConfigEntry<bool> FullDescription(Harness h)
        {
            ConfigEntry<bool> entry = null;
            if (Chainloader.PluginInfos.TryGetValue(GameNames.LookingGlassGUID, out BepInEx.PluginInfo info) && info.Instance)
            {
                info.Instance.Config.TryGetEntry(LookingGlassSection, LookingGlassKey, out entry);
            }
            if (entry == null) throw h.Skip("LookingGlass isn't installed, or has no Full Description in Hud setting");
            return entry;
        }

        // The game writes the description at Show; this stands in for LookingGlass's rewrite of it after, so the stats
        // are the same whatever its other settings.
        private static IEnumerator WriteLookingGlassText(Harness h)
        {
            yield return h.Wait(SettleSeconds);
            TMP_Text description = Description(Controller(h));
            description.text = LookingGlassText;
            yield return null;
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

        // Null when the column, the line and the description are as design B draws them, else what differs.
        private static string StatsProblem(Harness h)
        {
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
            if (Mathf.Abs(lineRect.height - (boxRect.height - 2f * PaddingY)) > Tolerance && lineRect.height < column.height - Tolerance)
            {
                return $"line {lineRect.height:0.#}px tall, not the content's {boxRect.height - 2f * PaddingY:0.#}";
            }
            if (Mathf.Abs(column.xMin - lineRect.xMax - StatsSpacing) > Tolerance) return $"column {column.xMin - lineRect.xMax:0.#}px after the line, not {StatsSpacing}";
            if (Mathf.Abs(column.width - StatsWidth) > Tolerance) return $"column {column.width:0.#}px wide, not {StatsWidth}";
            List<TMP_Text> labels = Rows(stats, ScoreboardInspect.LabelName);
            List<TMP_Text> values = Rows(stats, ScoreboardInspect.ValueName);
            if (labels.Count != StatLabels.Length || values.Count != StatLabels.Length)
            {
                return $"{labels.Count} names and {values.Count} values for {StatLabels.Length} stats";
            }
            float unit = HudUnits.PerCanvasPixel;
            float top = column.yMax - StatsTop;
            for (int i = 0; i < labels.Count; i++)
            {
                if (labels[i].text != StatLabels[i] || values[i].text != StatValues[i]) return $"row {i}: {labels[i].text}{values[i].text}";
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
            if (description.text != Described) return $"description \"{description.text}\" not the full text without the stats";
            return null;
        }

        private static List<TMP_Text> Rows(Transform column, string name) =>
            column.Cast<Transform>().Where(row => row.name == name && row.gameObject.activeSelf)
                .Select(row => row.GetComponent<TMP_Text>()).ToList();

        // Shows what the interact prompt's Inspect would, through the game's controller. Autopause, which the panel asks
        // for while it is up in a single-player run, would stop the game, so it is off for the scenario.
        private static IEnumerator Inspect(Harness h, InspectInfo info)
        {
            InspectPanelController controller = null;
            yield return h.Until(() => (controller = Controller(h)) != null, "the scoreboard's inspect panel");
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
            controller.Show(info);
            yield return null;
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
            if (Mathf.Abs(panel.xMin - boardBox.xMin) > Tolerance || Mathf.Abs(panel.width - boardBox.width) > Tolerance)
            {
                return $"panel {Format(panel)} not the board's width {Format(boardBox)}";
            }
            // Screen y grows upward: the panel's top is the board's bottom less the gap.
            if (Mathf.Abs(boardBox.yMin - panel.yMax - BoardGap) > Tolerance)
            {
                return $"{boardBox.yMin - panel.yMax:0.#}px between the board and the panel, not {BoardGap}";
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
            Color expected = new Color(titleColor.r, titleColor.g, titleColor.b, 1f);
            if (!SevenUIChecks.Near(strip.color, expected)) return $"strip {strip.color} not the title's {expected}";
            if (!SevenUIChecks.Near(title.color, titleColor)) return $"name {title.color} not the game's {titleColor}";
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
            Rect textBox = CanvasSizeChecks.Box(h, (RectTransform)texts);
            if (Mathf.Abs(textBox.xMin - slotBox.xMax - IconGap) > Tolerance)
            {
                return $"texts start {textBox.xMin - slotBox.xMax:0.#}px from the icon, not {IconGap}";
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
            float lines = description.font.faceInfo.lineHeight / description.font.faceInfo.pointSize * (1f + description.lineSpacing / 100f);
            if (Mathf.Abs(lines - LineHeight) > LineTolerance) return $"description line height {lines:0.##}, not {LineHeight}";
            GameObject flavor = Flavor(controller);
            if (flavor && flavor.activeInHierarchy) return "the lore still shows";
            if (description.text.Length == 0 || title.text.Length == 0) return "the name or description is empty";
            return null;
        }
    }
}
