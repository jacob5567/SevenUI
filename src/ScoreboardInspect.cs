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
    // board's width and a gap above it (top padding in the panel's own layout, not the scoreboard container's spacing),
    // a thin strip along its top in the title's color, the icon on a dark slot with a strip of that color along its
    // foot, and the name in bold over the description. The game's objects are not moved or replaced, only resized through their
    // layout groups, and its texts keep what it writes into them: the title's color is whatever the game set for the
    // pickup, tier, drone kind or interactable (copied to the strip every update), and the description's own rich-text
    // colors stay. The icon's own color is never touched, as the game blacks out a drone not yet seen.
    internal sealed class ScoreboardInspect
    {
        public const string StripName = "SevenUIInspectStrip";
        public const string FootStripName = "SevenUIInspectFootStrip";

        private const string BoxName = "HorizontalBox";
        private const string IconContainerName = "InspectIconContainer";
        private const string IconBackgroundName = "InspectVisualBackground";
        private const string TextContainerName = "InspectDescriptionContainer";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private const float Canvas = HudUnits.PerCanvasPixel;

        // The canvas's sizes, in its pixels, scaled to the HUD's units (HudUnits).
        private const int BoardGap = (int)(6f * Canvas + 0.5f);
        private const float StripHeight = 2f * Canvas;
        private const float FootStripHeight = 3f * Canvas;
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
        private const float DumpDelaySeconds = 0.5f;

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
        private Image footStrip;
        private GameObject flavorObject;
        private Transform panel;
        private Transform boardRoot;
        private TMP_Text description;
        private float shownSince = -1f;
        private bool dumped;
        private bool reportedFade;

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
            this.description = description;
            panel = panelObject.transform;
            boardRoot = board;

            FullWidth(panelObject.transform, container);
            Transform box = inspection.Find(BoxName);
            // Down the chain the game lays the panel out through, each part the width of the one above and as tall as its
            // content. The gap above the face is the top padding of the nearest group above the face itself.
            Transform gapPart = GapPart(inspection, panelObject.transform);
            foreach (Transform part in new[] { panelObject.transform, panelObject.transform.childCount > 0 ?
                         panelObject.transform.GetChild(0) : null, inspection })
            {
                Control(part, true, false, TextAnchor.UpperLeft, 0, 0, null, part == gapPart ? BoardGap : 0);
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
                strip = existing ? existing.GetComponent<Image>() : AddStrip(inspection, StripName, true, StripHeight);
            }
            if (!footStrip && background)
            {
                Transform existing = background.Find(FootStripName);
                footStrip = existing ? existing.GetComponent<Image>() : AddStrip(background, FootStripName, false, FootStripHeight);
            }
            Update();
        }

        // The strips take the title's color, which the game sets each time it shows something, at full opacity. The lore
        // is hidden again if the game's Show turned it back on.
        public void Update()
        {
            if (title)
            {
                Color color = title.color;
                color.a = 1f;
                if (strip && strip.color != color)
                {
                    strip.color = color;
                }
                if (footStrip && footStrip.color != color)
                {
                    footStrip.color = color;
                }
            }
            if (flavorObject && flavorObject.activeSelf)
            {
                flavorObject.SetActive(false);
            }
            KeepOpaque();
        }

        // The game's panel fades in through a CanvasGroup (UIJuice) on its area, and with the panel restyled that fade
        // was seen to stay near zero, leaving the face, icon and texts barely visible. A group between the panel and
        // the board is held at full alpha while the panel shows; the board's own groups are not touched. This should
        // run after the game's Updates (LateUpdate), so the game's value is not the one drawn.
        private void KeepOpaque()
        {
            if (!panel || !boardRoot || !panel.gameObject.activeInHierarchy)
            {
                shownSince = -1f;
                return;
            }
            if (shownSince < 0f)
            {
                shownSince = Time.unscaledTime;
            }
            for (Transform part = panel; part && part != boardRoot; part = part.parent)
            {
                CanvasGroup group = part.GetComponent<CanvasGroup>();
                if (group && group.alpha < 1f)
                {
                    if (!reportedFade && SevenUIPlugin.Settings.DebugLogging.Value)
                    {
                        reportedFade = true;
                        SevenUIPlugin.Log.LogInfo($"Inspect panel: '{part.name}' CanvasGroup alpha {group.alpha:0.##} held at 1");
                    }
                    group.alpha = 1f;
                }
            }
            if (!dumped && Time.unscaledTime - shownSince > DumpDelaySeconds && SevenUIPlugin.Settings.DebugLogging.Value)
            {
                dumped = true;
                Dump();
            }
        }

        // The product of the CanvasGroup alphas from a part up to the root (a group that ignores its parents ends it).
        public static float EffectiveAlpha(Transform part)
        {
            float alpha = 1f;
            for (; part; part = part.parent)
            {
                CanvasGroup group = part.GetComponent<CanvasGroup>();
                if (!group) continue;
                alpha *= group.alpha;
                if (group.ignoreParentGroups) break;
            }
            return alpha;
        }

        // The enabled graphics drawn after a text in its canvas (hierarchy order is draw order) whose rects overlap its
        // own and that would show: not clear, not under a faded group. The mod's own strips are left out.
        public static List<Graphic> DrawnOver(Graphic text)
        {
            List<Graphic> covering = new List<Graphic>();
            Canvas canvas = text ? text.canvas : null;
            if (!canvas) return covering;
            Rect own = WorldRect((RectTransform)text.transform);
            bool after = false;
            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(false))
            {
                if (graphic == text)
                {
                    after = true;
                    continue;
                }
                if (!after || !graphic.enabled || graphic.canvas != canvas || graphic.color.a <= 0f) continue;
                if (graphic.name == StripName || graphic.name == FootStripName) continue;
                if (EffectiveAlpha(graphic.transform) <= 0f) continue;
                if (own.Overlaps(WorldRect(graphic.rectTransform))) covering.Add(graphic);
            }
            return covering;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static string PathOf(Transform part, Transform root)
        {
            string path = part.name;
            for (Transform up = part.parent; up && up != root; up = up.parent)
            {
                path = up.name + "/" + path;
            }
            return path;
        }

        // Once, behind DebugLogging: what could dim or cover the panel's texts.
        private void Dump()
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder("Inspect panel dump: groups up the hierarchy:");
            for (Transform part = panel; part; part = part.parent)
            {
                CanvasGroup group = part.GetComponent<CanvasGroup>();
                if (group)
                {
                    text.Append($" [{part.name} alpha {group.alpha:0.##}{(group.ignoreParentGroups ? " ignoring parents" : "")}]");
                }
            }
            foreach (TMP_Text shown in new[] { title, description })
            {
                if (!shown) continue;
                text.Append($"; '{shown.name}' effective alpha {EffectiveAlpha(shown.transform) * shown.color.a:0.##}, drawn over by:");
                foreach (Graphic over in DrawnOver(shown))
                {
                    text.Append($" {PathOf(over.transform, boardRoot)} ({over.GetType().Name} {over.color})");
                }
            }
            SevenUIPlugin.Log.LogInfo(text.ToString());
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
            description = null;
            panel = null;
            boardRoot = null;
            shownSince = -1f;
            strip = null;
            footStrip = null;
            flavorObject = null;
            captured.Clear();
        }

        // Spans the board: the game's panel hands out the container's width, after the board's share.
        private void FullWidth(Transform panel, VerticalLayoutGroup container)
        {
            if (container)
            {
                if (captured.Add(container))
                {
                    bool controlWidth = container.childControlWidth;
                    recorder.OnRestore(() =>
                    {
                        if (container) container.childControlWidth = controlWidth;
                    });
                }
                container.childControlWidth = true;
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

        // The nearest layout group above the face, up to the panel: its top padding holds the gap, which moves the face
        // down without touching the container's spacing (that is shared with the board and the game's other children).
        private static Transform GapPart(Transform inspection, Transform panel)
        {
            for (Transform part = inspection.parent; part; part = part.parent)
            {
                if (part.GetComponent<HorizontalOrVerticalLayoutGroup>()) return part;
                if (part == panel) break;
            }
            return null;
        }

        // A layout group the game puts a part's children in: given the sizes, and the children's their own size.
        private void Control(Transform part, bool expandWidth, bool expandHeight, TextAnchor alignment, int paddingX,
            int paddingY, float? spacing, int extraTop = 0)
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
            group.padding = new RectOffset(paddingX, paddingX, paddingY + extraTop, paddingY);
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
            GameObject hidden = flavor.gameObject;
            if ((name && name.transform.IsChildOf(hidden.transform)) ||
                (description && description.transform.IsChildOf(hidden.transform)) ||
                hidden == controller.gameObject) return;
            flavorObject = hidden;
            if (hidden.activeSelf)
            {
                recorder.SetActive(hidden, false);
            }
        }

        // A line along the top or foot of a part, outside its layout and over its other children, for Update to color.
        private Image AddStrip(Transform parent, string name, bool top, float height)
        {
            GameObject created = UiFactory.NewObject(name, parent).gameObject;
            created.AddComponent<LayoutIgnorer>();
            RectTransform rect = (RectTransform)created.transform;
            float edge = top ? 1f : 0f;
            rect.anchorMin = new Vector2(0f, edge);
            rect.anchorMax = new Vector2(1f, edge);
            rect.pivot = new Vector2(0.5f, edge);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
            Image image = created.AddComponent<Image>();
            image.raycastTarget = false;
            recorder.Add(created);
            return image;
        }
    }
}
