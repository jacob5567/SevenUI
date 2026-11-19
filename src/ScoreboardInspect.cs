using System.Collections.Generic;
using System.Reflection;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the game's inspect panel under the scoreboard (the description of the pickup or interactable the
    // pointer is on, or that Tab was pressed beside) as the canvas's design A, in place: the board's flat face the
    // board's width and a gap under it, a thin strip along its top in the title's color, the icon on a dark slot, and
    // the name in bold over the description. The game's objects are not moved or replaced, only resized through their
    // layout groups, and its texts keep what it writes into them: the title's color is whatever the game set for the
    // pickup, tier, drone kind or interactable (copied to the strip every update), and the description's own rich-text
    // colors stay. The icon's own color is never touched, as the game blacks out a drone not yet seen.
    internal sealed class ScoreboardInspect
    {
        public const string StripName = "SevenUIInspectStrip";
        public const string DividerName = "SevenUIInspectDivider";
        public const string StatsName = "SevenUIInspectStats";
        public const string LabelName = "Label";
        public const string ValueName = "Value";

        private const string BoxName = "HorizontalBox";
        private const string IconContainerName = "InspectIconContainer";
        private const string IconBackgroundName = "InspectVisualBackground";
        private const string TextContainerName = "InspectDescriptionContainer";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private const float Canvas = HudUnits.PerCanvasPixel;

        // The canvas's sizes, in its pixels, scaled to the HUD's units (HudUnits).
        private const float BoardGap = 6f * Canvas;
        private const float StripHeight = 2f * Canvas;
        private const int PaddingX = (int)(14f * Canvas + 0.5f);
        private const int PaddingY = (int)(12f * Canvas + 0.5f);
        private const float IconSize = 56f * Canvas;
        private const int IconInset = (int)(4f * Canvas + 0.5f);
        private const float IconGap = 12f * Canvas;
        private const float ColumnGap = 4f * Canvas;
        // LookingGlass's stats: a column on the right, after a 1px line, each gap between them this wide.
        private const float StatsSpacing = 14f * Canvas;
        private const float StatsWidth = 200f * Canvas;
        private const float DividerWidth = 1f * Canvas;
        private const float StatsTop = 2f * Canvas;
        private const float StatsRowGap = 6f * Canvas;
        private const float StatsLabelGap = 16f * Canvas;
        private const float StatsSize = 13f * Canvas;
        private const float NameSize = 16f * Canvas;
        private const float DescriptionSize = 13f * Canvas;
        private const float DescriptionLineHeight = 1.4f;
        private const float PercentPerLine = 100f;
        // Beside the inspect panel the container holds the board; any more spaced children and its gap is left alone.
        private const int MaxOtherChildren = 1;

        private static readonly FieldInfo PanelField = typeof(InspectPanelController).GetField("InspectPanel", Fields);
        private static readonly FieldInfo TitleField = typeof(InspectPanelController).GetField("InspectTitleText", Fields);
        private static readonly FieldInfo DescriptionField = typeof(InspectPanelController).GetField("InspectDescription", Fields);
        private static readonly FieldInfo FlavorField = typeof(InspectPanelController).GetField("InspectFlavor", Fields);

        private readonly StyleRecorder recorder;
        // Components whose game values were put back by an undo already registered, so a second pass doesn't capture
        // the mod's.
        private readonly HashSet<Object> captured = new HashSet<Object>();
        private TMP_Text title;
        private Image strip;
        private TMP_Text description;
        private HorizontalOrVerticalLayoutGroup boxGroup;
        private RectTransform box;
        private GameObject divider;
        private RectTransform stats;
        private LayoutElement statsElement;
        private readonly List<TMP_Text> labels = new List<TMP_Text>();
        private readonly List<TMP_Text> values = new List<TMP_Text>();
        // The description's text as the mod last left it, and the game's text it replaced; null when the game's stands.
        private string handled;
        private string gameText;
        private bool statsShown;

        public ScoreboardInspect(StyleRecorder recorder)
        {
            this.recorder = recorder;
        }

        public bool Styled => strip;

        // Restyles the board's inspect panel; does nothing for a board without one. The container is the board's
        // vertical layout, which the panel is a child of. Safe to run again: everything is recorded the first time and
        // the strip is found by name.
        public void Style(Transform board, string inspectionPath, VerticalLayoutGroup container)
        {
            InspectPanelController controller = board.GetComponentInChildren<InspectPanelController>(true);
            Transform inspection = board.Find(inspectionPath);
            GameObject panelObject = controller && PanelField != null ? PanelField.GetValue(controller) as GameObject : null;
            TMP_Text titleText = controller && TitleField != null ? TitleField.GetValue(controller) as TMP_Text : null;
            Component descriptionBehaviour = controller && DescriptionField != null
                ? DescriptionField.GetValue(controller) as Component : null;
            TMP_Text descriptionText = descriptionBehaviour ? descriptionBehaviour.GetComponent<TMP_Text>() : null;
            if (!inspection || !panelObject || !titleText) return;
            title = titleText;
            description = descriptionText;

            FullWidth(panelObject.transform, container);
            Transform box = inspection.Find(BoxName);
            // Down the chain the game lays the panel out through, each part the width of the one above and as tall as its
            // content.
            foreach (Transform part in new[] { panelObject.transform, panelObject.transform.childCount > 0 ?
                         panelObject.transform.GetChild(0) : null, inspection })
            {
                Control(part, true, false, TextAnchor.UpperLeft, 0, 0, null);
            }
            Control(box, false, false, TextAnchor.UpperLeft, PaddingX, PaddingY, IconGap);
            this.box = box as RectTransform;
            boxGroup = box ? box.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;
            recorder.OnRestore(RestoreText);

            Transform iconContainer = box ? box.Find(IconContainerName) : null;
            Transform background = iconContainer ? iconContainer.Find(IconBackgroundName) : null;
            Fixed(iconContainer, IconSize);
            Fixed(background, IconSize);
            Control(iconContainer, true, true, TextAnchor.UpperLeft, 0, 0, null);
            Control(background, true, true, TextAnchor.MiddleCenter, IconInset, IconInset, null);
            Image visual = controller.InspectVisual;
            if (visual && background && !background.GetComponent<HorizontalOrVerticalLayoutGroup>())
            {
                // Only its size: its color is the game's (black for a drone not seen yet).
                recorder.RecordRect(visual.rectTransform);
                UiFactory.Stretch(visual.rectTransform, Vector2.zero, Vector2.one, new Vector2(IconInset, IconInset),
                    new Vector2(-IconInset, -IconInset));
            }

            Transform textContainer = box ? box.Find(TextContainerName) : null;
            Flexible(textContainer);
            Control(textContainer, true, false, TextAnchor.UpperLeft, 0, 0, ColumnGap);

            StyleName(titleText);
            StyleDescription(descriptionText);
            HideFlavor(controller, titleText, descriptionText);
            if (!strip)
            {
                Transform existing = inspection.Find(StripName);
                strip = existing ? existing.GetComponent<Image>() : AddStrip(inspection);
            }
            Update();
        }

        // The strip takes the title's color, which the game sets each time it shows something, at full opacity.
        public void Update()
        {
            UpdateStats();
            if (strip && title)
            {
                Color color = title.color;
                color.a = 1f;
                if (strip.color != color)
                {
                    strip.color = color;
                }
            }
        }

        // Puts the game's panel back by the recorder's restore. The title's color is the game's, set at each Show since it
        // was recorded, so it is read first and put back over the recorded one.
        public void Restore(System.Action restoreRecorded)
        {
            TMP_Text text = title;
            Color live = text ? text.color : default;
            restoreRecorded();
            box = null;
            boxGroup = null;
            description = null;
            divider = null;
            stats = null;
            statsElement = null;
            labels.Clear();
            values.Clear();
            if (text)
            {
                text.color = live;
            }
            title = null;
            strip = null;
            captured.Clear();
        }

        // With LookingGlass's full descriptions on, the item stats it appends to the description move to a column on the
        // right of the panel; the description keeps the rest. The game writes the description at each Show (and
        // LookingGlass after it), so it is split again whenever it isn't the text left here. Without stats the game's
        // text and the panel as it was stand.
        private void UpdateStats()
        {
            if (!description || !box) return;
            if (statsShown && boxGroup && boxGroup.spacing != StatsSpacing)
            {
                // Styled again meanwhile, which sets the icon's gap.
                SetSpacing(StatsSpacing);
            }
            if (!PickupDescriptions.Full())
            {
                if (handled != null)
                {
                    UndoStats();
                }
                return;
            }
            string current = description.text;
            if (current == handled) return;
            ItemStatsText.Parts parts = ItemStatsText.Split(Body(current));
            if (!parts.HasStats)
            {
                UndoStats();
                handled = current;
                return;
            }
            gameText = current;
            handled = parts.Description;
            description.text = handled;
            ShowStats(parts);
        }

        // The text to split: LookingGlass's own tooltip for what the pointer is on, when it has one that carries stats,
        // else what the panel holds.
        private static string Body(string current)
        {
            if (ItemStatsText.Split(current).HasStats) return current;
            MPEventSystem events = EventSystem.current as MPEventSystem;
            TooltipProvider provider = events ? events.currentTooltipProvider : null;
            return provider && !string.IsNullOrEmpty(provider.overrideBodyText) ? provider.overrideBodyText : current;
        }

        private void ShowStats(ItemStatsText.Parts parts)
        {
            if (!stats)
            {
                divider = UiFactory.NewObject(DividerName, box).gameObject;
                recorder.Add(divider);
                Image line = divider.AddComponent<Image>();
                line.color = Palette.Divider;
                line.raycastTarget = false;
                LayoutElement lineElement = divider.AddComponent<LayoutElement>();
                lineElement.minWidth = DividerWidth;
                lineElement.preferredWidth = DividerWidth;
                lineElement.flexibleWidth = 0f;
                lineElement.minHeight = 0f;
                lineElement.preferredHeight = 0f;
                lineElement.flexibleHeight = 1f;

                stats = UiFactory.NewObject(StatsName, box);
                recorder.Add(stats.gameObject);
                statsElement = stats.gameObject.AddComponent<LayoutElement>();
                statsElement.minWidth = StatsWidth;
                statsElement.preferredWidth = StatsWidth;
                statsElement.flexibleWidth = 0f;
                statsElement.flexibleHeight = 0f;
            }
            int rows = parts.Labels.Count;
            while (labels.Count < rows)
            {
                labels.Add(NewStatText(LabelName, Palette.HeaderText, TextAlignmentOptions.TopLeft, FontStyles.Normal));
                values.Add(NewStatText(ValueName, Palette.Accent, TextAlignmentOptions.TopRight, FontStyles.Bold));
            }
            float y = StatsTop;
            for (int i = 0; i < labels.Count; i++)
            {
                bool used = i < rows;
                labels[i].gameObject.SetActive(used);
                values[i].gameObject.SetActive(used);
                if (!used) continue;
                labels[i].text = parts.Labels[i];
                values[i].text = parts.Values[i];
                float valueWidth = Mathf.Min(values[i].GetPreferredValues(values[i].text).x, StatsWidth);
                float labelWidth = Mathf.Max(0f, StatsWidth - valueWidth - StatsLabelGap);
                float height = Mathf.Max(labels[i].GetPreferredValues(labels[i].text, labelWidth, 0f).y,
                    values[i].GetPreferredValues(values[i].text, valueWidth, 0f).y);
                StatsSection.TopLeft(labels[i].rectTransform, 0f, y, labelWidth, height);
                StatsSection.TopLeft(values[i].rectTransform, StatsWidth - valueWidth, y, valueWidth, height);
                y += height + StatsRowGap;
            }
            statsElement.minHeight = statsElement.preferredHeight = y - StatsRowGap;
            divider.SetActive(true);
            stats.gameObject.SetActive(true);
            statsShown = true;
            SetSpacing(StatsSpacing);
        }

        private TMP_Text NewStatText(string name, Color color, TextAlignmentOptions alignment, FontStyles style)
        {
            TMP_Text text = UiFactory.NewText(name, stats, description, StatsSize, color);
            text.spriteAsset = description.spriteAsset;
            text.fontStyle = style;
            text.alignment = alignment;
            text.enableWordWrapping = alignment == TextAlignmentOptions.TopLeft;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        // The game's description back when it is still the one left here. The recorder puts back the rest.
        private void RestoreText()
        {
            if (description && gameText != null && description.text == handled)
            {
                description.text = gameText;
            }
            handled = null;
            gameText = null;
            statsShown = false;
        }

        // The panel as without stats: the column and line hidden, the box's gap the icon's, the game's description back
        // when it is still the one left here.
        private void UndoStats()
        {
            bool shown = statsShown;
            RestoreText();
            if (!shown) return;
            if (divider) divider.SetActive(false);
            if (stats) stats.gameObject.SetActive(false);
            SetSpacing(IconGap);
        }

        private void SetSpacing(float spacing)
        {
            if (!boxGroup) return;
            boxGroup.spacing = spacing;
            LayoutRebuilder.ForceRebuildLayoutImmediate(box);
        }

        // Spans the board: the game's panel hands out the container's width, after the board's share.
        private void FullWidth(Transform panel, VerticalLayoutGroup container)
        {
            if (container)
            {
                recorder.Record(container);
                if (captured.Add(container))
                {
                    bool controlWidth = container.childControlWidth;
                    recorder.OnRestore(() =>
                    {
                        if (container) container.childControlWidth = controlWidth;
                    });
                }
                container.childControlWidth = true;
                int others = 0;
                foreach (Transform child in container.transform)
                {
                    if (child != panel && child.gameObject.activeSelf)
                    {
                        others++;
                    }
                }
                if (others <= MaxOtherChildren)
                {
                    container.spacing = BoardGap;
                }
            }
            LayoutElement element = Element(panel);
            element.minWidth = 0f;
            element.preferredWidth = 0f;
            element.flexibleWidth = 1f;
            ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
            if (fitter && fitter.horizontalFit != ContentSizeFitter.FitMode.Unconstrained)
            {
                ContentSizeFitter.FitMode fit = fitter.horizontalFit;
                recorder.OnRestore(() =>
                {
                    if (fitter) fitter.horizontalFit = fit;
                });
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
        }

        // A layout group the game puts a part's children in: given the sizes, and the children's their own size.
        private void Control(Transform part, bool expandWidth, bool expandHeight, TextAnchor alignment, int paddingX,
            int paddingY, float? spacing)
        {
            HorizontalOrVerticalLayoutGroup group = part ? part.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;
            if (!group) return;
            recorder.Record(group);
            if (captured.Add(group))
            {
                bool controlWidth = group.childControlWidth;
                bool controlHeight = group.childControlHeight;
                bool expandedWidth = group.childForceExpandWidth;
                bool expandedHeight = group.childForceExpandHeight;
                TextAnchor aligned = group.childAlignment;
                recorder.OnRestore(() =>
                {
                    if (!group) return;
                    group.childControlWidth = controlWidth;
                    group.childControlHeight = controlHeight;
                    group.childForceExpandWidth = expandedWidth;
                    group.childForceExpandHeight = expandedHeight;
                    group.childAlignment = aligned;
                });
            }
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = expandWidth;
            group.childForceExpandHeight = expandHeight;
            group.childAlignment = alignment;
            group.padding = new RectOffset(paddingX, paddingX, paddingY, paddingY);
            if (spacing.HasValue)
            {
                group.spacing = spacing.Value;
            }
        }

        private void Fixed(Transform part, float size)
        {
            if (!part) return;
            LayoutElement element = Element(part);
            element.minWidth = size;
            element.preferredWidth = size;
            element.minHeight = size;
            element.preferredHeight = size;
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;
        }

        // Takes the width the icon leaves, the text wrapping within it.
        private void Flexible(Transform part)
        {
            if (!part) return;
            LayoutElement element = Element(part);
            element.minWidth = 0f;
            element.preferredWidth = 0f;
            element.flexibleWidth = 1f;
        }

        private LayoutElement Element(Transform part)
        {
            LayoutElement element = part.GetComponent<LayoutElement>();
            if (!element)
            {
                element = part.gameObject.AddComponent<LayoutElement>();
                recorder.Add(element);
            }
            recorder.Record(element);
            return element;
        }

        // Its color is the game's, set at each Show for the pickup's tier or the interactable's kind.
        private void StyleName(TMP_Text text)
        {
            recorder.Record(text);
            text.fontSize = NameSize;
            text.fontStyle |= FontStyles.Bold;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableAutoSizing = false;
        }

        // The description in the body color with its own rich-text colors, whose text the game fills from its token.
        private void StyleDescription(TMP_Text text)
        {
            if (!text) return;
            recorder.Record(text);
            if (captured.Add(text))
            {
                float spacing = text.lineSpacing;
                bool rich = text.richText;
                recorder.OnRestore(() =>
                {
                    if (!text) return;
                    text.lineSpacing = spacing;
                    text.richText = rich;
                });
            }
            text.fontSize = DescriptionSize;
            text.color = Palette.TooltipBody;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            text.richText = true;
            TMP_FontAsset font = text.font;
            if (font && font.faceInfo.pointSize > 0f)
            {
                text.lineSpacing = (DescriptionLineHeight - font.faceInfo.lineHeight / font.faceInfo.pointSize) * PercentPerLine;
            }
        }

        // The lore, which the design doesn't show; unless it is one of the parts that are shown.
        private void HideFlavor(InspectPanelController controller, TMP_Text name, TMP_Text description)
        {
            Component flavor = FlavorField != null ? FlavorField.GetValue(controller) as Component : null;
            if (!flavor) return;
            GameObject flavorObject = flavor.gameObject;
            if ((name && name.transform.IsChildOf(flavorObject.transform)) ||
                (description && description.transform.IsChildOf(flavorObject.transform)) ||
                flavorObject == controller.gameObject) return;
            if (flavorObject.activeSelf)
            {
                recorder.SetActive(flavorObject, false);
            }
        }

        // A line along the panel's top, outside its layout, for Update to color.
        private Image AddStrip(Transform inspection)
        {
            GameObject created = UiFactory.NewObject(StripName, inspection).gameObject;
            created.AddComponent<LayoutIgnorer>();
            RectTransform rect = (RectTransform)created.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, StripHeight);
            Image image = created.AddComponent<Image>();
            image.raycastTarget = false;
            recorder.Add(created);
            return image;
        }
    }
}
