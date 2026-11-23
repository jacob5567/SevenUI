using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SevenUI.PickerPanel;
using static SevenUI.PickerUi;

namespace SevenUI
{
    // The scrappers' exchange strip (J2 design E, Jacob's pick, 2026-11-23): across the panel's bottom, on a darker band
    // under a thin line, it reads like a trade for the hovered option. At the left, the option's icon on a slot, its
    // name in its tier's color (a drone's with its tier badge) and how many of it you hold ("1 of your 2"; for a drone
    // that carries items, that it does); at the right, an arrow, the scrap's icon, its name in its tier's color and how
    // many ("×2" for a drone combined from two).
    internal sealed class PickerExchange
    {
        public const string StripName = "SevenUIPickerExchange";
        // The design canvas's sizes: an 80px band, 14px in from the sides, a 56px slot, the scrap's icon at 44px, the
        // arrow 24px wide, 12px between the parts, and 3px between a name and the line under it.
        internal const float Height = 80f * PickerPanel.Canvas;
        private const float Padding = 14f * PickerPanel.Canvas;
        private const float Slot = 56f * PickerPanel.Canvas;
        private const float ScrapIcon = 44f * PickerPanel.Canvas;
        private const float ArrowSize = 24f * PickerPanel.Canvas;
        private const float Gap = 12f * PickerPanel.Canvas;
        private const float LineGap = 3f * PickerPanel.Canvas;
        private const float NameSize = 17f * PickerPanel.Canvas;
        private const float ScrapNameSize = 15f * PickerPanel.Canvas;
        private const float SubSize = 13f * PickerPanel.Canvas;
        private const string HeldText = "1 of your {0}";
        private const string CarryingText = "Carrying items";
        private const string CountText = "×{0}";

        private readonly PickerPanel owner;
        private readonly PickerUi ui;
        private readonly PickerSource source;

        private RectTransform strip;
        private RectTransform slot;
        private Image icon;
        private Image tierStrip;
        private TMP_Text name;
        private TMP_Text sub;
        private RectTransform arrow;
        private Image scrap;
        private TMP_Text scrapName;
        private TMP_Text scrapCount;
        private int shownIndex = -1;
        private int shownHeld = -1;
        private PickupDef shownScrap;

        public PickerExchange(PickerPanel owner, PickerUi ui, PickerSource source)
        {
            this.owner = owner;
            this.ui = ui;
            this.source = source;
        }

        public RectTransform Strip => strip;

        public TMP_Text NameText => name;

        public TMP_Text SubText => sub;

        public Image ScrapIconImage => scrap;

        public TMP_Text ScrapNameText => scrapName;

        public TMP_Text ScrapCountText => scrapCount;

        public Image OptionIcon => icon;

        public void Clear()
        {
            strip = null;
            shownScrap = null;
            shownIndex = -1;
            shownHeld = -1;
        }

        public void Add()
        {
            strip = ui.NewImage(StripName, ui.Juice, Palette.ExchangeStrip);
            RectTransform line = ui.NewImage("Line", strip, Palette.Divider);
            AcrossTop(line, HudUnits.DividerHeight);
            slot = ui.NewImage("Slot", strip, Palette.ItemSlot);
            icon = ui.NewImage("Icon", slot, Color.white).GetComponent<Image>();
            icon.preserveAspect = true;
            Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(IconInset, IconInset);
            icon.rectTransform.offsetMax = new Vector2(-IconInset, -IconInset);
            tierStrip = ui.NewImage("TierStrip", slot, Palette.Text).GetComponent<Image>();
            tierStrip.rectTransform.anchorMin = Vector2.zero;
            tierStrip.rectTransform.anchorMax = new Vector2(1f, 0f);
            tierStrip.rectTransform.pivot = new Vector2(0.5f, 0f);
            tierStrip.rectTransform.sizeDelta = new Vector2(0f, TierStripHeight);
            name = ui.AddText("Name", strip, NameSize, Palette.Text);
            name.fontStyle = FontStyles.Bold;
            name.enableWordWrapping = false;
            name.overflowMode = TextOverflowModes.Ellipsis;
            sub = ui.AddText("Held", strip, SubSize, Palette.HeaderSubtle);
            sub.enableWordWrapping = false;
            arrow = ui.NewImage("Arrow", strip, Palette.HeaderText);
            arrow.GetComponent<Image>().sprite = FlatIcons.Arrow;
            scrap = ui.NewImage("ScrapIcon", strip, Color.white).GetComponent<Image>();
            scrap.preserveAspect = true;
            scrapName = ui.AddText("ScrapName", strip, ScrapNameSize, Palette.Text);
            scrapName.fontStyle = FontStyles.Bold;
            scrapName.enableWordWrapping = false;
            scrapCount = ui.AddText("ScrapCount", strip, SubSize, Palette.HeaderSubtle);
            scrapCount.enableWordWrapping = false;
            shownIndex = -1;
        }

        // The hovered option's trade, written again when the option or its held count changes (a scrap taking one).
        public void Show(int index, Inventory inventory)
        {
            if (!strip || index < 0 || index >= owner.Buttons.Count) return;
            Exchange exchange = source.ExchangeOf(index);
            int held = exchange != null && exchange.Item != ItemIndex.None && inventory ? inventory.GetItemCountPermanent(exchange.Item) : 0;
            if (index == shownIndex && held == shownHeld)
            {
                // The scrap's icon, and the game's icon on the option's button, load in the background the first time.
                if (!scrap.sprite && shownScrap != null)
                {
                    scrap.sprite = PickupIcons.Get(shownScrap);
                    scrap.enabled = scrap.sprite;
                }
                if (!icon.sprite)
                {
                    ShowIcon(owner.Buttons[index]);
                }
                return;
            }
            shownIndex = index;
            shownHeld = held;
            PickerButton option = owner.Buttons[index];
            PickerEntry entry = option.Entry;
            ShowIcon(option);
            Color tier = entry != null ? entry.Color : Palette.Text;
            tierStrip.color = tier;
            name.color = tier;
            if (entry != null && entry.Drone && exchange != null && DroneTier.HasTier(entry.Drone))
            {
                TierBadge.Attach(name);
                name.text = DroneTier.WithBadge(entry.Name, exchange.DroneUpgrades);
            }
            else
            {
                name.text = entry != null ? entry.Name : "";
            }
            sub.text = exchange == null ? "" : exchange.Item != ItemIndex.None ? string.Format(HeldText, Mathf.Max(held, 1))
                : exchange.CarryingItems ? CarryingText : "";
            PickupDef scrapDef = exchange?.Scrap;
            shownScrap = scrapDef;
            scrap.sprite = PickupIcons.Get(scrapDef);
            scrap.enabled = scrap.sprite;
            scrapName.text = scrapDef != null ? Language.GetString(scrapDef.nameToken) : "";
            scrapName.color = scrapDef != null ? GameColors.OfTier(scrapDef.itemTier, Palette.Text) : Palette.Text;
            scrapCount.text = exchange != null ? string.Format(CountText, exchange.ScrapCount) : "";
            Layout();
        }

        private void ShowIcon(PickerButton option)
        {
            Image optionIcon = option.Icon ? option.Icon.GetComponent<Image>() : null;
            icon.sprite = optionIcon ? optionIcon.sprite : null;
            icon.enabled = icon.sprite;
        }

        // Across the panel at this height from its top, as wide as the panel.
        public void Place(float y, float width)
        {
            if (!strip) return;
            TopLeft(strip, 0f, y, width, Height);
            Layout();
        }

        private void Layout()
        {
            float width = strip.rect.width;
            if (width <= 0f) return;
            TopLeft(slot, Padding, (Height - Slot) / 2f, Slot, Slot);
            // The scrap's side, from the right: its texts as wide as the wider of them, its icon, then the arrow.
            float scrapWidth = Mathf.Max(scrapName.GetPreferredValues(scrapName.text).x, scrapCount.GetPreferredValues(scrapCount.text).x);
            float scrapTextLeft = width - Padding - scrapWidth;
            float iconLeft = scrapTextLeft - Gap / 2f - ScrapIcon;
            float arrowLeft = iconLeft - Gap - ArrowSize;
            Lines(scrapName, scrapCount, scrapTextLeft, scrapWidth);
            TopLeft((RectTransform)scrap.transform, iconLeft, (Height - ScrapIcon) / 2f, ScrapIcon, ScrapIcon);
            TopLeft(arrow, arrowLeft, (Height - ArrowSize) / 2f, ArrowSize, ArrowSize);
            float nameLeft = Padding + Slot + Gap;
            Lines(name, sub, nameLeft, Mathf.Max(0f, arrowLeft - Gap - nameLeft));
        }

        // A name over the line under it, the pair centered on the strip's height.
        private static void Lines(TMP_Text top, TMP_Text bottom, float x, float width)
        {
            float topHeight = top.GetPreferredValues("A").y;
            float bottomHeight = string.IsNullOrEmpty(bottom.text) ? 0f : bottom.GetPreferredValues("A").y;
            float total = topHeight + (bottomHeight > 0f ? LineGap + bottomHeight : 0f);
            float y = (Height - total) / 2f;
            TopLeft(top.rectTransform, x, y, width, topHeight);
            TopLeft(bottom.rectTransform, x, y + topHeight + LineGap, width, bottomHeight);
        }
    }
}
