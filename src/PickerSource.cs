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

        // What the game shows for the hovered option itself (the drone scrapper's description and scrap), in place of
        // the details the mod would put under the grid.
        public virtual bool OwnsDetails => false;

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

        // Puts the game's own details for the hovered option under the buttons, at this place and width.
        public virtual float PlaceDetails(float x, float y, float width) => 0f;

        // The hovered option, as the panel last read it.
        public virtual void Hovered(PickerEntry entry)
        {
        }
    }
}
