using System.Collections.Generic;
using System.Reflection;
using RoR2.UI;
using TMPro;
using UnityEngine;
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
            TMP_Text description = descriptionBehaviour ? descriptionBehaviour.GetComponent<TMP_Text>() : null;
            if (!inspection || !panelObject || !titleText) return;
            title = titleText;

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
            StyleDescription(description);
            HideFlavor(controller, titleText, description);
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
            if (text)
            {
                text.color = live;
            }
            title = null;
            strip = null;
            captured.Clear();
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
