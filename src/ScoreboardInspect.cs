using System.Collections.Generic;
using System.Reflection;
using RoR2;
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
        private const int BoardGap = (int)(6f * Canvas + 0.5f);
        private const float StripHeight = 2f * Canvas;
        private const float FootStripHeight = 3f * Canvas;
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
        private const float DumpDelaySeconds = 0.5f;

        private static readonly FieldInfo PanelField = typeof(InspectPanelController).GetField("InspectPanel", Fields);
        private static readonly FieldInfo TitleField = typeof(InspectPanelController).GetField("InspectTitleText", Fields);
        private static readonly FieldInfo DescriptionField = typeof(InspectPanelController).GetField("InspectDescription", Fields);
        private static readonly FieldInfo FlavorField = typeof(InspectPanelController).GetField("InspectFlavor", Fields);

        private readonly StyleRecorder recorder;
        private readonly HUD hud;
        // Components whose game values were put back by an undo already registered, so a second pass doesn't capture
        // the mod's.
        private readonly HashSet<Object> captured = new HashSet<Object>();
        private TMP_Text title;
        private Image strip;
        private Image footStrip;
        private GameObject flavorObject;
        private TMP_Text description;
        private LanguageTextMeshController descriptionToken;
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
        // What LookingGlass's text was last worked out for (the description's token, the viewer and their stack), and
        // its parts when it has stats; the parts the column shows.
        private string shownToken;
        private CharacterMaster shownMaster;
        private int shownHeld;
        private ItemStatsText.Parts shownParts;
        private ItemStatsText.Parts columnParts;
        // Each item by its description token, which the game's Show puts on the description; a token two items share
        // maps to none.
        private static Dictionary<string, ItemDef> itemsByDescription;
        private Transform panel;
        private Transform boardRoot;
        private float shownSince = -1f;
        private bool dumped;
        private bool reportedFade;

        public ScoreboardInspect(StyleRecorder recorder, HUD hud)
        {
            this.recorder = recorder;
            this.hud = hud;
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
            descriptionToken = descriptionBehaviour as LanguageTextMeshController;
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
            UpdateStats();
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
            box = null;
            boxGroup = null;
            description = null;
            descriptionToken = null;
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
            description = null;
            panel = null;
            boardRoot = null;
            shownSince = -1f;
            strip = null;
            footStrip = null;
            flavorObject = null;
            captured.Clear();
        }

        // With LookingGlass's full descriptions on, an item's stats as LookingGlass works them out go in a column on the
        // right of the panel, and the description is its full one without them. LookingGlass doesn't touch this panel, so
        // its text is asked for here: worked out again only when the item, the viewer or their stack changes, and put
        // back whenever the game rewrites the description (at each Show). Without stats the game's text and the panel as
        // it was stand.
        private void UpdateStats()
        {
            if (!description || !box) return;
            if (statsShown && boxGroup && boxGroup.spacing != StatsSpacing)
            {
                // Styled again meanwhile, which sets the icon's gap.
                SetSpacing(StatsSpacing);
            }
            ItemStatsText.Parts parts = StatsParts();
            if (parts == null)
            {
                if (statsShown || handled != null)
                {
                    UndoStats();
                }
                return;
            }
            if (description.text != handled)
            {
                gameText = description.text;
                handled = null;
            }
            if (handled != parts.Description)
            {
                handled = parts.Description;
                description.text = handled;
            }
            if (parts != columnParts || !statsShown)
            {
                ShowStats(parts);
            }
        }

        // LookingGlass's text split, for the item the panel shows, or null when it has no stats for it. The game's Show
        // keeps nothing of what it was given but the tokens it sets, so the item is found by the description's token: an
        // undiscovered item shows the unknown item's token and a disabled one a formatted text, so neither has a column.
        // The stack is the HUD target's, as the game inspects the pickup beside the target's body (HUD.ActivateScoreboard),
        // so while spectating it is the teammate's; the panel keeps no trace of where it was shown from, so an item
        // clicked on the board is counted the same way.
        private ItemStatsText.Parts StatsParts()
        {
            string token = PickupDescriptions.Full() && descriptionToken ? descriptionToken.token : null;
            CharacterMaster master = hud && hud.targetMaster ? hud.targetMaster : LocalUserManager.GetFirstLocalUser()?.cachedMaster;
            ItemDef item = ItemByDescription(token);
            int held = item && master && master.inventory ? master.inventory.GetItemCountPermanent(item) : 0;
            if (token == shownToken && master == shownMaster && held == shownHeld) return shownParts;
            shownToken = token;
            shownMaster = master;
            shownHeld = held;
            string text = item && master ? PickupDescriptions.WithStats(item, held, master) : null;
            ItemStatsText.Parts parts = text != null ? ItemStatsText.Split(text) : null;
            shownParts = parts != null && parts.HasStats ? parts : null;
            if (item && SevenUIPlugin.Settings.DebugLogging.Value)
            {
                SevenUIPlugin.Log.LogInfo($"Inspect panel: LookingGlass's stats for {item.name} ({held} held): " +
                    (shownParts != null ? string.Join(", ", shownParts.Values) : "none"));
            }
            return shownParts;
        }

        private static ItemDef ItemByDescription(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            if (itemsByDescription == null)
            {
                itemsByDescription = new Dictionary<string, ItemDef>();
                foreach (ItemDef item in ItemCatalog.allItemDefs)
                {
                    if (!item || string.IsNullOrEmpty(item.descriptionToken)) continue;
                    itemsByDescription[item.descriptionToken] = itemsByDescription.ContainsKey(item.descriptionToken) ? null : item;
                }
            }
            return itemsByDescription.TryGetValue(token, out ItemDef found) ? found : null;
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
            columnParts = parts;
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
            columnParts = null;
            shownToken = null;
            shownMaster = null;
            shownHeld = 0;
            shownParts = null;
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
