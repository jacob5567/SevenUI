using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using static SevenUI.PickerPanel;
using static SevenUI.PickerUi;

namespace SevenUI
{
    // A picker panel's cards: each option's card with its band, name and description, and the card width that fits the
    // captions and the screen.
    internal sealed class PickerCards
    {
        private readonly PickerPanel owner;
        private readonly PickerUi ui;

        public PickerCards(PickerPanel owner, PickerUi ui)
        {
            this.owner = owner;
            this.ui = ui;
        }

        // Each card's width, as last laid out.
        public float CardWidthShown { get; private set; } = CardWidth;

        // Whether the screen kept the cards narrower than their captions.
        public bool Narrowed { get; private set; }

        public PickerButton StyleCard(MPButton button, PickupDef pickup)
        {
            PickerButton card = ui.StyleButton(button, pickup, Palette.ScoreboardPanel);
            // Behind the icon: the band it sits on, across the card's top.
            RectTransform band = ui.NewImage(SlotName, button.transform, Palette.ItemSlot);
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
            RectTransform holder = owner.NewObject(CardName, button.transform);
            Stretch(holder);
            RectTransform bandParts = owner.NewObject("Band", holder);
            AcrossTop(bandParts, CardBandHeight);
            ui.AddTierStrip(bandParts, pickup);
            card.Count = ui.AddCount(bandParts);
            card.Name = ui.AddText("Name", holder, NameSize, Palette.Text);
            card.Name.fontStyle = FontStyles.Bold;
            card.Name.text = pickup != null ? Language.GetString(pickup.nameToken) : "";
            card.Description = ui.AddText("Description", holder, DescriptionSize, Palette.HeaderText);
            card.Description.text = card.Parts.Description;
            card.Stats = ui.NewStats(holder);
            card.Stats.Set(card.Parts);
            card.Outline = ui.AddOutline(holder);
            return card;
        }

        // As wide as the widest caption on one line needs, at least the canvas's width, and no wider than lets the row
        // fit the screen, measured in the panel's units. Remembered as the cards' width.
        public float FitCards(int count)
        {
            float caption = 0f;
            foreach (PickerButton card in owner.Buttons)
            {
                if (card.Stats != null)
                {
                    caption = Mathf.Max(caption, card.Stats.CaptionWidth);
                }
            }
            float wanted = Mathf.Max(CardWidth, Mathf.Ceil(caption) + 2f * CardPadding);
            float room = float.PositiveInfinity;
            RectTransform mainPanel = owner.MainPanel;
            Canvas canvas = mainPanel.GetComponentInParent<Canvas>();
            if (canvas && count > 0 && mainPanel.lossyScale.x > 0f)
            {
                Transform root = canvas.rootCanvas.transform;
                float screen = ((RectTransform)root).rect.width * root.lossyScale.x / mainPanel.lossyScale.x;
                room = (screen - 2f * CardScreenMargin - (count - 1) * CardGap) / count;
            }
            Narrowed = wanted > room;
            CardWidthShown = Mathf.Max(CardWidth, Mathf.Min(wanted, room));
            return CardWidthShown;
        }
    }
}
