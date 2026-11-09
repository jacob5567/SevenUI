using System.Collections.Generic;
using System.Collections.ObjectModel;
using RoR2;
using RoR2.UI;
using RoR2.UI.SkinControllers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // One pick-an-item panel (Command's, a Void Potential's, a Lemurian egg's) as on the design canvas. A few options
    // become cards side by side, each with its item's name and description always shown; more become a grid of item
    // slots on a flat panel, with the hovered item's name and description under it. The game's buttons stay the
    // buttons, laid out by the game's grid with the game's navigation, so choosing works as before; only their look and
    // the panel around them change. The game's spinning outlines and tinted backgrounds are hidden, its title becomes the
    // header, and its key legend sits under the panel.
    internal sealed class PickerPanel
    {
        public const string BackdropName = "SevenUIPickerBackdrop";
        public const string CardName = "SevenUIPickerCard";
        public const string SlotName = "SevenUIPickerSlot";
        public const string DetailsName = "SevenUIPickerDetails";
        // Children of the game's panels, the same in each of them.
        private const string MainPanelName = "MainPanel";
        private const string SevenUIPrefix = "SevenUI";
        private const string TitleName = "Label";
        private const string LegendName = "SubmenuLegend";
        private const string CancelName = "CancelButton";
        private const string IconName = "Icon";
        private const string BaseOutlineName = "BaseOutline";
        private const string HoverOutlineName = "HoverOutline";
        // The parts of a panel's content the flat look keeps; everything else is the panel's own art (its outlines,
        // backgrounds and overlays, which differ from panel to panel, such as the cursed book's stained glass).
        private static readonly string[] KeptParts = { TitleName, LegendName, CancelName };

        // Up to this many options are cards, in one row, with the gamepad navigation set to match; more are a grid in
        // the game's own columns, which differ from panel to panel and which other mods may change (LookingGlass resizes
        // Command's), so the grid keeps the game's navigation.
        public const int CardLimit = 6;

        // The design canvas's sizes, in its pixels, scaled to the HUD's units.
        private const float Canvas = HudUnits.PerCanvasPixel;
        // The grid: 80px slots 6px apart on a panel at least 300px wide, with a 32px header strip, 14px of padding at
        // the sides and the bottom and 12px between the parts; the details under a 1px line, 10px below it. The
        // canvas's 40px slots were too small to tell the items apart in game.
        public const float SlotSize = 80f * Canvas;
        private const float SlotGap = 6f * Canvas;
        private const float MinPanelWidth = 300f * Canvas;
        private const float StripHeight = 32f * Canvas;
        private const float PaddingX = 14f * Canvas;
        private const float PaddingBottom = 14f * Canvas;
        private const float PartGap = 12f * Canvas;
        private const float DividerHeight = 1f * Canvas;
        private const float DetailsGap = 10f * Canvas;
        // The cards: at least 196px wide and 8px apart under the title, with 14px of padding and 10px between their
        // parts. Across each card's top is a 92px band in the slot's grey with the item centered on it at 72px and the
        // tier's strip along its foot. They widen to keep LookingGlass's "With this item, you will have:" on one line,
        // as long as the row stays 40px inside the screen's sides.
        public const float CardWidth = 196f * Canvas;
        private const float CardScreenMargin = 40f * Canvas;
        private const float CardGap = 8f * Canvas;
        private const float CardPadding = 14f * Canvas;
        private const float CardBandHeight = 92f * Canvas;
        private const float CardIconSize = 72f * Canvas;
        private const float CardPartGap = 10f * Canvas;
        // Both: a slot's or band's tier strip, a slot's icon margin, the hover outline's edge, the item's name and description, the
        // owned count, and the gap between a name and its description in the details.
        private const float TierStripHeight = 3f * Canvas;
        private const float IconInset = 4f * Canvas;
        private const float OutlineEdge = 2f * Canvas;
        private const float NameSize = 19f * Canvas;
        private const float DescriptionSize = 14f * Canvas;
        private const float CountSize = 15f * Canvas;
        private const float CountInset = 3f * Canvas;
        private const float NameGap = 3f * Canvas;
        // The legend's and the cancel button's distance under the panel, and the button's size and text; its resting
        // outline is 1px in the divider's grey.
        private const float LegendGap = 8f * Canvas;
        private const float CancelWidth = 120f * Canvas;
        private const float CancelHeight = 28f * Canvas;
        private const float CancelTextSize = 13f * Canvas;
        private const float RestOutlineEdge = 1f * Canvas;

        private readonly PickupPickerPanel panel;
        private readonly HUD hud;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<PickerButton> buttons = new List<PickerButton>();
        private readonly RectTransform mainPanel;
        private readonly RectTransform juice;
        private readonly TMP_Text title;
        private PickupPickerController.Option[] shownOptions;
        private int shownButtonCount = -1;
        private RectTransform strip;
        private RectTransform divider;
        private TMP_Text detailsName;
        private TMP_Text detailsDescription;
        private StatsSection detailsStats;
        private int detailsIndex = -1;
        private TMP_FontAsset shownFont;
        private PickerButton cancel;
        // A cancel button the game hadn't shown yet when the panel was styled, styled once it is.
        private bool cancelWaiting;
        private int columns;
        private float cardWidth = CardWidth;
        private bool fullDescriptions;

        private PickerPanel(PickupPickerPanel panel, HUD hud, RectTransform mainPanel)
        {
            this.panel = panel;
            this.hud = hud;
            this.mainPanel = mainPanel;
            juice = (RectTransform)panel.buttonContainer.parent;
            Transform titleObject = juice.Find(TitleName);
            title = titleObject ? titleObject.GetComponent<TMP_Text>() : null;
        }

        // Only the game's own panel class, laid out as the plain pickers are, with the buttons straight under the
        // panel's content; the scrapper's scrolling list and the panels of their own classes stay as the game made them.
        public static PickerPanel For(PickupPickerPanel panel, HUD hud)
        {
            if (!panel || panel.GetType() != typeof(PickupPickerPanel) || !panel.buttonContainer) return null;
            Transform mainPanel = panel.transform.Find(MainPanelName);
            Transform content = panel.buttonContainer.parent;
            if (!mainPanel || !content || content.parent != mainPanel || !panel.gridlayoutGroup) return null;
            return new PickerPanel(panel, hud, (RectTransform)mainPanel);
        }

        public bool Alive => panel && mainPanel;

        public PickupPickerPanel Panel => panel;

        public bool Styled => shownOptions != null;

        public bool Cards { get; private set; }

        // The options per row: all of them as cards, the game's columns as a grid.
        public int Columns => columns;

        // Each card's width, as last laid out.
        public float ShownCardWidth => cardWidth;

        // Whether the screen kept the cards narrower than their captions.
        public bool CardsNarrowed { get; private set; }

        public IReadOnlyList<PickerButton> Buttons => buttons;

        // Read by the SevenUI.Tests plugin.
        public PickerButton Cancel => cancel;

        public RectTransform MainPanel => mainPanel;

        public TMP_Text DetailsNameText => detailsName;

        public TMP_Text DetailsDescriptionText => detailsDescription;

        public void Restore()
        {
            recorder.Restore();
            buttons.Clear();
            shownOptions = null;
            shownButtonCount = -1;
            strip = null;
            divider = null;
            detailsName = null;
            detailsDescription = null;
            detailsStats = null;
            detailsIndex = -1;
            shownFont = null;
            cancel = null;
            cancelWaiting = false;
        }

        // Every frame while the panel is open: restyles it once the game has made its buttons, again if the game gives
        // it new options, and otherwise reads each button's hover and availability.
        public void Update()
        {
            if (!Alive) return;
            PickupPickerController.Option[] options = panel.pickupOptions;
            ReadOnlyCollection<MPButton> elements = panel.buttonAllocator?.elements;
            if (options == null || elements == null || elements.Count < options.Length || options.Length == 0) return;
            if (options != shownOptions || elements.Count != shownButtonCount)
            {
                if (Styled)
                {
                    Restore();
                }
                Style(options, elements);
            }
            // The font swap gives the texts Barlow after they are made, which measures differently.
            TMP_FontAsset font = buttons.Count > 0 && buttons[0].Name ? buttons[0].Name.font : detailsName ? detailsName.font : null;
            if (font != shownFont)
            {
                shownFont = font;
                Layout();
            }
            Inventory inventory = hud && hud.targetMaster ? hud.targetMaster.inventory : null;
            // A gamepad's selection and the pointer can each hover an option at once, so the details follow the one
            // hovered last.
            int newlyHovered = -1;
            int firstHovered = -1;
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].Update(inventory);
                if (buttons[i].JustHovered)
                {
                    newlyHovered = i;
                }
                if (firstHovered < 0 && buttons[i].Hovered)
                {
                    firstHovered = i;
                }
            }
            bool stillHovered = detailsIndex >= 0 && detailsIndex < buttons.Count && buttons[detailsIndex].Hovered;
            int hovered = newlyHovered >= 0 ? newlyHovered : stillHovered ? detailsIndex : firstHovered;
            if (cancelWaiting)
            {
                StyleCancel();
            }
            cancel?.Update(null);
            KeepColumns();
            if (detailsName)
            {
                ShowDetails(hovered >= 0 ? hovered : detailsIndex >= 0 ? detailsIndex : 0);
            }
        }

        private void Style(PickupPickerController.Option[] options, ReadOnlyCollection<MPButton> elements)
        {
            shownOptions = options;
            shownButtonCount = elements.Count;
            Cards = options.Length <= CardLimit;
            columns = Cards ? options.Length : GameColumns(options.Length);
            fullDescriptions = PickupDescriptions.Full();
            recorder.RecordRect(mainPanel);

            foreach (Transform child in juice)
            {
                if (child == panel.buttonContainer || child.name.StartsWith(SevenUIPrefix) || System.Array.IndexOf(KeptParts, child.name) >= 0) continue;
                recorder.SetActive(child.gameObject, false);
            }
            if (!Cards)
            {
                RectTransform backdrop = NewImage(BackdropName, juice, Palette.ScoreboardPanel);
                backdrop.SetAsFirstSibling();
                Stretch(backdrop);
                strip = NewImage("SevenUIPickerStrip", juice, CanvasBlend.Over(Palette.HeaderStrip, ScoreboardStyler.CanvasBoard));
                strip.SetSiblingIndex(1);
                strip.anchorMin = new Vector2(0f, 1f);
                strip.anchorMax = Vector2.one;
                strip.pivot = new Vector2(0.5f, 1f);
            }
            StyleTitle();
            StyleLegend();
            StyleCancel();
            StyleGrid();
            for (int i = 0; i < options.Length; i++)
            {
                PickupDef pickup = PickupCatalog.GetPickupDef(options[i].pickup.pickupIndex);
                buttons.Add(Cards ? StyleCard(elements[i], pickup) : StyleSlot(elements[i], pickup));
            }
            if (Cards)
            {
                Navigate(elements, options.Length);
            }
            else
            {
                AddDetails();
            }
            shownFont = null;
        }

        // The game's title, "What is Your Command?", as the header: in the strip over the grid, or over the cards.
        private void StyleTitle()
        {
            if (!title) return;
            recorder.Record(title);
            recorder.RecordRect(title.rectTransform);
            HeaderStyle.Apply(title);
            // Top-aligned, since the void potential's title ends in a line break.
            title.alignment = TextAlignmentOptions.TopLeft;
            bool wrapping = title.enableWordWrapping;
            TextOverflowModes overflow = title.overflowMode;
            recorder.OnRestore(() =>
            {
                if (title)
                {
                    title.enableWordWrapping = wrapping;
                    title.overflowMode = overflow;
                }
            });
            title.enableWordWrapping = false;
            title.overflowMode = TextOverflowModes.Overflow;
            // Art the game hangs on the title, such as the delusion panel's outline frames around it.
            foreach (Image decoration in title.GetComponentsInChildren<Image>(true))
            {
                recorder.Disable(decoration);
            }
        }

        // The game's key legend ("Cancel"), without its backdrop, right-aligned under the panel.
        private void StyleLegend()
        {
            Transform legend = juice.Find(LegendName);
            if (!legend) return;
            recorder.Disable(legend.GetComponent<Image>());
            RectTransform rect = (RectTransform)legend;
            recorder.RecordRect(rect);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -LegendGap);
            HorizontalOrVerticalLayoutGroup group = legend.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (!group) return;
            recorder.Record(group);
            TextAnchor alignment = group.childAlignment;
            recorder.OnRestore(() =>
            {
                if (group)
                {
                    group.childAlignment = alignment;
                }
            });
            group.childAlignment = TextAnchor.UpperRight;
            group.padding = new RectOffset(0, 0, 0, 0);
        }

        // The game's cancel button, which it hangs just under its panel, there for the pointer: a small outlined button
        // at the panel's right under it, with a faint face the stage shows through, a thin grey outline and grey text,
        // lit with the options' accent outline and white text while hovered.
        private void StyleCancel()
        {
            Transform button = juice.Find(CancelName);
            MPButton mpButton = button ? button.GetComponent<MPButton>() : null;
            if (!mpButton) return;
            // Shown for the first time, a button wakes its components, and the game's skin then puts its own look on it
            // over this one even while turned off; the cursed book shows its cancel button after the panel opens.
            cancelWaiting = !button.gameObject.activeInHierarchy;
            if (cancelWaiting) return;
            cancel = StyleButton(mpButton, null, Palette.GhostButton);
            RectTransform rect = (RectTransform)button;
            recorder.RecordRect(rect);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(0f, -LegendGap);
            rect.sizeDelta = new Vector2(CancelWidth, CancelHeight);
            // The game sizes the label to fit its own larger button, which overrides the font size: its label is
            // 13px regular text, centered in the button.
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                recorder.Record(text);
                bool autoSizing = text.enableAutoSizing;
                bool wrapping = text.enableWordWrapping;
                recorder.OnRestore(() =>
                {
                    if (text)
                    {
                        text.enableAutoSizing = autoSizing;
                        text.enableWordWrapping = wrapping;
                    }
                });
                text.enableAutoSizing = false;
                text.enableWordWrapping = false;
                text.fontSize = CancelTextSize;
                text.fontStyle = FontStyles.Normal;
                text.characterSpacing = 0f;
                text.margin = Vector4.zero;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Palette.HeaderText;
                if (!cancel.Label)
                {
                    cancel.Label = text;
                }
            }
            RectTransform outlines = NewObject("SevenUIPickerCancel", button);
            Stretch(outlines);
            cancel.RestOutline = AddOutline(outlines, Palette.Divider, RestOutlineEdge);
            cancel.RestOutline.enabled = true;
            cancel.Outline = AddOutline(outlines, Palette.Accent, OutlineEdge);
        }

        // The game's grid, which places the buttons: its cell and spacing become the slots' or the cards'.
        private void StyleGrid()
        {
            GridLayoutGroup grid = panel.gridlayoutGroup;
            Vector2 cell = grid.cellSize;
            Vector2 spacing = grid.spacing;
            RectOffset padding = grid.padding;
            TextAnchor alignment = grid.childAlignment;
            GridLayoutGroup.Constraint constraint = grid.constraint;
            int constraintCount = grid.constraintCount;
            recorder.OnRestore(() =>
            {
                if (!grid) return;
                grid.cellSize = cell;
                grid.spacing = spacing;
                grid.padding = padding;
                grid.childAlignment = alignment;
                grid.constraint = constraint;
                grid.constraintCount = constraintCount;
            });
            KeepColumns();
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.spacing = Cards ? new Vector2(CardGap, CardGap) : new Vector2(SlotGap, SlotGap);
            RectTransform container = panel.buttonContainer;
            recorder.RecordRect(container);
            container.anchorMin = container.anchorMax = container.pivot = new Vector2(0f, 1f);
        }

        // Every frame too, since another mod may set the grid's columns after the panel is styled: the cards are held
        // to one row, and a grid is laid out again in the game's columns when they change.
        private void KeepColumns()
        {
            GridLayoutGroup grid = panel.gridlayoutGroup;
            if (!Cards)
            {
                // The options, not the buttons, which are styled after the grid.
                int game = GameColumns(shownOptions.Length);
                if (game != columns)
                {
                    columns = game;
                    Layout();
                }
                return;
            }
            if (grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            }
            if (grid.constraintCount != columns)
            {
                grid.constraintCount = columns;
            }
        }

        // The columns the game's grid lays this many options out in. PickupPickerPanel fixes the columns at
        // maxColumnCount as it wakes, but LookingGlass's larger Command window fixes the rows instead (and sets
        // maxColumnCount to the same number), so there the columns are the options over the rows.
        private int GameColumns(int options)
        {
            GridLayoutGroup grid = panel.gridlayoutGroup;
            int count = grid.constraintCount;
            int game;
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && count > 0)
            {
                game = count;
            }
            else if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount && count > 0)
            {
                game = (options + count - 1) / count;
            }
            else
            {
                game = panel.maxColumnCount;
            }
            return Mathf.Clamp(game, 1, Mathf.Max(1, options));
        }

        // The game's explicit gamepad navigation (PickupPickerPanel.SetPickupOptions) for the cards' one row, so up,
        // down, left and right move as the cards are laid out.
        private void Navigate(ReadOnlyCollection<MPButton> elements, int count)
        {
            if (panel.automaticButtonNavigation) return;
            for (int i = 0; i < count; i++)
            {
                MPButton button = elements[i];
                Navigation navigation = button.navigation;
                recorder.OnRestore(() =>
                {
                    if (button)
                    {
                        button.navigation = navigation;
                    }
                });
                int column = i % columns;
                Navigation flat = navigation;
                flat.mode = Navigation.Mode.Explicit;
                flat.selectOnLeft = column > 0 ? elements[i - 1] : null;
                flat.selectOnRight = column < columns - 1 && i + 1 < count ? elements[i + 1] : null;
                flat.selectOnUp = i - columns >= 0 ? elements[i - columns] : null;
                flat.selectOnDown = i + columns < count ? elements[i + columns] : null;
                button.navigation = flat;
            }
        }

        // The game's button frame and outlines give way to a flat look; its hover outline, which the game fades in, to
        // the canvas's sharp one, shown while the game's would be.
        private PickerButton StyleButton(MPButton button, PickupDef pickup, Color face)
        {
            PickerButton styled = new PickerButton
            {
                Button = button, Hover = button as HGButton, Pickup = pickup,
                DescriptionText = PickupDescriptions.For(pickup, button, fullDescriptions),
            };
            styled.Parts = ItemStatsText.Split(styled.DescriptionText);
            // The game's skin sets the button's tint, which multiplies the face's color (the options' slots came out
            // black, not the item bar's grey), and puts its text style back every frame. The tint is left neutral; an
            // option that can't be picked keeps the game's.
            recorder.SetEnabled(button.GetComponent<ButtonSkinController>(), false);
            // The hovered option's name and description show in its card or under the grid, so a tooltip over it
            // (LookingGlass gives each option one) would repeat them. A turned-off provider gets no pointer or
            // selection events; its text is still read for the description (PickupDescriptions).
            foreach (TooltipProvider tooltip in button.GetComponents<TooltipProvider>())
            {
                recorder.SetEnabled(tooltip, false);
            }
            ColorBlock colors = button.colors;
            recorder.OnRestore(() =>
            {
                if (button)
                {
                    button.colors = colors;
                }
            });
            ColorBlock flat = colors;
            flat.normalColor = flat.highlightedColor = flat.pressedColor = flat.selectedColor = Color.white;
            button.colors = flat;
            // The button's own image when its target graphic isn't set, as on the cursed book's cancel button.
            Image frame = button.image ? button.image : button.GetComponent<Image>();
            if (frame)
            {
                recorder.Record(frame);
                frame.sprite = null;
                frame.type = Image.Type.Simple;
                frame.color = face;
                // The game's button skin gives the image a material of its own, which can draw the face darker than
                // its color (the cancel button's faint face came out near black), so the flat face uses the default
                // UI material.
                Material material = frame.material;
                if (material != frame.defaultMaterial)
                {
                    recorder.OnRestore(() =>
                    {
                        if (frame)
                        {
                            frame.material = material;
                        }
                    });
                    frame.material = null;
                }
            }
            Transform baseOutline = button.transform.Find(BaseOutlineName);
            recorder.Disable(baseOutline ? baseOutline.GetComponent<Image>() : null);
            Transform hoverOutline = button.transform.Find(HoverOutlineName);
            recorder.Disable(hoverOutline ? hoverOutline.GetComponent<Image>() : null);
            // The game's button turns its 'image on interactable' back on every frame while it can be pressed, which
            // would bring back a frame hidden above, so the button loses the reference while the flat look is on.
            HGButton hg = button as HGButton;
            if (hg && hg.imageOnInteractable)
            {
                Image onInteractable = hg.imageOnInteractable;
                recorder.OnRestore(() =>
                {
                    if (hg)
                    {
                        hg.imageOnInteractable = onInteractable;
                    }
                });
                hg.imageOnInteractable = null;
            }
            Transform icon = button.transform.Find(IconName);
            if (icon)
            {
                styled.Icon = (RectTransform)icon;
                recorder.RecordRect(styled.Icon);
            }
            return styled;
        }

        private PickerButton StyleSlot(MPButton button, PickupDef pickup)
        {
            PickerButton slot = StyleButton(button, pickup, Palette.ItemSlot);
            if (slot.Icon)
            {
                Stretch(slot.Icon);
                slot.Icon.offsetMin = new Vector2(IconInset, IconInset);
                slot.Icon.offsetMax = new Vector2(-IconInset, -IconInset);
            }
            RectTransform holder = NewObject(SlotName, button.transform);
            Stretch(holder);
            AddTierStrip(holder, pickup);
            slot.Count = AddCount(holder);
            slot.Outline = AddOutline(holder);
            return slot;
        }

        private PickerButton StyleCard(MPButton button, PickupDef pickup)
        {
            PickerButton card = StyleButton(button, pickup, Palette.ScoreboardPanel);
            // Behind the icon: the band it sits on, across the card's top.
            RectTransform band = NewImage(SlotName, button.transform, Palette.ItemSlot);
            band.SetAsFirstSibling();
            AcrossTop(band, CardBandHeight);
            if (card.Icon)
            {
                // Centered on the band above its tier strip.
                card.Icon.anchorMin = card.Icon.anchorMax = new Vector2(0.5f, 1f);
                card.Icon.pivot = new Vector2(0.5f, 0.5f);
                card.Icon.anchoredPosition = new Vector2(0f, -(CardBandHeight - TierStripHeight) / 2f);
                card.Icon.sizeDelta = new Vector2(CardIconSize, CardIconSize);
            }
            // Over it: the rest of the card.
            RectTransform holder = NewObject(CardName, button.transform);
            Stretch(holder);
            RectTransform bandParts = NewObject("Band", holder);
            AcrossTop(bandParts, CardBandHeight);
            AddTierStrip(bandParts, pickup);
            card.Count = AddCount(bandParts);
            card.Name = AddText("Name", holder, NameSize, Palette.Text);
            card.Name.fontStyle = FontStyles.Bold;
            card.Name.text = pickup != null ? Language.GetString(pickup.nameToken) : "";
            card.Description = AddText("Description", holder, DescriptionSize, Palette.HeaderText);
            card.Description.text = card.Parts.Description;
            card.Stats = NewStats(holder);
            card.Stats.Set(card.Parts);
            card.Outline = AddOutline(holder);
            return card;
        }

        // Under the grid: the hovered item's name in its tier's color and its description, the first item's until one
        // is hovered, as tall as the longest so the panel keeps its size.
        private void AddDetails()
        {
            RectTransform details = NewObject(DetailsName, juice);
            Stretch(details);
            // In the grey of the cards' line over their stats; the scoreboard's darker row line barely showed here.
            divider = NewImage("Divider", details, Palette.Divider);
            detailsName = AddText("Name", details, NameSize, Palette.Text);
            detailsName.fontStyle = FontStyles.Bold;
            detailsDescription = AddText("Description", details, DescriptionSize, Palette.HeaderText);
            detailsStats = NewStats(details);
            detailsIndex = -1;
        }

        private void ShowDetails(int index)
        {
            if (index == detailsIndex || index >= buttons.Count) return;
            detailsIndex = index;
            PickupDef pickup = buttons[index].Pickup;
            detailsName.text = pickup != null ? Language.GetString(pickup.nameToken) : "";
            detailsName.color = TierColor(pickup);
            detailsDescription.text = buttons[index].Parts.Description;
            detailsStats.Set(buttons[index].Parts);
            Layout();
        }

        // Sizes the panel and places its parts, from the texts as measured in their current font.
        private void Layout()
        {
            if (!Styled) return;
            int count = buttons.Count;
            float titleHeight = title ? title.GetPreferredValues("A").y : 0f;
            RectTransform container = panel.buttonContainer;
            GridLayoutGroup grid = panel.gridlayoutGroup;
            Vector2 size;
            // How far above the screen's middle the panel's top sits.
            float top;
            if (Cards)
            {
                cardWidth = FitCards(count);
                float textWidth = cardWidth - 2f * CardPadding;
                float nameHeight = 0f;
                float descriptionHeight = 0f;
                foreach (PickerButton card in buttons)
                {
                    nameHeight = Mathf.Max(nameHeight, Measure(card.Name, textWidth));
                    descriptionHeight = Mathf.Max(descriptionHeight, Measure(card.Description, textWidth));
                }
                float y = CardBandHeight + CardPartGap;
                float statsTop = y + nameHeight + CardPartGap + descriptionHeight;
                float statsHeight = 0f;
                foreach (PickerButton card in buttons)
                {
                    TopLeft(card.Name.rectTransform, CardPadding, y, textWidth, nameHeight);
                    TopLeft(card.Description.rectTransform, CardPadding, y + nameHeight + CardPartGap, textWidth, descriptionHeight);
                    statsHeight = Mathf.Max(statsHeight, card.Stats.Layout(CardPadding, statsTop, textWidth));
                }
                float cardHeight = statsTop + statsHeight + CardPadding;
                float width = count * cardWidth + (count - 1) * CardGap;
                grid.cellSize = new Vector2(cardWidth, cardHeight);
                container.anchoredPosition = new Vector2(0f, -(titleHeight + CardGap));
                container.sizeDelta = new Vector2(width, cardHeight);
                if (title)
                {
                    TopLeft(title.rectTransform, 0f, 0f, width, titleHeight);
                }
                size = new Vector2(width, titleHeight + CardGap + cardHeight);
                top = size.y / 2f;
            }
            else
            {
                int rows = (count + columns - 1) / columns;
                float gridWidth = columns * SlotSize + (columns - 1) * SlotGap;
                float gridHeight = rows * SlotSize + (rows - 1) * SlotGap;
                float width = Mathf.Max(MinPanelWidth, gridWidth + 2f * PaddingX);
                float textWidth = width - 2f * PaddingX;
                strip.sizeDelta = new Vector2(0f, StripHeight);
                if (title)
                {
                    TopLeft(title.rectTransform, PaddingX, (StripHeight - titleHeight) / 2f, textWidth, titleHeight);
                }
                grid.cellSize = new Vector2(SlotSize, SlotSize);
                float y = StripHeight + PartGap;
                container.anchoredPosition = new Vector2(PaddingX, -y);
                container.sizeDelta = new Vector2(gridWidth, gridHeight);
                y += gridHeight + PartGap;
                TopLeft(divider, PaddingX, y, textWidth, DividerHeight);
                y += DividerHeight + DetailsGap;
                float nameHeight = detailsName.GetPreferredValues("A").y;
                // The panel fits the hovered item's details. Its top stays where it would be for the tallest item's,
                // centered on the screen, so the grid keeps its place and only the bottom moves as the item changes.
                float detailsHeight = 0f;
                foreach (PickerButton slot in buttons)
                {
                    float description = detailsDescription.GetPreferredValues(slot.Parts.Description, textWidth, 0f).y;
                    detailsStats.Set(slot.Parts);
                    detailsHeight = Mathf.Max(detailsHeight, description + detailsStats.Layout(0f, 0f, textWidth));
                }
                ItemStatsText.Parts shown = detailsIndex >= 0 && detailsIndex < count ? buttons[detailsIndex].Parts : new ItemStatsText.Parts();
                detailsStats.Set(shown);
                float shownDescription = detailsDescription.GetPreferredValues(shown.Description, textWidth, 0f).y;
                TopLeft(detailsName.rectTransform, PaddingX, y, textWidth, nameHeight);
                y += nameHeight + NameGap;
                TopLeft(detailsDescription.rectTransform, PaddingX, y, textWidth, shownDescription);
                float shownStats = detailsStats.Layout(PaddingX, y + shownDescription, textWidth);
                size = new Vector2(width, y + shownDescription + shownStats + PaddingBottom);
                top = (y + detailsHeight + PaddingBottom) / 2f;
            }
            mainPanel.anchorMin = mainPanel.anchorMax = new Vector2(0.5f, 0.5f);
            mainPanel.pivot = new Vector2(0.5f, 1f);
            mainPanel.anchoredPosition = new Vector2(0f, top);
            mainPanel.sizeDelta = size;
        }

        // As wide as the widest caption on one line needs, at least the canvas's width, and no wider than lets the row
        // fit the screen, measured in the panel's units.
        private float FitCards(int count)
        {
            float caption = 0f;
            foreach (PickerButton card in buttons)
            {
                if (card.Stats != null)
                {
                    caption = Mathf.Max(caption, card.Stats.CaptionWidth);
                }
            }
            float wanted = Mathf.Max(CardWidth, Mathf.Ceil(caption) + 2f * CardPadding);
            float room = float.PositiveInfinity;
            Canvas canvas = mainPanel.GetComponentInParent<Canvas>();
            if (canvas && count > 0 && mainPanel.lossyScale.x > 0f)
            {
                Transform root = canvas.rootCanvas.transform;
                float screen = ((RectTransform)root).rect.width * root.lossyScale.x / mainPanel.lossyScale.x;
                room = (screen - 2f * CardScreenMargin - (count - 1) * CardGap) / count;
            }
            CardsNarrowed = wanted > room;
            return Mathf.Max(CardWidth, Mathf.Min(wanted, room));
        }

        private static float Measure(TMP_Text text, float width) => text ? text.GetPreferredValues(text.text, width, 0f).y : 0f;

        private static Color TierColor(PickupDef pickup) => pickup != null ? pickup.baseColor : Palette.Text;

        private void AddTierStrip(RectTransform slot, PickupDef pickup)
        {
            RectTransform tierStrip = NewImage("TierStrip", slot, TierColor(pickup));
            tierStrip.anchorMin = Vector2.zero;
            tierStrip.anchorMax = new Vector2(1f, 0f);
            tierStrip.pivot = new Vector2(0.5f, 0f);
            tierStrip.sizeDelta = new Vector2(0f, TierStripHeight);
        }

        // Bottom right, just above the tier strip, as on the item bar's slots.
        private TMP_Text AddCount(RectTransform slot)
        {
            TMP_Text count = AddText("Count", slot, CountSize, Color.white);
            count.gameObject.AddComponent<CondensedText>();
            count.fontStyle = FontStyles.Bold;
            count.alignment = TextAlignmentOptions.BottomRight;
            count.margin = new Vector4(0f, 0f, CountInset, TierStripHeight);
            Stretch(count.rectTransform);
            return count;
        }

        private PixelOutline AddOutline(RectTransform holder) => AddOutline(holder, Palette.Accent, OutlineEdge);

        // An outline of this color and edge width over the holder, hidden until shown.
        private PixelOutline AddOutline(RectTransform holder, Color color, float edge)
        {
            PixelOutline outline = PixelOutline.Add(holder, "Outline", color, edge);
            recorder.Add(outline.gameObject);
            return outline;
        }

        // In the game title's font, so the font swap gives it Barlow like the rest of the HUD.
        private TMP_Text AddText(string name, RectTransform holder, float size, Color color)
        {
            RectTransform rect = NewObject(name, holder);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (title)
            {
                text.font = title.font;
                text.fontSharedMaterial = title.fontSharedMaterial;
                text.spriteAsset = title.spriteAsset;
            }
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            return text;
        }

        private StatsSection NewStats(Transform holder) =>
            new StatsSection(holder, title, Canvas, DescriptionSize, made => recorder.Add(made));

        private RectTransform NewImage(string name, Transform holder, Color color)
        {
            RectTransform rect = NewObject(name, holder);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private RectTransform NewObject(string name, Transform holder)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = holder.gameObject.layer;
            created.transform.SetParent(holder, false);
            recorder.Add(created);
            return (RectTransform)created.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // Across the full width of its parent's top, this tall.
        private static void AcrossTop(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        // Placed from its parent's top left corner, x across and y down.
        private static void TopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
