using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using static SevenUI.PickerPanel;
using static SevenUI.PickerUi;

namespace SevenUI
{
    // A picker panel's grid mode: the item slots, the backdrop and header strip behind them, and the details under them
    // that follow the hovered item.
    internal sealed class PickerSlots
    {
        private readonly PickerPanel owner;
        private readonly PickerUi ui;

        public PickerSlots(PickerPanel owner, PickerUi ui)
        {
            this.owner = owner;
            this.ui = ui;
        }

        public RectTransform Strip { get; private set; }

        public RectTransform Divider { get; private set; }

        public TMP_Text DetailsName { get; private set; }

        public TMP_Text DetailsDescription { get; private set; }

        public StatsSection DetailsStats { get; private set; }

        public int DetailsIndex { get; private set; } = -1;

        public void Clear()
        {
            Strip = null;
            Divider = null;
            DetailsName = null;
            DetailsDescription = null;
            DetailsStats = null;
            DetailsIndex = -1;
        }

        // The flat panel behind the slots, and the header strip along its top.
        public void AddFrame()
        {
            RectTransform backdrop = ui.NewImage(BackdropName, ui.Juice, Palette.ScoreboardPanel);
            backdrop.SetAsFirstSibling();
            Stretch(backdrop);
            Strip = ui.NewImage("SevenUIPickerStrip", ui.Juice, CanvasBlend.Over(Palette.HeaderStrip, ScoreboardStyler.CanvasBoard));
            Strip.SetSiblingIndex(1);
            Strip.anchorMin = new Vector2(0f, 1f);
            Strip.anchorMax = Vector2.one;
            Strip.pivot = new Vector2(0.5f, 1f);
        }

        public PickerButton StyleSlot(MPButton button, PickupDef pickup)
        {
            PickerButton slot = ui.StyleButton(button, pickup, Palette.ItemSlot);
            if (slot.Icon)
            {
                Stretch(slot.Icon);
                slot.Icon.offsetMin = new Vector2(IconInset, IconInset);
                slot.Icon.offsetMax = new Vector2(-IconInset, -IconInset);
            }
            RectTransform holder = owner.NewObject(SlotName, button.transform);
            Stretch(holder);
            ui.AddTierStrip(holder, pickup);
            slot.Count = ui.AddCount(holder);
            slot.Outline = ui.AddOutline(holder);
            return slot;
        }

        // Under the grid: the hovered item's name in its tier's color and its description, the first item's until one
        // is hovered, as tall as the longest so the panel keeps its size.
        public void AddDetails()
        {
            RectTransform details = owner.NewObject(PickerPanel.DetailsName,ui.Juice);
            Stretch(details);
            // In the grey of the cards' line over their stats; the scoreboard's darker row line barely showed here.
            Divider = ui.NewImage("Divider", details, Palette.Divider);
            DetailsName = ui.AddText("Name", details, NameSize, Palette.Text);
            DetailsName.fontStyle = FontStyles.Bold;
            DetailsDescription = ui.AddText("Description", details, DescriptionSize, Palette.HeaderText);
            DetailsStats = ui.NewStats(details);
            DetailsIndex = -1;
        }

        public void ShowDetails(int index)
        {
            if (index == DetailsIndex || index >= owner.Buttons.Count) return;
            DetailsIndex = index;
            PickupDef pickup = owner.Buttons[index].Pickup;
            DetailsName.text = pickup != null ? Language.GetString(pickup.nameToken) : "";
            DetailsName.color = TierColor(pickup);
            DetailsDescription.text = owner.Buttons[index].Parts.Description;
            DetailsStats.Set(owner.Buttons[index].Parts);
            owner.Layout();
        }
    }
}
