using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SevenUI.PickerPanel;
using static SevenUI.PickerUi;

namespace SevenUI
{
    // Sizes a picker panel and places its parts, whether cards or a grid of slots, from the texts as measured in their
    // current font.
    internal sealed class PickerLayout
    {
        private readonly PickerPanel owner;
        private readonly PickerUi ui;
        private readonly PickerCards cards;
        private readonly PickerGrid pickerGrid;
        private readonly PickerSlots slots;

        // What the tallest-details measurement was made from. Hovering changes none of it, so hovers reuse the result.
        private PickupPickerController.Option[] measuredOptions;
        private int measuredCount = -1;
        private float measuredTextWidth;
        private TMP_FontAsset measuredFont;
        private bool measuredFull;
        private float tallestDetails;

        public PickerLayout(PickerPanel owner, PickerUi ui, PickerCards cards, PickerGrid pickerGrid, PickerSlots slots)
        {
            this.owner = owner;
            this.ui = ui;
            this.cards = cards;
            this.pickerGrid = pickerGrid;
            this.slots = slots;
        }

        // The tallest description plus stats over all options, measured again only when the options, the text width, the
        // font or the short/full setting change. Measuring is costly and Command has 100+ options.
        private float TallestDetails(float textWidth)
        {
            var buttons = owner.Buttons;
            TMP_FontAsset font = slots.DetailsDescription.font;
            bool full = ui.FullDescriptions;
            if (owner.ShownOptions == measuredOptions && buttons.Count == measuredCount && textWidth == measuredTextWidth
                && font == measuredFont && full == measuredFull)
            {
                return tallestDetails;
            }
            float tallest = 0f;
            foreach (PickerButton slot in buttons)
            {
                float description = slots.DetailsDescription.GetPreferredValues(slot.Parts.Description, textWidth, 0f).y;
                slots.DetailsStats.Set(slot.Parts);
                tallest = Mathf.Max(tallest, description + slots.DetailsStats.Layout(0f, 0f, textWidth));
            }
            measuredOptions = owner.ShownOptions;
            measuredCount = buttons.Count;
            measuredTextWidth = textWidth;
            measuredFont = font;
            measuredFull = full;
            tallestDetails = tallest;
            return tallest;
        }

        // Sizes the panel and places its parts, from the texts as measured in their current font.
        public void Layout()
        {
            if (!owner.Styled) return;
            var buttons = owner.Buttons;
            RectTransform mainPanel = owner.MainPanel;
            var title = ui.Title;
            int columns = pickerGrid.Columns;
            int count = buttons.Count;
            float titleHeight = title ? title.GetPreferredValues("A").y : 0f;
            RectTransform container = ui.Panel.buttonContainer;
            GridLayoutGroup grid = ui.Panel.gridlayoutGroup;
            Vector2 size;
            // How far above the screen's middle the panel's top sits.
            float top;
            if (owner.Cards)
            {
                float cardWidth = cards.FitCards(count);
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
                slots.Strip.sizeDelta = new Vector2(0f, StripHeight);
                if (title)
                {
                    TopLeft(title.rectTransform, PaddingX, (StripHeight - titleHeight) / 2f, textWidth, titleHeight);
                }
                grid.cellSize = new Vector2(SlotSize, SlotSize);
                float y = StripHeight + PartGap;
                container.anchoredPosition = new Vector2(PaddingX, -y);
                container.sizeDelta = new Vector2(gridWidth, gridHeight);
                y += gridHeight + PartGap;
                TopLeft(slots.Divider, PaddingX, y, textWidth, HudUnits.DividerHeight);
                y += HudUnits.DividerHeight + DetailsGap;
                float nameHeight = slots.DetailsName.GetPreferredValues("A").y;
                // The panel fits the hovered item's details. Its top stays where it would be for the tallest item's,
                // centered on the screen, so the grid keeps its place and only the bottom moves as the item changes.
                float detailsHeight = TallestDetails(textWidth);
                int detailsIndex = slots.DetailsIndex;
                ItemStatsText.Parts shown = detailsIndex >= 0 && detailsIndex < count ? buttons[detailsIndex].Parts : new ItemStatsText.Parts();
                slots.DetailsStats.Set(shown);
                float shownDescription = slots.DetailsDescription.GetPreferredValues(shown.Description, textWidth, 0f).y;
                TopLeft(slots.DetailsName.rectTransform, PaddingX, y, textWidth, nameHeight);
                y += nameHeight + NameGap;
                TopLeft(slots.DetailsDescription.rectTransform, PaddingX, y, textWidth, shownDescription);
                float shownStats = slots.DetailsStats.Layout(PaddingX, y + shownDescription, textWidth);
                size = new Vector2(width, y + shownDescription + shownStats + PaddingBottom);
                top = (y + detailsHeight + PaddingBottom) / 2f;
            }
            mainPanel.anchorMin = mainPanel.anchorMax = new Vector2(0.5f, 0.5f);
            mainPanel.pivot = new Vector2(0.5f, 1f);
            mainPanel.anchoredPosition = new Vector2(0f, top);
            mainPanel.sizeDelta = size;
        }
    }
}
