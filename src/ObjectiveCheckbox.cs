using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // An objective's checkbox as on the design canvas: a flat square outline, grey while the objective is open. The
    // game's pictures (the open box, the check, the cross) have wide empty margins and draw much smaller than their
    // rect, so they stay hidden. The game switches the picture as the objective is done or failed (yellow or red in its
    // colors); the outline then takes SevenUI's softer yellow or red, a flat check or cross of that color fills it, and
    // the objective's text dims (design H in the Phase H design doc, Jacob's pick, 2026-11-21).
    internal sealed class ObjectiveCheckbox : MonoBehaviour
    {
        public Image Checkbox;
        public Sprite OpenSprite;
        public Sprite DoneSprite;
        public Sprite FailedSprite;
        public PixelOutline Box;
        public Image Mark;
        public TMP_Text Label;

        private Sprite shown;
        private bool dimmed;
        private Color undimmed;

        // A sprite compare when nothing changed.
        private void LateUpdate()
        {
            if (!Checkbox) return;
            if (Checkbox.enabled)
            {
                Checkbox.enabled = false;
            }
            // The outline starts hidden (PixelOutline.Add); it shows in every state.
            if (!Box.enabled)
            {
                Box.enabled = true;
            }
            Sprite sprite = Checkbox.sprite;
            if (sprite == shown) return;
            shown = sprite;
            bool done = sprite && sprite == DoneSprite;
            bool failed = sprite && sprite == FailedSprite;
            Box.color = done ? Palette.ObjectiveDone : failed ? Palette.ObjectiveFailed : Palette.HeaderText;
            if (Mark)
            {
                Mark.enabled = done || failed;
                Mark.sprite = failed ? FlatIcons.Cross : FlatIcons.Check;
                Mark.color = Box.color;
            }
            Dim(done || failed);
        }

        // The game greys a retired objective's text only when its text changes, so the mod dims it as the box changes.
        private void Dim(bool dim)
        {
            if (!Label || dim == dimmed) return;
            if (dim)
            {
                undimmed = Label.color;
                Label.color = Palette.HeaderSubtle;
            }
            else if (Label.color == Palette.HeaderSubtle)
            {
                Label.color = undimmed;
            }
            dimmed = dim;
        }
    }
}
