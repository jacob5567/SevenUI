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
        internal const string MainPanelName = "MainPanel";
        internal const string SevenUIPrefix = "SevenUI";
        internal const string TitleName = "Label";
        internal const string LegendName = "SubmenuLegend";
        internal const string CancelName = "CancelButton";
        internal const string IconName = "Icon";
        internal const string BaseOutlineName = "BaseOutline";
        internal const string HoverOutlineName = "HoverOutline";
        // The parts of a panel's content the flat look keeps; everything else is the panel's own art (its outlines,
        // backgrounds and overlays, which differ from panel to panel, such as the cursed book's stained glass).
        private static readonly string[] KeptParts = { TitleName, LegendName, CancelName };

        // Up to this many options are cards, in one row, with the gamepad navigation set to match; more are a grid in
        // the game's own columns, which differ from panel to panel and which other mods may change (LookingGlass resizes
        // Command's), so the grid keeps the game's navigation.
        public const int CardLimit = 6;

        // The design canvas's sizes, in its pixels, scaled to the HUD's units.
        internal const float Canvas = HudUnits.PerCanvasPixel;
        // The grid: 80px slots 6px apart on a panel at least 300px wide, with a 32px header strip, 14px of padding at
        // the sides and the bottom and 12px between the parts; the details under a 1px line, 10px below it. The
        // canvas's 40px slots were too small to tell the items apart in game.
        public const float SlotSize = 80f * Canvas;
        internal const float SlotGap = 6f * Canvas;
        internal const float MinPanelWidth = 300f * Canvas;
        internal const float StripHeight = 32f * Canvas;
        internal const float PaddingX = 14f * Canvas;
        internal const float PaddingBottom = 14f * Canvas;
        internal const float PartGap = 12f * Canvas;
        internal const float DetailsGap = 10f * Canvas;
        // The cards: at least 196px wide and 8px apart under the title, with 14px of padding and 10px between their
        // parts. Across each card's top is a 92px band in the slot's grey with the item centered on it at 72px and the
        // tier's strip along its foot. They widen to keep LookingGlass's "With this item, you will have:" on one line,
        // as long as the row stays 40px inside the screen's sides.
        public const float CardWidth = 196f * Canvas;
        internal const float CardScreenMargin = 40f * Canvas;
        internal const float CardGap = 8f * Canvas;
        internal const float CardPadding = 14f * Canvas;
        internal const float CardBandHeight = 92f * Canvas;
        internal const float CardIconSize = 72f * Canvas;
        internal const float CardPartGap = 10f * Canvas;
        // Both: a slot's or band's tier strip, a slot's icon margin, the hover outline's edge, the item's name and description, the
        // owned count, and the gap between a name and its description in the details.
        internal const float TierStripHeight = 3f * Canvas;
        internal const float IconInset = 4f * Canvas;
        internal const float OutlineEdge = 2f * Canvas;
        internal const float NameSize = 19f * Canvas;
        internal const float DescriptionSize = 14f * Canvas;
        internal const float CountSize = 15f * Canvas;
        internal const float CountInset = 3f * Canvas;
        internal const float NameGap = 3f * Canvas;
        // The legend's and the cancel button's distance under the panel, and the button's size and text; its resting
        // outline is 1px in the divider's grey.
        internal const float LegendGap = 8f * Canvas;
        internal const float CancelWidth = 120f * Canvas;
        internal const float CancelHeight = 28f * Canvas;
        internal const float CancelTextSize = 13f * Canvas;
        internal const float RestOutlineEdge = 1f * Canvas;

        private readonly PickupPickerPanel panel;
        private readonly HUD hud;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<PickerButton> buttons = new List<PickerButton>();
        private readonly RectTransform mainPanel;
        private readonly RectTransform juice;
        private readonly PickerUi ui;
        private readonly PickerCards cards;
        private readonly PickerGrid grid;
        private readonly PickerCancel cancel;
        private readonly PickerLegend legend;
        private readonly PickerSlots slots;
        private readonly PickerLayout layout;
        private PickupPickerController.Option[] shownOptions;
        private int shownButtonCount = -1;
        private TMP_FontAsset shownFont;

        private PickerPanel(PickupPickerPanel panel, HUD hud, RectTransform mainPanel)
        {
            this.panel = panel;
            this.hud = hud;
            this.mainPanel = mainPanel;
            juice = (RectTransform)panel.buttonContainer.parent;
            Transform titleObject = juice.Find(TitleName);
            TMP_Text title = titleObject ? titleObject.GetComponent<TMP_Text>() : null;
            ui = new PickerUi(this, panel, recorder, juice, title);
            cards = new PickerCards(this, ui);
            grid = new PickerGrid(this, ui);
            cancel = new PickerCancel(this, ui);
            legend = new PickerLegend(ui);
            slots = new PickerSlots(this, ui);
            layout = new PickerLayout(this, ui, cards, grid, slots);
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

        public bool Cards { get; internal set; }

        // The options per row: all of them as cards, the game's columns as a grid.
        public int Columns => grid.Columns;

        // Each card's width, as last laid out.
        public float ShownCardWidth => cards.CardWidthShown;

        // Whether the screen kept the cards narrower than their captions.
        public bool CardsNarrowed => cards.Narrowed;

        public IReadOnlyList<PickerButton> Buttons => buttons;

        // Read by the SevenUI.Tests plugin.
        public PickerButton Cancel => cancel.Button;

        public RectTransform MainPanel => mainPanel;

        public TMP_Text DetailsNameText => slots.DetailsName;

        public TMP_Text DetailsDescriptionText => slots.DetailsDescription;

        internal PickupPickerController.Option[] ShownOptions => shownOptions;

        public void Restore()
        {
            recorder.Restore();
            buttons.Clear();
            shownOptions = null;
            shownButtonCount = -1;
            slots.Clear();
            shownFont = null;
            cancel.Clear();
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
            TMP_FontAsset font = buttons.Count > 0 && buttons[0].Name ? buttons[0].Name.font : slots.DetailsName ? slots.DetailsName.font : null;
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
            int detailsIndex = slots.DetailsIndex;
            bool stillHovered = detailsIndex >= 0 && detailsIndex < buttons.Count && buttons[detailsIndex].Hovered;
            int hovered = newlyHovered >= 0 ? newlyHovered : stillHovered ? detailsIndex : firstHovered;
            if (cancel.Waiting)
            {
                cancel.StyleCancel();
            }
            cancel.Button?.Update(null);
            grid.KeepColumns();
            if (slots.DetailsName)
            {
                slots.ShowDetails(hovered >= 0 ? hovered : detailsIndex >= 0 ? detailsIndex : 0);
            }
        }

        private void Style(PickupPickerController.Option[] options, ReadOnlyCollection<MPButton> elements)
        {
            shownOptions = options;
            shownButtonCount = elements.Count;
            Cards = options.Length <= CardLimit;
            grid.Columns = Cards ? options.Length : grid.GameColumns(options.Length);
            ui.FullDescriptions = PickupDescriptions.Full();
            recorder.RecordRect(mainPanel);

            foreach (Transform child in juice)
            {
                if (child == panel.buttonContainer || child.name.StartsWith(SevenUIPrefix) || System.Array.IndexOf(KeptParts, child.name) >= 0) continue;
                recorder.SetActive(child.gameObject, false);
            }
            if (!Cards)
            {
                slots.AddFrame();
            }
            legend.StyleTitle();
            legend.StyleLegend();
            cancel.StyleCancel();
            grid.StyleGrid();
            for (int i = 0; i < options.Length; i++)
            {
                PickupDef pickup = PickupCatalog.GetPickupDef(options[i].pickup.pickupIndex);
                buttons.Add(Cards ? cards.StyleCard(elements[i], pickup) : slots.StyleSlot(elements[i], pickup));
            }
            if (Cards)
            {
                grid.Navigate(elements, options.Length);
            }
            else
            {
                slots.AddDetails();
            }
            shownFont = null;
        }

        // Sizes the panel and places its parts, from the texts as measured in their current font.
        internal void Layout() => layout.Layout();

        internal RectTransform NewObject(string name, Transform holder)
        {
            RectTransform created = UiFactory.NewObject(name, holder);
            recorder.Add(created.gameObject);
            return created;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
