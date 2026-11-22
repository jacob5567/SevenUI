using System;
using System.Collections.ObjectModel;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SevenUI
{
    // The item pickers' panel (Command's, a Void Potential's, a Lemurian egg's): the game's PickupPickerPanel itself.
    internal sealed class PickupSource : PickerSource
    {
        // The panels that take the chosen item from the player's inventory, by their prefab's name: the scrapper's,
        // the Lemurian egg's, the rebirth shrine's, the Solus vendor's, the meal prep's and the acolyte unlock's.
        private static readonly string[] TakingPanels =
        {
            "ScrapperPickerPanel", "LemurianEggPickerPanel", "RebirthPickerPanel", "SolusVendorShrinePickerPanel",
            "MealPrepPickerPanel", "UnlockableAcolytePickerPanel",
        };
        // Delusion's guess at the item a chest gave, which is held already: right, it stays; wrong, it goes.
        private const string DelusionPanel = "DelusionOptionPickerPanel";
        // The game's UnityEvent call that fills a panel's options from the interactor's inventory.
        private const string OptionsFromInventory = nameof(PickupPickerController.SetOptionsFromInteractor);

        private StackChange? change;

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
            UniquePickup pickup = given[index].pickup;
            change ??= ChangeOf(Panel);
            return PickerEntry.ForPickup(PickupCatalog.GetPickupDef(pickup.pickupIndex), change.Value);
        }

        // What a pick does to the player's stack: the panel's name says it for the game's panels; any other panel takes
        // the item when its controller fills its options from the interactor's inventory (as the game's own taking
        // panels do, through their prefab's interaction event or, for the Solus vendor and the acolyte unlock, in
        // code), and otherwise gives it. Read only: the event's persistent calls are read, never invoked.
        private static StackChange ChangeOf(PickupPickerPanel panel)
        {
            string name = panel.name;
            if (name.StartsWith(DelusionPanel, StringComparison.Ordinal)) return StackChange.None;
            foreach (string taking in TakingPanels)
            {
                if (name.StartsWith(taking, StringComparison.Ordinal)) return StackChange.Lose;
            }
            PickupPickerController controller = panel.pickerController;
            if (!controller) return StackChange.Gain;
            if (controller.isDelusionChoice) return StackChange.None;
            if (controller is AcolyteUnlockPickerController || controller.GetComponent<SolusVendorShrineController>()) return StackChange.Lose;
            UnityEventBase interaction = controller.onServerInteractionBegin;
            for (int i = 0; interaction != null && i < interaction.GetPersistentEventCount(); i++)
            {
                if (interaction.GetPersistentMethodName(i) == OptionsFromInventory) return StackChange.Lose;
            }
            return StackChange.Gain;
        }
    }
}
