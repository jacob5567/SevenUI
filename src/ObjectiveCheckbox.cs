using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // An open objective's checkbox as on the design canvas: a flat square outline in grey. The game's box picture has
    // wide empty margins, so it draws much smaller than its rect. The game switches the picture as the objective is
    // done or failed; while it shows the open box, that picture is hidden for the outline, and the game's check and
    // cross show as they are.
    internal sealed class ObjectiveCheckbox : MonoBehaviour
    {
        public Image Checkbox;
        public Sprite OpenSprite;
        public Image Box;

        // A sprite compare when nothing changed.
        private void LateUpdate()
        {
            if (!Checkbox) return;
            bool open = Checkbox.sprite == OpenSprite;
            if (Box.enabled != open)
            {
                Box.enabled = open;
                Checkbox.enabled = !open;
            }
        }
    }
}
