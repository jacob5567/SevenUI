using System.Collections.Generic;
using RoR2;
using RoR2.Items;
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
        public PickerEntry Entry;
        // The entry's item, equipment or drone pickup; null for a drone or Cancel.
        public PickupDef Pickup;
        // The option's description as the panel shows it (PickupDescriptions), chosen once when it is styled.
        public string DescriptionText = "";
        // That description split from LookingGlass's stats, which a card shows in its own section under it.
        public ItemStatsText.Parts Parts = new ItemStatsText.Parts();
        public StatsSection Stats;
        public RectTransform Icon;
        public PixelOutline Outline;
        // An outlined button's resting outline and label, which the hover outline replaces and lights.
        public PixelOutline RestOutline;
        public TMP_Text Label;
        public TMP_Text Count;
        public TMP_Text Name;
        public TMP_Text Description;
        public TMP_Text Key;

        private int shownCount = -1;
        private int shownCorrupting = -1;
        // The items that corrupt this one (the void item it turns into) or that it corrupts (for a void item), found
        // the first time the count is shown.
        private ItemIndex corruptedInto = ItemIndex.None;
        private List<ItemIndex> corrupts;
        private Color corruptsColor;
        private bool shownAvailable = true;
        private bool wasHovered;

        // How many held items are tied to this one by void corruption, as LookingGlass's own picker count shows it (which
        // SevenUI hides): of the void item that would corrupt it, in the void color, or, for a void item, of the items
        // it would corrupt, in their tier's color. 0 when none are held.
        private int Corrupting(Inventory inventory, out Color color)
        {
            color = Color.white;
            if (!inventory || Pickup == null || Pickup.itemIndex == ItemIndex.None) return 0;
            if (corrupts == null)
            {
                corruptedInto = ContagiousItemManager.GetTransformedItemIndex(Pickup.itemIndex);
                corrupts = new List<ItemIndex>();
                foreach (ContagiousItemManager.TransformationInfo info in ContagiousItemManager.transformationInfos)
                {
                    if (info.transformedItem == Pickup.itemIndex)
                    {
                        corrupts.Add(info.originalItem);
                    }
                }
                ItemDef first = corrupts.Count > 0 ? ItemCatalog.GetItemDef(corrupts[0]) : null;
                ItemTierDef tier = first ? ItemTierCatalog.GetItemTierDef(first.tier) : null;
                corruptsColor = tier ? GameColors.ToCanvas(ColorCatalog.GetColor(tier.colorIndex)) : Palette.Text;
            }
            if (corruptedInto != ItemIndex.None)
            {
                int held = inventory.GetItemCountTotal(corruptedInto);
                if (held > 0)
                {
                    color = Palette.CountVoid;
                    return held;
                }
            }
            int originals = 0;
            foreach (ItemIndex original in corrupts)
            {
                originals += inventory.GetItemCountTotal(original);
            }
            color = corruptsColor;
            return originals;
        }

        // Hovered with the pointer, or selected with a gamepad: the game's own state, which it shows by fading in its
        // hover outline.
        public bool Hovered
        {
            get
            {
                if (Hover) return Hover.hovering;
                // A plain MPButton (the drone scrapper's) has no hover state of its own; the pointer selects it as it
                // enters, and a gamepad selects it too.
                MPEventSystem system = Button ? Button.eventSystem : null;
                return system && Button.interactable && system.currentSelectedGameObject == Button.gameObject;
            }
        }

        // Hovered this frame and not the one before, as of the last Update.
        public bool JustHovered { get; private set; }

        public bool Available => Button && Button.interactable;

        // A few reads each frame; text and colors change only when what they show did.
        public void Update(Inventory inventory)
        {
            if (!Button) return;
            bool hovered = Hovered;
            JustHovered = hovered && !wasHovered;
            wasHovered = hovered;
            if (Outline)
            {
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
                Color color = Color.white;
                int corrupting = count == 0 ? Corrupting(inventory, out color) : 0;
                if (count != shownCount || corrupting != shownCorrupting)
                {
                    shownCount = count;
                    shownCorrupting = corrupting;
                    if (corrupting > 0)
                    {
                        // None of the item held, but void items tie it to a stack: the count in the color of what is held.
                        Count.text = corrupting.ToString();
                        Count.color = color;
                    }
                    else
                    {
                        // As in the item bar: a count only for two or more.
                        Count.text = count > 1 ? count.ToString() : "";
                        Count.color = Color.white;
                    }
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
