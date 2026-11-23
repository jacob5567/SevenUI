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
    // pointer is on, or that Tab was pressed beside) as the canvas's design A, in place: the board's flat face, 60% of
    // the board's width, centered under it, and a gap above it (top padding in the panel's own layout, not
    // the scoreboard container's spacing),
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

        // The canvas's sizes, in its pixels, scaled to the HUD's units (HudUnits). The text is 30% larger and the icon 15%
        // larger than the 1.7.0 design (I3, Jacob's pick, 2026-11-22): name 21, description and stats 17, icon 64, and the
        // stats column wider to hold the larger text.
        private const int BoardGap = (int)(6f * Canvas + 0.5f);
        private const float StripHeight = 2f * Canvas;
        private const float FootStripHeight = 3f * Canvas;
        private const int PaddingX = (int)(14f * Canvas + 0.5f);
        private const int PaddingY = (int)(12f * Canvas + 0.5f);
        private const float IconSize = 64f * Canvas;
        private const int IconInset = (int)(4f * Canvas + 0.5f);
        private const float IconGap = 12f * Canvas;
        private const float ColumnGap = 4f * Canvas;
        // LookingGlass's stats: a column on the right, after a 1px line, each gap between them this wide.
        private const float StatsSpacing = 14f * Canvas;
        private const float StatsWidth = 260f * Canvas;
        private const float DividerWidth = 1f * Canvas;
        private const float StatsTop = 2f * Canvas;
        private const float StatsRowGap = 6f * Canvas;
        private const float StatsLabelGap = 16f * Canvas;
        private const float StatsSize = 17f * Canvas;
        private const float LineHeightSlack = 0.5f;
        private const float NameSize = 21f * Canvas;
        private const float DescriptionSize = 17f * Canvas;
        private const float DescriptionLineHeight = 1.4f;
        private const float PercentPerLine = 100f;
        private const float DumpDelaySeconds = 0.5f;
        public const string CorruptionLineName = "SevenUICorruptionLine";
        // The share of the board's width the panel spans, centered under the board.
        public const float WidthShare = 0.6f;

        private static readonly FieldInfo PanelField = typeof(InspectPanelController).GetField("InspectPanel", Fields);
        private static readonly FieldInfo TitleField = typeof(InspectPanelController).GetField("InspectTitleText", Fields);
        private static readonly FieldInfo DescriptionField = typeof(InspectPanelController).GetField("InspectDescription", Fields);
        private static readonly FieldInfo FlavorField = typeof(InspectPanelController).GetField("InspectFlavor", Fields);
        private static readonly FieldInfo SidecarField = typeof(InspectPanelController).GetField("Sidecar", Fields);
        private static readonly FieldInfo SidecarInfoField = typeof(InspectPanelController).GetField("SidecarStoredInfo", Fields);

        private readonly StyleRecorder recorder;
        private readonly HUD hud;
        // Components whose game values were put back by an undo already registered, so a second pass doesn't capture
        // the mod's.
        private readonly HashSet<Object> captured = new HashSet<Object>();
        private TMP_Text title;
        // The title's color as the game last set it, and the shade it was given in its place.
        private Color gameTitleColor;
        private Color shadedTitleColor;
        private bool titleShaded;
        private Image strip;
        private Image footStrip;
        private GameObject flavorObject;
        private TMP_Text description;
        // The corruption line under the description (Corruption, I2), and what it was built from.
        private TMP_Text corruptionText;
        private ItemIndex corruptionItem = ItemIndex.None;
        private int corruptionInto = -1;
        private int corruptionOriginals = -1;
        // For a void item, what it would corrupt of the held items (Corruption.Corrupts), and the description it was last
        // said in.
        private string corruptsText = "";
        private string corruptsSource;
        private string corruptsMergedFor;
        private string corruptsMerged;
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
        private bool shownOneMore;
        // The board's item icon whose click showed the panel, with its row's master; none while the panel shows what
        // Tab was pressed beside. Set from the icons' own click callback (ItemIcon.OnItemClicked, which the game calls
        // right after its Show), and cleared when the panel closes or its sidecar button shows that pickup again: the
        // game leaves the sidecar up after a click on the board, so it doesn't tell the two apart.
        private ItemIcon clicked;
        private CharacterMaster clickedMaster;
        private readonly Dictionary<ItemIcon, System.Action> clickHandlers = new Dictionary<ItemIcon, System.Action>();
        private readonly Dictionary<ItemInventoryDisplay, int> watchedIcons = new Dictionary<ItemInventoryDisplay, int>();
        private Button sidecarButton;
        private InspectPanelController controller;
        // The info the game keeps for the sidecar, which it replaces only when it shows a pickup from beside the
        // player (Show with the sidecar), never on a click on the board.
        private object sidecarInfo;
        private ItemStatsText.Parts shownParts;
        private ItemStatsText.Parts columnParts;
        // Each item by its description token, which the game's Show puts on the description; a token two items share
        // maps to none.
        private static Dictionary<string, ItemDef> itemsByDescription;
        private Transform panel;
        private LayoutElement panelElement;
        private Transform sizedPart;
        private RectTransform boardFace;
        private Transform boardRoot;
        private Image iconVisual;
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

            boardFace = board.Find(ScoreboardStyler.BoardPath) as RectTransform;
            FullWidth(panelObject.transform, container);
            Transform box = inspection.Find(BoxName);
            // Down the chain the game lays the panel out through, each part the width of the one above and as tall as its
            // content. The gap above the face is the top padding of the nearest group above the face itself.
            Transform gapPart = GapPart(inspection, panelObject.transform);
            // The face itself is a share of the board's width (FitWidth), centered, so the group it is in doesn't
            // stretch it.
            foreach (Transform part in new[] { panelObject.transform, panelObject.transform.childCount > 0 ?
                         panelObject.transform.GetChild(0) : null, inspection })
            {
                bool faceGroup = part == inspection.parent;
                Control(part, !faceGroup, false, faceGroup ? TextAnchor.UpperCenter : TextAnchor.UpperLeft, 0, 0, null,
                    part == gapPart ? BoardGap : 0);
            }
            sizedPart = inspection;
            panelElement = Element(inspection);
            panelElement.minWidth = 0f;
            panelElement.flexibleWidth = 0f;
            FitWidth();
            Control(box, false, false, TextAnchor.UpperLeft, PaddingX, PaddingY, IconGap);
            this.box = box as RectTransform;
            if (box)
            {
                // Only as tall as its content: nothing in the box may take the height the panel's parents have spare.
                LayoutElement boxElement = Element(box);
                boxElement.flexibleHeight = 0f;
            }
            boxGroup = box ? box.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;
            recorder.OnRestore(RestoreText);

            Transform iconContainer = box ? box.Find(IconContainerName) : null;
            Transform background = iconContainer ? iconContainer.Find(IconBackgroundName) : null;
            Fixed(iconContainer, IconSize);
            Fixed(background, IconSize);
            Control(iconContainer, true, true, TextAnchor.UpperLeft, 0, 0, null);
            Control(background, true, true, TextAnchor.MiddleCenter, IconInset, IconInset, null);
            Image visual = controller.InspectVisual;
            if (visual && background)
            {
                // The slot's layout group would size the icon at the prefab's 100 units (it was seen about twice the slot,
                // off to one corner), so the icon leaves the layout and is placed by anchors: inset, centered, with the
                // sprite's aspect kept. Anything else that sizes it (fitters) is switched off. Its color is the game's
                // (black for a drone not seen yet).
                recorder.RecordRect(visual.rectTransform);
                if (!visual.GetComponent<LayoutIgnorer>())
                {
                    recorder.Add(visual.gameObject.AddComponent<LayoutIgnorer>());
                }
                // A layout group keeps a child if any of its ILayoutIgnorers says not to ignore it, and the icon has the
                // game's LayoutElement, so that one has to ignore the layout too.
                LayoutElement element = visual.GetComponent<LayoutElement>();
                if (element && !element.ignoreLayout)
                {
                    element.ignoreLayout = true;
                    recorder.OnRestore(() =>
                    {
                        if (element)
                        {
                            element.ignoreLayout = false;
                        }
                    });
                }
                foreach (Behaviour fitter in visual.GetComponents<Behaviour>())
                {
                    if (fitter is ContentSizeFitter || fitter is AspectRatioFitter)
                    {
                        recorder.Disable(fitter);
                    }
                }
                if (!visual.preserveAspect)
                {
                    visual.preserveAspect = true;
                    recorder.OnRestore(() =>
                    {
                        if (visual)
                        {
                            visual.preserveAspect = false;
                        }
                    });
                }
                if (visual.rectTransform.localScale != Vector3.one)
                {
                    Vector3 scale = visual.rectTransform.localScale;
                    recorder.OnRestore(() =>
                    {
                        if (visual)
                        {
                            visual.rectTransform.localScale = scale;
                        }
                    });
                }
                iconVisual = visual;
                FitIcon();
            }

            Transform textContainer = box ? box.Find(TextContainerName) : null;
            Flexible(textContainer);
            Control(textContainer, true, false, TextAnchor.UpperLeft, 0, 0, ColumnGap);

            StyleName(titleText);
            StyleDescription(descriptionText);
            AddCorruptionLine(textContainer, descriptionText);
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
            WatchSidecar(controller);
            Refresh();
        }

        // Every frame while the scoreboard is styled: the board's rows, for the clicks on their item icons.
        public void Update(IReadOnlyList<ScoreboardItemGrid> grids)
        {
            WatchClicks(grids);
            object info = controller && SidecarInfoField != null ? SidecarInfoField.GetValue(controller) : null;
            if (info != sidecarInfo)
            {
                sidecarInfo = info;
                FromWorld();
            }
            if (clicked && (!panel || !panel.gameObject.activeSelf))
            {
                FromWorld();
            }
            Refresh();
        }

        // Listens for clicks on icons the rows have added since the last frame; a row only adds and removes icons at its
        // end, so a count that hasn't changed means none are new.
        private void WatchClicks(IReadOnlyList<ScoreboardItemGrid> grids)
        {
            if (grids == null) return;
            foreach (ScoreboardItemGrid grid in grids)
            {
                ItemInventoryDisplay display = grid.Alive ? grid.Display : null;
                List<ItemIcon> icons = display ? display.itemIcons : null;
                if (icons == null || watchedIcons.TryGetValue(display, out int count) && count == icons.Count) continue;
                watchedIcons[display] = icons.Count;
                foreach (ItemIcon icon in icons)
                {
                    if (!icon || clickHandlers.ContainsKey(icon)) continue;
                    ScoreboardStrip row = grid.Strip;
                    System.Action handler = () =>
                    {
                        clicked = icon;
                        clickedMaster = row ? row.master : null;
                    };
                    icon.OnItemClicked += handler;
                    clickHandlers[icon] = handler;
                }
            }
        }

        private void WatchSidecar(InspectPanelController controller)
        {
            this.controller = controller;
            sidecarInfo = SidecarInfoField?.GetValue(controller);
            if (sidecarButton) return;
            GameObject sidecar = SidecarField != null ? SidecarField.GetValue(controller) as GameObject : null;
            sidecarButton = sidecar ? sidecar.GetComponentInChildren<HGButton>(true) : null;
            if (sidecarButton)
            {
                sidecarButton.onClick.AddListener(FromWorld);
            }
        }

        private void FromWorld()
        {
            clicked = null;
            clickedMaster = null;
        }

        private void Unwatch()
        {
            foreach (KeyValuePair<ItemIcon, System.Action> pair in clickHandlers)
            {
                if (pair.Key)
                {
                    pair.Key.OnItemClicked -= pair.Value;
                }
            }
            clickHandlers.Clear();
            watchedIcons.Clear();
            if (sidecarButton)
            {
                sidecarButton.onClick.RemoveListener(FromWorld);
            }
            sidecarButton = null;
            controller = null;
            sidecarInfo = null;
            FromWorld();
        }

        // The strips take the title's color, which the game sets each time it shows something, at full opacity, in
        // SevenUI's shade of it (GameColors.ToCanvas), and the title turns that shade too, so a tier is one color
        // everywhere (Jacob's pick, 2026-11-20). The lore is hidden again if the game's Show turned it back on.
        private void Refresh()
        {
            FitWidth();
            UpdateCorruptionLine();
            UpdateStats();
            FitIcon();
            if (title)
            {
                // Only a color other than the shade is the game's: its alpha alone may change after.
                Color current = title.color;
                if (!titleShaded || !SameRgb(current, shadedTitleColor))
                {
                    gameTitleColor = current;
                }
                Color color = GameColors.ToCanvas(gameTitleColor);
                Color shaded = new Color(color.r, color.g, color.b, current.a);
                shadedTitleColor = shaded;
                titleShaded = true;
                if (current != shaded)
                {
                    title.color = shaded;
                }
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

        // Keeps the icon stretched to the slot minus its inset at scale 1. Whatever sizes an Image once its sprite has loaded
        // (the game's async image loading, a layout pass) runs after Style, so the fit is checked each frame and re-applied
        // only when it differs.
        private void FitIcon()
        {
            if (!iconVisual)
            {
                return;
            }
            RectTransform rect = iconVisual.rectTransform;
            Vector2 inset = new Vector2(IconInset, IconInset);
            if (rect.anchorMin != Vector2.zero || rect.anchorMax != Vector2.one || rect.offsetMin != inset ||
                rect.offsetMax != -inset)
            {
                UiFactory.Stretch(rect, Vector2.zero, Vector2.one, inset, -inset);
            }
            if (rect.localScale != Vector3.one)
            {
                rect.localScale = Vector3.one;
            }
        }

        // The area's CanvasGroup was seen at alpha 0 with the panel restyled, leaving the face, icon and texts barely
        // visible. Nothing the mod does writes it, and the game never starts a fade on it (UIJuice.TransitionAlphaFadeIn
        // has no callers; a UIJuice only copies the group's authored alpha), so it is held at full alpha, only while the
        // panel is active in the hierarchy: the game hides the panel with SetActive(false), which this leaves alone.
        // The board's own groups are not touched.
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
                // The text's own fallback-font submeshes (TMP_SubMeshUI children) are the text, not something over it.
                if (after && graphic.transform.IsChildOf(text.transform)) continue;
                // The text's own fallback-font submeshes (TMP_SubMeshUI children) are the text, not something over it.
                if (after && graphic.transform.IsChildOf(text.transform)) continue;
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

        private static bool SameRgb(Color a, Color b) =>
            Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) && Mathf.Approximately(a.b, b.b);

        // Puts the game's panel back by the recorder's restore. The title's color is the game's, set at each Show since it
        // was recorded, so it is read first and put back over the recorded one; when it is still the shade Refresh gave
        // it, the game's color it replaced goes back instead.
        public void Restore(System.Action restoreRecorded)
        {
            TMP_Text text = title;
            Color live = text ? text.color : default;
            if (titleShaded && SameRgb(live, shadedTitleColor))
            {
                live = new Color(gameTitleColor.r, gameTitleColor.g, gameTitleColor.b, live.a);
            }
            titleShaded = false;
            restoreRecorded();
            Unwatch();
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
            panelElement = null;
            sizedPart = null;
            boardFace = null;
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
            if (statsShown)
            {
                SizeDivider();
            }
            ItemStatsText.Parts parts = StatsParts();
            if (parts == null && statsShown)
            {
                UndoStats();
            }
            if (description.text != handled)
            {
                gameText = description.text;
                handled = null;
            }
            // LookingGlass's description without its stats, or the game's; a void item's sentence saying what it would
            // corrupt of the held items (Corruption.WithCorrupts).
            string text = WithCorrupts(parts != null ? parts.Description : gameText);
            if (text == gameText)
            {
                if (handled != null)
                {
                    description.text = gameText;
                    handled = null;
                }
            }
            else if (handled != text)
            {
                handled = text;
                description.text = handled;
            }
            if (parts != null && (parts != columnParts || !statsShown))
            {
                ShowStats(parts);
            }
        }

        // LookingGlass's text split, for the item the panel shows, or null when it has no stats for it. The game's Show
        // keeps nothing of what it was given but the tokens it sets, so the item is found by the description's token: an
        // undiscovered item shows the unknown item's token and a disabled one a formatted text, so neither has a column.
        // Shown from beside a pickup (HUD.ActivateScoreboard), the stats are for one more than the HUD target holds, so
        // for the teammate's stack while spectating; shown by a click on an item on the board, they are for the stack
        // that row's icon shows, as its tooltip has them.
        private ItemStatsText.Parts StatsParts()
        {
            string token = PickupDescriptions.Full() && descriptionToken ? descriptionToken.token : null;
            ItemDef item = ItemByDescription(token);
            bool onBoard = item && clicked && clicked.itemIndex == item.itemIndex;
            CharacterMaster master = onBoard ? clickedMaster
                : hud && hud.targetMaster ? hud.targetMaster : LocalUserManager.GetFirstLocalUser()?.cachedMaster;
            int held = onBoard ? clicked.itemCount
                : item && master && master.inventory ? master.inventory.GetItemCountPermanent(item) : 0;
            bool oneMore = !onBoard;
            if (token == shownToken && master == shownMaster && held == shownHeld && oneMore == shownOneMore) return shownParts;
            shownToken = token;
            shownMaster = master;
            shownHeld = held;
            shownOneMore = oneMore;
            string text = item && master ? PickupDescriptions.WithStats(item, held, master, oneMore) : null;
            ItemStatsText.Parts parts = text != null ? ItemStatsText.Split(text) : null;
            shownParts = parts != null && parts.HasStats ? parts : null;
            if (item && SevenUIPlugin.Settings.DebugLogging.Value)
            {
                SevenUIPlugin.Log.LogInfo($"Inspect panel: LookingGlass's stats for {item.name} ({held} held{(oneMore ? ", one more" : "")}): " +
                    (shownParts != null ? string.Join(", ", shownParts.Values) : "none"));
            }
            return shownParts;
        }

        // 'text' with the void sentence for corruptsText, worked out again only when either changes.
        private string WithCorrupts(string text)
        {
            if (corruptsText.Length == 0) return text;
            if (text != corruptsSource || corruptsText != corruptsMergedFor)
            {
                corruptsSource = text;
                corruptsMergedFor = corruptsText;
                corruptsMerged = Corruption.WithCorrupts(text, corruptsText);
            }
            return corruptsMerged;
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
                lineElement.flexibleHeight = 0f;

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
            SizeDivider();
        }

        // The line as tall as the content beside it: the tallest of the icon's slot, the text and the column. It has no
        // height of its own to ask the layout for, and letting it stretch would make it as tall as whatever the box is
        // given. The text's height is what the layout gave it, so this follows a description that wraps differently.
        private void SizeDivider()
        {
            if (!divider || !box || !statsElement) return;
            Transform textContainer = box.Find(TextContainerName);
            float height = Mathf.Max(IconSize, statsElement.preferredHeight);
            if (textContainer)
            {
                height = Mathf.Max(height, ((RectTransform)textContainer).rect.height);
            }
            LayoutElement lineElement = divider.GetComponent<LayoutElement>();
            if (Mathf.Abs(lineElement.preferredHeight - height) < LineHeightSlack) return;
            lineElement.minHeight = height;
            lineElement.preferredHeight = height;
            LayoutRebuilder.ForceRebuildLayoutImmediate(box);
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

        // Spans the board: the game's panel hands out the container's width, after the board's share. The face inside it
        // is narrower (FitWidth).
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

        // The face a share of the board's width (WidthShare), checked each update since the board's width changes with
        // the number of players and its place; its group's alignment centers it under the board.
        private void FitWidth()
        {
            if (!panelElement || !boardFace || !sizedPart) return;
            float scale = sizedPart.lossyScale.x;
            if (scale <= 0f) return;
            float width = Mathf.Round(boardFace.rect.width * boardFace.lossyScale.x / scale * WidthShare);
            if (!Mathf.Approximately(panelElement.preferredWidth, width))
            {
                panelElement.preferredWidth = width;
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

        // Its color is the game's, set at each Show for the pickup's tier or the interactable's kind; Refresh shades it.
        private void StyleName(TMP_Text text)
        {
            recorder.Record(text);
            text.fontSize = NameSize;
            text.fontStyle |= FontStyles.Bold;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableAutoSizing = false;
        }

        // The description in the body color with its own rich-text colors, whose text the game fills from its token.
        // Under the description, in its size, hidden until an item held ties to the one shown.
        private void AddCorruptionLine(Transform textContainer, TMP_Text descriptionText)
        {
            if (corruptionText || !textContainer || !descriptionText) return;
            corruptionText = UiFactory.NewText(CorruptionLineName, textContainer, descriptionText, DescriptionSize, Palette.TooltipBody);
            recorder.Add(corruptionText.gameObject);
            corruptionText.alignment = TextAlignmentOptions.TopLeft;
            corruptionText.enableWordWrapping = true;
            corruptionText.richText = true;
            corruptionText.transform.SetSiblingIndex(descriptionText.transform.GetSiblingIndex() + 1);
            corruptionText.gameObject.SetActive(false);
            corruptionItem = ItemIndex.None;
            corruptionInto = corruptionOriginals = -1;
            corruptsText = "";
        }

        // The item shown, found by its description's token as for the stats, against the HUD target's inventory (a
        // teammate's while spectating). Built again only when the item or the held counts change. Reads only.
        private void UpdateCorruptionLine()
        {
            if (!corruptionText) return;
            ItemDef item = ItemByDescription(descriptionToken ? descriptionToken.token : null);
            ItemIndex index = item ? item.itemIndex : ItemIndex.None;
            CharacterMaster master = hud && hud.targetMaster ? hud.targetMaster : LocalUserManager.GetFirstLocalUser()?.cachedMaster;
            Inventory inventory = master ? master.inventory : null;
            Corruption.Counts(index, inventory, out int into, out int originals);
            if (index == corruptionItem && into == corruptionInto && originals == corruptionOriginals) return;
            corruptionItem = index;
            corruptionInto = into;
            corruptionOriginals = originals;
            corruptsText = Corruption.Corrupts(index, inventory);
            string line = Corruption.Line(index, inventory);
            corruptionText.text = line;
            if (corruptionText.gameObject.activeSelf != line.Length > 0)
            {
                corruptionText.gameObject.SetActive(line.Length > 0);
            }
        }

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
