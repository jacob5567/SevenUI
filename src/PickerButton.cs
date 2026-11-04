using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // One option's button in a restyled pick-an-item panel and the parts the mod added to it, which PickerPanel lays out
    // as a card or a grid slot and keeps up to date: the hover outline, the owned count and the dimmed text of an
    // option that is no longer available.
    internal sealed class PickerButton
    {
        // The share of its color an unavailable option's text keeps, as on the design canvas.
        private const float UnavailableAlpha = 0.4f;

        public MPButton Button;
        public HGButton Hover;
        public PickupDef Pickup;
        // The option's description as the panel shows it (PickupDescriptions), chosen once when it is styled.
        public string DescriptionText = "";
        // That description split from LookingGlass's stats, which a card shows in its own section under it.
        public ItemStatsText.Parts Parts = new ItemStatsText.Parts();
        public StatsSection Stats;
        public RectTransform Icon;
        public Image Outline;
        // An outlined button's resting outline and label, which the hover outline replaces and lights.
        public Image RestOutline;
        public TMP_Text Label;
        public TMP_Text Count;
        public TMP_Text Name;
        public TMP_Text Description;
        public TMP_Text Key;

        private int shownCount = -1;
        private bool shownAvailable = true;
        private bool wasHovered;

        // Hovered with the pointer, or selected with a gamepad: the game's own state, which it shows by fading in its
        // hover outline.
        public bool Hovered => Hover && Hover.hovering;

        // Hovered this frame and not the one before, as of the last Update.
        public bool JustHovered { get; private set; }

        public bool Available => Button && Button.interactable;

        // A few reads each frame; text and colors change only when what they show did.
        public void Update(Inventory inventory)
        {
            if (!Button) return;
            bool hovering = Hovered;
            JustHovered = hovering && !wasHovered;
            wasHovered = hovering;
            if (Outline)
            {
                bool hovered = Hovered;
                if (Outline.enabled != hovered)
                {
                    Outline.enabled = hovered;
                    if (RestOutline)
                    {
                        RestOutline.enabled = !hovered;
                    }
                    if (Label)
                    {
                        Label.color = hovered ? Palette.Text : Palette.HeaderText;
                    }
                }
            }
            if (Count)
            {
                int count = inventory && Pickup != null && Pickup.itemIndex != ItemIndex.None ? inventory.GetItemCountTotal(Pickup.itemIndex) : 0;
                if (count != shownCount)
                {
                    shownCount = count;
                    // As in the item bar: a count only for two or more.
                    Count.text = count > 1 ? count.ToString() : "";
                }
            }
            bool available = Available;
            if (available != shownAvailable)
            {
                shownAvailable = available;
                float alpha = available ? 1f : UnavailableAlpha;
                foreach (TMP_Text text in new[] { Name, Description, Key })
                {
                    if (text)
                    {
                        text.alpha = alpha;
                    }
                }
            }
        }
    }
}
