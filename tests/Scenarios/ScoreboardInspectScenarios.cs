using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // The scoreboard's inspect panel (what Tab shows for the pickup or interactable beside the player), restyled in place
    // as the canvas's design A: the board's width, 6px under it (the top padding of the panel's own layout, not the
    // container's spacing), the board's flat face, a 2px strip along its top in the title's color, the 56px icon on a
    // dark slot with a 3px strip of that color along its foot, the name in bold 16 over the description in 13, in the
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
        private const float IconSize = 56f;
        private const float PaddingX = 14f;
        private const float PaddingY = 12f;
        private const float IconGap = 12f;
        private const float NameSize = 16f;
        private const float DescriptionSize = 13f;
        private const float LineHeight = 1.4f;
        private const float Tolerance = 1.5f;
        private const float LineTolerance = 0.05f;
        private const float OpaqueAlpha = 0.99f;
        private const float SettleSeconds = 0.5f;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "scoreboard_inspect_item_as_canvas", Stages.TitanicPlains, ItemAsCanvas);
            yield return new Scenario(1, "scoreboard_inspect_chest_as_canvas", Stages.TitanicPlains, ChestAsCanvas);
            yield return new Scenario(1, "scoreboard_inspect_flat_panels_off_and_on", Stages.TitanicPlains, FlatPanelsOffAndOn);
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

        // A chest has no tier: its inspect info's own title color names it and colors both strips.
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
            if (Mathf.Abs(panel.xMin - boardBox.xMin) > Tolerance || Mathf.Abs(panel.width - boardBox.width) > Tolerance)
            {
                return $"panel {Format(panel)} not the board's width {Format(boardBox)}";
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
            Image foot = FootStrip(h);
            if (!foot) return "no strip along the icon slot's foot";
            Rect footBox = CanvasSizeChecks.Box(h, foot.rectTransform);
            if (Mathf.Abs(footBox.height - FootStripHeight) > Tolerance || Mathf.Abs(footBox.width - slotBox.width) > Tolerance ||
                Mathf.Abs(footBox.xMin - slotBox.xMin) > Tolerance || Mathf.Abs(footBox.yMin - slotBox.yMin) > Tolerance)
            {
                return $"foot strip {Format(footBox)} not {FootStripHeight}px along the foot of the icon slot {Format(slotBox)}";
            }
            if (!SevenUIChecks.Near(foot.color, expected)) return $"foot strip {foot.color} not the title's {expected}";
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
