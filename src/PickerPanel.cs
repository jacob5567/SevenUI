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
    // One pick-an-item panel (Command's, a Void Potential's, a Lemurian egg's, the Drone Scrapper's) as on the design canvas. A few options
    // become cards side by side, each with its item's name and description always shown; more become a grid of item
    // slots on a flat panel, with the hovered item's name and description under it. The game's buttons stay the
    // buttons, laid out by the game's grid with the game's navigation, so choosing works as before; only their look and
    // the panel around them change. With no options at all (an egg with no items to give) the panel shows a short
    // message in place of the grid. The game's spinning outlines and tinted backgrounds are hidden, its title becomes the
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
        // LookingGlass's count of the option held, which it adds to each option's button.
        internal const string LookingGlassCountName = "LG_Quantity";
        internal const string BaseOutlineName = "BaseOutline";
        internal const string HoverOutlineName = "HoverOutline";
        // The parts of a panel's content the flat look keeps; everything else is the panel's own art (its outlines,
        // backgrounds and overlays, which differ from panel to panel, such as the cursed book's stained glass).
        private static readonly string[] KeptParts = { TitleName, LegendName, CancelName };

        // Up to this many options are cards, in one row, with the gamepad navigation set to match; more are a grid in
        // the game's own columns, which differ from panel to panel and which other mods may change (LookingGlass resizes
        // Command's), so the grid keeps the game's navigation.
        public const int CardLimit = 6;
        public const string EmptyMessageName = "SevenUIPickerEmpty";
        public const string EmptyMessageText = "No items to offer";

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
        // The Drone Scrapper's scroll view shows at most this much of the grid.
        internal const float MaxScrollHeight = 360f * Canvas;
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
        // The void corner on an option a held void item would corrupt (I2).
        internal const float CorruptMarkSize = 14f * Canvas;
        internal const float NameGap = 3f * Canvas;
        // The legend's and the cancel button's distance under the panel, and the button's size and text; its resting
        // outline is 1px in the divider's grey.
        internal const float LegendGap = 8f * Canvas;
        internal const float CancelWidth = 120f * Canvas;
        internal const float CancelHeight = 28f * Canvas;
        internal const float CancelTextSize = 13f * Canvas;
        internal const float RestOutlineEdge = 1f * Canvas;

        private readonly PickerSource source;
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
        private object shownOptions;
        private int shownOptionCount;
        private int shownButtonCount = -1;
        private TMP_FontAsset shownFont;

        private PickerPanel(PickerSource source, HUD hud)
        {
            this.source = source;
            this.hud = hud;
            mainPanel = source.MainPanel;
            juice = source.Juice;
            Transform titleObject = juice.Find(TitleName);
            TMP_Text title = titleObject ? titleObject.GetComponent<TMP_Text>() : null;
            ui = new PickerUi(this, source, recorder, juice, title);
            cards = new PickerCards(this, ui);
            grid = new PickerGrid(this, ui);
            cancel = new PickerCancel(this, ui);
            legend = new PickerLegend(ui);
            slots = new PickerSlots(this, ui);
            layout = new PickerLayout(this, ui, cards, grid, slots);
        }

        public static PickerPanel For(PickupPickerPanel panel, HUD hud)
        {
            PickupSource source = PickupSource.For(panel);
            return source != null ? new PickerPanel(source, hud) : null;
        }

        public static PickerPanel For(DroneScrapperPickerPanel panel, HUD hud)
        {
            DroneSource source = DroneSource.For(panel);
            return source != null ? new PickerPanel(source, hud) : null;
        }

        public bool Alive => source.Alive && mainPanel;

        // The game's panel, whichever class it is.
        public Component Component => source.Component;

        // The item panel's own class; null for the drone scrapper's.
        public PickupPickerPanel Panel => (source as PickupSource)?.Panel;

        public bool Styled => shownOptions != null;

        public bool Cards { get; internal set; }

        // No options to pick from: the panel shows a message and its Cancel button, with no grid.
        public bool Empty { get; private set; }

        public TMP_Text EmptyMessage { get; private set; }

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

        // The game's title, the header. Read by the SevenUI.Tests plugin.
        public TMP_Text Title => ui.Title;

        internal PickerSource Source => source;

        public TMP_Text DetailsNameText => slots.DetailsName;

        public TMP_Text DetailsDescriptionText => slots.DetailsDescription;

        internal object ShownOptions => shownOptions;

        internal int ShownOptionCount => shownOptionCount;

        public void Restore()
        {
            recorder.Restore();
            buttons.Clear();
            shownOptions = null;
            shownOptionCount = 0;
            shownButtonCount = -1;
            Empty = false;
            EmptyMessage = null;
            slots.Clear();
            shownFont = null;
            cancel.Clear();
            source.Restored();
        }

        // Every frame while the panel is open: restyles it once the game has made its buttons, again if the game gives
        // it new options, and otherwise reads each button's hover and availability.
        public void Update()
        {
            if (!Alive) return;
            if (!source.TryGetOptions(out object options, out int count, out ReadOnlyCollection<MPButton> elements)) return;
            if (options != shownOptions || elements.Count != shownButtonCount)
            {
                if (Styled)
                {
                    Restore();
                }
                Style(options, count, elements);
            }
            source.KeepShown();
            cancel.Keep();
            // The font swap gives the texts Barlow after they are made, which measures differently.
            TMP_FontAsset font = buttons.Count > 0 && buttons[0].Name ? buttons[0].Name.font
                : slots.DetailsName ? slots.DetailsName.font : EmptyMessage ? EmptyMessage.font : null;
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
                HideLookingGlassCount(buttons[i]);
                if (buttons[i].CorruptionChanged)
                {
                    buttons[i].CorruptionChanged = false;
                    ShowDescriptionAgain(buttons[i], i);
                }
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
            if (!Empty)
            {
                grid.KeepColumns();
            }
            if (slots.DetailsName)
            {
                slots.ShowDetails(hovered >= 0 ? hovered : detailsIndex >= 0 ? detailsIndex : 0);
            }
        }

        // The option's description with its corruption line, on its card or in the details if they show it.
        private void ShowDescriptionAgain(PickerButton button, int index)
        {
            if (button.Description)
            {
                button.Description.text = button.ShownDescription;
                Layout();
            }
            if (slots.DetailsName && slots.DetailsIndex == index)
            {
                slots.ShowDetailsAgain();
            }
        }

        // LookingGlass's own count ("x2"), which the option's count already shows. LookingGlass makes it and turns it on
        // again each time the game sets the options, which can be after the panel was styled, so it is hidden whenever it
        // shows. A Find a frame only until it exists.
        private void HideLookingGlassCount(PickerButton button)
        {
            if (!button.Button) return;
            if (!button.LookingGlassCount)
            {
                button.LookingGlassCount = button.Button.transform.Find(LookingGlassCountName);
                if (!button.LookingGlassCount) return;
            }
            GameObject count = button.LookingGlassCount.gameObject;
            if (count.activeSelf)
            {
                recorder.SetActive(count, false);
            }
        }

        private void Style(object options, int count, ReadOnlyCollection<MPButton> elements)
        {
            shownOptions = options;
            shownOptionCount = count;
            shownButtonCount = elements.Count;
            Empty = count == 0;
            Cards = !Empty && !source.AlwaysGrid && count <= CardLimit;
            grid.Columns = Cards ? count : grid.GameColumns(count);
            ui.FullDescriptions = PickupDescriptions.Full();
            recorder.RecordRect(mainPanel);
            source.Prepare(recorder);

            foreach (Transform child in juice)
            {
                if (child == source.ContentRoot || child.name.StartsWith(SevenUIPrefix) || System.Array.IndexOf(KeptParts, child.name) >= 0) continue;
                recorder.SetActive(child.gameObject, false);
            }
            if (!Cards)
            {
                slots.AddFrame();
            }
            legend.StyleTitle();
            legend.StyleLegend();
            cancel.StyleCancel();
            if (Empty)
            {
                // The game's grid has nothing in it, and would still take its place in the panel.
                recorder.SetActive(source.ContentRoot.gameObject, false);
                EmptyMessage = ui.AddText(EmptyMessageName, ui.Juice, DescriptionSize, Palette.HeaderText);
                EmptyMessage.text = EmptyMessageText;
                shownFont = null;
                return;
            }
            grid.StyleGrid();
            for (int i = 0; i < count; i++)
            {
                PickerEntry entry = source.Entry(i);
                buttons.Add(Cards ? cards.StyleCard(elements[i], entry) : slots.StyleSlot(elements[i], entry));
                if (!Cards)
                {
                    source.HideSelection(elements[i]);
                }
            }
            if (Cards)
            {
                grid.Navigate(elements, count);
            }
            else
            {
                slots.AddDetails();
            }
            shownFont = null;
        }

        // Sizes the panel and places its parts, from the texts as measured in their current font.
        internal void Layout()
        {
            layout.Layout();
            cancel.Align();
        }

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
