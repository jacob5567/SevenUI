using System.Collections.ObjectModel;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The item pickers' panel (Command's, a Void Potential's, a Lemurian egg's): the game's PickupPickerPanel itself.
    internal sealed class PickupSource : PickerSource
    {
        private PickupSource(PickupPickerPanel panel, RectTransform mainPanel)
        {
            Panel = panel;
            MainPanel = mainPanel;
            Juice = (RectTransform)panel.buttonContainer.parent;
        }

        public PickupPickerPanel Panel { get; }

        public override Component Component => Panel;

        public override GridLayoutGroup Grid => Panel.gridlayoutGroup;

        public override RectTransform ButtonContainer => Panel.buttonContainer;

        public override int MaxColumnCount => Panel.maxColumnCount;

        public override bool AutomaticNavigation => Panel.automaticButtonNavigation;

        // Only the game's own panel class, laid out as the plain pickers are, with the buttons straight under the
        // panel's content; the item scrapper's scrolling list and the panels of their own classes stay as the game made
        // them.
        public static PickupSource For(PickupPickerPanel panel)
        {
            if (!panel || panel.GetType() != typeof(PickupPickerPanel) || !panel.buttonContainer) return null;
            Transform mainPanel = panel.transform.Find(PickerPanel.MainPanelName);
            Transform content = panel.buttonContainer.parent;
            if (!mainPanel || !content || content.parent != mainPanel || !panel.gridlayoutGroup) return null;
            return new PickupSource(panel, (RectTransform)mainPanel);
        }

        public override bool TryGetOptions(out object options, out int count, out ReadOnlyCollection<MPButton> elements)
        {
            PickupPickerController.Option[] given = Panel.pickupOptions;
            options = given;
            elements = Panel.buttonAllocator?.elements;
            count = given != null ? given.Length : 0;
            return given != null && elements != null && elements.Count >= given.Length;
        }

        public override PickerEntry Entry(int index)
        {
            PickupPickerController.Option[] given = Panel.pickupOptions;
            return PickerEntry.ForPickup(PickupCatalog.GetPickupDef(given[index].pickup.pickupIndex));
        }
    }
}
