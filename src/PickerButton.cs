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
        // The game's padlock picture (PickupPickerPanel's Textures/MiscIcons/texUnlockIcon).
        private const string LockSpriteName = "texUnlockIcon";

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
        // The void corner shown while a held void item would corrupt the option, and the line under its description:
        // "Corrupted by Polylute", or for a void item "Corrupts your 2 Ukulele" (I2). Empty when nothing held ties to it.
        public Image CorruptMark;
        public string CorruptionLine { get; private set; } = "";
        private int shownInto = -1;
        private int shownOriginals = -1;
        public TMP_Text Name;
        public TMP_Text Description;
        public TMP_Text Key;

        private int shownCount = -1;
        private bool shownLocked;
        // The game's icon on the option, which it swaps for a padlock when the option can't be picked.
        private Image iconImage;
        private Sprite seenSprite;
        private bool locked;
        // LookingGlass's own count on the option, found once it exists.
        public Transform LookingGlassCount;
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
            FindCorruption();
            if (corruptedInto != ItemIndex.None)
            {
                int held = inventory.GetItemCountPermanent(corruptedInto);
                if (held > 0)
                {
                    color = Palette.CountVoid;
                    return held;
                }
            }
            int originals = 0;
            foreach (ItemIndex original in corrupts)
            {
                originals += inventory.GetItemCountPermanent(original);
            }
            color = corruptsColor;
            return originals;
        }

        // The void item the option would turn into, and the items a void option would corrupt, found once.
        private void FindCorruption()
        {
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
                corruptsColor = first ? GameColors.OfTier(first.tier, Palette.Text) : Palette.Text;
            }
        }

        // The corner and the line (Corruption), built again only when the held counts behind them change. Reads only.
        private void UpdateCorruption(Inventory inventory)
        {
            ItemIndex item = Pickup != null ? Pickup.itemIndex : ItemIndex.None;
            Corruption.Counts(item, inventory, out int into, out int originals);
            if (into == shownInto && originals == shownOriginals) return;
            shownInto = into;
            shownOriginals = originals;
            CorruptionLine = Corruption.Line(item, inventory);
            if (CorruptMark && CorruptMark.enabled != into > 0)
            {
                CorruptMark.enabled = into > 0;
            }
            CorruptionChanged = true;
        }

        // The description as the card or the details show it: with the corruption line under it, if any.
        // LookingGlass's own note, when its full description has one, already says it, so the line isn't added again.
        public bool LookingGlassNote;
        public string ShownDescription => CorruptionLine.Length > 0 && !LookingGlassNote
            ? Parts.Description + "\n" + CorruptionLine
            : Parts.Description;

        // Set when the line changes, for the panel to show it again; the panel clears it.
        public bool CorruptionChanged;


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
            if (Icon && !iconImage)
            {
                iconImage = Icon.GetComponent<Image>();
            }
            Sprite sprite = iconImage ? iconImage.sprite : null;
            if (sprite != seenSprite)
            {
                seenSprite = sprite;
                locked = sprite && sprite.name == LockSpriteName;
            }
            UpdateCorruption(inventory);
            if (Count)
            {
                // Permanent stacks only, as LookingGlass counts them in its own picker count and stats (Jacob's call,
                // 2026-11-22): a temporary or Ethereal stack isn't counted, though the item bar counts it.
                // None on an option behind the game's padlock, as LookingGlass shows none there: the padlock hides
                // which item it is.
                int count = !locked && inventory && Pickup != null && Pickup.itemIndex != ItemIndex.None
                    ? inventory.GetItemCountPermanent(Pickup.itemIndex)
                    : 0;
                Color color = Color.white;
                int corrupting = count == 0 && !locked ? Corrupting(inventory, out color) : 0;
                if (count != shownCount || corrupting != shownCorrupting || locked != shownLocked)
                {
                    shownCount = count;
                    shownCorrupting = corrupting;
                    shownLocked = locked;
                    if (corrupting > 0)
                    {
                        // None of the item held, but void items tie it to a stack: the count in the color of what is held.
                        Count.text = corrupting.ToString();
                        Count.color = color;
                    }
                    else
                    {
                        // From one up, as LookingGlass's own count ("x1") shows it, unlike the item bar, which counts only
                        // two or more (Jacob's call, 2026-11-22).
                        Count.text = count > 0 ? count.ToString() : "";
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
