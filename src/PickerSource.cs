using System.Collections.ObjectModel;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The game's panel behind a PickerPanel, as the pieces the restyle reads: where its buttons and grid are, what its
    // options are, and what each one shows. The item pickers and the drone scrapper are separate classes in the game
    // with the same parts.
    // What a scrapper gives for an option: the scrap and how many, and what the strip says of the option given up.
    internal sealed class Exchange
    {
        public PickupDef Scrap;
        public int ScrapCount = 1;
        // The item scrapped (its held stack is counted), or none for a drone.
        public ItemIndex Item = ItemIndex.None;
        public int DroneUpgrades;
        public bool CarryingItems;
    }

    internal abstract class PickerSource
    {
        public abstract Component Component { get; }

        public bool Alive => Component;

        public Transform Transform => Component.transform;

        public abstract GridLayoutGroup Grid { get; }

        public abstract RectTransform ButtonContainer { get; }

        public abstract int MaxColumnCount { get; }

        // Whether the game leaves the gamepad navigation between the buttons to Unity.
        public abstract bool AutomaticNavigation { get; }

        // The panel's rect, and the content under it that holds the title, the buttons, the legend and Cancel.
        public RectTransform MainPanel { get; protected set; }

        public RectTransform Juice { get; protected set; }

        // Whether the options are always a grid of slots, whatever their number (the drone scrapper's).
        public virtual bool AlwaysGrid => false;

        // The tallest the buttons' scroll view shows before the rest scrolls.
        public virtual float MaxContentHeight => float.MaxValue;

        // The most columns the grid shows, fewer than the game's (the item scrapper's one long row), or 0 for the game's.
        public virtual int ColumnCap => 0;

        // The narrowest the panel is in grid mode.
        public virtual float MinWidth => PickerPanel.MinPanelWidth;

        // Whether the panel trades each option for something (the scrappers), shown in an exchange strip across its
        // bottom (PickerExchange) in place of the option's name in the details.
        public virtual bool Exchanges => false;

        // What picking the option gives, for a panel that Exchanges; null when it can't be told.
        public virtual Exchange ExchangeOf(int index) => null;

        // The part of the content holding the buttons, which stays when the rest is hidden, and goes with no options.
        public virtual Transform ContentRoot => ButtonContainer;

        // The options when the panel has them and has made the buttons for them: some object that changes when the game
        // gives it new ones, how many there are, and the buttons.
        public abstract bool TryGetOptions(out object options, out int count, out ReadOnlyCollection<MPButton> elements);

        public abstract PickerEntry Entry(int index);

        // Called as the panel is styled, before its parts are hidden.
        public virtual void Prepare(StyleRecorder recorder)
        {
        }

        // Puts the buttons' container at x, y from the content's top left, taking this width and height of it, of which
        // the buttons need contentHeight.
        public virtual void PlaceContent(float x, float y, float width, float height, float contentHeight)
        {
            ButtonContainer.anchoredPosition = new Vector2(x, -y);
            ButtonContainer.sizeDelta = new Vector2(width, height);
        }

        // Hides the game's own selection and hover graphics on a slot's button.
        public virtual void HideSelection(MPButton button)
        {
        }

        // Every frame while the panel is styled: holds game parts the game itself may change back.
        public virtual void KeepShown()
        {
        }

        // Called after the game's look is put back.
        public virtual void Restored()
        {
        }
    }
}
